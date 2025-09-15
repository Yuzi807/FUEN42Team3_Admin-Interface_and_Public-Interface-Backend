namespace FUEN42Team3.Frontend.WebApi.Models.DTOs
{
    public class PostCreateDto
    {
        public string Title { get; set; } = default!;
        public string PostContent { get; set; } = default!;
        public string? CoverImage { get; set; }  // 封面圖片路徑（上傳後存檔名或 URL）
        public int PostTypeId { get; set; }      // 類型 Id
        public int StatusId { get; set; }        // 1=公開, 2=草稿
        public int MemberId { get; set; }        // 作者 Id（後端也可以用登入帳號帶入）
        public List<string> Tags { get; set; } = new(); // 標籤（多選）
    }
}
