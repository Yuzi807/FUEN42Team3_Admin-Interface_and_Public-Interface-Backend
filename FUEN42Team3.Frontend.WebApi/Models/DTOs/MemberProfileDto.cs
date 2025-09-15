namespace FUEN42Team3.Frontend.WebApi.Models.DTOs
{
    public class MemberProfileDto
    {
        public string? PhotoUrl { get; set; }
        public string? RealName { get; set; }
        public string UserName { get; set; }
        public string? Gender { get; set; }
        public DateOnly? Birthday { get; set; }
        public string? Phone { get; set; }

    }
}
