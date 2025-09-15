namespace FUEN42Team3.Frontend.WebApi.Models.DTOs
{
    public class PostReportCreateDto
    {
        public int RuleId { get; set; } // 檢舉規則
    }

    public class RuleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public string Description { get; set; } = default!;
    }
}
