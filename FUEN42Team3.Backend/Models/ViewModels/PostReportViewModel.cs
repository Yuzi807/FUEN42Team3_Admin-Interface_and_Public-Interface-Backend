namespace FUEN42Team3.Backend.Models.ViewModels
{
    public class PostReportViewModel
    {
        public int Id { get; set; }
        public int PostId { get; set; }
        public string PostTitle { get; set; }

        public int? ReporterId { get; set; }
        public string ReporterName { get; set; }

        public int? PosterId { get; set; } // 被檢舉人
        public string PosterName { get; set; }

        public string RuleName { get; set; }
        public DateTime ReportTime { get; set; }

        public string StatusName { get; set; }
        public string ResultName { get; set; }

    }

    


}
