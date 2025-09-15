namespace FUEN42Team3.Frontend.WebApi.Models.DTOs
{
    public class BannerAdDto
    {
        public int Id { get; set; }
        public string Title { get; set; }       // 輪播圖標題
        public string ImageUrl { get; set; }    // 圖片網址
        public string LinkUrl { get; set; }     // 點擊導向連結
        public bool IsActive { get; set; }    // 是否啟用
        public int SortOrder { get; set; }   // 排序欄位，用於控制顯示順序

    }
}
