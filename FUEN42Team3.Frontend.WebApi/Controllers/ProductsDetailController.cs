using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Frontend.WebApi.Models.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace FUEN42Team3.Frontend.WebApi.Controllers
{
    [Route("api/store/products")]
    [ApiController]
    public class ProductsDetailController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProductsDetailController(AppDbContext context)
        {
            _context = context;
        }
        // GET: api/ProductsAPI/1
        [HttpGet("{id}")]
        public async Task<ActionResult<ProductDetailDto>> GetProduct(int id)
        {
            string NormalizeImage(string? path)
            {
                if (string.IsNullOrWhiteSpace(path)) return "/img/placeholder.svg";
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

                return "/img/products/" + p;
            }

            var product = await _context.Products
                .AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => new ProductDetailDto
                {
                    Id = p.Id,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.CategoryName : null,
                    BrandId = p.BrandId,
                    BrandName = p.Brand != null ? p.Brand.BrandName : null,
                    StatusId = p.StatusId,
                    StatusName = p.Status != null ? p.Status.StatusName : null,
                    ProductName = p.ProductName,
                    Sku = p.Sku,
                    BasePrice = p.BasePrice,
                    SpecialPrice = p.SpecialPrice,
                    SpecialPriceStartDate = p.SpecialPriceStartDate,
                    SpecialPriceEndDate = p.SpecialPriceEndDate,
                    Quantity = p.Quantity,
                    MinimumOrderQuantity = p.MinimumOrderQuantity,
                    MaximumOrderQuantity = p.MaximumOrderQuantity,
                    Weight = p.Weight,
                    Length = p.Length,
                    Width = p.Width,
                    Height = p.Height,
                    ShortDescription = null,
                    Description = p.Description,
                    IsPreorder = p.IsPreorder,
                    EstimatedReleaseDate = p.EstimatedReleaseDate.HasValue
                        ? p.EstimatedReleaseDate.Value.ToDateTime(TimeOnly.MinValue)
                        : (DateTime?)null,
                    IsActive = p.IsActive,
                    ProductImages = p.ProductImages
                        .Where(i => !i.IsDeleted)
                        .OrderBy(i => i.SortOrder)
                        .Select(i => new ProductImageDto
                        {
                            Id = i.Id,
                            ImageUrl = i.ImagePath,
                            IsCover = i.IsMainImage,
                            SortOrder = i.SortOrder
                        }).ToList()
                })
                .FirstOrDefaultAsync();

            if (product == null) return NotFound();
            // 正規化圖片 URL
            foreach (var img in product.ProductImages)
            {
                img.ImageUrl = NormalizeImage(img.ImageUrl);
            }
            return Ok(product);
        }

        // GET: api/store/products/batch?ids=1,2,3,4,5
        [HttpGet("batch")]
        public async Task<ActionResult<IEnumerable<ProductDetailDto>>> GetProductsBatch([FromQuery] string ids)
        {
            // 處理 ids 參數，轉換為整數數組
            if (string.IsNullOrWhiteSpace(ids))
            {
                return BadRequest("ids 參數不能為空");
            }

            List<int> productIds = new List<int>();
            foreach (var idStr in ids.Split(','))
            {
                if (int.TryParse(idStr.Trim(), out int id))
                {
                    productIds.Add(id);
                }
            }

            if (productIds.Count == 0)
            {
                return BadRequest("沒有有效的產品 ID");
            }

            // 限制一次查詢的數量以避免過度負載
            if (productIds.Count > 50)
            {
                productIds = productIds.Take(50).ToList();
            }

            string NormalizeImage(string? path)
            {
                if (string.IsNullOrWhiteSpace(path)) return "/img/placeholder.svg";
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

                return "/img/products/" + p;
            }

            // 批量查詢產品
            var products = await _context.Products
                .AsNoTracking()
                .Where(p => productIds.Contains(p.Id) && !p.IsDeleted)
                .Select(p => new ProductDetailDto
                {
                    Id = p.Id,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.CategoryName : null,
                    BrandId = p.BrandId,
                    BrandName = p.Brand != null ? p.Brand.BrandName : null,
                    StatusId = p.StatusId,
                    StatusName = p.Status != null ? p.Status.StatusName : null,
                    ProductName = p.ProductName,
                    Sku = p.Sku,
                    BasePrice = p.BasePrice,
                    SpecialPrice = p.SpecialPrice,
                    SpecialPriceStartDate = p.SpecialPriceStartDate,
                    SpecialPriceEndDate = p.SpecialPriceEndDate,
                    Quantity = p.Quantity,
                    MinimumOrderQuantity = p.MinimumOrderQuantity,
                    MaximumOrderQuantity = p.MaximumOrderQuantity,
                    Weight = p.Weight,
                    Length = p.Length,
                    Width = p.Width,
                    Height = p.Height,
                    ShortDescription = null,
                    Description = p.Description,
                    IsPreorder = p.IsPreorder,
                    EstimatedReleaseDate = p.EstimatedReleaseDate.HasValue
                        ? p.EstimatedReleaseDate.Value.ToDateTime(TimeOnly.MinValue)
                        : (DateTime?)null,
                    IsActive = p.IsActive,
                    ProductImages = p.ProductImages
                        .Where(i => !i.IsDeleted)
                        .OrderBy(i => i.SortOrder)
                        .Select(i => new ProductImageDto
                        {
                            Id = i.Id,
                            ImageUrl = i.ImagePath,
                            IsCover = i.IsMainImage,
                            SortOrder = i.SortOrder
                        }).ToList()
                })
                .ToListAsync();

            // 正規化圖片 URL
            foreach (var product in products)
            {
                foreach (var img in product.ProductImages)
                {
                    img.ImageUrl = NormalizeImage(img.ImageUrl);
                }
            }

            return Ok(products);
        }

        // GET: api/Products/batch-simple?ids=1,2,3,4,5
        [HttpGet("batch-simple")]
        public async Task<ActionResult<IEnumerable<object>>> GetProductsBatchSimple([FromQuery] string ids)
        {
            // 處理 ids 參數，轉換為整數數組
            if (string.IsNullOrWhiteSpace(ids))
            {
                return BadRequest("ids 參數不能為空");
            }

            List<int> productIds = new List<int>();
            foreach (var idStr in ids.Split(','))
            {
                if (int.TryParse(idStr.Trim(), out int id))
                {
                    productIds.Add(id);
                }
            }

            if (productIds.Count == 0)
            {
                return BadRequest("沒有有效的產品 ID");
            }

            // 限制一次查詢的數量以避免過度負載
            if (productIds.Count > 50)
            {
                productIds = productIds.Take(50).ToList();
            }

            string NormalizeImage(string? path)
            {
                if (string.IsNullOrWhiteSpace(path)) return "/img/placeholder.svg";
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

                return "/img/products/" + p;
            }

            // 批量查詢簡化的產品資訊
            var products = await _context.Products
                .AsNoTracking()
                .Where(p => productIds.Contains(p.Id) && !p.IsDeleted)
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

            // 正規化圖片路徑
            var normalized = products.Select(p => new
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
            }).ToList();

            return Ok(normalized);
        }
    }
}
