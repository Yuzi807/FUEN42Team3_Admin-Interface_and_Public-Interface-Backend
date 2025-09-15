using FUEN42Team3.Frontend.WebApi.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FUEN42Team3.Backend.Models.EfModels;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace FUEN42Team3.Frontend.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
   
    public class MarqueesController : ControllerBase
    {
        private readonly AppDbContext _context;
        public MarqueesController(AppDbContext context)
        {
            _context = context;
        }
        // GET: api/<MarqueesController>
        [HttpGet]
        [ResponseCache(Duration = 60)] // 快取 60 秒,減少資料庫查詢次數

        public async Task<ActionResult<IEnumerable<MarqueesDto>>> GetMarquees()
        {
            var marquees = await _context.Marquees
                .Where(m => m.IsActive) // 只取啟用的滾動訊息
                .OrderBy(m => m.SortOrder) // 可選排序欄位
                .Select(m => new MarqueesDto
                {
                    Id = m.Id,
                    Message = m.Message,
                    LinkUrl = m.Link,
                })
                .ToListAsync();

            return Ok(marquees);
        }
    }
}
