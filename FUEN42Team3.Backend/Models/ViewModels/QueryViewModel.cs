using X.PagedList;

namespace FUEN42Team3.Backend.Models.ViewModels
{
    public class QueryViewModel<T>//分頁+搜尋+時間範圍查詢
        where T : class
    {
        public string? Keyword { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 5;

        public IPagedList<T> Items { get; set; } = new PagedList<T>(new List<T>(), 1, 1);
    }
}
