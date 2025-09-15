namespace FUEN42Team3.Backend.Models.ViewModels
{
    public class UsersViewModel
    {
        public int Id { get; set; } // 編號
        public string UserName { get; set; }  // 使用者名稱
        public string Account { get; set; } // 帳號
        public string RoleName { get; set; }  // 權限名稱
        public bool IsActive { get; set; }  // 是否啟用
        public DateTime? CreatedAt { get; set; } // 建立時間
        public int RoleId { get; set; } // 角色ID
    }
    // 建立使用者的 ViewModel
    public class UserCreateViewModel
    {
        public string UserName { get; set; }
        public string Account { get; set; }
        public string Password { get; set; }
        public int RoleId { get; set; }
        public bool IsActive { get; set; } = true;
    }

    // 更新使用者的 ViewModel
    public class UserUpdateViewModel
    {
        public int Id { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; } // 可為空，表示不更新密碼
        public int RoleId { get; set; }
        public bool IsActive { get; set; }
    }

    // 修改密碼的 ViewModel
    public class PasswordChangeViewModel
    {
        public int UserId { get; set; }
        public string CurrentPassword { get; set; } // 目前密碼
        public string NewPassword { get; set; } // 新密碼
        public string ConfirmPassword { get; set; } // 確認新密碼
    }

    // 修改密碼的回應 ViewModel
    public class PasswordChangeResultViewModel
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public bool RequireLogout { get; set; } // 是否需要登出
    }
}
