namespace FUEN42Team3.Frontend.WebApi.Models.DTOs
{
    public class MarqueesDto
    {
        public int Id { get; set; }
        public string Message { get; set; }   // 滾動訊息內容
        public string LinkUrl { get; set; }   // 點擊導向連結
        public bool IsActive { get; set; }    // 是否啟用
        public int SortOrder { get; set; }   // 排序欄位，用於控制顯示順序
    }
}
