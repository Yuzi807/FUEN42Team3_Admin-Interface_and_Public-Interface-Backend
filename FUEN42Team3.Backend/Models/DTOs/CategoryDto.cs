using System.Text.Json.Serialization;

namespace FUEN42Team3.Backend.Models.DTOs
{
    public class CategoryDto
    {
        [JsonPropertyName("value")] public string Value { get; set; }
        [JsonPropertyName("text")] public string Text { get; set; }
    }
}