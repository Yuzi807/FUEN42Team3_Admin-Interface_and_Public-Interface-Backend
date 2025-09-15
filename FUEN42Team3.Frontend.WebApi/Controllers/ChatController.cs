using FUEN42Team3.Backend.Models.EfModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Frontend.WebApi.Controllers
{
    [ApiController]
    [Route("api/chat")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly AppDbContext _db;
        public ChatController(AppDbContext db) => _db = db;

        private int? GetCurrentMemberId()
        {
            var val = User.FindFirst("MemberId")?.Value;
            return int.TryParse(val, out var id) ? id : (int?)null;
        }

        // 取得目前會員的會話（若無則建立一筆 Open），回傳會話 Id 與狀態
        // GET /api/chat/current
        [HttpGet("current")]
        public async Task<IActionResult> GetOrCreateCurrent()
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized();

            var conv = await _db.ChatConversations
                .FirstOrDefaultAsync(c => c.MemberId == mid && (c.Status == "Open" || c.Status == "Live"));
            if (conv == null)
            {
                conv = new ChatConversation
                {
                    MemberId = mid.Value,
                    Status = "Open",
                    CreatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now,
                    LastMessageAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now
                };
                _db.ChatConversations.Add(conv);
                await _db.SaveChangesAsync();
            }

            return Ok(new { conversationId = conv.Id, status = conv.Status });
        }

        // POST /api/chat/ask -> FAQ 簡易自動回覆（Live 模式不回 FAQ）
        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] AskRequest req)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized();
            var q = (req?.Question ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(q)) return BadRequest("empty question");

            // 取得或建立會話（Open/Live 擇一）
            var conv = await _db.ChatConversations.FirstOrDefaultAsync(c => c.MemberId == mid && (c.Status == "Open" || c.Status == "Live"))
                        ?? new ChatConversation { MemberId = mid.Value, Status = "Open", CreatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now, LastMessageAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now };
            if (conv.Id == 0)
            {
                _db.ChatConversations.Add(conv);
                await _db.SaveChangesAsync();
            }

            // 先記錄會員訊息
            var m1 = new ChatMessage { ConversationId = conv.Id, Sender = "Member", Type = "Text", Content = q, CreatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now };
            _db.ChatMessages.Add(m1);
            conv.LastMessageAt = m1.CreatedAt;

            // 若為 Live 模式，僅記錄，不回覆 FAQ
            if (string.Equals(conv.Status, "Live", StringComparison.OrdinalIgnoreCase))
            {
                await _db.SaveChangesAsync();
                return Ok(new { conversationId = conv.Id, isLive = true, answer = (string?)null });
            }

            // Open 模式：以 FAQ + 規則回覆
            string answer = await BestFaqAnswer(_db, q) ?? "客服稍後回覆您，謝謝。";
            var m2 = new ChatMessage { ConversationId = conv.Id, Sender = "System", Type = "Text", Content = answer, CreatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now };
            _db.ChatMessages.Add(m2);
            conv.LastMessageAt = m2.CreatedAt;
            await _db.SaveChangesAsync();

            return Ok(new { conversationId = conv.Id, isLive = false, answer });
        }

        // GET /api/chat/messages?conversationId=xxx
        [HttpGet("messages")]
        public async Task<IActionResult> GetMessages([FromQuery] int conversationId)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized();

            var conv = await _db.ChatConversations.FirstOrDefaultAsync(c => c.Id == conversationId);
            if (conv == null || conv.MemberId != mid) return Forbid();

            var list = await _db.ChatMessages
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new
                {
                    id = m.Id,
                    sender = m.Sender,
                    type = m.Type,
                    content = m.Content,
                    productId = m.ProductId,
                    url = m.Url,
                    createdAt = m.CreatedAt
                })
                .ToListAsync();

            return Ok(list);
        }

        public class AskRequest { public string? Question { get; set; } }

        private static async Task<string?> BestFaqAnswer(AppDbContext db, string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            var q = input.Trim();
            // 規則優先：地址變更
            if (q.Contains("改地址") || q.Contains("地址變更") || q.Contains("換地址"))
            {
                return "已為您轉接人工客服，請稍候，我們將儘速協助您更改收件地址。";
            }
            // SQL 檢索
            var faqs = await db.Faqs.Where(f => f.IsActive).Select(f => new { f.Answer, f.Question, f.Keywords }).ToListAsync();
            int Score(string text, string? keywords)
            {
                int s = 0;
                if (!string.IsNullOrWhiteSpace(text) && q.Contains(text)) s += 2;
                if (!string.IsNullOrWhiteSpace(keywords))
                {
                    foreach (var k in keywords.Split(new[] { ',', '，', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        if (q.Contains(k, StringComparison.OrdinalIgnoreCase)) s += 3;
                }
                return s;
            }
            var best = faqs.Select(f => new { f.Answer, Score = Score(f.Question ?? string.Empty, f.Keywords) })
                            .OrderByDescending(x => x.Score).FirstOrDefault();
            if (best != null && best.Score > 0) return best.Answer;
            if (q.Contains("運費"))
                return "台灣本島單筆滿 NT$2,000 免運，未達收超商 NT$50 或宅配 NT$100；離島依物流報價。";
            if (q.Contains("貨到付款") || q.Contains("到貨付款"))
                return "可以，亦支援信用卡、超商代碼/條碼、ATM 轉帳等，實際可用方式以結帳頁為準。";
            return null;
        }
    }
}
