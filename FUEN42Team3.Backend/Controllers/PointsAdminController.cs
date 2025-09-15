using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Backend.Models.Services;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Backend.Controllers
{
    [Authorize]
    [Route("admin/points")]
    public class PointsAdminController : Controller
    {
        private readonly AppDbContext _db;
        private readonly PointsAdminService _svc;
        private readonly IBackgroundJobClient _jobs;
        private readonly IRecurringJobManager _recurringJobs;

        public PointsAdminController(AppDbContext db, PointsAdminService svc, IBackgroundJobClient jobs, IRecurringJobManager recurringJobs)
        {
            _db = db; _svc = svc; _jobs = jobs; _recurringJobs = recurringJobs;
        }

        // UI page for managing rules
        [HttpGet("rules-page")]
        public IActionResult RulesPage()
        {
            return View();
        }

        // List rules (basic)
        [HttpGet("rules")]
        public async Task<IActionResult> Rules()
        {
            var list = await _db.PointGrantRules.AsNoTracking().OrderByDescending(r => r.Id).ToListAsync();
            return Ok(list);
        }

        // Create a new rule
        public class CreateRuleDto
        {
            public string RuleName { get; set; } = string.Empty;
            public string? RuleType { get; set; } = "General"; // required by DB, keep a safe default
            public int Amount { get; set; }
            public int ExpiryDays { get; set; }
            public string? Cron { get; set; }
            public DateTime? StartAt { get; set; }
            public DateTime? EndAt { get; set; }
            public string? Target { get; set; } = "AllMembers";
            public bool IsEnabled { get; set; } = true;
        }

        [HttpPost("rules")]
        public async Task<IActionResult> Create([FromBody] CreateRuleDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.RuleName))
                return BadRequest(new { message = "RuleName is required" });
            if (dto.Amount <= 0)
                return BadRequest(new { message = "Amount must be greater than 0" });
            if (dto.ExpiryDays <= 0)
                return BadRequest(new { message = "ExpiryDays must be greater than 0" });
            var target = string.IsNullOrWhiteSpace(dto.Target) ? "AllMembers" : dto.Target!.Trim();
            var allowedTargets = new[] { "AllMembers", "NewMembersLast30Days", "BirthdayToday" };
            if (!allowedTargets.Contains(target))
                return BadRequest(new { message = $"Invalid Target. Allowed: {string.Join(',', allowedTargets)}" });
            if (!string.IsNullOrWhiteSpace(dto.Cron))
            {
                // 簡單檢查 cron 結構（5 或 6 段）
                var parts = dto.Cron.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5 || parts.Length > 6) return BadRequest(new { message = "Cron format invalid" });
            }

            var rule = new PointGrantRule
            {
                RuleName = dto.RuleName.Trim(),
                RuleType = string.IsNullOrWhiteSpace(dto.RuleType) ? "General" : dto.RuleType!.Trim(),
                Amount = dto.Amount,
                ExpiryDays = dto.ExpiryDays,
                Cron = string.IsNullOrWhiteSpace(dto.Cron) ? null : dto.Cron!.Trim(),
                StartAt = dto.StartAt,
                EndAt = dto.EndAt,
                Target = target,
                IsEnabled = dto.IsEnabled,
                CreatedAt = DateTime.UtcNow
            };

            _db.PointGrantRules.Add(rule);
            await _db.SaveChangesAsync();

            // If cron set and enabled, create recurring job now
            bool scheduleCreated = false; string? scheduleError = null; string? recurringId = null;
            if (rule.IsEnabled && !string.IsNullOrWhiteSpace(rule.Cron))
            {
                try
                {
                    recurringId = $"PointRule:{rule.Id}";
                    _recurringJobs.AddOrUpdate(recurringId, () => _svc.RunNowAsync(rule.Id, null), rule.Cron);
                    scheduleCreated = true;
                }
                catch (Exception ex)
                {
                    scheduleError = ex.Message;
                }
            }

            return Ok(new { id = rule.Id, scheduleCreated, recurringId, scheduleError });
        }

        // Toggle enable
        [HttpPost("rules/{id:int}/toggle")]
        public async Task<IActionResult> Toggle(int id)
        {
            var rule = await _db.PointGrantRules.FirstOrDefaultAsync(r => r.Id == id);
            if (rule == null) return NotFound();
            rule.IsEnabled = !rule.IsEnabled;
            await _db.SaveChangesAsync();
            return Ok(new { rule.Id, rule.IsEnabled });
        }

        // Preview
        [HttpGet("rules/{id:int}/preview")]
        public async Task<IActionResult> Preview(int id)
        {
            var result = await _svc.PreviewAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        // Run Now (enqueue a background job)
        [HttpPost("rules/{id:int}/run-now")]
        public IActionResult RunNow(int id)
        {
            var jobId = _jobs.Enqueue(() => _svc.RunNowAsync(id, null));
            return Ok(new { jobId });
        }

        // Create or update recurring job according to Cron
        [HttpPost("rules/{id:int}/schedule")]
        public async Task<IActionResult> Schedule(int id)
        {
            var rule = await _db.PointGrantRules.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
            if (rule == null) return NotFound();
            if (string.IsNullOrWhiteSpace(rule.Cron)) return BadRequest(new { message = "Cron is empty" });

            var recurringId = $"PointRule:{rule.Id}";
            _recurringJobs.AddOrUpdate(recurringId, () => _svc.RunNowAsync(rule.Id, null), rule.Cron);
            return Ok(new { recurringId, rule.Cron });
        }
    }
}
