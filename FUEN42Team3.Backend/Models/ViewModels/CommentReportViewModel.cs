namespace FUEN42Team3.Backend.Models.ViewModels
{
    public class CommentReportViewModel
    {
        public int Id { get; set; }

        public int CommentId { get; set; }
        public string CommentText { get; set; }

        public string PostTitle { get; set; } // 所屬文章標題

        public int? ReporterId { get; set; }
        public string ReporterName { get; set; }

        public int? CommenterId { get; set; }
        public string CommenterName { get; set; }

        public string RuleName { get; set; }
        public DateTime ReportTime { get; set; }

        public string StatusName { get; set; }
        public string ResultName { get; set; }

    }


}
