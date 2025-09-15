namespace FUEN42Team3.Frontend.WebApi.Models.DTOs
{
    public class PostDetailDto
    {
        public int Id { get; set; }

        // 標題
        public string Title { get; set; } = default!;

        // 文章內容 (HTML from Summernote)
        public string PostContent { get; set; } = default!;

        // 封面圖片路徑
        public string? CoverImage { get; set; }

        // 類型 (外鍵)
        public int PostTypeId { get; set; }
        public string? PostType { get; set; }

        // 狀態 (1=公開, 2=草稿)
        public int StatusId { get; set; }
        public string? StatusName { get; set; }

        // 發文者
        public int MemberId { get; set; }
        public string? AuthorUserName { get; set; }

        // 標籤（多個）
        public List<string> Tags { get; set; } = new();

        public int NumOfHits { get; set; }         // 點閱數
        public int NumOfGoods { get; set; }
        // 發文時間
        public DateTime PostTime { get; set; }

        public DateTime? LastEditTime { get; set; }
        public bool IsLiked { get; set; }

        public bool IsFavorited { get; set; }

        public bool HasOpenReport { get; set; }

        public string? AuthorPhoto { get; set; }   // 作者頭像路徑

    }


}
