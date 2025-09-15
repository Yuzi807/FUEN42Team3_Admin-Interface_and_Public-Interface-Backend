using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Backend.Models.ViewModels;
using FUEN42Team3.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FUEN42Team3.Backend.Controllers
{

    public class AuthController : Controller
    {
        private readonly AppDbContext _context; // 使用AppDbContext來存取資料庫
        public AuthController(AppDbContext context)
        {
            _context = context;
        }
        public IActionResult Login()
        {
            // 檢查是否有記住的帳號存在 Cookie 中
            if (Request.Cookies.ContainsKey("RememberedAccount"))
            {
                var rememberedAccount = Request.Cookies["RememberedAccount"];
                var viewModel = new LoginViewModel
                {
                    Account = rememberedAccount,
                    RememberMe = true
                };
                return View(viewModel);
            }

            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel vm, string? returnUrl = "/Home/Index/")
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            // 取得使用者資料
            User? user = _context.Users
                .AsNoTracking()
                .FirstOrDefault(m => m.Account == vm.Account);

            // 驗證帳號是否存在
            if (user == null)
            {
                ModelState.AddModelError("", "帳號或密碼錯誤");
                return View(vm);
            }

            // 判斷帳號是否啟用
            if (!(user.IsActive))
            {
                ModelState.AddModelError("", "帳號未啟用或已被停用");
                return View(vm);
            }

            // 驗證密碼
            if (!HashUtility.VerifyPassword(vm.Password, user.Password))
            {
                ModelState.AddModelError("", "帳號或密碼錯誤");
                return View(vm);
            }

            // 準備登入 Cookie
            var claims = new List<Claim>
            {
                // 將Account作為Name識別（這是主要的識別方式）
                new Claim(ClaimTypes.Name, user.Account),
                // 將用戶ID保存在NameIdentifier中
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                // 將用戶角色保存在Role中
                new Claim(ClaimTypes.Role, "admin"), // todo 待修改~~user.Role
                // 將用戶名保存在GivenName中
                new Claim(ClaimTypes.GivenName, user.UserName)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            // 設定 cookie 有效期，依據是否勾選 RememberMe
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true, // 永遠設置為持久化，這樣身份驗證可以跨會話保持
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(60) // 登入狀態有效期60分鐘
            };

            // 處理記住帳號功能
            if (vm.RememberMe)
            {
                // 如果選擇記住帳號，將帳號儲存在 Cookie 中，保存30天
                var cookieOptions = new CookieOptions
                {
                    Expires = DateTime.Now.AddDays(30),
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax
                };

                Response.Cookies.Append("RememberedAccount", vm.Account, cookieOptions);
            }
            else
            {
                // 如果不記住帳號，則清除之前可能存在的記住帳號 Cookie
                if (Request.Cookies.ContainsKey("RememberedAccount"))
                {
                    Response.Cookies.Delete("RememberedAccount");
                }
            }

            // 進行登入
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                claimsPrincipal,
                authProperties);

            // 登入成功後導向指定頁面
            return LocalRedirect(returnUrl ?? "/");
        }

        public async Task<IActionResult> Logout()
        {
            // 登出用戶 - 這會清除 ASP.NET Core 的認證 cookie
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // 注意：我們不會在登出時刪除 RememberedAccount cookie
            // 這樣下次用戶進入登入頁面時，帳號仍會被記住
            // 如果要徹底登出包括忘記帳號，可以取消下面的註解

            // Response.Cookies.Delete("RememberedAccount");

            // 登出成功，導向登入頁面
            return RedirectToAction("Login", "Auth");
        }
    }
}
