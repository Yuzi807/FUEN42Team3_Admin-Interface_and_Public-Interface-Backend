using System.ComponentModel.DataAnnotations;
using System.IO;
using FUEN42Team3.Backend.Models.EfModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace FUEN42Team3.Backend.Controllers
{
    [Authorize]
    // 這是後台 MVC 頁面用控制器，不需要列入前台 WebApi 的 Swagger，避免 IFormFile 表單上傳造成 Swashbuckle 解析錯誤
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("admin/gift-rules")]
    public class GiftRulesAdminController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        public GiftRulesAdminController(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

        // UI
        [HttpGet]
        public IActionResult Index() => View();

        // ======= API: Rules =======
        public record RuleListItemDto(int Id, string Name, string ConditionType, decimal ConditionValue, DateTime? StartDate, DateTime? EndDate, bool IsDeleted);

        [HttpGet("list")]
        public async Task<IActionResult> List()
        {
            var list = await _db.GiftRules.AsNoTracking()
                .OrderByDescending(r => r.Id)
                .Select(r => new RuleListItemDto(r.Id, r.Name, r.ConditionType, r.ConditionValue, r.StartDate, r.EndDate, r.IsDeleted))
                .ToListAsync();
            return Ok(list);
        }

        public class UpsertRuleDto
        {
            [Required]
            public string Name { get; set; } = string.Empty;
            [Required]
            public string ConditionType { get; set; } = "Amount"; // Amount | Quantity
            [Range(0, double.MaxValue)]
            public decimal ConditionValue { get; set; }
            public DateTime? StartDate { get; set; }
            public DateTime? EndDate { get; set; }
            public List<UpsertRuleItemDto>? Items { get; set; }
        }

        public class UpsertRuleItemDto
        {
            [Required]
            public int GiftId { get; set; }
            [Range(1, int.MaxValue)]
            public int Quantity { get; set; } = 1;
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var rule = await _db.GiftRules.AsNoTracking()
                .Where(r => r.Id == id)
                .Select(r => new
                {
                    r.Id,
                    r.Name,
                    r.ConditionType,
                    r.ConditionValue,
                    r.StartDate,
                    r.EndDate,
                    r.IsDeleted,
                    Items = r.GiftRuleItems.Where(i => !i.IsDeleted).Select(i => new
                    {
                        i.Id,
                        i.GiftId,
                        GiftName = i.Gift.Name,
                        i.Quantity
                    }).ToList()
                }).FirstOrDefaultAsync();
            return rule == null ? NotFound() : Ok(rule);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] UpsertRuleDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest(new { message = "Name is required" });
            if (dto.ConditionType != "Amount" && dto.ConditionType != "Quantity")
                return BadRequest(new { message = "ConditionType must be Amount or Quantity" });

            var now = DateTime.UtcNow;
            var uid = GetUserId();
            var entity = new GiftRule
            {
                Name = dto.Name.Trim(),
                ConditionType = dto.ConditionType,
                ConditionValue = dto.ConditionValue,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = uid,
                UpdatedBy = uid,
                IsDeleted = false
            };
            if (dto.Items != null)
            {
                foreach (var it in dto.Items)
                {
                    entity.GiftRuleItems.Add(new GiftRuleItem
                    {
                        GiftId = it.GiftId,
                        Quantity = Math.Max(1, it.Quantity),
                        CreatedAt = now,
                        UpdatedAt = now,
                        CreatedBy = uid,
                        UpdatedBy = uid,
                        IsDeleted = false
                    });
                }
            }
            _db.GiftRules.Add(entity);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                // Unique name constraint
                return BadRequest(new { message = ex.InnerException?.Message ?? ex.Message });
            }
            return Ok(new { id = entity.Id });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpsertRuleDto dto)
        {
            var entity = await _db.GiftRules.Include(r => r.GiftRuleItems).FirstOrDefaultAsync(r => r.Id == id);
            if (entity == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(dto.Name)) entity.Name = dto.Name.Trim();
            if (!string.IsNullOrWhiteSpace(dto.ConditionType)) entity.ConditionType = dto.ConditionType;
            entity.ConditionValue = dto.ConditionValue;
            entity.StartDate = dto.StartDate;
            entity.EndDate = dto.EndDate;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = GetUserId();

            // Optional: replace items if provided
            if (dto.Items != null)
            {
                // Soft-delete existing items
                foreach (var it in entity.GiftRuleItems)
                {
                    it.IsDeleted = true;
                    it.DeletedAt = DateTime.UtcNow;
                    it.DeletedBy = entity.UpdatedBy;
                }
                // Add new ones
                foreach (var it in dto.Items)
                {
                    entity.GiftRuleItems.Add(new GiftRuleItem
                    {
                        GiftId = it.GiftId,
                        Quantity = Math.Max(1, it.Quantity),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedBy = entity.UpdatedBy ?? 0,
                        UpdatedBy = entity.UpdatedBy,
                        IsDeleted = false
                    });
                }
            }

            await _db.SaveChangesAsync();
            return Ok(new { id = entity.Id });
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _db.GiftRules.FirstOrDefaultAsync(r => r.Id == id);
            if (entity == null) return NotFound();
            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
            entity.DeletedBy = GetUserId();
            await _db.SaveChangesAsync();
            return Ok();
        }

        // ======= API: Rule Items =======
        public class AddItemDto { public int GiftId { get; set; } public int Quantity { get; set; } = 1; }

        [HttpPost("{id:int}/items")]
        public async Task<IActionResult> AddItem(int id, [FromBody] AddItemDto dto)
        {
            var rule = await _db.GiftRules.FirstOrDefaultAsync(r => r.Id == id);
            if (rule == null) return NotFound();
            var now = DateTime.UtcNow;
            var uid = GetUserId();
            var item = new GiftRuleItem
            {
                RuleId = id,
                GiftId = dto.GiftId,
                Quantity = Math.Max(1, dto.Quantity),
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = uid,
                UpdatedBy = uid,
                IsDeleted = false
            };
            _db.GiftRuleItems.Add(item);
            await _db.SaveChangesAsync();
            return Ok(new { id = item.Id });
        }

        [HttpDelete("{id:int}/items/{itemId:int}")]
        public async Task<IActionResult> RemoveItem(int id, int itemId)
        {
            var item = await _db.GiftRuleItems.FirstOrDefaultAsync(i => i.Id == itemId && i.RuleId == id);
            if (item == null) return NotFound();
            item.IsDeleted = true;
            item.DeletedAt = DateTime.UtcNow;
            item.DeletedBy = GetUserId();
            await _db.SaveChangesAsync();
            return Ok();
        }

        // ======= API: Gifts (for selection) =======
        [HttpGet("gifts")]
        public async Task<IActionResult> Gifts()
        {
            var gifts = await _db.Gifts.AsNoTracking()
                .Where(g => !g.IsDeleted)
                .OrderBy(g => g.Name)
                .Select(g => new { g.Id, g.Name, g.ImageUrl })
                .ToListAsync();
            return Ok(gifts);
        }

        // 上傳/變更贈品圖片，並更新 Gift.ImageUrl
        [HttpPost("gifts/{giftId:int}/image")]
        [IgnoreAntiforgeryToken]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(10_000_000)] // ~10MB
        public async Task<IActionResult> UploadGiftImage(int giftId, [FromForm] IFormFile? file)
        {
            try
            {
                if (file == null || file.Length == 0) return BadRequest(new { message = "no file" });

                var gift = await _db.Gifts.FirstOrDefaultAsync(g => g.Id == giftId && !g.IsDeleted);
                if (gift == null) return NotFound(new { message = "gift not found" });

                // 驗證副檔名
                var allowedExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(ext) || !allowedExt.Contains(ext))
                {
                    return BadRequest(new { message = "invalid file type" });
                }

                var webRoot = _env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
                var targetDir = Path.Combine(webRoot, "img", "gift");
                Directory.CreateDirectory(targetDir);

                var fileName = $"{giftId}{ext.ToLowerInvariant()}";
                var fullPath = Path.Combine(targetDir, fileName);
                using (var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
                {
                    await file.CopyToAsync(stream);
                }

                // 嘗試同步一份到前端專案 yuzi/public/img/gift 供前台使用
                try
                {
                    var contentRoot = _env.ContentRootPath; // .../FUEN42Team3/FUEN42Team3.Backend
                    var workspaceRoot = Directory.GetParent(contentRoot)?.Parent?.FullName; // .../0815修正更新
                    if (!string.IsNullOrEmpty(workspaceRoot))
                    {
                        var feGiftDir = Path.Combine(workspaceRoot, "yuzi", "public", "img", "gift");
                        if (Directory.Exists(feGiftDir))
                        {
                            Directory.CreateDirectory(feGiftDir);
                            var fePath = Path.Combine(feGiftDir, fileName);
                            System.IO.File.Copy(fullPath, fePath, overwrite: true);
                        }
                    }
                }
                catch { /* 忽略鏡像失敗，不影響後台顯示 */ }

                // 更新 DB 圖片路徑（相對於網站根目錄）
                gift.ImageUrl = $"/img/gift/{fileName}";
                gift.UpdatedAt = DateTime.UtcNow;
                var uid = GetUserId();
                gift.UpdatedBy = uid > 0 ? uid : null;
                await _db.SaveChangesAsync();

                return Ok(new { imageUrl = gift.ImageUrl });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private int GetUserId()
        {
            var userIdStr = User.FindFirst("UserId")?.Value ?? User.Identity?.Name;
            return int.TryParse(userIdStr, out var uid) ? uid : 0;
        }
    }
}
