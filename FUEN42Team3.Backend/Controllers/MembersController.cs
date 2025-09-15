using FUEN42Team3.Backend.Models.EfModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Backend.Controllers
{
    [Authorize]
    public class MembersController : Controller
    {
        private readonly AppDbContext _context;

        public MembersController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            // 傳遞統計數據到視圖，這些數據將在客戶端通過API獲取
            return View();
        }

        // 其他會員相關的視圖可以根據需要添加，例如詳細資料視圖
        public async Task<IActionResult> Details(int id)
        {
            var member = await _context.Members.FindAsync(id);
            if (member == null)
            {
                return NotFound();
            }

            // 取得最後登入時間
            var lastLogin = await _context.MemberLoginLogs
                .Where(log => log.MemberId == id && log.IsSuccessful == true)
                .OrderByDescending(log => log.LoginAt)
                .Select(log => log.LoginAt)
                .FirstOrDefaultAsync();

            ViewBag.LastLoginTime = lastLogin;

            return View(member);
        }
    }
}
