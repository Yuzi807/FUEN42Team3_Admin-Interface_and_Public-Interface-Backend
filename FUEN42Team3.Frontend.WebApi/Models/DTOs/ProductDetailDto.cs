namespace FUEN42Team3.Frontend.WebApi.Models.DTOs
{
    public class ProductDetailDto
    {

            public int Id { get; set; }
            public int CategoryId { get; set; }
            public string? CategoryName { get; set; }

            public int BrandId { get; set; }
            public string? BrandName { get; set; }

            public int StatusId { get; set; }
            public string? StatusName { get; set; }

            public string ProductName { get; set; } = string.Empty;
            public string? Sku { get; set; }

            public decimal BasePrice { get; set; }
            public decimal? SpecialPrice { get; set; }
            public DateTime? SpecialPriceStartDate { get; set; }
            public DateTime? SpecialPriceEndDate { get; set; }

            public int Quantity { get; set; }
            public int? MinimumOrderQuantity { get; set; }
            public int? MaximumOrderQuantity { get; set; }

            public decimal? Weight { get; set; }
            public decimal? Length { get; set; }
            public decimal? Width { get; set; }
            public decimal? Height { get; set; }

            public string? ShortDescription { get; set; }
            public string? Description { get; set; }

            public bool IsPreorder { get; set; }
            public DateTime? EstimatedReleaseDate { get; set; }

            public bool IsActive { get; set; }

            // 圖片清單
            public List<ProductImageDto> ProductImages { get; set; } = new();
        }

        public class ProductImageDto
        {
            public int Id { get; set; }
            public string ImageUrl { get; set; } = string.Empty;
            public bool IsCover { get; set; }
            public int SortOrder { get; set; }
        }
    }

