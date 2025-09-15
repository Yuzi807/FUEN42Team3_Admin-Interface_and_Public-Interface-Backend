namespace FUEN42Team3.Frontend.WebApi.Models.DTOs.Auth
{
    public class ChangePasswordDto
    {
        public string OldPassword { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmPassword { get; set; }

    }
}
