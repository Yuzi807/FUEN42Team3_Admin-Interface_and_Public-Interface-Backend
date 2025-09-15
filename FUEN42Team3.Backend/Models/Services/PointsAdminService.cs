using FUEN42Team3.Backend.Models.EfModels;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Backend.Models.Services
{
    public class PointsAdminService
    {
        private readonly AppDbContext _db;
        public PointsAdminService(AppDbContext db) { _db = db; }

        public class PreviewResult
        {
            public int RuleId { get; set; }
            public string RuleName { get; set; } = string.Empty;
            public int TargetCount { get; set; }
            public int AmountPerMember { get; set; }
            public int TotalPoints => TargetCount * AmountPerMember;
            public DateTime ExpiresAt { get; set; }
        }

        public async Task<PreviewResult?> PreviewAsync(int ruleId)
        {
            var rule = await _db.PointGrantRules.AsNoTracking().FirstOrDefaultAsync(r => r.Id == ruleId && r.IsEnabled);
            if (rule == null) return null;

            var targetMembers = await QueryTargetMembers(rule).ToListAsync();
            return new PreviewResult
            {
                RuleId = rule.Id,
                RuleName = rule.RuleName,
                TargetCount = targetMembers.Count,
                AmountPerMember = rule.Amount,
                ExpiresAt = DateTime.UtcNow.AddDays(rule.ExpiryDays)
            };
        }

        public async Task<int> RunNowAsync(int ruleId, int? actorUserId = null)
        {
            var rule = await _db.PointGrantRules.FirstOrDefaultAsync(r => r.Id == ruleId && r.IsEnabled);
            if (rule == null) return 0;

            var expiresAt = DateTime.UtcNow.AddDays(rule.ExpiryDays);
            var members = await QueryTargetMembers(rule).Select(m => m.Id).ToListAsync();

            foreach (var mid in members)
            {
                var lot = new PointLot
                {
                    MemberId = mid,
                    Points = rule.Amount,
                    RemainingPoints = rule.Amount,
                    Reason = $"Rule:{rule.RuleName}",
                    ExpiresAt = expiresAt,
                    CreatedAt = DateTime.UtcNow,
                };
                _db.PointLots.Add(lot);
            }

            // 可加上 MemberPointLog 紀錄
            return await _db.SaveChangesAsync();
        }

        private IQueryable<Member> QueryTargetMembers(PointGrantRule rule)
        {
            var q = _db.Members.AsQueryable();
            q = q.Where(m => m.IsActive);
            // 簡化 Target 過濾：AllMembers 或 NewMembersLast30Days
            var today = DateTime.UtcNow.Date;
            return rule.Target switch
            {
                "NewMembersLast30Days" => q.Where(m => m.CreatedAt >= DateTime.UtcNow.AddDays(-30)),
                "BirthdayToday" =>
                    from m in q
                    join p in _db.MemberProfiles on m.Id equals p.MemberId
                    where p.Birthdate != null
                    let b = p.Birthdate!.Value
                    where b.Month == DateOnly.FromDateTime(today).Month && b.Day == DateOnly.FromDateTime(today).Day
                    select m,
                _ => q // AllMembers
            };
        }
    }
}
