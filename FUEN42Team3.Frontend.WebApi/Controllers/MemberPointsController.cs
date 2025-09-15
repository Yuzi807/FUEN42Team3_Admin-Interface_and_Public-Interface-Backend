using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Frontend.WebApi.Models.DTOs;
using FUEN42Team3.Frontend.WebApi.Models.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Frontend.WebApi.Controllers
{
    [ApiController]
    [Route("api/member/points")]
    [Authorize]
    public class MemberPointsController : ControllerBase
    {
        private readonly AppDbContext _db;
        public MemberPointsController(AppDbContext db) => _db = db;

        private int? GetCurrentMemberId()
        {
            var val = User.FindFirst("MemberId")?.Value;
            return int.TryParse(val, out var id) ? id : (int?)null;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized();

            var now = TaipeiTime.Now;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var monthEnd = monthStart.AddMonths(1);

            // 目前總點數：以 PointLots 剩餘（未過期）為準，確保包含排程贈點
            var current = await _db.PointLots
                .Where(l => l.MemberId == mid && l.ExpiresAt > now)
                .SumAsync(l => (int?)l.RemainingPoints) ?? 0;

            // 本月獲得/使用
            // 當月獲得（PointLots.Points）與使用（PointRedemptions.UsedPoints）
            var lotsThisMonth = await _db.PointLots
                .Where(l => l.MemberId == mid && l.CreatedAt >= monthStart && l.CreatedAt < monthEnd)
                .Select(l => l.Points)
                .ToListAsync();
            var redemptionsThisMonth = await _db.PointRedemptions
                .Where(r => r.MemberId == mid && r.CreatedAt >= monthStart && r.CreatedAt < monthEnd)
                .Select(r => r.UsedPoints)
                .ToListAsync();

            var earnedThisMonth = lotsThisMonth.Sum();
            var usedThisMonth = redemptionsThisMonth.Sum();

            // 簡易：將「即將到期」定義為一年內到期（以 CreatedAt + 1 年）且到期日在 3 個月內
            var threeMonthsLater = now.AddMonths(3);
            // 到期計算：依 PointLots.ExpiresAt
            var lots = await _db.PointLots
                .Where(l => l.MemberId == mid && l.ExpiresAt > now)
                .Select(l => new { l.RemainingPoints, l.ExpiresAt })
                .ToListAsync();
            var expiringPoints = lots
                .Where(l => l.ExpiresAt <= threeMonthsLater)
                .Sum(l => l.RemainingPoints);
            var nearestExpDate = lots
                .Select(l => l.ExpiresAt)
                .Where(d => d > now)
                .DefaultIfEmpty()
                .OrderBy(d => d)
                .FirstOrDefault();

            var dto = new MemberPointSummaryDto
            {
                CurrentPoints = current,
                ExpiringPoints = expiringPoints,
                ExpiringDate = nearestExpDate == default ? null : nearestExpDate,
                EarnedThisMonth = earnedThisMonth,
                UsedThisMonth = usedThisMonth
            };

            return Ok(new { success = true, data = dto });
        }

        [HttpGet("records")]
        public async Task<IActionResult> GetRecords(
            [FromQuery] string type = "all", // all|earned|used|expired
            [FromQuery] string timeRange = "all", // all|3months|6months|1year
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized();

            // 以 PointLots 與 PointRedemptions 統一輸出紀錄
            DateTime? start = null;
            var now = TaipeiTime.Now;
            switch (timeRange)
            {
                case "3months": start = now.AddMonths(-3); break;
                case "6months": start = now.AddMonths(-6); break;
                case "1year": start = now.AddYears(-1); break;
            }

            var earnedQuery = _db.PointLots.Where(l => l.MemberId == mid);
            var usedQuery = _db.PointRedemptions.Where(r => r.MemberId == mid);
            if (start.HasValue)
            {
                earnedQuery = earnedQuery.Where(l => l.CreatedAt >= start.Value);
                usedQuery = usedQuery.Where(r => r.CreatedAt >= start.Value);
            }

            var earnedList = await earnedQuery
                .OrderBy(l => l.CreatedAt)
                .Select(l => new { Id = l.Id, Date = l.CreatedAt, Points = l.Points, Reason = l.Reason, Exp = l.ExpiresAt })
                .ToListAsync();
            var usedList = await usedQuery
                .OrderBy(r => r.CreatedAt)
                .Select(r => new
                {
                    Id = r.Id,
                    Date = r.CreatedAt,
                    Points = r.UsedPoints,
                    Items = r.PointRedemptionItems.Select(i => new { i.LotId, i.UsedPoints }).ToList()
                })
                .ToListAsync();

            var records = new List<MemberPointRecordDto>();
            foreach (var e in earnedList)
            {
                records.Add(new MemberPointRecordDto
                {
                    Id = e.Id,
                    Date = e.Date,
                    Points = e.Points,
                    Type = "earned",
                    Description = string.IsNullOrWhiteSpace(e.Reason) ? "排程贈點" : e.Reason,
                    ExpirationDate = e.Exp
                });
            }
            foreach (var u in usedList)
            {
                string desc = "點數折抵";
                if (u.Items != null && u.Items.Count > 0)
                {
                    var parts = u.Items.Select(i => $"批次#{i.LotId}:{i.UsedPoints}點");
                    desc += $"（{string.Join(", ", parts)}）";
                }
                records.Add(new MemberPointRecordDto
                {
                    Id = u.Id,
                    Date = u.Date,
                    Points = u.Points,
                    Type = "used",
                    Description = desc,
                    ExpirationDate = null
                });
            }

            // 先依時間升序計算完整餘額（不受類型篩選影響）
            int running = 0;
            foreach (var r in records.OrderBy(r => r.Date))
            {
                running += (r.Type == "used" ? -r.Points : r.Points);
                r.Balance = running;
            }

            // 類型與過期篩選
            if (type == "earned") records = records.Where(r => r.Type == "earned").ToList();
            else if (type == "used") records = records.Where(r => r.Type == "used").ToList();
            else if (type == "expired") records = records.Where(r => r.Type == "earned" && r.ExpirationDate.HasValue && r.ExpirationDate.Value < now).Select(r => { r.Type = "expired"; return r; }).ToList();

            // 依時間倒序
            records = records.OrderByDescending(r => r.Date).ToList();

            var total = records.Count;
            var items = records.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            var result = new PagedResult<MemberPointRecordDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = total
            };
            return Ok(new { success = true, data = result });
        }

        private static string BuildDescription(string? reason, int? orderId)
        {
            var text = (reason ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(text)) return text;
            return orderId.HasValue ? $"訂單 {orderId}" : "點數異動";
        }
    }
}
