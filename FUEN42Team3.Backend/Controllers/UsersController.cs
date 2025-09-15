using FUEN42Team3.Backend.Models.EfModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Backend.Controllers
{
    [Authorize]
    public class UsersController : Controller
    {
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // 取得所有角色資料，傳遞給視圖
            var roles = await _context.UserRoles.ToListAsync();
            ViewBag.Roles = roles;
            
            return View();
        }
    }
}
