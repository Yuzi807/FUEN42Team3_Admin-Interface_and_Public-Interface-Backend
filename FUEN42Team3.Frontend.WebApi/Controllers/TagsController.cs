using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Frontend.WebApi.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Frontend.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TagsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private const int Public = 1; // 你的公開狀態代碼

        public TagsController(AppDbContext context)
        {
            _context = context;
        }

        // === 既有 CRUD ===

        // GET: api/Tags
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Tag>>> GetTags()
        {
            return await _context.Tags.AsNoTracking().ToListAsync();
        }

        // GET: api/Tags/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Tag>> GetTag(int id)
        {
            var tag = await _context.Tags.FindAsync(id);
            if (tag == null) return NotFound();
            return tag;
        }

        // PUT: api/Tags/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTag(int id, Tag tag)
        {
            if (id != tag.Id) return BadRequest();

            _context.Entry(tag).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TagExists(id)) return NotFound();
                throw;
            }
            return NoContent();
        }

        // POST: api/Tags
        [HttpPost]
        public async Task<ActionResult<Tag>> PostTag(Tag tag)
        {
            _context.Tags.Add(tag);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetTag), new { id = tag.Id }, tag);
        }

        // DELETE: api/Tags/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTag(int id)
        {
            var tag = await _context.Tags.FindAsync(id);
            if (tag == null) return NotFound();

            _context.Tags.Remove(tag);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private bool TagExists(int id) => _context.Tags.Any(e => e.Id == id);

        // === 新增：本地快取建議會用到的一次性清單 ===
        // GET: /api/tags/options?onlyActive=true&onlyPublic=true
        [HttpGet("options")]
        public async Task<ActionResult<IEnumerable<OptionDto>>> GetOptions(
            [FromQuery] bool onlyActive = true,
            [FromQuery] bool onlyPublic = true)
        {
            var q = _context.Tags.AsNoTracking().AsQueryable();

            if (onlyActive)
                q = q.Where(t => t.IsActive);

            if (onlyPublic)
            {
                // 你的模型：Tag -> Posts（直接 Post）
                q = q.Where(t => t.Posts.Any(p => p.StatusId == Public && !p.IsDeleted));

                // 如果你的模型其實是 Tag.PostTags -> Post，就改成：
                // q = q.Where(t => t.PostTags.Any(pt => pt.Post.StatusId == Public && !pt.Post.IsDeleted));
            }

            var list = await q
                .OrderBy(t => t.Name)
                .Select(t => new OptionDto { Id = t.Id, Name = t.Name })
                .ToListAsync();

            return Ok(list);
        }

        // === 既有：熱門標籤 ===
        // GET: /api/tags/hot?count=10
        [HttpGet("hot")]
        public async Task<ActionResult<IEnumerable<OptionDto>>> GetHotTags([FromQuery] int count = 10)
        {
            count = Math.Clamp(count, 1, 50);

            // 你的模型：Tag -> Posts（直接 Post）
            var hotTags = await _context.Tags
                .AsNoTracking()
                .Where(t => t.IsActive)
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    UsageCount = t.Posts.Count(p => p.StatusId == Public && !p.IsDeleted)
                })
                .Where(x => x.UsageCount > 0)
                .OrderByDescending(x => x.UsageCount)
                .ThenBy(x => x.Name)
                .Take(count)
                .Select(x => new OptionDto { Id = x.Id, Name = x.Name })
                .ToListAsync();

            // 如果你的模型其實是 Tag.PostTags -> Post，就把上面 Select/Count 改成：
            // UsageCount = t.PostTags.Count(pt => pt.Post.StatusId == Public && !pt.Post.IsDeleted)

            return Ok(hotTags);
        }

        // === 可選：若未來改回遠端即時建議，可啟用這支 ===
        // GET: /api/tags/suggest?q=vue&limit=8&onlyActive=true&onlyPublic=true
        [HttpGet("suggest")]
        public async Task<ActionResult<IEnumerable<OptionDto>>> Suggest(
            [FromQuery] string q = "",
            [FromQuery] int limit = 8,
            [FromQuery] bool onlyActive = true,
            [FromQuery] bool onlyPublic = true)
        {
            q = (q ?? "").Trim();
            if (q.Length == 0) return Ok(Array.Empty<OptionDto>());
            limit = Math.Clamp(limit, 1, 50);

            var baseQuery = _context.Tags.AsNoTracking().AsQueryable();

            if (onlyActive)
                baseQuery = baseQuery.Where(t => t.IsActive);

            if (onlyPublic)
            {
                // Tag -> Posts（直接 Post）
                baseQuery = baseQuery.Where(t => t.Posts.Any(p => p.StatusId == Public && !p.IsDeleted));

                // 如果你的模型其實是 Tag.PostTags -> Post，就改成：
                // baseQuery = baseQuery.Where(t => t.PostTags.Any(pt => pt.Post.StatusId == Public && !pt.Post.IsDeleted));
            }

            // 體驗較好：先前綴、後包含
            var starts = await baseQuery
                .Where(t => EF.Functions.Like(t.Name, $"{q}%"))
                .OrderBy(t => t.Name)
                .Take(limit)
                .Select(t => new OptionDto { Id = t.Id, Name = t.Name })
                .ToListAsync();

            var remains = limit - starts.Count;
            if (remains > 0)
            {
                var contains = await baseQuery
                    .Where(t => !EF.Functions.Like(t.Name, $"{q}%") && EF.Functions.Like(t.Name, $"%{q}%"))
                    .OrderBy(t => t.Name)
                    .Take(remains)
                    .Select(t => new OptionDto { Id = t.Id, Name = t.Name })
                    .ToListAsync();

                starts.AddRange(contains);
            }

            return Ok(starts);
        }
    }
}
