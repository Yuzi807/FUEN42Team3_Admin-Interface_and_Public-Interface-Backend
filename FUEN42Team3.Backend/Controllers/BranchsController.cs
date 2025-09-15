using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Backend.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;

namespace FUEN42Team3.Backend.Controllers
{
    [Authorize]
    public class BranchsController : Controller
    {
        private readonly AppDbContext _context;

        public BranchsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // 載入分店資料和區域資料
            var viewModel = new BranchsIndexViewModel
            {
                // 載入所有分店資料
                BranchList = await _context.Branches
                    .Include(b => b.Region)
                    .Select(b => new BranchViewModel
                    {
                        Id = b.Id,
                        Name = b.Name,
                        Address = b.Address,
                        Phone = b.Phone,
                        MapUrl = b.MapUrl,
                        RegionId = b.RegionId,
                        RegionName = b.Region.Name,
                        IsVisible = b.IsVisible,
                        CreatedAt = b.CreatedAt,
                        UpdatedAt = b.UpdatedAt,
                    })
                    .OrderBy(b => b.Id)
                    .ToListAsync(),
                
                // 載入區域資料供下拉選單使用
                RegionList = await _context.Regions
                    .Select(r => new RegionViewModel { Id = r.Id, Name = r.Name })
                    .ToListAsync()
            };
            
            return View(viewModel);
        }
        // 添加篩選方法
        public async Task<IActionResult> Index(string searchTerm = "", int? regionId = null, bool? isVisible = null)
        {
            // 儲存篩選參數到 ViewData
            ViewData["SearchTerm"] = searchTerm;
            ViewData["RegionId"] = regionId;
            ViewData["IsVisible"] = isVisible;

            // 建立基本查詢
            var branchQuery = _context.Branches
                .Include(b => b.Region)
                .Include(b => b.BranchOpenTimes)
                .AsQueryable();

            // 套用篩選條件
            if (!string.IsNullOrEmpty(searchTerm))
            {
                branchQuery = branchQuery.Where(b =>
                    b.Name.Contains(searchTerm) ||
                    b.Address.Contains(searchTerm) ||
                    b.Phone.Contains(searchTerm));
            }

            if (regionId.HasValue)
            {
                branchQuery = branchQuery.Where(b => b.RegionId == regionId.Value);
            }

            if (isVisible.HasValue)
            {
                branchQuery = branchQuery.Where(b => b.IsVisible == isVisible.Value);
            }

            // 選取資料並映射到 ViewModel
            var branches = await branchQuery
                .Select(b => new BranchViewModel
                {
                    Id = b.Id,
                    Name = b.Name,
                    // 其他屬性...
                })
                .OrderBy(b => b.Id)
                .ToListAsync();

            // 建立 ViewModel
            var viewModel = new BranchsIndexViewModel
            {
                BranchList = branches,
                RegionList = await _context.Regions.Select(r => new RegionViewModel { Id = r.Id, Name = r.Name }).ToListAsync()
            };

            return View(viewModel);
        }

        // 輔助方法：取得星期名稱
        private static string GetWeekdayName(byte weekday)
        {
            return weekday switch
            {
                0 => "星期日",
                1 => "星期一",
                2 => "星期二",
                3 => "星期三",
                4 => "星期四",
                5 => "星期五",
                6 => "星期六",
                _ => $"星期{weekday}"
            };
        }
    }
}
