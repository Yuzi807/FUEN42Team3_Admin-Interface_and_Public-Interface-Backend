using FUEN42Team3.Backend.Models;
using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Backend.Models.Services;
using FUEN42Team3.Backend.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FUEN42Team3.Backend.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class NewsApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly NewsService _newsService;

        public NewsApiController(AppDbContext context, NewsService newsService)
        {
            _context = context;
            _newsService = newsService;
        }

        // 取得所有公告
        [HttpGet("news")]
        public async Task<ActionResult<IEnumerable<NewsViewModel>>> GetAllNews()
        {
            var news = await _context.News
                .Include(n => n.Category)
                .Include(n => n.User)
                .Include(n => n.Status)
                .Select(n => new NewsViewModel
                {
                    IsPinned = n.IsPinned,
                    Id = n.Id,
                    Title = n.Title,
                    CategoryName = n.Category.CategoryName,
                    UserName = n.User.UserName,
                    PublishedAt = n.PublishedAt,
                    UpdatedAt = n.UpdatedAt,
                    ViewCountToday = n.ViewCountToday,
                    ViewCountTotal = n.ViewCountTotal,
                    Status = n.Status.Name
                })
                .ToListAsync();

            return Ok(news);
        }

        // 取得單一公告
        [HttpGet("news/{id}")]
        public async Task<ActionResult<NewsViewModel>> GetNewsById(int id)
        {
            var n = await _context.News
                .Include(x => x.Category)
                .Include(x => x.User)
                .Include(x => x.Status)
                .FirstOrDefaultAsync(nw => nw.Id == id);

            if (n == null) return NotFound();

            var vm = new NewsViewModel
            {
                IsPinned = n.IsPinned,
                Id = n.Id,
                Title = n.Title,
                CategoryName = n.Category.CategoryName,
                UserName = n.User.UserName,
                PublishedAt = n.PublishedAt,
                UpdatedAt = n.UpdatedAt,
                ViewCountToday = n.ViewCountToday,
                ViewCountTotal = n.ViewCountTotal,
                Status = n.Status.Name
            };
            return Ok(vm);
        }

        // 新增公告
        [HttpPost("news")]
        public async Task<ActionResult> CreateNews([FromBody] NewsViewModel model)
        {
            // 需查詢外鍵id
            var category = await _context.NewsCategories.FirstOrDefaultAsync(c => c.CategoryName == model.CategoryName);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == model.UserName);
            var status = await _context.PostStatuses.FirstOrDefaultAsync(s => s.Name == model.Status);

            var news = new News
            {
                IsPinned = model.IsPinned,
                Title = model.Title,
                CategoryId = category?.Id ?? 0,
                UserId = user?.Id ?? 0,
                PublishedAt = model.PublishedAt ?? System.DateTime.Now,
                UpdatedAt = model.UpdatedAt,
                ViewCountToday = model.ViewCountToday,
                ViewCountTotal = model.ViewCountTotal,
                StatusId = status?.Id ?? 0
            };

            _context.News.Add(news);
            await _context.SaveChangesAsync();
            return Ok(news.Id); // 回傳新 Id
        }

        // === 分類相關 API (統一在這裡提供給前端使用) ===
        
        // 取得所有分類
        [HttpGet("categories")]
        public async Task<ActionResult<IEnumerable<NewsCategoriesViewModel>>> GetAllCategories()
        {
            var cats = await _context.NewsCategories
                .Select(c => new NewsCategoriesViewModel
                {
                    Id = c.Id,
                    CategoryName = c.CategoryName,
                    IconPath = c.Icon,
                    IsVisible = c.IsVisible
                })
                .ToListAsync();

            return Ok(cats);
        }

        // 取得單一分類
        [HttpGet("categories/{id}")]
        public async Task<ActionResult<NewsCategoriesViewModel>> GetCategoryById(int id)
        {
            var c = await _context.NewsCategories.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound();
            var vm = new NewsCategoriesViewModel
            {
                Id = c.Id,
                CategoryName = c.CategoryName,
                IconPath = c.Icon,
                IsVisible = c.IsVisible
            };
            return Ok(vm);
        }

        // 新增分類
        [HttpPost("categories")]
        public async Task<ActionResult> CreateCategory([FromBody] NewsCategoriesViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            string? savedIconPath = null;
            if (model.Icon != null && model.Icon.Length > 0)
            {
                // 建立儲存路徑
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "news");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // 建立唯一檔名
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(model.Icon.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                // 儲存檔案
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.Icon.CopyToAsync(stream);
                }

                // 儲存相對路徑（供前端使用）
                savedIconPath = $"/uploads/news/{fileName}";
            }

            var category = new NewsCategory
            {
                CategoryName = model.CategoryName,
                Icon = savedIconPath,
                IsVisible = model.IsVisible
            };
            _context.NewsCategories.Add(category);
            await _context.SaveChangesAsync();
            return Ok(new { id = category.Id, success = true });
        }

        // 更新分類
        [HttpPut("categories/{id}")]
        public async Task<ActionResult> UpdateCategory(int id, [FromBody] NewsCategoriesViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var category = await _context.NewsCategories.FindAsync(id);
            if (category == null)
            {
                return NotFound();
            }
            string? savedIconPath = category.Icon; // 預設保留原本的 icon
            if (model.Icon != null && model.Icon.Length > 0)
            {
                // 建立儲存路徑
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "news");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // 建立唯一檔名
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(model.Icon.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                // 儲存檔案
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.Icon.CopyToAsync(stream);
                }

                // 儲存相對路徑
                savedIconPath = $"/uploads/news/{fileName}";
            }

            category.CategoryName = model.CategoryName;
            category.Icon = savedIconPath;
            category.IsVisible = model.IsVisible;

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // 刪除分類
        [HttpDelete("categories/{id}")]
        public async Task<ActionResult> DeleteCategory(int id)
        {
            var category = await _context.NewsCategories.FindAsync(id);
            if (category == null)
            {
                return NotFound();
            }

            // 檢查是否有公告使用此分類
            bool hasNews = await _context.News.AnyAsync(n => n.CategoryId == id);
            if (hasNews)
            {
                // 回傳衝突，表示此分類已被公告文章使用
                return Conflict("無法刪除：此分類已被公告文章使用");
            }

            _context.NewsCategories.Remove(category);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // === 批量操作 API ===
        
        // 批量刪除公告
        [HttpPost("news/batch-delete")]
        public async Task<ActionResult> BatchDeleteNews([FromBody] BatchDeleteRequest request)
        {
            if (request?.Ids == null || !request.Ids.Any())
            {
                return BadRequest("沒有提供要刪除的公告ID");
            }

            try
            {
                // 查詢要刪除的公告
                var newsToDelete = await _context.News
                    .Where(n => request.Ids.Contains(n.Id))
                    .ToListAsync();

                if (!newsToDelete.Any())
                {
                    return NotFound("找不到指定的公告");
                }

                // 記錄成功和失敗的項目
                var deletedCount = 0;
                var errors = new List<string>();

                foreach (var news in newsToDelete)
                {
                    try
                    {
                        // 刪除圖片檔案 (如果存在)
                        if (!string.IsNullOrEmpty(news.ImageUrl))
                        {
                            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                            string filePath = Path.Combine(uploadsPath, news.ImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(filePath))
                            {
                                System.IO.File.Delete(filePath);
                            }
                        }

                        // 從資料庫刪除記錄
                        _context.News.Remove(news);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"公告「{news.Title}」刪除失敗: {ex.Message}");
                    }
                }

                // 儲存變更
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    deletedCount,
                    totalRequested = request.Ids.Count(),
                    errors,
                    message = errors.Any()
                        ? $"成功刪除 {deletedCount} 篇公告，{errors.Count} 篇失敗"
                        : $"成功刪除 {deletedCount} 篇公告"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "批量刪除過程中發生錯誤",
                    error = ex.Message
                });
            }
        }

        // 批量刪除請求模型
        public class BatchDeleteRequest
        {
            public List<int> Ids { get; set; } = new List<int>();
        }

        // === 分類批量操作 API ===
        
        // 批量刪除分類
        [HttpPost("categories/batch-delete")]
        public async Task<ActionResult> BatchDeleteCategories([FromBody] BatchDeleteRequest request)
        {
            if (request?.Ids == null || !request.Ids.Any())
            {
                return BadRequest("沒有提供要刪除的分類ID");
            }

            try
            {
                // 查詢要刪除的分類
                var categoriesToDelete = await _context.NewsCategories
                    .Where(c => request.Ids.Contains(c.Id))
                    .ToListAsync();

                if (!categoriesToDelete.Any())
                {
                    return NotFound("找不到指定的分類");
                }

                // 記錄成功和失敗的項目
                var deletedCount = 0;
                var errors = new List<string>();

                foreach (var category in categoriesToDelete)
                {
                    try
                    {
                        // 檢查是否有公告使用此分類
                        bool hasNews = await _context.News.AnyAsync(n => n.CategoryId == category.Id);
                        if (hasNews)
                        {
                            errors.Add($"分類「{category.CategoryName}」正在被使用，無法刪除");
                            continue;
                        }

                        // 從資料庫刪除記錄
                        _context.NewsCategories.Remove(category);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"分類「{category.CategoryName}」刪除失敗: {ex.Message}");
                    }
                }

                // 儲存變更
                if (deletedCount > 0)
                {
                    await _context.SaveChangesAsync();
                }

                return Ok(new
                {
                    success = true,
                    deletedCount,
                    totalRequested = request.Ids.Count(),
                    errors,
                    message = errors.Any()
                        ? $"成功刪除 {deletedCount} 個分類，{errors.Count} 個失敗"
                        : $"成功刪除 {deletedCount} 個分類"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "批量刪除過程中發生錯誤",
                    error = ex.Message
                });
            }
        }
    }
}