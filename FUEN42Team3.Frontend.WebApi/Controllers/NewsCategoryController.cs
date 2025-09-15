using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Frontend.WebApi.Models.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Frontend.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NewsCategoryController : ControllerBase
    {
        private readonly AppDbContext _context;
        public NewsCategoryController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("GetNewsCategories")] // 獲取新聞分類列表
        public async Task<IActionResult> GetNewsCategories()
        {
            try
            {
                var categories = await _context.NewsCategories
                .Where(c => c.IsVisible) // 只取啟用的分類
                .Select(c => new NewsCategoryDto
                {
                    Id = c.Id,
                    Name = c.CategoryName,
                    icon = c.Icon,
                    IsVisible = c.IsVisible
                })
                .ToListAsync();
                return Ok(categories);
            }
            catch (Exception ex)
            {
                // 日誌記錄錯誤
                return StatusCode(StatusCodes.Status500InternalServerError, "伺服器錯誤: " + ex.Message);
            }
        }       
    }
}
