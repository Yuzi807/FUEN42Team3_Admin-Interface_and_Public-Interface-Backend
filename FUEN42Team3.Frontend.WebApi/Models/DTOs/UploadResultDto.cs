namespace FUEN42Team3.Frontend.WebApi.Models.DTOs
{
    public class UploadResultDto
    {
        public string FileName { get; set; } = default!;
        public string Url { get; set; } = default!;
        public long FileSize { get; set; }
        public string ContentType { get; set; } = default!;
    }
}
