using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Frontend.WebApi.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Frontend.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostTypesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PostTypesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<OptionDto>>> GetPostTypes()
        {
            var types = await _context.PostTypes
                .OrderBy(t => t.Id) // 固定順序
                .Select(t => new OptionDto
                {
                    Id = t.Id,
                    Name = t.Name
                })
                .ToListAsync();

            return Ok(types);
        }

        // GET: api/PostTypes/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PostType>> GetPostType(int id)
        {
            var postType = await _context.PostTypes.FindAsync(id);

            if (postType == null)
            {
                return NotFound();
            }

            return postType;
        }

        // PUT: api/PostTypes/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutPostType(int id, PostType postType)
        {
            if (id != postType.Id)
            {
                return BadRequest();
            }

            _context.Entry(postType).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PostTypeExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/PostTypes
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<PostType>> PostPostType(PostType postType)
        {
            _context.PostTypes.Add(postType);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetPostType", new { id = postType.Id }, postType);
        }

        // DELETE: api/PostTypes/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePostType(int id)
        {
            var postType = await _context.PostTypes.FindAsync(id);
            if (postType == null)
            {
                return NotFound();
            }

            _context.PostTypes.Remove(postType);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool PostTypeExists(int id)
        {
            return _context.PostTypes.Any(e => e.Id == id);
        }
    }
}
