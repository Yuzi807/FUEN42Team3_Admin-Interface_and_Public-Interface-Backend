namespace FUEN42Team3.Security
{
    using BCrypt.Net;

    public class HashUtility
    {
        /// <summary>
        /// 使用 BCrypt 對密碼進行雜湊
        /// </summary>
        /// <param name="password">要雜湊的密碼</param>
        /// <param name="workFactor">工作因子 (預設 12，數字越高越安全但越慢)</param>
        /// <returns>雜湊後的密碼</returns>
        public static string HashPassword(string password)
        {
            const int workFactor = 12;
            return BCrypt.EnhancedHashPassword(password, workFactor);
        }

        /// <summary>
        /// 驗證密碼是否正確
        /// </summary>
        /// <param name="password">原始密碼</param>
        /// <param name="hashedPassword">雜湊後的密碼</param>
        /// <returns>密碼是否正確</returns>
        public static bool VerifyPassword(string password, string hashedPassword)
        {
            return BCrypt.EnhancedVerify(password, hashedPassword);
        }
    }
}
