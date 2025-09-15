using FUEN42Team3.Backend.Models.EfModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Backend.Controllers.APIs
{
    [ApiController]
    [Route("api/ProductStatuses")]
    public class ProductStatusesApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProductStatusesApiController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 取得所有商品狀態（下拉選單用）
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetForSelect()
        {
            var statuses = await _context.ProductStatuses
                .AsNoTracking()
                .OrderBy(s => s.Id)
                .Select(s => new
                {
                    id = s.Id,
                    name = s.StatusName
                })
                .ToListAsync();

            return Ok(statuses);
        }

        /// <summary>
        /// 取得單一商品狀態
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var status = await _context.ProductStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);

            if (status == null) return NotFound();

            return Ok(new
            {
                id = status.Id,
                name = status.StatusName
            });
        }

        /// <summary>
        /// 新增商品狀態
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ProductStatus status)
        {
            if (status == null || string.IsNullOrWhiteSpace(status.StatusName))
                return BadRequest("StatusName 必填");

            _context.ProductStatuses.Add(status);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = status.Id }, new
            {
                id = status.Id,
                name = status.StatusName
            });
        }

        /// <summary>
        /// 修改商品狀態
        /// </summary>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ProductStatus updated)
        {
            var status = await _context.ProductStatuses.FindAsync(id);
            if (status == null) return NotFound();

            if (string.IsNullOrWhiteSpace(updated.StatusName))
                return BadRequest("StatusName 必填");

            status.StatusName = updated.StatusName;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                id = status.Id,
                name = status.StatusName
            });
        }

        /// <summary>
        /// 刪除商品狀態（硬刪除，若要軟刪除可自行加欄位）
        /// </summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var status = await _context.ProductStatuses.FindAsync(id);
            if (status == null) return NotFound();

            _context.ProductStatuses.Remove(status);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
