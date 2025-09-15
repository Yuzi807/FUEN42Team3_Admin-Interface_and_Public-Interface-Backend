using FUEN42Team3.Backend.Models.EfModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Backend.Controllers.APIs
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductImagesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ProductImagesController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // 上傳商品圖片
        [HttpPost("upload/{productId}")]
        public async Task<IActionResult> UploadImage(int productId, IFormFile file)
        {
            if (file == null || file.Length == 0) 
                return BadRequest("請選擇檔案");

            // 檢查產品是否存在
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted);
            
            if (product == null) 
                return NotFound("找不到商品");

            // 檢查檔案大小 (限制為 5MB)
            if (file.Length > 5 * 1024 * 1024)
                return BadRequest("檔案大小不能超過 5MB");

            // 檢查檔案類型
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            if (!allowedExtensions.Contains(extension))
                return BadRequest("只允許上傳 jpg, jpeg, png, gif 或 webp 格式的圖片");

            try
            {
                // 建立上傳目錄
                string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "products");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                // 生成唯一檔名
                string uniqueFileName = $"{Guid.NewGuid()}{extension}";
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // 保存檔案
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                // 建立資料庫記錄
                var productImage = new ProductImage
                {
                    ProductId = productId,
                    ImagePath = $"/uploads/products/{uniqueFileName}",
                    IsMainImage = !await _context.ProductImages.AnyAsync(p => p.ProductId == productId && !p.IsDeleted), // 如果是第一張圖片，設為主圖
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = 1 // 假設目前用戶 ID
                };

                _context.ProductImages.Add(productImage);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    id = productImage.Id,
                    productId = productImage.ProductId,
                    imagePath = productImage.ImagePath,
                    isMainImage = productImage.IsMainImage
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"上傳圖片失敗: {ex.Message}");
            }
        }

        // 設定主圖
        [HttpPut("set-main/{id}")]
        public async Task<IActionResult> SetMainImage(int id)
        {
            var image = await _context.ProductImages
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
            
            if (image == null)
                return NotFound("找不到圖片");

            // 先將此產品的所有圖片設為非主圖
            var productImages = await _context.ProductImages
                .Where(i => i.ProductId == image.ProductId && !i.IsDeleted)
                .ToListAsync();
            
            foreach (var img in productImages)
            {
                img.IsMainImage = false;
            }

            // 將目標圖片設為主圖
            image.IsMainImage = true;
            
            await _context.SaveChangesAsync();
            
            return Ok(new
            {
                id = image.Id,
                productId = image.ProductId,
                imagePath = image.ImagePath,
                isMainImage = true
            });
        }

        // 刪除圖片
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteImage(int id)
        {
            var image = await _context.ProductImages
                .FirstOrDefaultAsync(i => i.Id == id);
            
            if (image == null)
                return NotFound("找不到圖片");

            // 採用軟刪除
            image.IsDeleted = true;
            image.DeletedAt = DateTime.UtcNow;
            image.DeletedBy = 1; // 假設目前用戶 ID

            // 如果刪除的是主圖，需要選一張新的圖作為主圖
            if (image.IsMainImage)
            {
                var newMainImage = await _context.ProductImages
                    .Where(i => i.ProductId == image.ProductId && i.Id != id && !i.IsDeleted)
                    .FirstOrDefaultAsync();
                
                if (newMainImage != null)
                {
                    newMainImage.IsMainImage = true;
                }
            }

            await _context.SaveChangesAsync();
            
            return Ok();
        }

        // 獲取產品的所有圖片
        [HttpGet("product/{productId}")]
        public async Task<IActionResult> GetProductImages(int productId)
        {
            var images = await _context.ProductImages
                .Where(i => i.ProductId == productId && !i.IsDeleted)
                .OrderByDescending(i => i.IsMainImage)
                .ThenBy(i => i.Id)
                .Select(i => new
                {
                    id = i.Id,
                    productId = i.ProductId,
                    imagePath = i.ImagePath,
                    isMainImage = i.IsMainImage
                })
                .ToListAsync();
            
            return Ok(images);
        }
    }
}
