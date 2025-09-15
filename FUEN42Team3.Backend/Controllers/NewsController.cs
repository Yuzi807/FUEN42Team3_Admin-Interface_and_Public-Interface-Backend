using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Backend.Models.Services;
using FUEN42Team3.Backend.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FUEN42Team3.Backend.Controllers
{
    [Authorize]
    public class NewsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly NewsService _newsService;

        public NewsController(AppDbContext context, 
                              IWebHostEnvironment hostEnvironment, 
                              NewsService newsService)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
            _newsService = newsService;
        }
        public async Task<IActionResult> Index() // 公告列表
        {
            var news = await _newsService.GetAllNewsAsync();
            var categories = await _newsService.GetAllCategoriesAsync();

            var model = new NewsIndexViewModel
            {
                NewsList = news,
                CategoryList = categories
            };

            return View(model);
        }

        // GET: News/Create
        public IActionResult Create() //新增公告
        {
            // 載入分類選項
            ViewBag.Categories = _context.NewsCategories
                .Where(c => c.IsVisible)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.CategoryName
                })
                .ToList();
            
            return View();
        }

        // POST: News/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(NewsCreateViewModel model)
        {
            // 驗證表單
            if (!ModelState.IsValid)
            {
                // 重新載入分類選項
                ViewBag.Categories = _context.NewsCategories
                    .Where(c => c.IsVisible)
                    .Select(c => new SelectListItem
                    {
                        Value = c.Id.ToString(),
                        Text = c.CategoryName
                    })
                    .ToList();
                
                return View(model);
            }

            // 處理圖片上傳
            string imageUrl = null;
            if (model.ImageFile != null)
            {
                // 建立上傳檔案名稱
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.ImageFile.FileName);
                // 設定上傳路徑
                string uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "uploads", "news");
                
                // 確保資料夾存在
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }
                
                string filePath = Path.Combine(uploadsFolder, fileName);
                
                // 儲存檔案
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ImageFile.CopyToAsync(fileStream);
                }
                
                // 設定相對路徑
                imageUrl = $"/uploads/news/{fileName}";
            }

            // 取得當前使用者ID (這裡假設使用者已登入且ID為1，實際應該從Claims中取得)
            int userId = 1; // 預設值
            if (User.Identity.IsAuthenticated)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int parsedUserId))
                {
                    userId = parsedUserId;
                }
            }

            // 依據狀態選擇正確的 StatusId
            int statusId;
            if (model.Status == "draft")
            {
                // 草稿狀態
                statusId = await _context.PostStatuses
                    .Where(s => s.Name == "草稿")
                    .Select(s => s.Id)
                    .FirstOrDefaultAsync();
            }
            else
            {
                // 根據 IsPublished 決定是公開還是隱藏
                string statusName = model.IsPublished ? "公開" : "隱藏";
                statusId = await _context.PostStatuses
                    .Where(s => s.Name == statusName)
                    .Select(s => s.Id)
                    .FirstOrDefaultAsync();
            }

            // 建立新的公告
            var news = new News
            {
                Title = model.Title,
                Content = model.Content,
                CategoryId = model.CategoryId,
                ImageUrl = imageUrl,
                IsPinned = model.IsPinned,
                PublishedAt = model.IsPublished ? (model.PublishedAt ?? DateTime.Now) : null,
                StatusId = statusId,
                UserId = userId,
                ViewCountToday = 0,
                ViewCountTotal = 0,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            // 儲存到資料庫
            _context.News.Add(news);
            await _context.SaveChangesAsync();

            // 重定向到列表
            TempData["SuccessMessage"] = "公告已成功建立!";
            return RedirectToAction(nameof(Index));
        }

        // GET: News/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var news = await _context.News
                .Include(n => n.Status)
                .FirstOrDefaultAsync(n => n.Id == id);

            if (news == null)
            {
                return NotFound();
            }

            // 載入分類選項
            ViewBag.Categories = _context.NewsCategories
                .Where(c => c.IsVisible)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.CategoryName,
                    Selected = c.Id == news.CategoryId
                })
                .ToList();

            // 判斷是否上架
            bool isPublished = news.Status.Name != "隱藏" && news.Status.Name != "草稿";

            // 建立編輯用 ViewModel
            var model = new NewsEditViewModel
            {
                Id = news.Id,
                Title = news.Title,
                CategoryId = news.CategoryId,
                Content = news.Content,
                ImageUrl = news.ImageUrl,
                IsPinned = news.IsPinned,
                IsPublished = isPublished,
                PublishedAt = news.PublishedAt,
                Status = news.Status.Name == "草稿" ? "draft" : "published",
                CreatedAt = news.CreatedAt,
                ViewCountToday = news.ViewCountToday,
                ViewCountTotal = news.ViewCountTotal
            };

            return View(model);
        }

        // POST: News/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, NewsEditViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                // 重新載入分類選項
                ViewBag.Categories = _context.NewsCategories
                    .Where(c => c.IsVisible)
                    .Select(c => new SelectListItem
                    {
                        Value = c.Id.ToString(),
                        Text = c.CategoryName,
                        Selected = c.Id == model.CategoryId
                    })
                    .ToList();

                return View(model);
            }

            try
            {
                // 獲取現有的新聞數據
                var news = await _context.News
                    .Include(n => n.Status)
                    .FirstOrDefaultAsync(n => n.Id == id);

                if (news == null)
                {
                    return NotFound();
                }

                bool wasDraft = news.Status.Name == "草稿";

                // 處理圖片更新
                string imageUrl = news.ImageUrl;
                
                // 如果選擇不保留原圖，或上傳了新圖片
                if (!model.KeepOriginalImage || model.ImageFile != null)
                {
                    // 如果上傳了新圖片
                    if (model.ImageFile != null)
                    {
                        // 刪除舊圖片檔案 (如果存在)
                        if (!string.IsNullOrEmpty(news.ImageUrl))
                        {
                            string oldFilePath = Path.Combine(_hostEnvironment.WebRootPath, news.ImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(oldFilePath))
                            {
                                System.IO.File.Delete(oldFilePath);
                            }
                        }

                        // 上傳新圖片
                        string fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.ImageFile.FileName);
                        string uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "uploads", "news");
                        
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }
                        
                        string filePath = Path.Combine(uploadsFolder, fileName);
                        
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await model.ImageFile.CopyToAsync(fileStream);
                        }
                        
                        // 更新圖片URL
                        imageUrl = $"/uploads/news/{fileName}";
                    }
                    else
                    {
                        // 如果沒有上傳新圖片且不保留原圖，則刪除舊圖片
                        if (!string.IsNullOrEmpty(news.ImageUrl))
                        {
                            string oldFilePath = Path.Combine(_hostEnvironment.WebRootPath, news.ImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(oldFilePath))
                            {
                                System.IO.File.Delete(oldFilePath);
                            }
                        }
                        imageUrl = null; // 清空圖片URL
                    }
                }

                // 依據狀態選擇正確的 StatusId
                int statusId;
                
                // 根據前端傳回的 model.Status 決定狀態
                if (model.Status == "draft")
                {
                    // 使用者明確設定為草稿狀態
                    statusId = await _context.PostStatuses
                        .Where(s => s.Name == "草稿")
                        .Select(s => s.Id)
                        .FirstOrDefaultAsync();
                }
                else 
                {
                    // 根據 IsPublished 決定是公開還是隱藏
                    string statusName = model.IsPublished ? "公開" : "隱藏";
                    statusId = await _context.PostStatuses
                        .Where(s => s.Name == statusName)
                        .Select(s => s.Id)
                        .FirstOrDefaultAsync();
                }

                // 更新公告資料
                news.Title = model.Title;
                news.Content = model.Content;
                news.CategoryId = model.CategoryId;
                news.ImageUrl = imageUrl;
                news.IsPinned = model.IsPinned;
                
                // 設定發布時間
                if (model.Status != "draft" && model.IsPublished)
                {
                    // 如果是公開狀態，則設定發布時間
                    news.PublishedAt = model.PublishedAt ?? DateTime.Now;
                }
                else if (model.Status == "draft")
                {
                    // 如果是草稿，則不設定發布時間
                    news.PublishedAt = null;
                }
                else
                {
                    // 如果是隱藏狀態，則清除發布時間
                    news.PublishedAt = null;
                }
                
                news.StatusId = statusId;
                news.UpdatedAt = DateTime.Now;

                // 儲存變更
                _context.Update(news);
                await _context.SaveChangesAsync();

                // 成功訊息
                string successMessage;
                if (wasDraft && model.Status == "draft")
                {
                    successMessage = "草稿已成功更新!";
                }
                else if (wasDraft && model.Status != "draft")
                {
                    successMessage = "公告已從草稿狀態發布!";
                }
                else
                {
                    successMessage = "公告已成功更新!";
                }
                
                TempData["SuccessMessage"] = successMessage;
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!NewsExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
        }

        // 檢查公告是否存在
        private bool NewsExists(int id)
        {
            return _context.News.Any(e => e.Id == id);
        }

        // GET: News/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var news = await _context.News
                .Include(n => n.Category)
                .Include(n => n.Status)
                .Include(n => n.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (news == null)
            {
                return NotFound();
            }

            return View(news);
        }

        // POST: News/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var news = await _context.News.FindAsync(id);
            
            if (news == null)
            {
                return NotFound();
            }

            // 刪除圖片檔案 (如果存在)
            if (!string.IsNullOrEmpty(news.ImageUrl))
            {
                string filePath = Path.Combine(_hostEnvironment.WebRootPath, news.ImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            // 從資料庫刪除記錄
            _context.News.Remove(news);
            await _context.SaveChangesAsync();
            
            // 成功訊息
            TempData["SuccessMessage"] = "公告已成功刪除!";
            return RedirectToAction(nameof(Index));
        }
    }
}
