using System.ComponentModel.DataAnnotations;

namespace FUEN42Team3.Backend.Models.ViewModels
{
    public class NewsViewModel
    {
        public bool IsPinned { get; set; }    // 置頂
        public int Id { get; set; }           // 編號
        public string Title { get; set; }     // 標題
        public string CategoryName { get; set; } // 分類
        public string UserName { get; set; }  // 發文者
        public DateTime? PublishedAt { get; set; } // 發布時間
        public DateTime UpdatedAt { get; set; }   // 更新時間
        public int ViewCountToday { get; set; }   // 當日瀏覽
        public int ViewCountTotal { get; set; }   // 累積瀏覽
        public string Status { get; set; }    // 狀態：上架/下架/草稿
    }
    public class NewsCategoriesViewModel
    {
        // 最新消息分類
        public int Id { get; set; }
        public string CategoryName { get; set; }

        // 用來接收上傳的檔案（新增或更新時）
        public IFormFile? Icon { get; set; }

        // 用來顯示儲存後的路徑（讀取資料時）
        public string? IconPath { get; set; }

        public bool IsVisible { get; set; }

    }
    // 複合型 ViewModel
    public class NewsIndexViewModel
    {
        public IEnumerable<NewsViewModel> NewsList { get; set; }
        public IEnumerable<NewsCategoriesViewModel> CategoryList { get; set; }
    }

    // 新增公告用的 ViewModel
    public class NewsCreateViewModel
    {
        [Required(ErrorMessage = "請輸入公告標題")]
        public string Title { get; set; }  // 標題
        
        [Required(ErrorMessage = "請選擇公告類別")]
        public int CategoryId { get; set; }  // 分類ID
        
        [Required(ErrorMessage = "請輸入公告內容")]
        public string Content { get; set; }  // 內容
        
        public IFormFile? ImageFile { get; set; }  // 上傳圖片
        
        public string? ImageUrl { get; set; }  // 圖片URL
        
        public bool IsPinned { get; set; }  // 置頂
        
        public bool IsPublished { get; set; } = true;  // 是否上架
        
        public DateTime? PublishedAt { get; set; }  // 發布時間
        
        public string Status { get; set; } = "published";  // 狀態(published/draft)
    }
    
    // 編輯公告用的 ViewModel
    public class NewsEditViewModel
    {
        public int Id { get; set; }  // 公告ID
        
        [Required(ErrorMessage = "請輸入公告標題")]
        public string Title { get; set; }  // 標題
        
        [Required(ErrorMessage = "請選擇公告類別")]
        public int CategoryId { get; set; }  // 分類ID
        
        [Required(ErrorMessage = "請輸入公告內容")]
        public string Content { get; set; }  // 內容
        
        public IFormFile? ImageFile { get; set; }  // 新上傳圖片
        
        public string? ImageUrl { get; set; }  // 現有圖片URL
        
        public bool KeepOriginalImage { get; set; } = true;  // 是否保留原圖
        
        public bool IsPinned { get; set; }  // 置頂
        
        public bool IsPublished { get; set; } = true;  // 是否上架
        
        public DateTime? PublishedAt { get; set; }  // 發布時間
        
        public string Status { get; set; } = "published";  // 狀態(published/draft)
        
        public DateTime CreatedAt { get; set; }  // 創建時間
        
        public int ViewCountToday { get; set; }  // 當日瀏覽數
        
        public int ViewCountTotal { get; set; }  // 總瀏覽數
    }
}
