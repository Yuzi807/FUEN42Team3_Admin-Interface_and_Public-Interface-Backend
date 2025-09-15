using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Backend.Models.ViewModels;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace FUEN42Team3.Backend.Controllers.APIs
{
    [Route("api/[controller]")]
    [ApiController]
    public class BranchsApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BranchsApiController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/BranchsApi
        [HttpGet]
        public async Task<ActionResult<List<BranchViewModel>>> GetBranches()
        {
            var branches = await _context.Branches
                .Include(b => b.Region)
                .Include(b => b.BranchOpenTimes)
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
                    OpeningHours = b.BranchOpenTimes.Select(ot => new BranchOpenTimeViewModel
                    {
                        Id = ot.Id,
                        BranchId = ot.BranchId,
                        Weekday = ot.Weekday,
                        WeekdayName = GetWeekdayName(ot.Weekday),
                        OpenTime = ot.OpenTime,
                        CloseTime = ot.CloseTime
                    }).OrderBy(ot => ot.Weekday).ToList()
                })
                .OrderBy(b => b.Id)
                .ToListAsync();

            return Ok(branches);
        }

        // GET: api/BranchsApi/5
        [HttpGet("{id}")]
        public async Task<ActionResult<BranchViewModel>> GetBranch(int id)
        {
            var branch = await _context.Branches
                .Include(b => b.Region)
                .Include(b => b.BranchOpenTimes)
                .Where(b => b.Id == id)
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
                    OpeningHours = b.BranchOpenTimes.Select(ot => new BranchOpenTimeViewModel
                    {
                        Id = ot.Id,
                        BranchId = ot.BranchId,
                        Weekday = ot.Weekday,
                        WeekdayName = GetWeekdayName(ot.Weekday),
                        OpenTime = ot.OpenTime,
                        CloseTime = ot.CloseTime
                    }).OrderBy(ot => ot.Weekday).ToList()
                })
                .FirstOrDefaultAsync();

            if (branch == null)
            {
                return NotFound();
            }

            return Ok(branch);
        }

        // GET: api/BranchsApi/{id}/OpeningHours
        [HttpGet("{id}/OpeningHours")]
        public async Task<ActionResult<IEnumerable<BranchOpenTimeViewModel>>> GetBranchOpeningHours(int id)
        {
            var branch = await _context.Branches
                .Include(b => b.BranchOpenTimes)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (branch == null)
            {
                return NotFound();
            }

            var openingHours = branch.BranchOpenTimes
                .Select(ot => new BranchOpenTimeViewModel
                {
                    Id = ot.Id,
                    BranchId = ot.BranchId,
                    Weekday = ot.Weekday,
                    WeekdayName = GetWeekdayName(ot.Weekday),
                    OpenTime = ot.OpenTime,
                    CloseTime = ot.CloseTime
                })
                .OrderBy(ot => ot.Weekday)
                .ToList();

            return Ok(openingHours);
        }

        // POST: api/BranchsApi
        [HttpPost]
        public async Task<ActionResult<BranchViewModel>> CreateBranch(BranchViewModel viewModel)
        {
            var branch = new Branch
            {
                Name = viewModel.Name,
                Address = viewModel.Address,
                Phone = viewModel.Phone,
                MapUrl = viewModel.MapUrl,
                RegionId = viewModel.RegionId,
                IsVisible = viewModel.IsVisible,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.Branches.Add(branch);
            await _context.SaveChangesAsync();

            // 處理營業時間
            if (viewModel.OpeningHours != null && viewModel.OpeningHours.Any())
            {
                foreach (var openTime in viewModel.OpeningHours)
                {
                    var branchOpenTime = new BranchOpenTime
                    {
                        BranchId = branch.Id,
                        Weekday = openTime.Weekday,
                        OpenTime = openTime.OpenTime,
                        CloseTime = openTime.CloseTime
                    };
                    _context.BranchOpenTimes.Add(branchOpenTime);
                }
                await _context.SaveChangesAsync();
            }

            // 返回創建後的資料
            var result = await GetBranch(branch.Id);
            return CreatedAtAction(nameof(GetBranch), new { id = branch.Id }, result.Value);
        }

        // PUT: api/BranchsApi/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBranch(int id, BranchViewModel viewModel)
        {
            if (id != viewModel.Id)
            {
                return BadRequest();
            }

            var branch = await _context.Branches.FindAsync(id);
            if (branch == null)
            {
                return NotFound();
            }

            // 更新分店基本資料
            branch.Name = viewModel.Name;
            branch.Address = viewModel.Address;
            branch.Phone = viewModel.Phone;
            branch.MapUrl = viewModel.MapUrl;
            branch.RegionId = viewModel.RegionId;
            branch.IsVisible = viewModel.IsVisible;
            branch.UpdatedAt = DateTime.Now;

            _context.Entry(branch).State = EntityState.Modified;

            // 更新營業時間
            if (viewModel.OpeningHours != null)
            {
                // 獲取所有現有的營業時間
                var existingOpenTimes = await _context.BranchOpenTimes
                    .Where(ot => ot.BranchId == id)
                    .ToListAsync();

                // 刪除所有現有的營業時間
                _context.BranchOpenTimes.RemoveRange(existingOpenTimes);

                // 添加新的營業時間
                foreach (var openTime in viewModel.OpeningHours)
                {
                    var branchOpenTime = new BranchOpenTime
                    {
                        BranchId = branch.Id,
                        Weekday = openTime.Weekday,
                        OpenTime = openTime.OpenTime,
                        CloseTime = openTime.CloseTime
                    };
                    _context.BranchOpenTimes.Add(branchOpenTime);
                }
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BranchExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // DELETE: api/BranchsApi/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBranch(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch == null)
            {
                return NotFound();
            }

            // 刪除相關的營業時間
            var openTimes = await _context.BranchOpenTimes
                .Where(ot => ot.BranchId == id)
                .ToListAsync();
            _context.BranchOpenTimes.RemoveRange(openTimes);

            // 刪除分店
            _context.Branches.Remove(branch);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // POST: api/BranchsApi/BatchDelete
        [HttpPost("BatchDelete")]
        public async Task<IActionResult> BatchDelete([FromBody] List<int> ids)
        {
            if (ids == null || !ids.Any())
            {
                return BadRequest("未提供要刪除的分店 ID");
            }

            // 刪除相關的營業時間
            var openTimes = await _context.BranchOpenTimes
                .Where(ot => ids.Contains(ot.BranchId))
                .ToListAsync();
            _context.BranchOpenTimes.RemoveRange(openTimes);

            // 刪除分店
            var branches = await _context.Branches
                .Where(b => ids.Contains(b.Id))
                .ToListAsync();
            _context.Branches.RemoveRange(branches);

            await _context.SaveChangesAsync();

            return Ok(new { message = $"已成功刪除 {branches.Count} 家分店" });
        }

        // GET: api/BranchsApi/Regions
        [HttpGet("Regions")]
        public async Task<ActionResult<List<RegionViewModel>>> GetRegions()
        {
            var regions = await _context.Regions
                .Select(r => new RegionViewModel
                {
                    Id = r.Id,
                    Name = r.Name
                })
                .ToListAsync();

            return Ok(regions);
        }

        // 輔助方法：檢查分店是否存在
        private bool BranchExists(int id)
        {
            return _context.Branches.Any(e => e.Id == id);
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
