namespace FUEN42Team3.Backend.Models.ViewModels
{
    public class PunishmentsViewModel
    {
        public int Id { get; set; }
        public string Member { get; set; } = "未知";
        public string TypeName { get; set; } = "未知";
        public string Description { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool IsActive { get; set; }

        // 新增：是否處於生效區間（Start <= now 且 (End==null 或 End>=now)）
        public bool IsCurrentlyEffective { get; set; }
    }

}
