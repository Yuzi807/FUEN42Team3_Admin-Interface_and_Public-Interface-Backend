using System.ComponentModel.DataAnnotations;

namespace FUEN42Team3.Backend.Models.DTOs
{
    public class ProductCreateDto : IValidatableObject
    {
        [Required, Range(1, int.MaxValue)]
        public int CategoryId { get; set; }

        [Required, Range(1, int.MaxValue)]
        public int BrandId { get; set; }

        [Required, Range(1, int.MaxValue)]
        public int StatusId { get; set; }

        [Required, StringLength(200)]
        public string ProductName { get; set; } = default!;

        [Required, Range(0, 99999999)]
        public decimal BasePrice { get; set; }

        [Range(0, 99999999)]
        public decimal? SpecialPrice { get; set; }

        [StringLength(4000)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        [Range(0, int.MaxValue)]
        public int? Quantity { get; set; }

        [Range(1, int.MaxValue)]
        public int? MinimumOrderQuantity { get; set; }
        
        [Range(1, int.MaxValue)]
        public int? MaximumOrderQuantity { get; set; }
        
        [Range(0, 999999)]
        public decimal? Weight { get; set; }
        
        [Range(0, 999999)]
        public decimal? Length { get; set; }
        
        [Range(0, 999999)]
        public decimal? Width { get; set; }
        
        [Range(0, 999999)]
        public decimal? Height { get; set; }

        public bool? IsPreorder { get; set; }

        // ★ 新增：預計發售日（可不填）
        [DataType(DataType.Date)]
        public DateTime? EstimatedReleaseDate { get; set; }

        // 特價期間
        [DataType(DataType.DateTime)]
        public DateTime? SpecialPriceStartDate { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime? SpecialPriceEndDate { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (SpecialPrice.HasValue)
            {
                if (SpecialPrice.Value <= 0)
                    yield return new ValidationResult("特惠價必須大於 0", new[] { nameof(SpecialPrice) });
                if (SpecialPrice.Value >= BasePrice)
                    yield return new ValidationResult("特惠價必須小於原價", new[] { nameof(SpecialPrice), nameof(BasePrice) });
            }

            bool hasStart = SpecialPriceStartDate.HasValue;
            bool hasEnd = SpecialPriceEndDate.HasValue;
            if (hasStart ^ hasEnd)
                yield return new ValidationResult("特惠開始與結束時間需同時填或都不填",
                    new[] { nameof(SpecialPriceStartDate), nameof(SpecialPriceEndDate) });
            if (hasStart && hasEnd && SpecialPriceStartDate!.Value >= SpecialPriceEndDate!.Value)
                yield return new ValidationResult("特惠開始時間必須早於結束時間",
                    new[] { nameof(SpecialPriceStartDate), nameof(SpecialPriceEndDate) });
        }
    }

    public class ProductUpdateDto : IValidatableObject
    {
        [Required, StringLength(200)]
        public string ProductName { get; set; } = default!;

        [Required, Range(1, int.MaxValue)]
        public int CategoryId { get; set; }

        [Required, Range(1, int.MaxValue)]
        public int BrandId { get; set; }

        [Required, Range(1, int.MaxValue)]
        public int StatusId { get; set; }

        [Required, Range(0, 99999999)]
        public decimal BasePrice { get; set; }

        [Range(0, 99999999)]
        public decimal? SpecialPrice { get; set; }

        [StringLength(4000)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
        
        [Range(0, int.MaxValue)]
        public int? Quantity { get; set; }

        [Range(1, int.MaxValue)]
        public int? MinimumOrderQuantity { get; set; }
        
        [Range(1, int.MaxValue)]
        public int? MaximumOrderQuantity { get; set; }
        
        [Range(0, 999999)]
        public decimal? Weight { get; set; }
        
        [Range(0, 999999)]
        public decimal? Length { get; set; }
        
        [Range(0, 999999)]
        public decimal? Width { get; set; }
        
        [Range(0, 999999)]
        public decimal? Height { get; set; }

        // ★ 新增：預計發售日（可不填）
        [DataType(DataType.Date)]
        public DateTime? EstimatedReleaseDate { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime? SpecialPriceStartDate { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime? SpecialPriceEndDate { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (SpecialPrice.HasValue)
            {
                if (SpecialPrice.Value <= 0)
                    yield return new ValidationResult("特惠價必須大於 0", new[] { nameof(SpecialPrice) });
                if (SpecialPrice.Value >= BasePrice)
                    yield return new ValidationResult("特惠價必須小於原價", new[] { nameof(SpecialPrice), nameof(BasePrice) });
            }

            bool hasStart = SpecialPriceStartDate.HasValue;
            bool hasEnd = SpecialPriceEndDate.HasValue;
            if (hasStart ^ hasEnd)
                yield return new ValidationResult("特惠開始與結束時間需同時填或都不填",
                    new[] { nameof(SpecialPriceStartDate), nameof(SpecialPriceEndDate) });
            if (hasStart && hasEnd && SpecialPriceStartDate!.Value >= SpecialPriceEndDate!.Value)
                yield return new ValidationResult("特惠開始時間必須早於結束時間",
                    new[] { nameof(SpecialPriceStartDate), nameof(SpecialPriceEndDate) });
        }
    }
}
