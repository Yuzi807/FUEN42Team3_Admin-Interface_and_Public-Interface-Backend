using FUEN42Team3.Backend.Models.EfModels;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Backend.Models.Services
{
    public class PointsRuleEngineService
    {
        private readonly AppDbContext _db;
        public PointsRuleEngineService(AppDbContext db) { _db = db; }

        // 執行：排程型（沒有外部交易金額上下文）
        public async Task<int> RunScheduleAsync(int ruleId)
        {
            var rule = await _db.PointRules.FirstOrDefaultAsync(r => r.Id == ruleId && r.Status == "Enabled" && r.TriggerType == "Schedule");
            if (rule == null) return 0;

            var targetMembers = await QueryTargetMembers(rule).Select(m => m.Id).ToListAsync();
            if (targetMembers.Count == 0) return 0;

            // 計算每人點數
            var rnd = new Random();
            int PointsOf() => rule.PointType switch
            {
                "Fixed" => rule.FixedAmount ?? 0,
                "Random" => rnd.Next(rule.RandomMin ?? 0, (rule.RandomMax ?? 0) + 1),
                _ => 0 // Percentage 不在排程情境
            };

            // 總額度與每人每月上限檢核（以 Reason 前綴 RuleV2:<id> 追蹤）
            var reasonPrefix = $"RuleV2:{rule.Id}";
            var now = TaipeiTime.Now;
            var firstDay = new DateTime(now.Year, now.Month, 1);

            // 計算目前總耗用
            var usedTotal = await _db.PointLots.Where(l => l.Reason.StartsWith(reasonPrefix)).SumAsync(l => (int?)l.Points) ?? 0;
            int? remainBudget = rule.TotalBudget.HasValue ? Math.Max(0, rule.TotalBudget.Value - usedTotal) : (int?)null;

            // 到期日
            DateTime CalcExpiry() => rule.ExpiryMode switch
            {
                "Days" => now.AddDays(rule.ExpiryDays ?? 0),
                "FixedDate" => rule.ExpiryDate ?? now,
                "ThisWeekSunday" =>
                    now.Date.AddDays(7 - (int)now.DayOfWeek).AddHours(23).AddMinutes(59).AddSeconds(59),
                _ => now.AddDays(30)
            };
            var expiry = CalcExpiry();

            int created = 0;
            foreach (var mid in targetMembers)
            {
                var amount = PointsOf();
                if (amount <= 0) continue;

                // 每人每月上限
                if (rule.PerUserMonthlyLimit.HasValue)
                {
                    var used = await _db.PointLots.Where(l => l.MemberId == mid && l.Reason.StartsWith(reasonPrefix) && l.CreatedAt >= firstDay)
                                                  .SumAsync(l => (int?)l.Points) ?? 0;
                    var remain = Math.Max(0, rule.PerUserMonthlyLimit.Value - used);
                    if (remain <= 0) continue;
                    if (amount > remain) amount = remain;
                }

                // 總額度
                if (remainBudget.HasValue)
                {
                    if (remainBudget.Value <= 0) break; // 沒預算了
                    if (amount > remainBudget.Value) amount = remainBudget.Value;
                    remainBudget -= amount;
                }

                var lot = new PointLot
                {
                    MemberId = mid,
                    Points = amount,
                    RemainingPoints = amount,
                    Reason = $"{reasonPrefix}:{rule.Name}",
                    ExpiresAt = expiry,
                    CreatedAt = now
                };
                _db.PointLots.Add(lot);
                created++;
            }

            await _db.SaveChangesAsync();
            return created;
        }

        private IQueryable<Member> QueryTargetMembers(PointRule rule)
        {
            var q = _db.Members.AsQueryable().Where(m => m.IsActive);
            var today = TaipeiTime.Now.Date;
            return (rule.Target ?? "AllMembers") switch
            {
                "NewMembersLast30Days" => q.Where(m => m.CreatedAt >= DateTime.UtcNow.AddDays(-30)),
                "BirthdayToday" => from m in q
                                   join p in _db.MemberProfiles on m.Id equals p.MemberId
                                   where p.Birthdate != null
                                   let b = p.Birthdate!.Value
                                   where b.Month == DateOnly.FromDateTime(today).Month && b.Day == DateOnly.FromDateTime(today).Day
                                   select m,
                _ => q
            };
        }

        // 事件入口：依 EventType 取可用規則並發點
        public async Task<int> HandleEventAsync(string eventType, int memberId, decimal? amount, int? orderId, string? customKey)
        {
            var now = TaipeiTime.Now;

            // 取得符合的 Enabled 規則
            var q = _db.PointRules.AsQueryable().Where(r => r.Status == "Enabled");
            q = eventType switch
            {
                "RegistrationCompleted" => q.Where(r => r.TriggerType == "RegistrationCompleted"),
                "FirstPurchaseCompleted" => q.Where(r => r.TriggerType == "FirstPurchaseCompleted"),
                "SpendingThreshold" => q.Where(r => r.TriggerType == "SpendingThreshold"),
                "CustomEvent" => q.Where(r => r.TriggerType == "CustomEvent" && r.CustomEventKey == customKey),
                "BirthdayToday" => q.Where(r => r.TriggerType == "BirthdayToday"),
                _ => q.Where(r => false)
            };

            var rules = await q.ToListAsync();
            if (rules.Count == 0) return 0;

            var member = await _db.Members.FirstOrDefaultAsync(m => m.Id == memberId && m.IsActive);
            if (member == null) return 0;

            int created = 0;
            foreach (var rule in rules)
            {
                // 篩目標（如規則要求 BirthdayToday 且 member 生日不符，則跳過）
                if (rule.Target == "BirthdayToday")
                {
                    var prof = await _db.MemberProfiles.FirstOrDefaultAsync(p => p.MemberId == memberId);
                    if (prof?.Birthdate == null) continue;
                    var b = prof.Birthdate.Value;
                    var d = DateOnly.FromDateTime(now);
                    if (b.Month != d.Month || b.Day != d.Day) continue;
                }

                // 計算點數
                var rnd = new Random();
                int amountPoints = rule.PointType switch
                {
                    "Fixed" => rule.FixedAmount ?? 0,
                    "Random" => rnd.Next(rule.RandomMin ?? 0, (rule.RandomMax ?? 0) + 1),
                    "Percentage" => amount.HasValue && rule.Percentage.HasValue
                        ? (int)Math.Floor(amount.Value * (rule.Percentage.Value / 100m))
                        : 0,
                    _ => 0
                };
                if (amountPoints <= 0) continue;

                // 消費達標：需金額≥門檻
                if (rule.TriggerType == "SpendingThreshold" && rule.SpendingThresholdAmount.HasValue)
                {
                    if (!amount.HasValue || amount.Value < rule.SpendingThresholdAmount.Value) continue;
                }

                // 每人每月上限、總額度
                var reasonPrefix = $"RuleV2:{rule.Id}";
                var firstDay = new DateTime(now.Year, now.Month, 1);

                if (rule.PerUserMonthlyLimit.HasValue)
                {
                    var used = await _db.PointLots.Where(l => l.MemberId == memberId && l.Reason.StartsWith(reasonPrefix) && l.CreatedAt >= firstDay)
                                                  .SumAsync(l => (int?)l.Points) ?? 0;
                    var remain = Math.Max(0, rule.PerUserMonthlyLimit.Value - used);
                    if (remain <= 0) continue;
                    if (amountPoints > remain) amountPoints = remain;
                }

                if (rule.TotalBudget.HasValue)
                {
                    var usedTotal = await _db.PointLots.Where(l => l.Reason.StartsWith(reasonPrefix)).SumAsync(l => (int?)l.Points) ?? 0;
                    var remainBudget = Math.Max(0, rule.TotalBudget.Value - usedTotal);
                    if (remainBudget <= 0) continue;
                    if (amountPoints > remainBudget) amountPoints = remainBudget;
                }

                // 到期
                DateTime CalcExpiry() => rule.ExpiryMode switch
                {
                    "Days" => now.AddDays(rule.ExpiryDays ?? 0),
                    "FixedDate" => rule.ExpiryDate?.ToUniversalTime() ?? now,
                    "ThisWeekSunday" => now.Date.AddDays(7 - (int)now.DayOfWeek).AddHours(23).AddMinutes(59).AddSeconds(59),
                    _ => now.AddDays(30)
                };

                var lot = new PointLot
                {
                    MemberId = memberId,
                    Points = amountPoints,
                    RemainingPoints = amountPoints,
                    Reason = $"{reasonPrefix}:{rule.Name}:{eventType}",
                    ExpiresAt = CalcExpiry(),
                    CreatedAt = now
                };
                _db.PointLots.Add(lot);

                // 可記 MemberPointLog
                _db.MemberPointLogs.Add(new MemberPointLog
                {
                    MemberId = memberId,
                    OrderId = orderId,
                    ChangeAmount = amountPoints,
                    Reason = $"{eventType}:{rule.Name}",
                    CreatedAt = TaipeiTime.Now
                });

                created++;
            }

            await _db.SaveChangesAsync();
            return created;
        }
    }
}
