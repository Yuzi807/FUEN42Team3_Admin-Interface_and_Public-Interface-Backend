namespace FUEN42Team3.Frontend.WebApi.Models.DTOs.Auth
{
    public class LoginDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public bool RememberMe { get; set; }
    }
}
