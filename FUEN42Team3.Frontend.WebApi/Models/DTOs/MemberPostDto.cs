namespace FUEN42Team3.Frontend.WebApi.Models.DTOs
{
    public class MyPostRowDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string PostType { get; set; } = "";   // = p.Type.Name
        public string? CoverImage { get; set; }      // = p.ImageUrl
        public int NumOfHits { get; set; }           // 點閱
        public int NumOfGoods { get; set; }          // 讚數
        public DateTime? PostTime { get; set; }      // 建議以前端「發佈時間」顯示
        public DateTime? LastEditTime { get; set; }  // 最後修改時間
        public int CommentCount { get; set; }        // 可選（若沒有留言表，先給 0）
        public string Summary { get; set; } = "";    // 前端摘要用

        public int MemberId { get; set; }//前端驗證用

    }

    public class FavoritePostRowDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string PostType { get; set; } = "";
        public string? CoverImage { get; set; }
        public int NumOfHits { get; set; }
        public int NumOfGoods { get; set; }
        public DateTime? PostTime { get; set; }
    }
}
