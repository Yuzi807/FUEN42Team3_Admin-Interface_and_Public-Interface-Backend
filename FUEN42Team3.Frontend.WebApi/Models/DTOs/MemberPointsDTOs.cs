using System;
using System.Collections.Generic;

namespace FUEN42Team3.Frontend.WebApi.Models.DTOs
{
    public class MemberPointRecordDto
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public int Points { get; set; } // 變動點數（正為獲得、負為使用）
        public string Type { get; set; } = "earned"; // earned | used | expired (衍生)
        public string Description { get; set; } = string.Empty;
        public DateTime? ExpirationDate { get; set; } // 正數點數預設一年後到期
        public int Balance { get; set; } // 該筆後累計餘額（依時間排序累加）
    }

    public class MemberPointSummaryDto
    {
        public int CurrentPoints { get; set; }
        public int ExpiringPoints { get; set; }
        public DateTime? ExpiringDate { get; set; }
        public int EarnedThisMonth { get; set; }
        public int UsedThisMonth { get; set; }
    }

    public class PagedResult<T>
    {
        public IEnumerable<T> Items { get; set; } = Array.Empty<T>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        // 舊程式有使用 TotalCount，這裡提供相容別名
        public int TotalCount
        {
            get => TotalItems;
            set => TotalItems = value;
        }
        public int TotalPages => PageSize <= 0 ? 1 : (int)Math.Ceiling(TotalItems / (double)PageSize);
    }
}
