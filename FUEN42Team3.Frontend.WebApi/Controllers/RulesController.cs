using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Frontend.WebApi.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Frontend.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RulesController : ControllerBase
    {
        private readonly AppDbContext _context;
        public RulesController(AppDbContext context) => _context = context;

        // GET: api/rules  取得檢舉規則清單（前端表單用）
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RuleDto>>> GetRules()
        {
            var rules = await _context.Rules
                .OrderBy(r => r.Id)
                .Select(r => new RuleDto { Id = r.Id, Name = r.Name, Description = r.Description })
                .ToListAsync();
            return Ok(rules);
        }
    }
}
