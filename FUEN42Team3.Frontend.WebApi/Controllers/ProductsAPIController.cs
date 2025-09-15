using FUEN42Team3.Backend.Models.EfModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Frontend.WebApi.Controllers
{
    [Route("api/store/products")]
    [ApiController]
    public class ProductsAPIController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProductsAPIController(AppDbContext context)
        {
            _context = context;
        }

        // 取得商品清單（前台用）
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var list = await _context.Products
                .AsNoTracking()
                .Where(p => !p.IsDeleted)
                .OrderByDescending(p => p.Id)
                .Select(p => new
                {
                    id = p.Id,
                    productName = p.ProductName,
                    sku = p.Sku,
                    categoryId = p.CategoryId,
                    categoryName = p.Category != null ? p.Category.CategoryName : null,
                    brandId = p.BrandId,
                    brandName = p.Brand != null ? p.Brand.BrandName : null,
                    statusId = p.StatusId,
                    statusName = p.Status != null ? p.Status.StatusName : null,
                    basePrice = p.BasePrice,
                    specialPrice = p.SpecialPrice,
                    specialPriceStartDate = p.SpecialPriceStartDate,
                    specialPriceEndDate = p.SpecialPriceEndDate,
                    quantity = p.Quantity,
                    image = _context.ProductImages
                        .Where(img => img.ProductId == p.Id && img.IsMainImage && !img.IsDeleted)
                        .Select(img => img.ImagePath)
                        .FirstOrDefault()
                })
                .ToListAsync();

            // 正規化圖片路徑，指向前端 public/img/products 下的靜態資源
            string? NormalizeImage(string? path)
            {
                if (string.IsNullOrWhiteSpace(path)) return null;
                var p = path.Trim().Replace("\\", "/");

                // 外部連結
                if (p.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    p.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return p;

                // 已是網站內部 img 路徑
                if (p.StartsWith("/img/", StringComparison.OrdinalIgnoreCase)) return p;
                if (p.Contains("/img/products/", StringComparison.OrdinalIgnoreCase))
                {
                    // 確保有前導斜線
                    return p.StartsWith("/") ? p : "/" + p;
                }

                // 去除前導斜線及可能的 "products/" 前綴
                p = p.TrimStart('/');
                if (p.StartsWith("products/", StringComparison.OrdinalIgnoreCase)) p = p.Substring(9);

                // 常見子資料夾或直接檔名
                var allowed = new[] { "blocks/", "figures/", "military/", "scifi/", "vehicles/" };
                var lower = p.ToLowerInvariant();
                var isInAllowed = allowed.Any(a => lower.StartsWith(a));
                return "/img/products/" + (isInAllowed ? p : p);
            }

            var normalized = list.Select(p => new
            {
                p.id,
                p.productName,
                p.sku,
                p.categoryId,
                p.categoryName,
                p.brandId,
                p.brandName,
                p.statusId,
                p.statusName,
                p.basePrice,
                p.specialPrice,
                p.specialPriceStartDate,
                p.specialPriceEndDate,
                p.quantity,
                image = NormalizeImage(p.image)
            });

            return Ok(normalized);
        }

        /// 取得熱門商品（隨機 N 筆），預設 4 筆
        /// GET: /api/store/products/hot?count=4
        [HttpGet("hot")]
        public async Task<IActionResult> GetHot([FromQuery] int count = 4, [FromQuery] bool onlyInStock = true)
        {
            if (count <= 0) count = 4;
            if (count > 20) count = 20; // 避免過大

            var q = _context.Products
                .AsNoTracking()
                .Where(p => !p.IsDeleted && p.IsActive);

            if (onlyInStock)
            {
                q = q.Where(p => p.Quantity > 0);
            }

            // 使用 NEWID() 隨機排序
            var list = await q
                .OrderBy(p => Guid.NewGuid())
                .Take(count)
                .Select(p => new
                {
                    id = p.Id,
                    productName = p.ProductName,
                    sku = p.Sku,
                    categoryId = p.CategoryId,
                    categoryName = p.Category != null ? p.Category.CategoryName : null,
                    brandId = p.BrandId,
                    brandName = p.Brand != null ? p.Brand.BrandName : null,
                    statusId = p.StatusId,
                    statusName = p.Status != null ? p.Status.StatusName : null,
                    basePrice = p.BasePrice,
                    specialPrice = p.SpecialPrice,
                    specialPriceStartDate = p.SpecialPriceStartDate,
                    specialPriceEndDate = p.SpecialPriceEndDate,
                    quantity = p.Quantity,
                    image = _context.ProductImages
                        .Where(img => img.ProductId == p.Id && img.IsMainImage && !img.IsDeleted)
                        .Select(img => img.ImagePath)
                        .FirstOrDefault()
                })
                .ToListAsync();

            string? NormalizeImage(string? path)
            {
                if (string.IsNullOrWhiteSpace(path)) return null;
                var p = path.Trim().Replace("\\", "/");

                if (p.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    p.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return p;

                if (p.StartsWith("/img/", StringComparison.OrdinalIgnoreCase)) return p;
                if (p.Contains("/img/products/", StringComparison.OrdinalIgnoreCase))
                {
                    return p.StartsWith("/") ? p : "/" + p;
                }

                p = p.TrimStart('/');
                if (p.StartsWith("products/", StringComparison.OrdinalIgnoreCase)) p = p.Substring(9);

                var allowed = new[] { "blocks/", "figures/", "military/", "scifi/", "vehicles/" };
                var lower = p.ToLowerInvariant();
                var isInAllowed = allowed.Any(a => lower.StartsWith(a));
                return "/img/products/" + (isInAllowed ? p : p);
            }

            var normalized = list.Select(p => new
            {
                p.id,
                p.productName,
                p.sku,
                p.categoryId,
                p.categoryName,
                p.brandId,
                p.brandName,
                p.statusId,
                p.statusName,
                p.basePrice,
                p.specialPrice,
                p.specialPriceStartDate,
                p.specialPriceEndDate,
                p.quantity,
                image = NormalizeImage(p.image)
            });

            return Ok(normalized);
        }
    }
}
