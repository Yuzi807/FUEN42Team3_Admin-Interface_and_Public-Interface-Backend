namespace FUEN42Team3.Frontend.WebApi.Models.DTOs
{
    public class PostDto
    {
        public int Id { get; set; }
        public string PostType { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public DateTime? PostTime { get; set; }
        public string Summary { get; set; } // 新增

        public int NumOfHits { get; set; }         // 點閱數
        public int NumOfGoods { get; set; }

        public string? CoverImage { get; set; }

        public DateTime? LastEditTime { get; set; }


    }
}
