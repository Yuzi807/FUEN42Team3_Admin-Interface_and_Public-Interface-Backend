using FUEN42Team3.Backend.Middlewares;
using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Backend.Models.Interfaces;
using FUEN42Team3.Backend.Models.Repositories;
using FUEN42Team3.Backend.Models.Services;
using FUEN42Team3.Models.Interfaces;
using FUEN42Team3.Models.Repositories;
using FUEN42Team3.Models.Services;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System;

namespace FUEN42Team3.Backend
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();


            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(
                    policy =>
                    {
                        policy.AllowAnyOrigin()
                              .AllowAnyMethod()
                              .AllowAnyHeader();
                    });
            });


            builder.Services.AddLogging(logging =>
            {
                logging.AddConsole();
                logging.AddDebug();
            });


            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");


            builder.Services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlServer(connectionString);

                if (builder.Environment.IsDevelopment())
                {
                    options.EnableSensitiveDataLogging();
                }
            });

            // Hangfire 設定：使用 SQL Server 儲存
            builder.Services.AddHangfire(config =>
            {
                config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                      .UseSimpleAssemblyNameTypeSerializer()
                      .UseRecommendedSerializerSettings()
                      .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
                      {
                          // Batch & queue tuning
                          CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                          SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                          QueuePollInterval = TimeSpan.FromSeconds(15),

                          // Concurrency & isolation
                          UseRecommendedIsolationLevel = true,
                          DisableGlobalLocks = true,

                          // Ensure schema & indexes are created/upgraded.
                          // Missing indexes on ExpireAt columns can lead to SQL error 8622 when hints are used.
                          PrepareSchemaIfNecessary = true,
                          EnableHeavyMigrations = true,

                          // Throttle expiration cleanup frequency to reduce pressure during investigation.
                          JobExpirationCheckInterval = TimeSpan.FromMinutes(15)
                      });
            });
            builder.Services.AddHangfireServer();




            builder.Services.AddScoped<IOrderRepository, OrderRepository>();

            builder.Services.AddScoped<OrderRepository>();

            builder.Services.AddScoped<OrderService>();

            builder.Services.AddScoped<NewsService>();
            builder.Services.AddScoped<PointsAdminService>();
            builder.Services.AddScoped<PointExpiryService>();
            builder.Services.AddScoped<PointsRuleEngineService>();




            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {

                    options.Cookie.Name = "Ghosttoys";


                    options.LoginPath = "/Auth/Login";


                    options.AccessDeniedPath = "/Auth/Login";

                    options.LogoutPath = "/Auth/Login";


                    // options.AccessDeniedPath = "/Auth/AccessDenied";


                    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);


                    options.SlidingExpiration = true;


                    options.Cookie.HttpOnly = true;


                    // options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                // Production: generic error page + HSTS
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }
            else
            {
                // Development: show detailed exception page
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();


            app.UseCors();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();


            app.UseUserStatusCheck();

            // 僅開發環境顯示 Hangfire Dashboard
            if (app.Environment.IsDevelopment())
            {
                app.UseHangfireDashboard("/hangfire");
            }

            app.MapControllers();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Auth}/{action=Login}/{id?}");

            // 啟動時：
            // 1) 建立點數到期清理的週期工作（台北時區）
            // 2) 嘗試自動註冊已啟用且為排程型（TriggerType=Schedule，ScheduleCron 不為空）的點數規則，
            //    避免後台忘記按下排程按鈕導致「時間到了卻未發點數」。
            using (var scope = app.Services.CreateScope())
            {
                var recurring = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
                var expiryJobId = "Points:ExpiryCleanup";
                // 以台北時區排程：開發每小時；正式每日 01:00（台北時間）
                var cron = app.Environment.IsDevelopment() ? Cron.Hourly() : Cron.Daily(1);
                RecurringJobOptions? opt = null;
                try
                {
                    opt = new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time") };
                }
                catch
                {
                    // Linux 容器名稱
                    try { opt = new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei") }; } catch { opt = null; }
                }
                if (opt != null)
                    recurring.AddOrUpdate(expiryJobId, () => scope.ServiceProvider.GetRequiredService<PointExpiryService>().CleanupExpiredAsync(), cron, opt);
                else
                    recurring.AddOrUpdate(expiryJobId, () => scope.ServiceProvider.GetRequiredService<PointExpiryService>().CleanupExpiredAsync(), cron);

                // 自動註冊已啟用的規則排程
                try
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var engine = scope.ServiceProvider.GetRequiredService<PointsRuleEngineService>();
                    // 僅抓取 Schedule 型且 Status=Enabled 且 Cron 不為空
                    var rules = db.PointRules.Where(r => r.Status == "Enabled" && r.TriggerType == "Schedule" && r.ScheduleCron != null && r.ScheduleCron != "").ToList();
                    foreach (var r in rules)
                    {
                        var rid = $"PointRuleV2:{r.Id}";
                        if (opt != null)
                            recurring.AddOrUpdate(rid, () => engine.RunScheduleAsync(r.Id), r.ScheduleCron!, opt);
                        else
                            recurring.AddOrUpdate(rid, () => engine.RunScheduleAsync(r.Id), r.ScheduleCron!);
                    }
                }
                catch
                {
                    // 忽略啟動期註冊失敗，避免影響主程式；可於後台重新排程。
                }
            }

            app.Run();
        }
    }
}
