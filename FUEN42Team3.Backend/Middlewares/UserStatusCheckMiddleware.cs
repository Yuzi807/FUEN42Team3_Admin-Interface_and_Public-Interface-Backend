using FUEN42Team3.Backend.Models.EfModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FUEN42Team3.Backend.Middlewares
{
    /// <summary>
    /// �ˬd�Τ᪬�A��������A�Ω�b�C���ШD�ɽT�{�w�n�J�Τ᪺�b���O�_���M�ҥ�
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
            // �ˬd�Τ�O�_�w���ҡ]�w�n�J�^
            if (context.User.Identity?.IsAuthenticated == true)
            {
                // �q Claims ������Τ� ID
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    // �ˬd�Τ�O�_���M�ҥ�
                    var user = await dbContext.Users
                        .AsNoTracking() // �ϥ� AsNoTracking �����d�߮Ĳv
                        .FirstOrDefaultAsync(u => u.Id == userId);

                    // �p�G�Τᤣ�s�b�Τw�Q���ΡA�h�n�X�Τ�
                    if (user == null || user.IsActive != true)
                    {
                        // �n�X�Τ�
                        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                        // �p�G���O API �ШD�A�h���w�V��n�J����
                        if (!context.Request.Path.StartsWithSegments("/api"))
                        {
                            context.Response.Redirect("/Auth/Login?message=Account_Deactivated");
                            return;
                        }
                    }
                }
            }

            // �~��B�z�ШD
            await _next(context);
        }
    }

    // ���U�������X�i��k
    public static class UserStatusCheckMiddlewareExtensions
    {
        public static IApplicationBuilder UseUserStatusCheck(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<UserStatusCheckMiddleware>();
        }
    }
}