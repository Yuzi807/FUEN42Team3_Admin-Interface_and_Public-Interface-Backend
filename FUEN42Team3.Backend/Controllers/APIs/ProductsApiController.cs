using FUEN42Team3.Backend.Models.DTOs;
using FUEN42Team3.Backend.Models.EfModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace FUEN42Team3.Backend.Controllers.APIs
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsAPIController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;

        public ProductsAPIController(AppDbContext context, IWebHostEnvironment hostEnvironment)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
        }

        // GET
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var products = await _context.Products
                .AsNoTracking()
                .Where(p => !p.IsDeleted)
                .OrderByDescending(p => p.Id)
                .Select(p => new
                {
                    // 基本資料
                    Id = p.Id,
                    ProductName = p.ProductName,
                    SKU = p.Sku,

                    // 分類/品牌/狀態：同時返回 Id 和「名稱」
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.CategoryName : null,
                    BrandId = p.BrandId,
                    BrandName = p.Brand != null ? p.Brand.BrandName : null,
                    StatusId = p.StatusId,
                    StatusName = p.Status != null ? p.Status.StatusName : null,

                    // 價格和上下架
                    BasePrice = p.BasePrice,
                    SpecialPrice = p.SpecialPrice,
                    IsActive = p.IsActive,

                    // 規格資料
                    Quantity = p.Quantity,
                    MinimumOrderQuantity = p.MinimumOrderQuantity,
                    MaximumOrderQuantity = p.MaximumOrderQuantity,
                    Weight = p.Weight,
                    Length = p.Length,
                    Width = p.Width,
                    Height = p.Height,
                    Description = p.Description,

                    // 預計發售日(DateOnly? 轉 DateTime?) & 特價期間
                    EstimatedReleaseDate = p.EstimatedReleaseDate.HasValue
                        ? p.EstimatedReleaseDate.Value.ToDateTime(TimeOnly.MinValue)
                        : (DateTime?)null,
                    SpecialPriceStartDate = p.SpecialPriceStartDate,
                    SpecialPriceEndDate = p.SpecialPriceEndDate,

                    // 商品圖片 - 修改為返回所有圖片
                    Images = _context.ProductImages
                        .Where(img => img.ProductId == p.Id && !img.IsDeleted)
                        .OrderByDescending(img => img.IsMainImage) // 主圖排前面
                        .Select(img => img.ImagePath)
                        .ToList()
                })
                .ToListAsync();

            return Ok(products);
        }

        // POST
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ProductCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (dto.SpecialPrice.HasValue && dto.SpecialPrice.Value >= dto.BasePrice)
                return BadRequest("特惠價必須小於原價");
            if (dto.SpecialPriceStartDate.HasValue ^ dto.SpecialPriceEndDate.HasValue)
                return BadRequest("特惠開始與結束時間需同時填或都不填");
            if (dto.SpecialPriceStartDate.HasValue && dto.SpecialPriceEndDate.HasValue &&
                dto.SpecialPriceStartDate.Value >= dto.SpecialPriceEndDate.Value)
                return BadRequest("特惠開始時間必須早於結束時間");

            var currentUserId = 1;

            var product = new Product
            {
                CategoryId = dto.CategoryId,
                BrandId = dto.BrandId,
                StatusId = dto.StatusId,
                ProductName = dto.ProductName,
                BasePrice = dto.BasePrice,
                SpecialPrice = dto.SpecialPrice,
                Description = dto.Description ?? string.Empty,

                SpecialPriceStartDate = dto.SpecialPriceStartDate,
                SpecialPriceEndDate = dto.SpecialPriceEndDate,

                // DateTime? 轉 DateOnly?
                EstimatedReleaseDate = dto.EstimatedReleaseDate.HasValue
                    ? DateOnly.FromDateTime(dto.EstimatedReleaseDate.Value)
                    : (DateOnly?)null,

                Quantity = dto.Quantity ?? 0,
                MinimumOrderQuantity = dto.MinimumOrderQuantity ?? 1,
                MaximumOrderQuantity = dto.MaximumOrderQuantity,
                Weight = dto.Weight,
                Length = dto.Length,
                Width = dto.Width,
                Height = dto.Height,
                IsPreorder = dto.IsPreorder ?? false,
                Sku = $"SKU-{Guid.NewGuid():N}".Substring(0, 12),
                IsActive = dto.IsActive,
                IsDeleted = false,
                IsPublished = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = currentUserId
            };

            try
            {
                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                // GET
                var result = await _context.Products
                    .AsNoTracking()
                    .Where(p => p.Id == product.Id)
                    .Select(p => new
                    {
                        p.Id,
                        ProductName = p.ProductName,
                        SKU = p.Sku,
                        p.CategoryId,
                        CategoryName = p.Category != null ? p.Category.CategoryName : null,
                        p.BrandId,
                        BrandName = p.Brand != null ? p.Brand.BrandName : null,
                        p.StatusId,
                        StatusName = p.Status != null ? p.Status.StatusName : null,
                        p.BasePrice,
                        p.SpecialPrice,
                        p.IsActive,

                        // 規格資料
                        p.Quantity,
                        p.MinimumOrderQuantity,
                        p.MaximumOrderQuantity,
                        p.Weight,
                        p.Length,
                        p.Width,
                        p.Height,
                        p.Description,

                        // 日期時間
                        EstimatedReleaseDate = p.EstimatedReleaseDate.HasValue
                            ? p.EstimatedReleaseDate.Value.ToDateTime(TimeOnly.MinValue)
                            : (DateTime?)null,
                        p.SpecialPriceStartDate,
                        p.SpecialPriceEndDate,

                        // 商品圖片 - 修改為返回所有圖片
                        Images = _context.ProductImages
                            .Where(img => img.ProductId == p.Id && !img.IsDeleted)
                            .OrderByDescending(img => img.IsMainImage) // 主圖排前面
                            .Select(img => img.ImagePath)
                            .ToList()
                    })
                    .FirstAsync();

                return Ok(result);
            }
            catch (DbUpdateException ex)
            {
                return BadRequest(ex.GetBaseException().Message);
            }
        }

        // PUT(編輯)
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] ProductUpdateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (dto.SpecialPrice.HasValue && dto.SpecialPrice.Value >= dto.BasePrice)
                return BadRequest("特惠價必須小於原價");
            if (dto.SpecialPriceStartDate.HasValue ^ dto.SpecialPriceEndDate.HasValue)
                return BadRequest("特惠開始與結束時間需同時填或都不填");
            if (dto.SpecialPriceStartDate.HasValue && dto.SpecialPriceEndDate.HasValue &&
                dto.SpecialPriceStartDate.Value >= dto.SpecialPriceEndDate.Value)
                return BadRequest("特惠開始時間必須早於結束時間");

            var dbProduct = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);
            if (dbProduct == null || dbProduct.IsDeleted) return NotFound();

            var currentUserId = 1;

            // 允許編輯的欄位
            dbProduct.ProductName = dto.ProductName;
            dbProduct.CategoryId = dto.CategoryId;
            dbProduct.BrandId = dto.BrandId;
            dbProduct.StatusId = dto.StatusId;
            dbProduct.Description = dto.Description;
            dbProduct.BasePrice = dto.BasePrice;
            dbProduct.SpecialPrice = dto.SpecialPrice;
            dbProduct.IsActive = dto.IsActive;
            dbProduct.SpecialPriceStartDate = dto.SpecialPriceStartDate;
            dbProduct.SpecialPriceEndDate = dto.SpecialPriceEndDate;

            // 規格資料
            dbProduct.Quantity = dto.Quantity ?? dbProduct.Quantity;
            dbProduct.MinimumOrderQuantity = dto.MinimumOrderQuantity ?? dbProduct.MinimumOrderQuantity;
            dbProduct.MaximumOrderQuantity = dto.MaximumOrderQuantity;
            dbProduct.Weight = dto.Weight;
            dbProduct.Length = dto.Length;
            dbProduct.Width = dto.Width;
            dbProduct.Height = dto.Height;

            // DateTime? 轉 DateOnly?
            dbProduct.EstimatedReleaseDate = dto.EstimatedReleaseDate.HasValue
                ? DateOnly.FromDateTime(dto.EstimatedReleaseDate.Value)
                : (DateOnly?)null;

            // 更新記錄
            dbProduct.UpdatedAt = DateTime.UtcNow;
            dbProduct.UpdatedBy = currentUserId;

            try
            {
                await _context.SaveChangesAsync();

                //  GET 
                var result = await _context.Products
                    .AsNoTracking()
                    .Where(p => p.Id == id)
                    .Select(p => new
                    {
                        p.Id,
                        ProductName = p.ProductName,
                        SKU = p.Sku,
                        p.CategoryId,
                        CategoryName = p.Category != null ? p.Category.CategoryName : null,
                        p.BrandId,
                        BrandName = p.Brand != null ? p.Brand.BrandName : null,
                        p.StatusId,
                        StatusName = p.Status != null ? p.Status.StatusName : null,
                        p.BasePrice,
                        p.SpecialPrice,
                        p.IsActive,

                        // 規格資料
                        p.Quantity,
                        p.MinimumOrderQuantity,
                        p.MaximumOrderQuantity,
                        p.Weight,
                        p.Length,
                        p.Width,
                        p.Height,
                        p.Description,

                        // 日期時間
                        EstimatedReleaseDate = p.EstimatedReleaseDate.HasValue
                            ? p.EstimatedReleaseDate.Value.ToDateTime(TimeOnly.MinValue)
                            : (DateTime?)null,
                        p.SpecialPriceStartDate,
                        p.SpecialPriceEndDate,

                        // 商品圖片 - 修改為返回所有圖片
                        Images = _context.ProductImages
                            .Where(img => img.ProductId == p.Id && !img.IsDeleted)
                            .OrderByDescending(img => img.IsMainImage) // 主圖排前面
                            .Select(img => img.ImagePath)
                            .ToList()
                    })
                    .FirstAsync();

                return Ok(result);
            }
            catch (DbUpdateException ex)
            {
                return BadRequest(ex.GetBaseException().Message);
            }
        }

        // DELETE
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            product.IsDeleted = true;
            product.DeletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok();
        }

        // 圖片上傳
        [HttpPost("upload-image/{productId}")]
        public async Task<IActionResult> UploadImage(int productId, IFormFile file)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null || product.IsDeleted) return NotFound("商品不存在");

            if (file == null || file.Length == 0)
                return BadRequest("未提供有效的圖片檔案");

            // 檢查是否為圖片
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!(extension == ".jpg" || extension == ".jpeg" || extension == ".png" || extension == ".gif" || extension == ".webp"))
                return BadRequest("僅接受 jpg、jpeg、png、gif 或 webp 格式的圖片");

            // 確保目錄存在
            var uploadDirectory = Path.Combine(_hostEnvironment.WebRootPath, "uploads", "products");
            Directory.CreateDirectory(uploadDirectory);

            // 產生唯一檔名 (使用 GUID + 原始副檔名)
            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadDirectory, uniqueFileName);

            // 儲存檔案
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            // 檢查是否為該產品第一張圖片，如果是，設為主圖
            var isFirstImage = !await _context.ProductImages.AnyAsync(pi => pi.ProductId == productId && !pi.IsDeleted);

            // 儲存路徑到資料庫
            var imagePath = $"/uploads/products/{uniqueFileName}";
            var productImage = new ProductImage
            {
                ProductId = productId,
                ImagePath = imagePath,
                IsMainImage = isFirstImage,
                SortOrder = await _context.ProductImages.Where(pi => pi.ProductId == productId && !pi.IsDeleted).CountAsync() + 1,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = 1, // 可根據實際使用者 ID 調整
                IsDeleted = false
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

        // 設定主圖
        [HttpPut("set-main-image/{imageId}")]
        public async Task<IActionResult> SetMainImage(int imageId)
        {
            var image = await _context.ProductImages.FindAsync(imageId);
            if (image == null || image.IsDeleted) return NotFound("圖片不存在");

            // 取消其他主圖設定
            var currentMainImages = await _context.ProductImages
                .Where(pi => pi.ProductId == image.ProductId && pi.IsMainImage && !pi.IsDeleted)
                .ToListAsync();

            foreach (var img in currentMainImages)
            {
                img.IsMainImage = false;
                img.UpdatedAt = DateTime.UtcNow;
                img.UpdatedBy = 1; // 可根據實際使用者 ID 調整
            }

            // 設定新主圖
            image.IsMainImage = true;
            image.UpdatedAt = DateTime.UtcNow;
            image.UpdatedBy = 1; // 可根據實際使用者 ID 調整

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
        [HttpDelete("delete-image/{imageId}")]
        public async Task<IActionResult> DeleteImage(int imageId)
        {
            var image = await _context.ProductImages.FindAsync(imageId);
            if (image == null || image.IsDeleted) return NotFound("圖片不存在");

            // 標記為已刪除
            image.IsDeleted = true;
            image.DeletedAt = DateTime.UtcNow;
            image.DeletedBy = 1; // 可根據實際使用者 ID 調整

            await _context.SaveChangesAsync();

            // 如果刪除的是主圖，則設定另一張為主圖（如果還有其他圖片）
            if (image.IsMainImage)
            {
                var anotherImage = await _context.ProductImages
                    .Where(pi => pi.ProductId == image.ProductId && !pi.IsDeleted)
                    .FirstOrDefaultAsync();

                if (anotherImage != null)
                {
                    anotherImage.IsMainImage = true;
                    anotherImage.UpdatedAt = DateTime.UtcNow;
                    anotherImage.UpdatedBy = 1; // 可根據實際使用者 ID 調整
                    await _context.SaveChangesAsync();
                }
            }

            return Ok();
        }
    }
}
