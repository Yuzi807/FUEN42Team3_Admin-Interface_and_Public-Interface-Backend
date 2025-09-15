using FUEN42Team3.Backend.Models.EfModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FUEN42Team3.Backend.Middlewares
{
    /// <summary>
    /// 檢查用戶狀態的中間件，用於在每次請求時確認已登入用戶的帳號是否仍然啟用
    /// </summary>
    public class UserStatusCheckMiddleware
    {
        private readonly RequestDelegate _next;

        public UserStatusCheckMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
        {
            // 檢查用戶是否已驗證（已登入）
            if (context.User.Identity?.IsAuthenticated == true)
            {
                // 從 Claims 中獲取用戶 ID
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    // 檢查用戶是否仍然啟用
                    var user = await dbContext.Users
                        .AsNoTracking() // 使用 AsNoTracking 提高查詢效率
                        .FirstOrDefaultAsync(u => u.Id == userId);

                    // 如果用戶不存在或已被停用，則登出用戶
                    if (user == null || user.IsActive != true)
                    {
                        // 登出用戶
                        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                        // 如果不是 API 請求，則重定向到登入頁面
                        if (!context.Request.Path.StartsWithSegments("/api"))
                        {
                            context.Response.Redirect("/Auth/Login?message=Account_Deactivated");
                            return;
                        }
                    }
                }
            }

            // 繼續處理請求
            await _next(context);
        }
    }

    // 註冊中間件的擴展方法
    public static class UserStatusCheckMiddlewareExtensions
    {
        public static IApplicationBuilder UseUserStatusCheck(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<UserStatusCheckMiddleware>();
        }
    }
}