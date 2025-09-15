namespace FUEN42Team3.Frontend.WebApi.Models.DTOs
{
    public class PageResult<T>
    {
        public IEnumerable<T> Items { get; set; } = new List<T>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }
}
