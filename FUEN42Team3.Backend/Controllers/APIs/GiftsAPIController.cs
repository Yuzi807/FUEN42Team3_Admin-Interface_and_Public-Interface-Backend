using FUEN42Team3.Backend.Models.EfModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Backend.Controllers.APIs
{
    [Route("api/[controller]")]
    [ApiController]
    public class GiftsAPIController : ControllerBase
    {
        private readonly AppDbContext _context;
        
        public GiftsAPIController(AppDbContext context)
        {
            _context = context;
        }
        
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var gifts = await _context.Gifts
                .Where(g => !g.IsDeleted)
                .OrderBy(g => g.Id)
                .Select(g => new { g.Id, g.Name })
                .ToListAsync();
                
            return Ok(gifts);
        }
    }
}