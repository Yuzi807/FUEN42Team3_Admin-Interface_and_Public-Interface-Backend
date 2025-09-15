using FUEN42Team3.Backend.Models.EfModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Backend.Controllers.APIs
{
    [ApiController]
    [Route("api/Brands")] // 前端請用 /api/Brands
    public class BrandsApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public BrandsApiController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // 下拉選用（僅未刪除，預設只回啟用）
        [HttpGet]
        public async Task<IActionResult> GetForSelect([FromQuery] bool includeInactive = false)
        {
            var query = _context.Brands.AsNoTracking().Where(b => !b.IsDeleted);
            if (!includeInactive) query = query.Where(b => b.IsActive);

            var list = await query
                .OrderBy(b => b.BrandName)
                .Select(b => new { id = b.Id, name = b.BrandName })
                .ToListAsync();

            return Ok(list);
        }

        // 後台表格：分頁 + 關鍵字（統一回傳形狀）
        // GET /api/Brands/paged?page=1&pageSize=10&keyword=xxx
        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? keyword = null)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var query = _context.Brands.AsNoTracking().Where(b => !b.IsDeleted);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim();
                query = query.Where(b => b.BrandName.Contains(kw));
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderBy(b => b.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new
                {
                    b.Id,
                    BrandName = b.BrandName,
                    LogoUrl = b.LogoUrl,
                    b.IsActive,
                    b.CreatedAt,
                    b.UpdatedAt
                })
                .ToListAsync();

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            return Ok(new { items, totalPages, totalCount });
        }

        // 取得單一
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var b = await _context.Brands.AsNoTracking()
                .Where(x => x.Id == id && !x.IsDeleted)
                .Select(x => new
                {
                    x.Id,
                    BrandName = x.BrandName,
                    LogoUrl = x.LogoUrl,
                    x.IsActive,
                    x.CreatedAt,
                    x.UpdatedAt
                })
                .FirstOrDefaultAsync();

            return b == null ? NotFound() : Ok(b);
        }

        // 新增
        [HttpPost]
        public async Task<IActionResult> Create([FromForm] BrandUpsertDto dto, IFormFile? imageFile)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.BrandName))
                return BadRequest("BrandName 必填");

            var entity = new Brand
            {
                BrandName = dto.BrandName.Trim(),
                IsActive = dto.IsActive,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = dto.CreatedBy ?? 1,   // ← 這裡
                UpdatedBy = dto.UpdatedBy ?? 1    // ← 這裡
            };

            if (imageFile != null && imageFile.Length > 0)
                entity.LogoUrl = await SaveImageAsync(imageFile, "uploads/band");

            _context.Brands.Add(entity);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, new
            {
                entity.Id,
                BrandName = entity.BrandName,
                LogoUrl = entity.LogoUrl,
                entity.IsActive,
                entity.CreatedAt
            });
        }

        // 修改
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromForm] BrandUpsertDto dto, IFormFile? imageFile)
        {
            var brand = await _context.Brands.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (brand == null) return NotFound();
            if (dto == null || string.IsNullOrWhiteSpace(dto.BrandName)) return BadRequest("BrandName 必填");

            brand.BrandName = dto.BrandName.Trim();
            brand.IsActive = dto.IsActive;
            brand.UpdatedAt = DateTime.UtcNow;
            brand.UpdatedBy = dto.UpdatedBy ?? brand.UpdatedBy; // ← 這裡

            if (imageFile != null && imageFile.Length > 0)
                brand.LogoUrl = await SaveImageAsync(imageFile, "uploads/band");

            await _context.SaveChangesAsync();

            return Ok(new
            {
                brand.Id,
                BrandName = brand.BrandName,
                LogoUrl = brand.LogoUrl,
                brand.IsActive,
                brand.UpdatedAt
            });
        }


        // 軟刪除
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var brand = await _context.Brands.FindAsync(id);
            if (brand == null || brand.IsDeleted) return NotFound();

            brand.IsDeleted = true;
            brand.DeletedAt = DateTime.UtcNow;
            brand.DeletedBy = 1; // TODO: 改為實際登入者
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ====== 小工具：存檔到 wwwroot/{subFolder}，回傳相對路徑（/uploads/...）======
        private async Task<string> SaveImageAsync(IFormFile file, string subFolder)
        {
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var folder = Path.Combine(webRoot, subFolder.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(folder);

            var ext = Path.GetExtension(file.FileName);
            var name = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(folder, name);

            using (var fs = System.IO.File.Create(fullPath))
            {
                await file.CopyToAsync(fs);
            }
            // 轉成網站可用的相對路徑
            return "/uploads/band/" + name;
        }

        // ====== DTO ======
        public class BrandUpsertDto
        {
            public string BrandName { get; set; } = "";
            public bool IsActive { get; set; } = true;
            public int? CreatedBy { get; set; } = 1;
            public int? UpdatedBy { get; set; } = 1;
        }
    }
}
