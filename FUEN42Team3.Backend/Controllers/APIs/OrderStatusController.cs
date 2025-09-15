using FUEN42Team3.Backend.Models.EfModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Backend.Controllers.APIs
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderStatusController : ControllerBase
    {
        private readonly AppDbContext _context;
        
        public OrderStatusController(AppDbContext context)
        {
            _context = context;
        }
        
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var statuses = await _context.OrderStatuses
                .OrderBy(s => s.Id)
                .Select(s => new { s.Id, s.StatusName, s.Description })
                .ToListAsync();
                
            return Ok(statuses);
        }
    }
}