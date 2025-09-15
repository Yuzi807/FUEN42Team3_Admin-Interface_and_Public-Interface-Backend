using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FUEN42Team3.Backend.Controllers
{
    // 兼容舊連結：將原 V2 UI 入口導向新的「Points 規則」單一頁面
    [Authorize]
    public class PointRulesController : Controller
    {
        [HttpGet("admin/point-rules/ui")]
        [HttpGet("admin/point-rules/edit/{id?}")]
        [HttpGet("admin/point-rules/estimate-ui")]
        [HttpGet("admin/point-rules/cron-helper")]
        public IActionResult RedirectToUnified()
        {
            return Redirect("/admin/points/rules-page");
        }
    }
}
