using System.Text.Json.Serialization;

namespace FUEN42Team3.Backend.Models.DTOs
{
    public class DeliveryMethodDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("shipping_name")] public string ShippingName { get; set; }
        [JsonPropertyName("base_shipping_cost")] public decimal BaseShippingCost { get; set; }
    }
}