namespace FUEN42Team3.Frontend.WebApi.Models.DTOs
{
    public class CreateCommentDto
    {
        public string CommentText { get; set; } = default!;
        public int PostId { get; set; }
        public int? ReplyToCommentId { get; set; }

    }
}
