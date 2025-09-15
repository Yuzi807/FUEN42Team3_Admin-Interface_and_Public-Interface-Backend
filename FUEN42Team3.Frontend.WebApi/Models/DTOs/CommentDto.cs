namespace FUEN42Team3.Frontend.WebApi.Models.DTOs
{
    public class CommentDto
    {
        public int Id { get; set; }
        public string CommentText { get; set; } = default!;
        public DateTime CommentTime { get; set; }

        public int MemberId { get; set; }

        public string MemberName { get; set; } = default!;
        public int? ReplyToCommentId { get; set; }

        public int NumOfGoods { get; set; }   // 按讚數

        public bool IsLiked { get; set; }
        // 巢狀子留言
        public string? MemberPhoto { get; set; }   // 留言者頭像路徑

        public List<CommentDto> Replies { get; set; } = new();

    }
}
