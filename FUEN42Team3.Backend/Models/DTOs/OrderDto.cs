using System.Text.Json.Serialization;

namespace FUEN42Team3.Backend.Models.DTOs
{
    public class OrderDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }

        [JsonPropertyName("user_id")] public int UserId { get; set; }

        [JsonPropertyName("total_amount")] public decimal TotalAmount { get; set; }

        [JsonPropertyName("shipping_fee")] public decimal ShippingFee { get; set; }

        [JsonPropertyName("used_points")] public int? UsedPoints { get; set; }

        [JsonPropertyName("category")] public string Category { get; set; } // "預購" / "現貨"

        [JsonPropertyName("status")] public string Status { get; set; } // "待處理"...

        [JsonPropertyName("order_date")] public DateTime OrderDate { get; set; }

        [JsonPropertyName("payment_method")] public string? PaymentMethod { get; set; }

        [JsonPropertyName("delivery_method")] public string? DeliveryMethod { get; set; }

        [JsonPropertyName("recipient_name")] public string RecipientName { get; set; }

        [JsonPropertyName("phone")] public string Phone { get; set; }

        [JsonPropertyName("address")] public string Address { get; set; }

        [JsonPropertyName("zip_code")] public string? ZipCode { get; set; }

        [JsonPropertyName("details")] public List<OrderDetailDto>? Details { get; set; }

        [JsonPropertyName("gifts")] public List<GiftDto>? Gifts { get; set; }
    }

    public class OrderDetailDto
    {
        [JsonPropertyName("productId")] public int ProductId { get; set; }
        [JsonPropertyName("productName")] public string ProductName { get; set; }
        [JsonPropertyName("quantity")] public int Quantity { get; set; }
        [JsonPropertyName("unitPrice")] public decimal UnitPrice { get; set; }
        [JsonPropertyName("discountAmount")] public decimal? DiscountAmount { get; set; }
        [JsonPropertyName("discountPercent")] public decimal? DiscountPercent { get; set; }
    }

    public class GiftDto
    {
        [JsonPropertyName("giftId")] public int GiftId { get; set; }
        [JsonPropertyName("giftProductName")] public string GiftProductName { get; set; }
        [JsonPropertyName("quantity")] public int Quantity { get; set; }
    }

    public class StatusUpdateDto
    {
        [JsonPropertyName("status")] public string Status { get; set; }
    }
}
