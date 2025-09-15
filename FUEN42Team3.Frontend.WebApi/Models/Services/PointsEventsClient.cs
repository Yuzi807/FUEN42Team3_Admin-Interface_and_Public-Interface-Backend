using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FUEN42Team3.Frontend.WebApi.Models.Services
{
    public class PointsEventsClient
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<PointsEventsClient> _logger;

        public PointsEventsClient(IHttpClientFactory httpFactory, IConfiguration config, ILogger<PointsEventsClient> logger)
        {
            _httpFactory = httpFactory;
            _config = config;
            _logger = logger;
        }

        private string? BaseUrl => _config["PointsEvents:BaseUrl"];
        private string? ApiKey => _config["PointsEvents:ApiKey"];

        public async Task<bool> SendEventAsync(string eventType, int memberId, decimal? amount = null, int? orderId = null, string? customEventKey = null, CancellationToken ct = default)
        {
            try
            {
                var baseUrl = (BaseUrl ?? string.Empty).TrimEnd('/');
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    _logger.LogWarning("PointsEvents BaseUrl not configured; skip sending event {Event} for MemberId={MemberId}", eventType, memberId);
                    return false;
                }

                var url = $"{baseUrl}/admin/point-rules/events";
                var payload = new { EventType = eventType, MemberId = memberId, Amount = amount, OrderId = orderId, CustomEventKey = customEventKey };
                var json = JsonSerializer.Serialize(payload);
                var client = _httpFactory.CreateClient();

                // 重試 3 次，指數退避 200ms, 400ms, 800ms
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    using var req = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };
                    if (!string.IsNullOrWhiteSpace(ApiKey)) req.Headers.Add("X-Points-ApiKey", ApiKey);

                    HttpResponseMessage res = null!;
                    try
                    {
                        res = await client.SendAsync(req, ct);
                        if (res.IsSuccessStatusCode) return true;
                        var body = await res.Content.ReadAsStringAsync(ct);
                        _logger.LogWarning("SendEvent attempt {Attempt} failed: {Status} {Body}", attempt, (int)res.StatusCode, body);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "SendEvent attempt {Attempt} threw", attempt);
                    }

                    if (attempt < 3) await Task.Delay(200 * (int)Math.Pow(2, attempt - 1), ct);
                }

                // 全數失敗 → 落檔 JSONL
                try
                {
                    var logsDir = Path.Combine(AppContext.BaseDirectory, "wwwroot", "logs");
                    Directory.CreateDirectory(logsDir);
                    var line = JsonSerializer.Serialize(new { ts = DateTime.UtcNow, eventType, memberId, amount, orderId, customEventKey });
                    await File.AppendAllTextAsync(Path.Combine(logsDir, "points-events.failed.jsonl"), line + Environment.NewLine, ct);
                }
                catch { /* ignore file log failure */ }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendEvent exception for {Event} MemberId={MemberId}", eventType, memberId);
                return false;
            }
        }

        public Task<bool> SendRegistrationCompletedAsync(int memberId, CancellationToken ct = default)
            => SendEventAsync("RegistrationCompleted", memberId, null, null, null, ct);

        public Task<bool> SendOrderCompletedAsync(int memberId, int orderId, decimal amount, bool isFirstPurchase, CancellationToken ct = default)
        {
            // 兩種事件都送：首購與達標
            var t1 = isFirstPurchase ? SendEventAsync("FirstPurchaseCompleted", memberId, amount, orderId, null, ct) : Task.FromResult(true);
            var t2 = SendEventAsync("SpendingThreshold", memberId, amount, orderId, null, ct);
            return Task.WhenAll(t1, t2).ContinueWith(t => t.Exception == null && t1.Result && t2.Result, ct);
        }
    }
}
