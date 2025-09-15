using FUEN42Team3.Backend.Models.EfModels;

namespace FUEN42Team3.Backend.Models.Interfaces
{
    public interface IUserRepository
    {
        /// <summary>
        /// 根據使用者名稱取得使用者資料
        /// </summary>
        /// <param name="username">使用者名稱</param>
        /// <returns>使用者實體，如果不存在則返回 null</returns>
        Task<User?> GetUserByUsernameAsync(string username);

        /// <summary>
        /// 根據使用者 ID 取得使用者資料
        /// </summary>
        /// <param name="userId">使用者 ID</param>
        /// <returns>使用者實體，如果不存在則返回 null</returns>
        Task<User?> GetUserByIdAsync(int userId);

        /// <summary>
        /// 更新使用者最後登入時間
        /// </summary>
        /// <param name="userId">使用者 ID</param>
        /// <returns>是否更新成功</returns>
        Task<bool> UpdateLastLoginTimeAsync(int userId);

        /// <summary>
        /// 檢查使用者是否為活躍狀態
        /// </summary>
        /// <param name="userId">使用者 ID</param>
        /// <returns>是否為活躍狀態</returns>
        Task<bool> IsUserActiveAsync(int userId);
    }
}
