using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace FUEN42Team3.Frontend.WebApi.Models.Services
{
    public class EmailSender
    {
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _smtpUsername;
        private readonly string _smtpPassword;
        private readonly bool _enableSsl;
        private readonly bool _checkCertRevocation;

        public EmailSender(IConfiguration config)
        {
            // 透過 DI 注入設定檔（appsettings.json）
            _smtpServer = config.GetValue<string>("SmtpSettings:SmtpServer", string.Empty) ?? string.Empty;
            _smtpPort = config.GetValue<int>("SmtpSettings:SmtpPort", 0);
            _smtpUsername = config.GetValue<string>("SmtpSettings:SmtpUsername", string.Empty) ?? string.Empty;
            _smtpPassword = config.GetValue<string>("SmtpSettings:SmtpPassword", string.Empty) ?? string.Empty;
            _enableSsl = config.GetValue<bool>("SmtpSettings:EnableSsl", true);
            // 若在某些 Windows 環境無法做憑證撤銷查核（CRL/OCSP），可於開發環境關閉
            _checkCertRevocation = config.GetValue<bool>("SmtpSettings:CheckCertificateRevocation", true);
        }

        public async Task SendEmail(string senderName, string senderEmail, string toName, string toEmail, string subject, string textContent)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderEmail));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;

            message.Body = new TextPart("html")
            {
                Text = textContent
            };

            using var client = new SmtpClient();
            try
            {
                // 依設定控制是否檢查憑證撤銷；在無法連外查核時避免握手失敗
                client.CheckCertificateRevocation = _checkCertRevocation;
                await client.ConnectAsync(_smtpServer, _smtpPort, _enableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);
                if (!string.IsNullOrEmpty(_smtpUsername))
                {
                    await client.AuthenticateAsync(_smtpUsername, _smtpPassword);
                }
                var result = await client.SendAsync(message);
                Console.WriteLine("Email成功寄出: \n" + result);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Email寄送失敗: \n" + ex.ToString());
                throw; // 讓背景佇列可感知失敗並做重試/記錄
            }
            finally
            {
                try { await client.DisconnectAsync(true); } catch { /* ignore */ }
            }
        }
        public async Task SendConfirmRegisterEmail(string url, string name, string userEmail)
        {
            string senderName = "魔型仔官方團隊";
            string senderEmail = "Ghosttoy0905@gmail.com";
            string username = name;
            string email = userEmail;
            string subject = "[魔型仔Ghost Toys]會員註冊確認信";

            string message = $@"
                <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <h2>歡迎加入魔型仔，{name}！</h2>
                        <p>感謝您的註冊。為了完成帳號開通，請點擊以下連結進行 Email 驗證：</p>
                        <p>
                            <a href='{url}' target='_blank' style='color: #007bff; text-decoration: none;'>
                                點我完成驗證
                            </a>或點選以下連接<br/>
                            {url}
                        </p>
                        <p>如果您沒有提出申請，請忽略本信件。</p>
                        <br/>
                        <p style='font-size: 0.9em; color: #888;'>魔型仔官方團隊敬上</p>
                    </body>
                </html>
            ";
            // 呼叫 SendEmail 方法寄送確認註冊信
            await SendEmail(senderName, senderEmail, username, email, subject, message);
        }

        public async Task SendForgotPasswordEmail(string code, string name, string userEmail)
        {
            string senderName = "魔型仔官方團隊";
            string senderEmail = "Ghosttoy0905@gmail.com";
            string username = name;
            string email = userEmail;
            string subject = "[魔型仔Ghost Toys]重設密碼驗證碼";

            string message = $@"
                <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <h2>重設密碼請求</h2>
                        <p>親愛的 {name}，</p>
                        <p>我們收到了您的重設密碼請求。您的一次性驗證碼為：</p>
                        <p>
                            <h2><span style=""color:#2980b9""><strong>{code}</strong></span></h2>
                        </p>
                        <p>請在15分鐘內完成驗證，謝謝!</p>
                        <br/>
                        <p style='font-size: 0.9em; color: #888;'>魔型仔官方團隊敬上</p>
                    </body>
                </html>
            ";
            // 呼叫 SendEmail 方法寄送重設密碼信
            await SendEmail(senderName, senderEmail, username, email, subject, message);
        }
    }
}
