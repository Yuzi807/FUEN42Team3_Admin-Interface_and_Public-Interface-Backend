using FUEN42Team3.Backend.Models.EfModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Backend.Controllers.APIs
{
    [Route("api/[controller]")]
    [ApiController]
    public class DeliveryMethodsController : ControllerBase
    {
        private readonly AppDbContext _context;
        
        public DeliveryMethodsController(AppDbContext context)
        {
            _context = context;
        }
        
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var methods = await _context.DeliveryMethods
                .Where(d => d.IsActive && !d.IsDeleted)
                .Select(d => new { d.Id, d.ShippingName, d.BaseShippingCost })
                .ToListAsync();
                
            return Ok(methods);
        }
    }
}