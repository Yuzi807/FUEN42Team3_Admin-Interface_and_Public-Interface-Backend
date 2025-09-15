using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Frontend.WebApi.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Frontend.WebApi.Models.Services
{
    // 會員無互動 X 分鐘時，將會話從 Live -> Open 並廣播給該會員房間
    public class ChatInactivityMonitor : BackgroundService
    {
        private readonly ILogger<ChatInactivityMonitor> _logger;
        private readonly IServiceProvider _sp;
        private readonly IHubContext<ChatHub> _hub;
        private readonly IConfiguration _config;

        public ChatInactivityMonitor(ILogger<ChatInactivityMonitor> logger, IServiceProvider sp, IHubContext<ChatHub> hub, IConfiguration config)
        {
            _logger = logger; _sp = sp; _hub = hub; _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 每 60 秒巡檢一次
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var minutes = _config.GetValue<int?>("Chat:AutoEndMinutes") ?? 10; // 預設 10 分鐘
                    if (minutes > 0)
                    {
                        await ScanAndAutoEndAsync(minutes, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ChatInactivityMonitor scan failed");
                }

                try { await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); } catch { }
            }
        }

        private async Task ScanAndAutoEndAsync(int minutes, CancellationToken ct)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var threshold = TaipeiTime.Now.AddMinutes(-minutes);

            // 找出 Live 但最近沒有會員訊息的會話
            var candidates = await db.ChatConversations
                .Where(c => c.Status == "Live")
                .Select(c => new
                {
                    Conv = c,
                    LastMemberAt = db.ChatMessages
                        .Where(m => m.ConversationId == c.Id && m.Sender == "Member")
                        .Max(m => (DateTime?)m.CreatedAt)
                })
                .ToListAsync(ct);

            var toEnd = candidates
                .Where(x => (TaipeiTime.ToTaipei(x.LastMemberAt ?? x.Conv.LastMessageAt)) < threshold)
                .Select(x => x.Conv)
                .ToList();

            if (toEnd.Count == 0) return;

            foreach (var conv in toEnd)
            {
                conv.Status = "Open";
            }
            await db.SaveChangesAsync(ct);

            // 廣播到各自房間
            foreach (var conv in toEnd)
            {
                var room = $"M{conv.MemberId}";
                await _hub.Clients.Group(room).SendAsync("liveStatusChanged", new { status = "Open" }, ct);
            }

            _logger.LogInformation("Auto-ended {Count} live conversations due to inactivity (>{Minutes}m)", toEnd.Count, minutes);
        }
    }
}
