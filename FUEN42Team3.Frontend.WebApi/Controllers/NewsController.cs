using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Frontend.WebApi.Models.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X.PagedList;

namespace FUEN42Team3.Frontend.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NewsController : ControllerBase
    {
        private readonly AppDbContext _context;
        public NewsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("GetCategoryNews")] // 獲取選中的分類新聞
        public async Task<IActionResult> GetCategoryNews(int categoryId, int page = 1, int pageSize = 6)
        {
            try
            {
                // 取得台灣當前時間
                var taiwanTime = DateTime.UtcNow.AddHours(8);
                Console.WriteLine($"=== GetCategoryNews 時間除錯 ===");
                Console.WriteLine($"台灣當前時間: {taiwanTime:yyyy-MM-dd HH:mm:ss.fff}");
                Console.WriteLine($"categoryId: {categoryId}");

                // 加上發布時間過濾條件
                var query = _context.News
                    .Include(n => n.Category)
                    .Where(n => n.StatusId == 1 && n.PublishedAt <= taiwanTime);

                // 如果 categoryId ≠ 0，則加上分類條件
                if (categoryId != 0)
                {
                    query = query.Where(n => n.CategoryId == categoryId);
                }

                var totalCount = await query.CountAsync();

                // 分頁查詢 + 投影成 DTO + 加上置頂排序
                var newsList = await query
                    .OrderByDescending(n => n.IsPinned)        // 先按置頂排序
                    .ThenByDescending(n => n.PublishedAt)      // 再按發布時間排序
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(n => new NewsDto
                    {
                        Id = n.Id,
                        Title = n.Title,
                        Content = n.Content,
                        PublishDate = n.PublishedAt,
                        CategoryName = n.Category.CategoryName ?? "",
                        ImageUrl = n.ImageUrl,
                        ViewCountTotal = n.ViewCountTotal,
                        IsPinned = n.IsPinned
                    })
                    .ToListAsync();

                // 除錯輸出
                Console.WriteLine($"=== GetCategoryNews 結果 ===");
                Console.WriteLine($"總筆數: {totalCount}, 本頁筆數: {newsList.Count}");
                foreach (var news in newsList)
                {
                    Console.WriteLine($"ID: {news.Id}, 置頂: {news.IsPinned}, 時間: {news.PublishDate:yyyy-MM-dd HH:mm:ss}, 標題: {news.Title}");
                }

                // 包裝成 PageResult<T>
                var result = new PageResult<NewsDto>
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    Items = newsList
                };
                return Ok(result);
            }
            catch (Exception ex)
            {
                // 日誌記錄錯誤
                return StatusCode(StatusCodes.Status500InternalServerError, "伺服器錯誤: " + ex.Message);
            }
        }
        [HttpGet]
        [Route("GetAllNews")] // 獲取所有新聞列表
        public async Task<IActionResult> GetAllNews(int categoryId = 0, int page = 1, int pageSize = 6, string keyword = "")
        {
            try
            {
                // 取得台灣當前時間
                var taiwanTime = DateTime.UtcNow.AddHours(8);
                Console.WriteLine($"=== 時間除錯 ===");
                Console.WriteLine($"台灣當前時間: {taiwanTime:yyyy-MM-dd HH:mm:ss.fff}");

                var query = _context.News
                    .Include(n => n.Category)
                    .Where(n => n.StatusId == 1);

                // 先查看所有資料的發布時間
                var allNews = await _context.News
                    .Where(n => n.StatusId == 1)
                    .Select(n => new { n.Id, n.Title, n.IsPinned, n.PublishedAt })
                    .ToListAsync();

                Console.WriteLine("=== 所有新聞的發布時間比較 ===");
                foreach (var item in allNews)
                {
                    var canShow = item.PublishedAt <= taiwanTime;
                    var timeDiff = item.PublishedAt <= taiwanTime ? "可顯示" : "未來(不顯示)";
                    Console.WriteLine($"ID: {item.Id}");
                    Console.WriteLine($"  發布時間: {item.PublishedAt:yyyy-MM-dd HH:mm:ss.fff}");
                    Console.WriteLine($"  當前時間: {taiwanTime:yyyy-MM-dd HH:mm:ss.fff}");
                    Console.WriteLine($"  比較結果: {timeDiff} (PublishedAt <= Now: {canShow})");
                    Console.WriteLine($"  置頂: {item.IsPinned}");
                    Console.WriteLine("---");
                }

                // 套用時間過濾
                query = query.Where(n => n.PublishedAt <= taiwanTime);

                if (categoryId != 0)
                {
                    query = query.Where(n => n.CategoryId == categoryId);
                }
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    keyword = keyword.Trim().ToLower();
                    query = query.Where(n =>
                        n.Title.ToLower().Contains(keyword) ||
                        n.Content.ToLower().Contains(keyword));
                }

                var totalCount = await query.CountAsync();
                var newsList = await query
                    .OrderByDescending(n => n.IsPinned)        // 先按置頂排序
                    .ThenByDescending(n => n.PublishedAt)      // 再按發布時間排序
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(n => new NewsDto
                    {
                        Id = n.Id,
                        Title = n.Title,
                        Content = n.Content,
                        PublishDate = n.PublishedAt,
                        CategoryName = n.Category.CategoryName ?? "",
                        ImageUrl = n.ImageUrl,
                        ViewCountTotal = n.ViewCountTotal,
                        IsPinned = n.IsPinned
                    })
                    .ToListAsync();

                var result = new PageResult<NewsDto>
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    Items = newsList
                };
                return Ok(result);
            }
            catch (Exception ex)
            {
                // 日誌記錄錯誤
                return StatusCode(StatusCodes.Status500InternalServerError, "伺服器錯誤: " + ex.Message);
            }
        }
        [HttpGet]
        [Route("GetNewsDetail")] // 獲取單一新聞
        public async Task<IActionResult> GetNewsDetail(int id)
        {
            try
            {
                var news = await _context.News
                    .Include(n => n.Category)
                    .Include(n => n.User)
                    .FirstOrDefaultAsync(n => n.Id == id && n.StatusId == 1);

                if (news == null)
                {
                    return NotFound("找不到該筆新聞資料");
                }

                var newsDto = new NewsDto
                {
                    Id = news.Id,
                    Title = news.Title,
                    Content = news.Content,
                    PublishDate = news.PublishedAt,
                    Author = news.User.UserName,
                    CategoryName = news.Category?.CategoryName ?? "",
                    ImageUrl = news.ImageUrl,
                    ViewCountTotal = news.ViewCountTotal,
                    IsPinned = news.IsPinned
                };
                return Ok(newsDto);
            }
            catch (Exception ex)
            {
                // 日誌記錄錯誤
                return StatusCode(StatusCodes.Status500InternalServerError, "伺服器錯誤: " + ex.Message);
            }
        }
    }
}
