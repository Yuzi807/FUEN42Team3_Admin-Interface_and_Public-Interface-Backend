using System.ComponentModel.DataAnnotations;

namespace FUEN42Team3.Backend.Models.ViewModels
{
    public class LoginViewModel
    {
        [Display(Name = "帳號")]
        [Required(ErrorMessage = "請輸入{0}")]
        public string Account { get; set; }

        [Display(Name = "密碼")]
        [Required(ErrorMessage = "請輸入{0}")]
        [DataType(DataType.Password)]
        public string Password { get; set; }
        [Display(Name = "記住帳號")]
        public bool RememberMe { get; set; } // 用於記住帳號功能
    }
}
