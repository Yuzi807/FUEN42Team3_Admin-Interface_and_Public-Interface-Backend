using Microsoft.Identity.Client;

namespace FUEN42Team3.Frontend.WebApi.Models.DTOs
{
    public class NewsDto
    {
        public int Id { get; set; }
        public string CategoryName { get; set; } // 顯示分類名稱
        public string Title { get; set; }
        public string Content { get; set; }
        public string ImageUrl { get; set; } // 封面圖片
        public DateTime? PublishDate { get; set; }
        public string? Author { get; set; } // 作者
        public int ViewCountTotal { get; set; } // 瀏覽次數
        public bool IsPinned { get; set; } // 是否置頂
    }
}
