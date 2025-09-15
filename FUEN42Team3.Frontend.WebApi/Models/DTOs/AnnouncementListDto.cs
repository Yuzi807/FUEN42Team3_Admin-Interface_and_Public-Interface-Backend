namespace FUEN42Team3.Frontend.WebApi.Models.DTOs
{
    public class AnnouncementListDto
    {
        public int Id { get; set; }
        public string AnnouncementType { get; set; } = "公告"; // 固定顯示"公告"
        public string Title { get; set; }
        public string Supervisor { get; set; } // 發布管理員帳號或名稱
        public DateTime PostTime { get; set; }
    }
}
