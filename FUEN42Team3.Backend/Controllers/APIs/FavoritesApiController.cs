using FUEN42Team3.Backend.Models.EfModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Backend.Controllers.APIs
{
    [ApiController]
    [Route("api/Favorites")]
    [Authorize]
    public class FavoritesApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        public FavoritesApiController(AppDbContext context)
        {
            _context = context;
        }

        // 新增收藏
        [HttpPost]
        public async Task<IActionResult> AddFavorite([FromBody] FavoriteDto dto)
        {
            if (dto == null || dto.MemberId <= 0 || dto.ProductId <= 0)
                return BadRequest("缺少必要參數");

            var exists = await _context.Favorites.AnyAsync(f => f.MemberId == dto.MemberId && f.ProductId == dto.ProductId);
            if (exists)
                return BadRequest("已收藏");

            var fav = new Favorite
            {
                MemberId = dto.MemberId,
                ProductId = dto.ProductId
            };
            _context.Favorites.Add(fav);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // 刪除收藏
        [HttpDelete]
        public async Task<IActionResult> RemoveFavorite([FromBody] FavoriteDto dto)
        {
            if (dto == null || dto.MemberId <= 0 || dto.ProductId <= 0)
                return BadRequest("缺少必要參數");

            var fav = await _context.Favorites
                .FirstOrDefaultAsync(f => f.MemberId == dto.MemberId && f.ProductId == dto.ProductId);

            if (fav == null)
                return NotFound();

            _context.Favorites.Remove(fav);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // 取得會員收藏清單
        [HttpGet("member/{memberId}")]
        public async Task<IActionResult> GetFavorites(int memberId)
        {
            var list = await _context.Favorites
                .Where(f => f.MemberId == memberId)
                .Select(f => f.ProductId)
                .ToListAsync();
            return Ok(list);
        }

        public class FavoriteDto
        {
            public int MemberId { get; set; }
            public int ProductId { get; set; }
        }
    }
}
