using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Frontend.WebApi.Models.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Ganss.Xss;//html sanitizer�M��

namespace FUEN42Team3.Frontend.WebApi

{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddTransient<EmailSender>();
            // Email
            builder.Services.Configure<FUEN42Team3.Frontend.WebApi.Models.Services.EmailQueueOptions>(builder.Configuration.GetSection("EmailQueue"));
            builder.Services.AddSingleton<FUEN42Team3.Frontend.WebApi.Models.Services.ChannelEmailQueue>();
            builder.Services.AddSingleton<FUEN42Team3.Frontend.WebApi.Models.Services.IEmailQueue>(sp => sp.GetRequiredService<FUEN42Team3.Frontend.WebApi.Models.Services.ChannelEmailQueue>());
            builder.Services.AddHostedService<FUEN42Team3.Frontend.WebApi.Models.Services.EmailBackgroundService>();


            //swagger���ե�Authorize
            builder.Services.AddSwaggerGen(c =>
            {
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer"
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
            });
            // HTML Sanitizer
            // ���U HtmlSanitizer�]�զW��^
            builder.Services.AddSingleton(provider =>
            {
                var s = new HtmlSanitizer();

                // ���\�����ҡ]�� Summernote �`���X�R�^
                s.AllowedTags.UnionWith(new[]
                {
        "p","br","b","strong","i","em","u",
        "ul","ol","li","blockquote",
        "h1","h2","h3","h4","h5","h6",
        "span","div","a","img",
        "pre","code","figure","figcaption",
        "table","thead","tbody","tr","th","td"
    });

                // ���\���ݩʡ]�`�N���n�]�t���� on* �ƥ��ݩʡ^
                s.AllowedAttributes.UnionWith(new[]
                {
        "href","title","target","rel",
        "src","alt","width","height",
        "class","style"
    });

                // ���\�� URL scheme�]���T�ư� javascript:�^
                s.AllowedSchemes.UnionWith(new[] { "http", "https", "data" });

                // �j�ơG�۰ʬ��~�s�[�W rel=noopener ��
                s.PostProcessNode += (sender, e) =>
                {
                    if (e.Node.NodeName.Equals("a", StringComparison.OrdinalIgnoreCase))
                    {
                        var el = (AngleSharp.Dom.IElement)e.Node;
                        var href = el.GetAttribute("href") ?? "";
                        // �ױ����w�� scheme�]Ganss.XSS �w�]�|�סA�o�̦A���O�I�^
                        if (href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                        {
                            el.RemoveAttribute("href");
                        }
                        el.SetAttribute("rel", "noopener nofollow ugc");
                        // �p���ݨD�i���\ target=_blank�A���n�t�X rel
                        // el.SetAttribute("target", "_blank");
                    }
                    if (e.Node.NodeName.Equals("img", StringComparison.OrdinalIgnoreCase))
                    {
                        var el = (AngleSharp.Dom.IElement)e.Node;
                        // �����Ҧ� on* �ƥ��ݩʡ]�w�]�w�|�����A�o�̦A���O�I�^
                        foreach (var attr in el.Attributes.ToArray())
                        {
                            if (attr.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                                el.RemoveAttribute(attr.Name);
                        }
                    }
                };

                return s;
            });
            // Services
            builder.Services.AddScoped<EcpayService>();
            builder.Services.AddScoped<EcpayLogisticsService>();
            builder.Services.AddScoped<NominatimGeocodingService>();
            builder.Services.AddScoped<Models.Services.PointsEventsClient>();
            builder.Services.AddHttpClient();

            //���U�֨�
            builder.Services.AddMemoryCache();

            // API
            builder.Services.AddControllers();
            builder.Services.AddSignalR();
            // Chat ?�員?��??�自?��??��?人�??�監??
            builder.Services.AddHostedService<FUEN42Team3.Frontend.WebApi.Models.Services.ChatInactivityMonitor>();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // add jwt authentication start
            builder.Services.AddAuthentication(option =>
            {
                option.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                option.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(option =>
            {
                option.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = builder.Configuration["Jwt:Issuer"],
                    ValidAudience = builder.Configuration["Jwt:Audience"],

                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? string.Empty))
                };

                // Allow SignalR to authenticate via access_token query string for WebSocket connections
                option.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        try
                        {
                            var memberId = context.Principal?.FindFirst("MemberId")?.Value;
                            Console.WriteLine($"[JWT] Validated for {context.HttpContext.Request.Path}, MemberId={(memberId ?? "<null>")}");
                        }
                        catch { }
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        Console.WriteLine($"[JWT] Auth failed for {context.HttpContext.Request.Path}: {context.Exception?.Message}");
                        return Task.CompletedTask;
                    }
                };
            });


            builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));




            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend",
                    policy => policy.WithOrigins(
                                        "http://localhost:5173", // Vite
                                        "http://localhost:5174", // Vite (alt port)
                                        "https://localhost:7262", // Frontend.WebApi https
                                        "http://localhost:5217",  // Frontend.WebApi http
                                        "http://localhost:5002",  // Backend http
                                        "https://localhost:7006",  // Backend https
                                        "https://33e850dd4283.ngrok-free.app" // External domain
                                    )
                                    .AllowAnyHeader()
                                    .AllowAnyMethod()
                                    .AllowCredentials());
            });


            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }


            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,

                KnownNetworks = { },
                KnownProxies = { }
            });


            if (!app.Environment.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }

            app.UseAuthentication();

            app.UseCors("AllowFrontend");
            app.UseAuthorization();
            app.UseStaticFiles();

            app.MapControllers();
            app.MapHub<FUEN42Team3.Frontend.WebApi.Hubs.ChatHub>("/hubs/chat");

            app.Run();
        }
    }
}
