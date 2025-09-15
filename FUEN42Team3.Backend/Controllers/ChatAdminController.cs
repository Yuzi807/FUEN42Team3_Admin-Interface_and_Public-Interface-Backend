using FUEN42Team3.Backend.Models.EfModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Backend.Controllers
{
    [Authorize]
    [Route("admin/chat")]
    public class ChatAdminController : Controller
    {
        private readonly AppDbContext _db;
        public ChatAdminController(AppDbContext db) { _db = db; }

        // UI page for agent chat console
        [HttpGet("console")]
        public IActionResult Console()
        {
            return View();
        }

        [HttpGet("conversations")]
        public async Task<IActionResult> List(string status = "Open", int page = 1, int pageSize = 20)
        {
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

            // 狀態篩選：未處理(未完) = Open 或 Live；已處理 = Closed
            var baseQuery = _db.ChatConversations.AsQueryable();
            if (string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase))
            {
                baseQuery = baseQuery.Where(c => c.Status == "Closed");
            }
            else
            {
                baseQuery = baseQuery.Where(c => c.Status == "Open" || c.Status == "Live");
            }

            // 目標：一會員一視窗 -> 依 MemberId 分組，取每位會員「最新的一筆」
            // 優先 Live，其次 LastMessageAt 新者優先
            // 先取得每位會員的最新會話 Id 清單，再據此查詢詳細資料與分頁。
            var latestIdsQuery = baseQuery
                .GroupBy(c => c.MemberId)
                .Select(g => g
                    .OrderByDescending(c => c.Status == "Live")
                    .ThenByDescending(c => c.LastMessageAt)
                    .Select(c => c.Id)
                    .FirstOrDefault());

            var latestIds = await latestIdsQuery.ToListAsync();
            var total = latestIds.Count;

            // 取出最新會話的詳細資料，並依最後訊息時間排序、分頁
            var rows = await _db.ChatConversations
                .Where(c => latestIds.Contains(c.Id))
                .OrderByDescending(c => c.LastMessageAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    c.Id,
                    c.MemberId,
                    c.Status,
                    c.AssignedAgentId,
                    c.TakenAt,
                    c.LastMessageAt,
                    // 取最後一則 Agent 訊息時間（若無，使用 TakenAt；仍無則 DateTime.MinValue）
                    LastAgentAt = _db.ChatMessages
                        .Where(m => m.ConversationId == c.Id && m.Sender == "Agent")
                        .Max(m => (DateTime?)m.CreatedAt),
                    // 最後一句摘要（簡化為最後一則訊息內容）
                    LastSnippet = _db.ChatMessages
                        .Where(m => m.ConversationId == c.Id)
                        .OrderByDescending(m => m.CreatedAt)
                        .Select(m => m.Content)
                        .FirstOrDefault(),
                    MemberAccount = c.Member.UserName,
                    MemberNickname = _db.MemberProfiles
                        .Where(p => p.MemberId == c.MemberId)
                        .OrderByDescending(p => p.UpdatedAt)
                        .Select(p => p.RealName)
                        .FirstOrDefault()
                })
                .AsNoTracking()
                .ToListAsync();

            var listWithUnread = rows.Select(x => new
            {
                x.Id,
                x.MemberId,
                x.Status,
                x.AssignedAgentId,
                x.TakenAt,
                x.LastMessageAt,
                MemberAccount = x.MemberAccount,
                MemberNickname = x.MemberNickname,
                LastSnippet = x.LastSnippet,
                UnreadCount = _db.ChatMessages.Count(m => m.ConversationId == x.Id
                    && m.Sender == "Member"
                    && m.CreatedAt > (x.LastAgentAt ?? x.TakenAt ?? DateTime.MinValue))
            }).ToList();
            return Ok(new { total, page, pageSize, data = listWithUnread });
        }

        [HttpPost("{id:int}/take")]
        public async Task<IActionResult> Take(int id)
        {
            var conv = await _db.ChatConversations.FirstOrDefaultAsync(c => c.Id == id);
            if (conv == null) return NotFound();
            if (conv.Status != "Open") return BadRequest("Conversation not open");
            // 盡力取得目前後台使用者的 Id
            int uid = 0;
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                             ?? User.FindFirst("UserId")?.Value;
            if (!int.TryParse(userIdStr, out uid))
            {
                // 後備：以使用者名稱查詢
                var uname = User.Identity?.Name;
                if (!string.IsNullOrWhiteSpace(uname))
                {
                    uid = await _db.Users.Where(u => u.UserName == uname).Select(u => u.Id).FirstOrDefaultAsync();
                }
            }

            conv.AssignedAgentId = uid;
            conv.TakenAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { conv.Id, conv.AssignedAgentId, conv.TakenAt });
        }

        [HttpPost("{id:int}/release")]
        public async Task<IActionResult> Release(int id)
        {
            var conv = await _db.ChatConversations.FirstOrDefaultAsync(c => c.Id == id);
            if (conv == null) return NotFound();
            conv.AssignedAgentId = null;
            conv.TakenAt = null;
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("{id:int}/close")]
        public async Task<IActionResult> Close(int id)
        {
            var conv = await _db.ChatConversations.FirstOrDefaultAsync(c => c.Id == id);
            if (conv == null) return NotFound();
            conv.Status = "Closed";
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("{id:int}/reopen")]
        public async Task<IActionResult> Reopen(int id)
        {
            var conv = await _db.ChatConversations.FirstOrDefaultAsync(c => c.Id == id);
            if (conv == null) return NotFound();
            conv.Status = "Open";
            await _db.SaveChangesAsync();
            return Ok();
        }

        // List messages in a conversation (admin)
        [HttpGet("{id:int}/messages")]
        public async Task<IActionResult> Messages(int id)
        {
            var conv = await _db.ChatConversations
                .Include(c => c.Member)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (conv == null) return NotFound();

            var agentName = conv.AssignedAgentId == null ? null : await _db.Users
                .Where(u => u.Id == conv.AssignedAgentId)
                .Select(u => u.UserName)
                .FirstOrDefaultAsync();

            var memberAccount = conv.Member?.UserName;
            var memberNickname = await _db.MemberProfiles
                .Where(p => p.MemberId == conv.MemberId)
                .OrderByDescending(p => p.UpdatedAt)
                .Select(p => p.RealName)
                .FirstOrDefaultAsync();

            var memberDisplay = $"{memberAccount} / {(!string.IsNullOrWhiteSpace(memberNickname) ? memberNickname : memberAccount)} / #{conv.MemberId}";

            // 先取出訊息資料，再於記憶體中組 SenderDisplay，避免在 SQL 端發生 ANSI 字串降轉造成中文變成 ??? 的問題
            var raw = await _db.ChatMessages
                .Where(m => m.ConversationId == id)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new
                {
                    m.Id,
                    m.Sender,
                    m.Type,
                    m.Content,
                    m.ProductId,
                    m.Url,
                    m.CreatedAt,
                    ProductName = m.ProductId == null ? null : _db.Products.Where(p => p.Id == m.ProductId).Select(p => p.ProductName).FirstOrDefault()
                })
                .AsNoTracking()
                .ToListAsync();

            var list = raw.Select(m => new
            {
                m.Id,
                m.Sender,
                m.Type,
                m.Content,
                m.ProductId,
                m.Url,
                m.CreatedAt,
                m.ProductName,
                SenderDisplay = m.Sender == "Agent"
                    ? (!string.IsNullOrWhiteSpace(agentName) ? agentName : "Agent")
                    : (m.Sender == "Member" ? memberDisplay : m.Sender)
            }).ToList();

            return Ok(list);
        }

        public class SendDto { public string? Text { get; set; } }

        // Send an agent message 透過 Hub 寄送
        [HttpPost("{id:int}/send")]
        public async Task<IActionResult> Send(int id, [FromBody] SendDto dto)
        {
            var conv = await _db.ChatConversations.FirstOrDefaultAsync(c => c.Id == id);
            if (conv == null) return NotFound();
            if (string.IsNullOrWhiteSpace(dto?.Text)) return BadRequest();
            // 僅確認會話存在即可，由前端頁面透過 Hub 實際推送
            return Accepted();
        }
    }
}
