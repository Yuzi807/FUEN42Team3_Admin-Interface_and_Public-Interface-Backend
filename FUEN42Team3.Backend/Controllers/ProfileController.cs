using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FUEN42Team3.Backend.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        public IActionResult Index()
        {
            // 獲取當前登入用戶的 ID
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            string userName = User.FindFirstValue(ClaimTypes.GivenName);
            string account = User.Identity?.Name;

            ViewBag.UserId = userId;
            ViewBag.UserName = userName;
            ViewBag.Account = account;

            return View();
        }

        public IActionResult ChangePassword()
        {
            // 獲取當前登入用戶的 ID
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ViewBag.UserId = userId;

            return View();
        }
    }
}