namespace FUEN42Team3.Frontend.WebApi.Models.DTOs.Auth
{
    public class ForgotPasswordDto
    {
        public string Email { get; set; }
    }

    // 重用相同格式的DTO用於重發驗證信
    public class ResendVerificationDto
    {
        public string Email { get; set; }
    }
}
