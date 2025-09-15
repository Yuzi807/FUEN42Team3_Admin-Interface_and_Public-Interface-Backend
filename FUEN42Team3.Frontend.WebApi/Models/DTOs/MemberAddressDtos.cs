using System;
using System.ComponentModel.DataAnnotations;

namespace FUEN42Team3.Frontend.WebApi.Models.DTOs
{
    public class MemberAddressDto
    {
        public int Id { get; set; }
        public string RecipientName { get; set; } = string.Empty;
        public string RecipientPhone { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        // 新增：地址名稱（例如：住家/公司）
        [StringLength(50)]
        public string? Label { get; set; }
        public bool IsDefault { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class MemberAddressCreateDto
    {
        [Required, StringLength(100)]
        public string RecipientName { get; set; } = string.Empty;
        [Required, StringLength(20)]
        public string RecipientPhone { get; set; } = string.Empty;
        [StringLength(10)]
        public string? PostalCode { get; set; }
        [Required, StringLength(50)]
        public string City { get; set; } = string.Empty;
        [Required, StringLength(50)]
        public string District { get; set; } = string.Empty;
        [Required, StringLength(255)]
        public string Street { get; set; } = string.Empty;
        // 新增：地址名稱（例如：住家/公司）
        [StringLength(50)]
        public string? Label { get; set; }
        public bool IsDefault { get; set; } = false;
    }

    public class MemberAddressUpdateDto
    {
        [Required, StringLength(100)]
        public string RecipientName { get; set; } = string.Empty;
        [Required, StringLength(20)]
        public string RecipientPhone { get; set; } = string.Empty;
        [StringLength(10)]
        public string? PostalCode { get; set; }
        [Required, StringLength(50)]
        public string City { get; set; } = string.Empty;
        [Required, StringLength(50)]
        public string District { get; set; } = string.Empty;
        [Required, StringLength(255)]
        public string Street { get; set; } = string.Empty;
        // 新增：地址名稱（例如：住家/公司）
        [StringLength(50)]
        public string? Label { get; set; }
        public bool IsDefault { get; set; } = false;
    }
}
