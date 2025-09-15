using System.Text.Json.Serialization;

namespace FUEN42Team3.Backend.Models.DTOs
{
    public class PaymentMethodDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("method_name")] public string MethodName { get; set; }
        [JsonPropertyName("description")] public string Description { get; set; }
    }
}