using System.ComponentModel.DataAnnotations;

namespace FUEN42Team3.Frontend.WebApi.Models.DTOs
{
    public class MemberCvsAddressDto
    {
        public int Id { get; set; }
        public string LogisticsSubType { get; set; } = string.Empty; // UNIMARTC2C/FAMIC2C/HILIFEC2C/OKMARTC2C
        public string StoreId { get; set; } = string.Empty;
        public string StoreName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? Telephone { get; set; }
        public string? RecipientName { get; set; }
        public string? RecipientPhone { get; set; }
        public bool IsDefault { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class MemberCvsAddressCreateDto
    {
        [Required] public string LogisticsSubType { get; set; } = string.Empty;
        [Required] public string StoreId { get; set; } = string.Empty;
        [Required] public string StoreName { get; set; } = string.Empty;
        [Required] public string Address { get; set; } = string.Empty;
        public string? Telephone { get; set; }
        public string? RecipientName { get; set; }
        public string? RecipientPhone { get; set; }
        public bool IsDefault { get; set; }
    }

    public class MemberCvsAddressUpdateDto
    {
        [Required] public string StoreName { get; set; } = string.Empty;
        [Required] public string Address { get; set; } = string.Empty;
        public string? Telephone { get; set; }
        public string? RecipientName { get; set; }
        public string? RecipientPhone { get; set; }
        public bool IsDefault { get; set; }
    }
}
