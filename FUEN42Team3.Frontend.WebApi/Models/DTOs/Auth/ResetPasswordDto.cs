namespace FUEN42Team3.Frontend.WebApi.Models.DTOs.Auth
{
    public class ResetPasswordDto
    {
        public string Token { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmPassword { get; set; }

    }
}
