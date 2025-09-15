namespace FUEN42Team3.Frontend.WebApi.Models.Services.EmailTemplates
{
    public class RegistrationEmailBuilder
    {
        public static string Build(string userName, string confirmationUrl)
        {
            return $@"
                <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <h2>歡迎加入魔型仔，{userName}！</h2>
                        <p>感謝您的註冊。為了完成帳號開通，請點擊以下連結進行 Email 驗證：</p>
                        <p>
                            <a href='{confirmationUrl}' target='_blank' style='color: #007bff; text-decoration: none;'>
                                👉 點我完成驗證
                            </a>
                        </p>
                        <p>如果您沒有提出申請，請忽略本信件。</p>
                        <br/>
                        <p style='font-size: 0.9em; color: #888;'>魔型仔官方團隊敬上</p>
                    </body>
                </html>
            ";
        }
    }
}
