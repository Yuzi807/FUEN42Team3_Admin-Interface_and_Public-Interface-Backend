namespace FUEN42Team3.Backend.Models.ViewModels
{
    public class MembersViewModel
    {
        public int Id { get; set; }
        public string UserName { get; set; } // 使用者名稱
        public string Email { get; set; } // 帳號
        public string RoleName { get; set; } // 角色名稱
        public bool IsActive { get; set; } // 是否啟用
        public DateTime CreatedAt { get; set; } // 創建時間
        public DateTime? LastLoginAt { get; set; } // 最後登入時間 目前還沒有
    }
}
