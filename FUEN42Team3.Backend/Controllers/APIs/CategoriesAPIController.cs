using FUEN42Team3.Backend.Models.EfModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Backend.Controllers.APIs
{
    [ApiController]
    [Route("api/Categories")] // 前端請用 /api/Categories
    public class CategoriesApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CategoriesApiController(AppDbContext context)
        {
            _context = context;
        }

        // 下拉選用（僅未刪除，預設只回啟用）
        [HttpGet]
        public async Task<IActionResult> GetForSelect([FromQuery] bool includeInactive = false)
        {
            var query = _context.Categories.AsNoTracking().Where(c => !c.IsDeleted);
            if (!includeInactive) query = query.Where(c => c.IsActive);

            var list = await query
                .OrderBy(c => c.CategoryName)
                .Select(c => new { id = c.Id, name = c.CategoryName })
                .ToListAsync();

            return Ok(list);
        }

        // 後台表格：分頁 + 關鍵字
        // GET /api/Categories/paged?page=1&pageSize=10&keyword=xxx
        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? keyword = null)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var query = _context.Categories.AsNoTracking().Where(c => !c.IsDeleted);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim();
                query = query.Where(c => c.CategoryName.Contains(kw) || (c.Description ?? "").Contains(kw));
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderBy(c => c.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    c.Id,
                    CategoryName = c.CategoryName,
                    c.Description,
                    c.IsActive,
                    c.CreatedAt,
                    c.UpdatedAt
                })
                .ToListAsync();

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            return Ok(new { items, totalPages, totalCount });
        }

        // 取得單一
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var c = await _context.Categories.AsNoTracking()
                .Where(x => x.Id == id && !x.IsDeleted)
                .Select(x => new
                {
                    x.Id,
                    CategoryName = x.CategoryName,
                    x.Description,
                    x.IsActive,
                    x.CreatedAt,
                    x.UpdatedAt
                })
                .FirstOrDefaultAsync();

            return c == null ? NotFound() : Ok(c);
        }

        // 新增（不處理圖片，上層用 FormData 也可）
        [HttpPost]
        public async Task<IActionResult> Create([FromForm] CategoryUpsertDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.CategoryName))
                return BadRequest("CategoryName 必填");

            var entity = new Category
            {
                CategoryName = dto.CategoryName.Trim(),
                Description = dto.Description,
                IsActive = dto.IsActive,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = dto.CreatedBy ?? 1,
                UpdatedBy = dto.UpdatedBy ?? 1
            };

            _context.Categories.Add(entity);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, new
            {
                entity.Id,
                CategoryName = entity.CategoryName,
                entity.Description,
                entity.IsActive,
                entity.CreatedAt
            });
        }

        // 修改（不處理圖片）
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromForm] CategoryUpsertDto dto)
        {
            var category = await _context.Categories.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (category == null) return NotFound();
            if (dto == null || string.IsNullOrWhiteSpace(dto.CategoryName)) return BadRequest("CategoryName 必填");

            category.CategoryName = dto.CategoryName.Trim();
            category.Description = dto.Description;
            category.IsActive = dto.IsActive;
            category.UpdatedAt = DateTime.UtcNow;
            category.UpdatedBy = dto.UpdatedBy ?? category.UpdatedBy;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                category.Id,
                CategoryName = category.CategoryName,
                category.Description,
                category.IsActive,
                category.UpdatedAt
            });
        }

        // 軟刪除
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null || category.IsDeleted) return NotFound();

            category.IsDeleted = true;
            category.DeletedAt = DateTime.UtcNow;
            category.DeletedBy = 1; // TODO: 實際登入者
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DTO
        public class CategoryUpsertDto
        {
            public string CategoryName { get; set; } = "";
            public string? Description { get; set; }
            public bool IsActive { get; set; } = true;
            public int? CreatedBy { get; set; } = 1;
            public int? UpdatedBy { get; set; } = 1;
        }
    }
}
