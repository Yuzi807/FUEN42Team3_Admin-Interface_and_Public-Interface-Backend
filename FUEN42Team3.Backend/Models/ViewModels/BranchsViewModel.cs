using FUEN42Team3.Backend.Models.EfModels;
using System.ComponentModel.DataAnnotations;

namespace FUEN42Team3.Backend.Models.ViewModels
{
    // 分店管理的主要 ViewModel
    public class BranchsIndexViewModel
    {
        // 分店列表
        public List<BranchViewModel> BranchList { get; set; } = new List<BranchViewModel>();
        
        // 區域列表 (用於下拉選單)
        public List<RegionViewModel> RegionList { get; set; } = new List<RegionViewModel>();
    }
    
    // 單一分店資訊的 ViewModel
    public class BranchViewModel
    {
        [Display(Name = "編號")]
        public int Id { get; set; }
        [Display(Name = "分店名稱")]
        public string Name { get; set; } = string.Empty;
        [Display(Name = "地址")]
        public string? Address { get; set; }
        [Display(Name = "聯絡電話")]
        public string? Phone { get; set; }
        [Display(Name = "地圖連結")]
        public string? MapUrl { get; set; }
        [Display(Name = "區域")]
        public int RegionId { get; set; }
        [Display(Name = "區域名稱")]
        public string RegionName { get; set; } = string.Empty;
        [Display(Name = "顯示於前台")]
        public bool IsVisible { get; set; }
        [Display(Name = "建立時間")]
        public DateTime CreatedAt { get; set; }
        [Display(Name = "最後更新時間")]
        public DateTime UpdatedAt { get; set; }
        
        // 營業時間列表
        public List<BranchOpenTimeViewModel> OpeningHours { get; set; } = new List<BranchOpenTimeViewModel>();
    }
    
    // 分店營業時間 ViewModel
    public class BranchOpenTimeViewModel
    {
        [Display(Name = "編號")]
        public int Id { get; set; }
        [Display(Name = "分店")]
        public int BranchId { get; set; }
        [Display(Name = "星期")]
        public byte Weekday { get; set; }
        public string WeekdayName { get; set; } = string.Empty;
        [Display(Name = "營業時間")]
        public TimeOnly? OpenTime { get; set; }
        public TimeOnly? CloseTime { get; set; }

        public bool IsOpen => OpenTime.HasValue && CloseTime.HasValue;
        public string DisplayTime
        {
            get
            {
                if (IsOpen)
                    return $"{OpenTime:HH:mm} - {CloseTime:HH:mm}";
                return "休息";
            }
        }
    }
    
    // 區域資訊 ViewModel
    public class RegionViewModel
    {
        [Display(Name = "編號")]
        public int Id { get; set; }
        [Display(Name = "區域名稱")]
        public string Name { get; set; } = string.Empty;
    }

}
