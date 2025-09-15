using FUEN42Team3.Backend.Models.DTOs;
using FUEN42Team3.Backend.Models.ViewModels;
using System.Text.Json.Serialization;

namespace FUEN42Team3.Models.ViewModel
{
    public class OrderViewModel
    {
        public int Id { get; set; }

        [JsonPropertyName("user_id")]
        public int UserId { get; set; }

        [JsonPropertyName("total_amount")]
        public decimal TotalAmount { get; set; }

        [JsonPropertyName("status")]
        public string StatusText { get; set; }

        [JsonPropertyName("order_date")]
        public string OrderDate { get; set; }

        [JsonPropertyName("payment_method")]
        public string PaymentMethod { get; set; }

        [JsonPropertyName("delivery_method")]
        public string DeliveryMethod { get; set; }

        [JsonPropertyName("shipping_fee")]
        public decimal ShippingFee { get; set; }

        [JsonPropertyName("used_points")]
        public int UsedPoints { get; set; }

        [JsonPropertyName("category")]
        public string Category { get; set; }



        // 收件人資訊
        [JsonPropertyName("recipient_name")]
        public string RecipientName { get; set; }

        [JsonPropertyName("phone")]
        public string Phone { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("zip_code")]
        public string ZipCode { get; set; }

        // 明細與贈品
        [JsonPropertyName("details")]
        public List<OrderDetailViewModel> Details { get; set; } = new();

        [JsonPropertyName("gifts")]
        public List<GiftViewModel> Gifts { get; set; } = new();
    }
    public class OrderDetailViewModel
    {
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal DiscountAmount { get; set; }
        public bool IsPreorder { get; set; }
    }

    public class GiftViewModel
    {
        public string GiftProductName { get; set; }
        public int Quantity { get; set; }
    }




}
