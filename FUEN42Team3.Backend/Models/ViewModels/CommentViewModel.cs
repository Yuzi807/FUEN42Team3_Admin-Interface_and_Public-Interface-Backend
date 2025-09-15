namespace FUEN42Team3.Backend.Models.ViewModels
{
    public class CommentViewModel
    {
        public int Id { get; set; }
        public string CommentText { get; set; }
        public DateTime CommentTime { get; set; }
        public string MemberName { get; set; }
        public int PostId { get; set; }
        public string PostTitle { get; set; }
    }
}
