namespace FUEN42Team3.Frontend.WebApi.Models.DTOs
{
    public class PostUpdateDto
    {
        public int Id { get; set; }              // 要修改的文章 Id
        public string Title { get; set; } = default!;
        public string PostContent { get; set; } = default!;
        public string? CoverImage { get; set; }
        public int PostTypeId { get; set; }
        public int StatusId { get; set; }
        public List<string> Tags { get; set; } = new();
    }
}
