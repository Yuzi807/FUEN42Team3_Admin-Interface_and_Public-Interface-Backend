using Humanizer;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.Blazor;
using NuGet.Protocol.Plugins;
using System;
using System.Security.Principal;

namespace FUEN42Team3.Backend.Models.ViewModels
{
    public class AdsViewModel
    {
        // 跑馬燈、彈窗和輪播的統一 ViewModel
        public IEnumerable<MarqueeViewModel> MarqueesList { get; set; }
        public IEnumerable<PopupViewModel> PopupList { get; set; }
        public IEnumerable<CarouselViewModel> CarouselList { get; set; }
    }

    public class MarqueeViewModel
    {
        public int Id { get; set; } // 跑馬燈的唯一識別碼
        public string Message { get; set; } = string.Empty; // 跑馬燈訊息
        public string? Link { get; set; } // 可選的連結
        public bool IsActive { get; set; } = true; // 預設為啟用
        public int SortOrder { get; set; } = 0; // 預設排序順序為0

        //public DateTime CreatedAt { get; set; } = DateTime.Now; // 預設建立時間為現在
        //public DateTime UpdatedAt { get; set; } = DateTime.Now; // 預設最後更新時間為現在
    }

    public class PopupViewModel
    {
        public int Id { get; set; } // 彈窗的唯一識別碼
        public string Title { get; set; } = string.Empty; // 彈窗標題

        public string Image { get; set; } = string.Empty; // 圖片路徑
        public string? Link { get; set; } // 可選的連結
        public bool IsActive { get; set; } = true; // 預設為啟用
        public int SortOrder { get; set; } = 0; // 預設排序順序為0
    }

    public class CarouselViewModel
    {
        public int Id { get; set; } // 輪播的唯一識別碼
        public string Title { get; set; } = string.Empty; // 輪播標題
        public string? SubTitle { get; set; } = string.Empty; // 可選的副標題
        public string Image { get; set; } = string.Empty; // 圖片路徑
        public string? Link { get; set; } // 可選的連結
        public bool IsActive { get; set; } = true; // 預設為啟用
        public int SortOrder { get; set; } = 0; // 預設排序順序為0
    }
}