using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace FUEN42Team3.Frontend.WebApi.Models.Services
{
    // Email 工作項目
    public record EmailMessage(
        string SenderName,
        string SenderEmail,
        string ToName,
        string ToEmail,
        string Subject,
        string Html
    );

    // 供控制器投遞用的簡單介面
    public interface IEmailQueue
    {
        bool Enqueue(EmailMessage message);
    }

    public class EmailQueueOptions
    {
        public int Capacity { get; set; } = 1000;      // 佇列容量
        public int Workers { get; set; } = 2;          // 併發工作執行緒數
        public int MaxRetry { get; set; } = 3;         // 每封信最大重試次數
        public int BaseDelayMs { get; set; } = 500;    // 重試基礎延遲
    }

    // 具體的 Channel 佇列；控制器依賴 IEmailQueue，背景服務依賴此具體類別以取得 Reader
    public class ChannelEmailQueue : IEmailQueue
    {
        private readonly ILogger<ChannelEmailQueue> _logger;
        private readonly Channel<EmailMessage> _channel;

        public ChannelReader<EmailMessage> Reader => _channel.Reader;

        public ChannelEmailQueue(IOptions<EmailQueueOptions> options, ILogger<ChannelEmailQueue> logger)
        {
            _logger = logger;
            var cap = Math.Max(64, options.Value?.Capacity ?? 1000);
            _channel = Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(cap)
            {
                FullMode = BoundedChannelFullMode.DropOldest // 滿載時丟最舊，避免阻塞熱路徑
            });
        }

        public bool Enqueue(EmailMessage message)
        {
            var ok = _channel.Writer.TryWrite(message);
            if (!ok)
            {
                _logger.LogWarning("Email queue full, drop message to={toEmail}, subject={subject}", message.ToEmail, message.Subject);
            }
            return ok;
        }
    }

    // 背景服務：從佇列取出並呼叫 EmailSender 寄送，具備簡易重試與退避
    public class EmailBackgroundService : BackgroundService
    {
        private readonly ChannelEmailQueue _queue;
        private readonly EmailSender _sender;
        private readonly ILogger<EmailBackgroundService> _logger;
        private readonly EmailQueueOptions _options;

        public EmailBackgroundService(
            ChannelEmailQueue queue,
            EmailSender sender,
            IOptions<EmailQueueOptions> options,
            ILogger<EmailBackgroundService> logger)
        {
            _queue = queue;
            _sender = sender;
            _logger = logger;
            _options = options?.Value ?? new EmailQueueOptions();
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var workers = Math.Max(1, _options.Workers);
            var tasks = new List<Task>(workers);
            for (int i = 0; i < workers; i++)
            {
                tasks.Add(Task.Run(() => WorkerLoop(i + 1, stoppingToken), stoppingToken));
            }
            return Task.WhenAll(tasks);
        }

        private async Task WorkerLoop(int workerId, CancellationToken ct)
        {
            await foreach (var msg in _queue.Reader.ReadAllAsync(ct))
            {
                var attempt = 0;
                var maxRetry = Math.Max(0, _options.MaxRetry);
                var baseDelay = Math.Max(50, _options.BaseDelayMs);
                while (true)
                {
                    try
                    {
                        await _sender.SendEmail(
                            senderName: msg.SenderName,
                            senderEmail: msg.SenderEmail,
                            toName: msg.ToName,
                            toEmail: msg.ToEmail,
                            subject: msg.Subject,
                            textContent: msg.Html);
                        break; // 成功
                    }
                    catch (Exception ex)
                    {
                        attempt++;
                        if (attempt > maxRetry || ct.IsCancellationRequested)
                        {
                            _logger.LogError(ex, "Email send failed after {attempt} attempts, to={to}, subject={subject}", attempt, msg.ToEmail, msg.Subject);
                            break;
                        }
                        var delay = TimeSpan.FromMilliseconds(baseDelay * Math.Pow(2, attempt - 1));
                        _logger.LogWarning(ex, "Email send failed (attempt {attempt}/{max}), retry in {delay}ms, to={to}, subject={subject}", attempt, maxRetry, delay.TotalMilliseconds, msg.ToEmail, msg.Subject);
                        try { await Task.Delay(delay, ct); } catch { /* ignore */ }
                    }
                }
            }
        }
    }
}
