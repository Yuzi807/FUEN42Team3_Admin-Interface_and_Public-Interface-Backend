using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Frontend.WebApi.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Frontend.WebApi.Controllers
{
    [ApiController]
    [Route("api/points")]
    [Authorize]
    public class PointsController : ControllerBase
    {
        private readonly AppDbContext _db;
        public PointsController(AppDbContext db) => _db = db;

        private int? GetCurrentMemberId()
        {
            var val = User.FindFirst("MemberId")?.Value;
            return int.TryParse(val, out var id) ? id : (int?)null;
        }

        // GET /api/points/balance -> { available }
        [HttpGet("balance")]
        public async Task<IActionResult> GetBalance()
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized();

            var now = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;
            var available = await _db.PointLots
                .Where(l => l.MemberId == mid && l.RemainingPoints > 0 && l.ExpiresAt > now)
                .SumAsync(l => (int?)l.RemainingPoints) ?? 0;

            return Ok(new { available });
        }

        // GET /api/points/expiring?days=7 -> 批次列表（依到期日 ASC）
        [HttpGet("expiring")]
        public async Task<IActionResult> GetExpiring([FromQuery] int days = 7)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized();

            if (days <= 0 || days > 365) days = 7;
            var now = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;
            var to = now.AddDays(days);

            var lots = await _db.PointLots
                .Where(l => l.MemberId == mid && l.RemainingPoints > 0 && l.ExpiresAt > now && l.ExpiresAt <= to)
                .OrderBy(l => l.ExpiresAt).ThenBy(l => l.CreatedAt)
                .Select(l => new ExpiringLotDto
                {
                    LotId = l.Id,
                    RemainingPoints = l.RemainingPoints,
                    ExpiresAt = l.ExpiresAt,
                    Reason = l.Reason
                })
                .ToListAsync();

            return Ok(lots);
        }

        // POST /api/points/redeem -> { usePoints } => { usedPoints, balance, items:[{lotId, usedPoints}] }
        [HttpPost("redeem")]
        public async Task<IActionResult> Redeem([FromBody] PointRedeemRequest req)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized();
            if (req == null || req.UsePoints <= 0) return BadRequest("usePoints must be > 0");

            var now = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;
            using var tx = await _db.Database.BeginTransactionAsync();

            // 先計算可用餘額
            var available = await _db.PointLots
                .Where(l => l.MemberId == mid && l.RemainingPoints > 0 && l.ExpiresAt > now)
                .SumAsync(l => (int?)l.RemainingPoints) ?? 0;

            if (available <= 0)
            {
                return Ok(new PointRedeemResponse
                {
                    UsedPoints = 0,
                    Balance = 0,
                    Items = new()
                });
            }

            var target = Math.Min(req.UsePoints, available);
            var items = new List<PointRedeemItemDto>();

            // FIFO: ExpiresAt ASC, CreatedAt ASC
            var lots = await _db.PointLots
                .Where(l => l.MemberId == mid && l.RemainingPoints > 0 && l.ExpiresAt > now)
                .OrderBy(l => l.ExpiresAt).ThenBy(l => l.CreatedAt)
                .ToListAsync();

            var redemption = new PointRedemption
            {
                MemberId = mid.Value,
                UsedPoints = 0,
                CreatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now
            };
            _db.PointRedemptions.Add(redemption);
            await _db.SaveChangesAsync();

            var remainingToUse = target;
            foreach (var lot in lots)
            {
                if (remainingToUse <= 0) break;
                var take = Math.Min(lot.RemainingPoints, remainingToUse);
                if (take <= 0) continue;

                lot.RemainingPoints -= take;
                redemption.UsedPoints += take;

                var item = new PointRedemptionItem
                {
                    RedemptionId = redemption.Id,
                    LotId = lot.Id,
                    UsedPoints = take
                };
                _db.PointRedemptionItems.Add(item);
                items.Add(new PointRedeemItemDto { LotId = lot.Id, UsedPoints = take });

                remainingToUse -= take;
            }

            await _db.SaveChangesAsync();

            // 新餘額
            var newBalance = await _db.PointLots
                .Where(l => l.MemberId == mid && l.RemainingPoints > 0 && l.ExpiresAt > now)
                .SumAsync(l => (int?)l.RemainingPoints) ?? 0;

            await tx.CommitAsync();

            return Ok(new PointRedeemResponse
            {
                UsedPoints = redemption.UsedPoints,
                Balance = newBalance,
                Items = items
            });
        }
    }
}
