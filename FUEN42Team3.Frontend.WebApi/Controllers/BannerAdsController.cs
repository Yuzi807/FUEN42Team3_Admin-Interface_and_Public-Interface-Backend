using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FUEN42Team3.Frontend.WebApi.Models.DTOs;
using FUEN42Team3.Backend.Models.EfModels;


namespace FUEN42Team3.Frontend.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BannerAdsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BannerAdsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/BannerAds
        [HttpGet]
        [ResponseCache(Duration = 60)] // 快取 60 秒,減少資料庫查詢次數

        public async Task<ActionResult<IEnumerable<BannerAdDto>>> GetBannerAds()
        {
            var banners = await _context.BannerAds
                .Where(b => b.IsActive) // 只取啟用的輪播圖
                .OrderBy(b => b.SortOrder) // 可選排序欄位
                .Select(b => new BannerAdDto
                {
                    Id = b.Id,
                    Title = b.Title,
                    ImageUrl = b.Image,
                    LinkUrl = b.Link,
                })
                .ToListAsync();

            return Ok(banners);

        }
    }
}
