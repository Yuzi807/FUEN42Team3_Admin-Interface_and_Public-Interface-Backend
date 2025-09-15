using FUEN42Team3.Backend.Models.EfModels;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Backend.Controllers
{
    [Authorize]
    [Route("admin/point-rules")]
    public class PointRulesAdminController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IBackgroundJobClient _jobs;
        private readonly IRecurringJobManager _recurring;
        private readonly Models.Services.PointsRuleEngineService _engine;
        private readonly IConfiguration _config;

        public PointRulesAdminController(AppDbContext db, IBackgroundJobClient jobs, IRecurringJobManager recurring, Models.Services.PointsRuleEngineService engine, IConfiguration config)
        { _db = db; _jobs = jobs; _recurring = recurring; _engine = engine; _config = config; }

        // 查詢清單
        [HttpGet]
        public async Task<IActionResult> List()
        {
            var list = await _db.PointRules.AsNoTracking().OrderByDescending(x => x.Id).ToListAsync();
            return Ok(list);
        }

        // 取得單筆
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var item = await _db.PointRules.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return item == null ? NotFound() : Ok(item);
        }

        public class UpsertDto
        {
            public string Name { get; set; } = string.Empty;
            public string Status { get; set; } = "Draft"; // Draft/Enabled/Disabled
            public string TriggerType { get; set; } = "Schedule";
            public string? CustomEventKey { get; set; }
            public string? ConditionsJson { get; set; }
            public string PointType { get; set; } = "Fixed"; // Fixed/Random/Percentage
            public int? FixedAmount { get; set; }
            public int? RandomMin { get; set; }
            public int? RandomMax { get; set; }
            public decimal? Percentage { get; set; }
            public int? PerUserMonthlyLimit { get; set; }
            public int? TotalBudget { get; set; }
            public string ExpiryMode { get; set; } = "Days"; // Days/FixedDate/ThisWeekSunday
            public int? ExpiryDays { get; set; }
            public DateTime? ExpiryDate { get; set; }
            public string? ScheduleCron { get; set; }
            public DateTime? StartAt { get; set; }
            public DateTime? EndAt { get; set; }
            public string? Target { get; set; } = "AllMembers";
            public bool NotifyEmail { get; set; }
            public bool NotifyAppPush { get; set; }
            public bool NotifySiteMessage { get; set; }
            public decimal? SpendingThresholdAmount { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] UpsertDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest(new { message = "Name is required" });

            var entity = new PointRule
            {
                Name = dto.Name.Trim(),
                Status = dto.Status,
                TriggerType = dto.TriggerType,
                CustomEventKey = dto.CustomEventKey,
                ConditionsJson = dto.ConditionsJson,
                PointType = dto.PointType,
                FixedAmount = dto.FixedAmount,
                RandomMin = dto.RandomMin,
                RandomMax = dto.RandomMax,
                Percentage = dto.Percentage,
                PerUserMonthlyLimit = dto.PerUserMonthlyLimit,
                TotalBudget = dto.TotalBudget,
                ExpiryMode = dto.ExpiryMode,
                ExpiryDays = dto.ExpiryDays,
                ExpiryDate = dto.ExpiryDate,
                ScheduleCron = dto.ScheduleCron,
                StartAt = dto.StartAt,
                EndAt = dto.EndAt,
                Target = dto.Target ?? "AllMembers",
                NotifyEmail = dto.NotifyEmail,
                NotifyAppPush = dto.NotifyAppPush,
                NotifySiteMessage = dto.NotifySiteMessage,
                SpendingThresholdAmount = dto.SpendingThresholdAmount,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.PointRules.Add(entity);
            await _db.SaveChangesAsync();

            return Ok(new { id = entity.Id });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpsertDto dto)
        {
            var entity = await _db.PointRules.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();

            entity.Name = dto.Name ?? entity.Name;
            entity.Status = dto.Status ?? entity.Status;
            entity.TriggerType = dto.TriggerType ?? entity.TriggerType;
            entity.CustomEventKey = dto.CustomEventKey;
            entity.ConditionsJson = dto.ConditionsJson;
            entity.PointType = dto.PointType ?? entity.PointType;
            entity.FixedAmount = dto.FixedAmount;
            entity.RandomMin = dto.RandomMin;
            entity.RandomMax = dto.RandomMax;
            entity.Percentage = dto.Percentage;
            entity.PerUserMonthlyLimit = dto.PerUserMonthlyLimit;
            entity.TotalBudget = dto.TotalBudget;
            entity.ExpiryMode = dto.ExpiryMode ?? entity.ExpiryMode;
            entity.ExpiryDays = dto.ExpiryDays;
            entity.ExpiryDate = dto.ExpiryDate;
            entity.ScheduleCron = dto.ScheduleCron;
            entity.StartAt = dto.StartAt;
            entity.EndAt = dto.EndAt;
            entity.Target = dto.Target ?? entity.Target;
            entity.NotifyEmail = dto.NotifyEmail;
            entity.NotifyAppPush = dto.NotifyAppPush;
            entity.NotifySiteMessage = dto.NotifySiteMessage;
            entity.SpendingThresholdAmount = dto.SpendingThresholdAmount;
            entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { id = entity.Id });
        }

        [HttpPost("{id:int}/enable")]
        public async Task<IActionResult> Enable(int id)
        {
            var entity = await _db.PointRules.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();
            entity.Status = "Enabled";
            entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            // 若為排程型且有 Cron，啟用時自動掛上 Hangfire 排程
            if (entity.TriggerType == "Schedule" && !string.IsNullOrWhiteSpace(entity.ScheduleCron))
            {
                var recurringId = $"PointRuleV2:{entity.Id}";
                RecurringJobOptions? opt = null;
                try { opt = new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time") }; }
                catch { try { opt = new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei") }; } catch { opt = null; } }
                if (opt != null)
                    _recurring.AddOrUpdate(recurringId, () => _engine.RunScheduleAsync(entity.Id), entity.ScheduleCron, opt);
                else
                    _recurring.AddOrUpdate(recurringId, () => _engine.RunScheduleAsync(entity.Id), entity.ScheduleCron);
            }
            return Ok();
        }

        [HttpPost("{id:int}/disable")]
        public async Task<IActionResult> Disable(int id)
        {
            var entity = await _db.PointRules.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();
            entity.Status = "Disabled";
            entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            // 停用時移除 Hangfire 排程（若存在）
            try { _recurring.RemoveIfExists($"PointRuleV2:{id}"); } catch { /* ignore */ }
            return Ok();
        }

        // 刪除規則（硬刪）。若需要可改軟刪欄位。
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _db.PointRules.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();
            _db.PointRules.Remove(entity);
            await _db.SaveChangesAsync();
            // 刪除時一併移除對應 Hangfire 排程
            try { _recurring.RemoveIfExists($"PointRuleV2:{id}"); } catch { /* ignore */ }
            return Ok();
        }

        // 設定/更新排程（僅當 TriggerType=Schedule）
        [HttpPost("{id:int}/schedule")]
        public async Task<IActionResult> Schedule(int id)
        {
            var entity = await _db.PointRules.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();
            if (entity.TriggerType != "Schedule" || string.IsNullOrWhiteSpace(entity.ScheduleCron))
                return BadRequest(new { message = "Rule is not a schedule type or Cron missing" });

            var recurringId = $"PointRuleV2:{entity.Id}";
            // 與 Program.cs 一致：指定台北時區，避免預設 UTC 造成排程時間偏移
            RecurringJobOptions? opt = null;
            try { opt = new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time") }; }
            catch
            {
                try { opt = new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei") }; }
                catch { opt = null; }
            }

            if (opt != null)
                _recurring.AddOrUpdate(recurringId, () => _engine.RunScheduleAsync(entity.Id), entity.ScheduleCron, opt);
            else
                _recurring.AddOrUpdate(recurringId, () => _engine.RunScheduleAsync(entity.Id), entity.ScheduleCron);
            return Ok(new { recurringId });
        }

        // 簡單預估：固定點數 x 模擬人數
        [HttpGet("estimate")]
        public IActionResult Estimate([FromQuery] int sample = 10, [FromQuery] int amount = 10)
        {
            var total = Math.Max(0, sample) * Math.Max(0, amount);
            var list = Enumerable.Range(1, Math.Max(0, sample)).Select(i => new { MemberId = i, Amount = amount }).ToList();
            return Ok(new { sample, amount, total, list });
        }

        // 立即執行（排程型）
        [HttpPost("{id:int}/run-now")]
        public IActionResult RunNow(int id)
        {
            var jobId = _jobs.Enqueue(() => _engine.RunScheduleAsync(id));
            return Ok(new { jobId });
        }

        // 目標 N 筆抽樣預估（可選 eventAmount 供 Percentage 計算）
        [HttpGet("{id:int}/estimate-targets")]
        public async Task<IActionResult> EstimateTargets(int id, [FromQuery] int count = 20, [FromQuery] decimal? eventAmount = null)
        {
            var rule = await _db.PointRules.FirstOrDefaultAsync(r => r.Id == id);
            if (rule == null) return NotFound(new { message = "rule not found" });

            // 目標成員查詢（與引擎一致）
            IQueryable<Member> QueryTargets(PointRule r)
            {
                var q = _db.Members.AsQueryable().Where(m => m.IsActive);
                var today = DateTime.UtcNow.Date;
                return (r.Target ?? "AllMembers") switch
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

            var targetsQ = QueryTargets(rule).AsNoTracking();
            var sampleCount = Math.Max(0, count);
            // 先簡化為取前 N 筆（若需隨機抽樣，可改為 client 側 shuffle 或以 SQL NEWID 實作）
            var memberIds = await targetsQ.Take(sampleCount).Select(m => m.Id).ToListAsync();
            if (memberIds.Count == 0) return Ok(new { ruleId = id, requested = count, sampled = 0, total = 0, items = Array.Empty<object>() });

            int BaseAmount()
            {
                return rule.PointType switch
                {
                    "Fixed" => rule.FixedAmount ?? 0,
                    "Random" => (int)Math.Floor(((rule.RandomMin ?? 0) + (rule.RandomMax ?? 0)) / 2.0m), // 取區間平均做估算
                    "Percentage" => (eventAmount.HasValue && rule.Percentage.HasValue)
                        ? (int)Math.Floor(eventAmount.Value * (rule.Percentage.Value / 100m))
                        : 0,
                    _ => 0
                };
            }

            var perMemberBase = BaseAmount();
            var items = new List<object>(memberIds.Count);

            // 預算與每人每月上限估算（以 PointLots Reason 前綴統計）
            var now = DateTime.UtcNow;
            var firstDay = new DateTime(now.Year, now.Month, 1);
            var reasonPrefix = $"RuleV2:{rule.Id}";
            var usedTotal = await _db.PointLots.Where(l => l.Reason.StartsWith(reasonPrefix)).SumAsync(l => (int?)l.Points) ?? 0;
            int? remainBudget = rule.TotalBudget.HasValue ? Math.Max(0, rule.TotalBudget.Value - usedTotal) : (int?)null;

            int total = 0;
            foreach (var mid in memberIds)
            {
                var amount = perMemberBase;
                if (amount <= 0) { items.Add(new { memberId = mid, amount = 0 }); continue; }

                // 每人每月上限
                if (rule.PerUserMonthlyLimit.HasValue)
                {
                    var used = await _db.PointLots.Where(l => l.MemberId == mid && l.Reason.StartsWith(reasonPrefix) && l.CreatedAt >= firstDay)
                                                  .SumAsync(l => (int?)l.Points) ?? 0;
                    var remain = Math.Max(0, rule.PerUserMonthlyLimit.Value - used);
                    if (remain <= 0) amount = 0; else if (amount > remain) amount = remain;
                }

                // 總預算
                if (remainBudget.HasValue)
                {
                    if (remainBudget.Value <= 0) amount = 0;
                    else if (amount > remainBudget.Value) amount = remainBudget.Value;
                }

                items.Add(new { memberId = mid, amount });
                total += amount;
                if (remainBudget.HasValue) remainBudget -= amount;
            }

            var avg = memberIds.Count > 0 ? (double)total / memberIds.Count : 0;
            return Ok(new { ruleId = id, requested = count, sampled = memberIds.Count, perMemberBase, average = avg, total, budgetRemaining = remainBudget, items });
        }

        // 事件入口：由前台後端在對應時機呼叫
        public class EventDto
        {
            public string EventType { get; set; } = string.Empty; // RegistrationCompleted / FirstPurchaseCompleted / SpendingThreshold / CustomEvent / BirthdayToday
            public int MemberId { get; set; }
            public int? OrderId { get; set; }
            public decimal? Amount { get; set; } // 消費金額等，百分比計算用
            public string? CustomEventKey { get; set; }
        }

        [HttpPost("events")]
        [AllowAnonymous]
        public async Task<IActionResult> IngestEvent([FromBody] EventDto dto)
        {
            // 簡單的 API Key 驗證，以允許前台後端呼叫
            var apikeyHeader = Request.Headers["X-Points-ApiKey"].FirstOrDefault();
            var expected = _config["PointsEvents:ApiKey"];
            if (string.IsNullOrWhiteSpace(expected) || !string.Equals(apikeyHeader, expected))
            {
                return Unauthorized(new { message = "invalid api key" });
            }
            if (dto.MemberId <= 0 || string.IsNullOrWhiteSpace(dto.EventType))
                return BadRequest(new { message = "MemberId and EventType are required" });

            var affected = await _engine.HandleEventAsync(dto.EventType.Trim(), dto.MemberId, dto.Amount, dto.OrderId, dto.CustomEventKey);
            return Ok(new { affected });
        }
    }
}
