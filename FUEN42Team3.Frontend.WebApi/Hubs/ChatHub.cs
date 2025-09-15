using FUEN42Team3.Backend.Models.EfModels;
using Microsoft.AspNetCore.SignalR;
using FUEN42Team3.Frontend.WebApi.Models.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace FUEN42Team3.Frontend.WebApi.Hubs
{
    // 注意：為了讓管理端(Agent)也能透過 AgentKey 連線與送訊息，本 Hub 不使用全域 [Authorize]
    // 成員端方法仍會嚴格驗證 JWT 內的 MemberId；Agent 端方法改以簡單 AgentKey 驗證（Demo 專用）。
    public class ChatHub : Hub
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly ILogger<ChatHub>? _logger;
        private const string AdminGroup = "Admins";
        // 真人模式覆寫：僅用於關閉 FAQ，不作為權限或資料一致性的依據（Demo 用）
        private static readonly ConcurrentDictionary<string, bool> _liveOverrides = new(StringComparer.OrdinalIgnoreCase);
        public ChatHub(AppDbContext db, IConfiguration config, ILogger<ChatHub>? logger = null)
        {
            _db = db;
            _config = config;
            _logger = logger;
        }

        private int? GetMemberId() => int.TryParse(Context.User?.FindFirst("MemberId")?.Value, out var id) ? id : (int?)null;

        public async Task JoinRoom(string roomId)
        {
            // roomId must be M{MemberId}
            if (string.IsNullOrWhiteSpace(roomId) || !roomId.StartsWith("M"))
                throw new HubException("Invalid room id");

            var memberId = GetMemberId();
            if (memberId == null)
                throw new HubException("Not a member");

            if ($"M{memberId}" != roomId)
                throw new HubException("Not allowed");

            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        }

        public async Task JoinRoomAsAgent(string roomId, string agentKey)
        {
            if (string.IsNullOrWhiteSpace(roomId) || !roomId.StartsWith("M"))
                throw new HubException("Invalid room id");
            EnsureAgent(agentKey);
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        }

        // 讓後台頁面加入 Admin 群組以接收系統廣播（未讀提示、最新訊息）
        public async Task JoinAgents(string agentKey)
        {
            EnsureAgent(agentKey);
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);
        }

        // 管理端系統廣播（例如 Take/Release/Close 後通知其他坐席更新清單）
        public async Task AdminBroadcast(object payload, string agentKey)
        {
            EnsureAgent(agentKey);
            await Clients.Group(AdminGroup).SendAsync("adminConvHint", payload);
        }

        public async Task SendText(string roomId, string content)
        {
            var memberId = GetMemberId();
            if (memberId == null) throw new HubException("Not a member");
            if ($"M{memberId}" != roomId) throw new HubException("Not allowed");

            var conv = await EnsureConversation(memberId.Value);
            var msg = new ChatMessage
            {
                ConversationId = conv.Id,
                Sender = "Member",
                Type = "Text",
                Content = content,
                CreatedAt = TaipeiTime.Now
            };
            _db.ChatMessages.Add(msg);
            conv.LastMessageAt = msg.CreatedAt;
            await _db.SaveChangesAsync();

            var senderDisplay = await BuildMemberDisplay(memberId.Value);
            await Clients.Group(roomId).SendAsync("messageReceived", new
            {
                conversationId = conv.Id,
                sender = msg.Sender,
                type = msg.Type,
                content = msg.Content,
                createdAt = msg.CreatedAt,
                senderDisplay
            });

            // FAQ 規則 + SQL 檢索自動回覆（Live 模式不回 FAQ）
            // 若有記憶體覆寫，優先依覆寫判定是否為 Live（僅影響 FAQ 行為）
            var roomKey = roomId;
            var liveByOverride = _liveOverrides.TryGetValue(roomKey, out var ov) && ov;
            var isLive = liveByOverride || string.Equals(conv.Status, "Live", StringComparison.OrdinalIgnoreCase);
            if (!isLive)
            {
                var auto = await FindFaqAutoReply(content);
                if (auto.HasValue)
                {
                    var a = auto.Value;
                    var reply = new ChatMessage
                    {
                        ConversationId = conv.Id,
                        Sender = a.Sender ?? "System",
                        Type = "Text",
                        Content = a.Answer,
                        CreatedAt = TaipeiTime.Now
                    };
                    _db.ChatMessages.Add(reply);
                    conv.LastMessageAt = reply.CreatedAt;
                    await _db.SaveChangesAsync();

                    await Clients.Group(roomId).SendAsync("messageReceived", new
                    {
                        conversationId = conv.Id,
                        sender = reply.Sender,
                        type = reply.Type,
                        content = reply.Content,
                        createdAt = reply.CreatedAt,
                        senderDisplay = reply.Sender == "Member" ? senderDisplay : (reply.Sender == "Agent" ? "Agent" : "系統")
                    });

                    // 若為「改地址」等需人工情境，對管理端推播升級提示
                    if (content.Contains("改地址") || content.Contains("地址變更") || content.Contains("更改地址") || content.Contains("換地址"))
                    {
                        await Clients.Group(AdminGroup).SendAsync("adminConvHint", new
                        {
                            conversationId = conv.Id,
                            memberId = memberId.Value,
                            display = senderDisplay,
                            lastMessageAt = reply.CreatedAt,
                            from = "System",
                            kind = "Escalated"
                        });
                    }
                }
                else
                {
                    // 沒有命中 FAQ：以系統訊息詢問是否要轉接真人客服
                    var prompt = new ChatMessage
                    {
                        ConversationId = conv.Id,
                        Sender = "System",
                        Type = "Text",
                        Content = "目前找不到相關解答，要為您轉接真人客服嗎？請點選『改為真人客服』。",
                        CreatedAt = TaipeiTime.Now
                    };
                    _db.ChatMessages.Add(prompt);
                    conv.LastMessageAt = prompt.CreatedAt;
                    await _db.SaveChangesAsync();
                    await Clients.Group(roomId).SendAsync("messageReceived", new
                    {
                        conversationId = conv.Id,
                        sender = prompt.Sender,
                        type = prompt.Type,
                        content = prompt.Content,
                        createdAt = prompt.CreatedAt,
                        senderDisplay = "系統"
                    });

                    // 通知管理端：有可能需要轉真人
                    await Clients.Group(AdminGroup).SendAsync("adminConvHint", new
                    {
                        conversationId = conv.Id,
                        memberId = memberId.Value,
                        display = senderDisplay,
                        lastMessageAt = prompt.CreatedAt,
                        from = "System",
                        kind = "SuggestLive"
                    });
                }
            }

            // 提示所有管理端有新會員訊息（未加入該房間者也能收到）
            await Clients.Group(AdminGroup).SendAsync("adminConvHint", new
            {
                conversationId = conv.Id,
                memberId = memberId.Value,
                display = senderDisplay,
                lastMessageAt = msg.CreatedAt,
                from = "Member",
                kind = msg.Type
            });
        }

        public async Task SendProduct(string roomId, int productId, string? sourceUrl)
        {
            var memberId = GetMemberId();
            if (memberId == null) throw new HubException("Not a member");
            if ($"M{memberId}" != roomId) throw new HubException("Not allowed");

            var conv = await EnsureConversation(memberId.Value);
            var msg = new ChatMessage
            {
                ConversationId = conv.Id,
                Sender = "Member",
                Type = "Product",
                ProductId = productId,
                Url = sourceUrl,
                CreatedAt = TaipeiTime.Now
            };
            _db.ChatMessages.Add(msg);
            conv.LastMessageAt = msg.CreatedAt;
            await _db.SaveChangesAsync();

            var senderDisplay = await BuildMemberDisplay(memberId.Value);
            // 取商品名稱用於前台與後台顯示（不更動資料表，只在訊息推播時夾帶）
            var productName = await _db.Products
                .Where(p => p.Id == productId)
                .Select(p => p.ProductName)
                .FirstOrDefaultAsync();
            await Clients.Group(roomId).SendAsync("messageReceived", new
            {
                conversationId = conv.Id,
                sender = msg.Sender,
                type = msg.Type,
                productId = msg.ProductId,
                url = msg.Url,
                createdAt = msg.CreatedAt,
                senderDisplay,
                productName
            });

            await Clients.Group(AdminGroup).SendAsync("adminConvHint", new
            {
                conversationId = conv.Id,
                memberId = memberId.Value,
                display = senderDisplay,
                lastMessageAt = msg.CreatedAt,
                from = "Member",
                kind = msg.Type
            });
        }

        public async Task SendAgentText(string roomId, string content, string agentKey, string? agentName = null)
        {
            if (string.IsNullOrWhiteSpace(roomId) || !roomId.StartsWith("M"))
                throw new HubException("Invalid room id");
            EnsureAgent(agentKey);
            if (string.IsNullOrWhiteSpace(content)) throw new HubException("Empty content");

            // 支援坐席快速指令：/faq 關鍵詞 -> 以 FAQ 內容回覆
            if (content.TrimStart().StartsWith("/faq", StringComparison.OrdinalIgnoreCase))
            {
                var ask = content.Trim().Substring(4).Trim();
                var auto = await FindFaqAutoReply(string.IsNullOrWhiteSpace(ask) ? "" : ask);
                if (auto.HasValue)
                {
                    var a = auto.Value;
                    content = a.Answer;
                }
            }

            var memberId = ParseMemberIdFromRoom(roomId);
            var conv = await EnsureConversation(memberId);
            var msg = new ChatMessage
            {
                ConversationId = conv.Id,
                Sender = "Agent",
                Type = "Text",
                Content = content,
                CreatedAt = TaipeiTime.Now
            };
            _db.ChatMessages.Add(msg);
            conv.LastMessageAt = msg.CreatedAt;
            await _db.SaveChangesAsync();

            // 若未傳入名稱或會話尚未指派，嘗試由 AssignedAgentId 取得名稱
            string? dbAgentName = agentName;
            if (string.IsNullOrWhiteSpace(dbAgentName) && conv.AssignedAgentId != null)
            {
                dbAgentName = await _db.Users.Where(u => u.Id == conv.AssignedAgentId).Select(u => u.UserName).FirstOrDefaultAsync();
            }
            var senderDisplay = (!string.IsNullOrWhiteSpace(dbAgentName) && conv.AssignedAgentId != null)
                ? $"{dbAgentName}"
                : "Agent";
            await Clients.Group(roomId).SendAsync("messageReceived", new
            {
                conversationId = conv.Id,
                sender = msg.Sender,
                type = msg.Type,
                content = msg.Content,
                createdAt = msg.CreatedAt,
                senderDisplay
            });

            // 管理端也收到更新（可用於將該會話未讀歸零或刷新排序）
            await Clients.Group(AdminGroup).SendAsync("adminConvHint", new
            {
                conversationId = conv.Id,
                memberId = memberId,
                display = await BuildMemberDisplay(memberId),
                lastMessageAt = msg.CreatedAt,
                from = "Agent",
                kind = msg.Type
            });
        }

        private async Task<ChatConversation> EnsureConversation(int memberId)
        {
            var conv = await _db.ChatConversations.FirstOrDefaultAsync(c => c.MemberId == memberId && (c.Status == "Open" || c.Status == "Live"));
            if (conv != null) return conv;
            conv = new ChatConversation
            {
                MemberId = memberId,
                Status = "Open",
                LastMessageAt = TaipeiTime.Now,
                CreatedAt = TaipeiTime.Now
            };
            _db.ChatConversations.Add(conv);
            await _db.SaveChangesAsync();
            return conv;
        }

        private int ParseMemberIdFromRoom(string roomId)
        {
            if (!roomId.StartsWith("M")) throw new HubException("Invalid room id");
            if (!int.TryParse(roomId.Substring(1), out var memberId)) throw new HubException("Invalid member id");
            return memberId;
        }

        private void EnsureAgent(string agentKey)
        {
            var key = _config["Chat:AgentKey"];
            if (string.IsNullOrWhiteSpace(key) || key != agentKey)
                throw new HubException("Unauthorized agent");
        }

        private async Task<string> BuildMemberDisplay(int memberId)
        {
            var account = await _db.Members.Where(m => m.Id == memberId).Select(m => m.UserName).FirstOrDefaultAsync();
            var nickname = await _db.MemberProfiles.Where(p => p.MemberId == memberId)
                .OrderByDescending(p => p.UpdatedAt)
                .Select(p => p.RealName)
                .FirstOrDefaultAsync();
            var nk = string.IsNullOrWhiteSpace(nickname) ? account : nickname;
            return $"{account} / {nk} / #{memberId}";
        }

        // 會員端：查詢目前會話狀態（Open/Live/Closed）
        public async Task<string> GetStatus()
        {
            var memberId = GetMemberId();
            // Demo 要求：GetStatus 不強制授權，用於同步 UI 狀態；若拿得到 memberId，仍可讀取 DB。
            // 先看記憶體覆寫（以 M{memberId} 為 key）；若無覆寫再查 DB；都沒有則 Open。
            if (memberId != null)
            {
                var key = $"M{memberId}";
                if (_liveOverrides.TryGetValue(key, out var ov) && ov) return "Live";
                var conv = await _db.ChatConversations.FirstOrDefaultAsync(c => c.MemberId == memberId && (c.Status == "Open" || c.Status == "Live"));
                return conv?.Status ?? "Open";
            }
            // 無 memberId：以 Caller 當前連線無法推導房間，回傳 Open 作為預設（關閉 FAQ 視 setLive 覆寫為準）
            return "Open";
        }

        // 管理端：查詢指定房間目前狀態（考慮記憶體覆寫 + DB），用於後台重新整理時與前台同步
        public async Task<string> GetRoomStatus(string roomId, string agentKey)
        {
            EnsureAgent(agentKey);
            if (string.IsNullOrWhiteSpace(roomId) || !roomId.StartsWith("M")) return "Open";

            // 先看覆寫（會員前台按下「真人客服」時會即時寫入）
            if (_liveOverrides.TryGetValue(roomId, out var ov) && ov) return "Live";

            // 再讀 DB 狀態
            try
            {
                var mid = ParseMemberIdFromRoom(roomId);
                var conv = await _db.ChatConversations.FirstOrDefaultAsync(c => c.MemberId == mid && (c.Status == "Open" || c.Status == "Live"));
                return conv?.Status ?? "Open";
            }
            catch
            {
                return "Open";
            }
        }

        // 會員端：切換真人客服（enable=true 啟用真人，不再跳出 FAQ；false 退回 Open）
        public async Task SetLive(string roomId, bool enable)
        {
            // Demo 要求：真人模式 = 只關閉 FAQ；不做任何驗證/授權。
            // 1) 放寬驗證：若無法從 JWT 解析 memberId，不拋錯；直接依傳入 roomId 作為鍵值。
            int? memberId = GetMemberId();
            var expected = memberId != null ? $"M{memberId}" : null;
            if (string.IsNullOrWhiteSpace(roomId) || !roomId.StartsWith("M"))
            {
                roomId = expected ?? roomId ?? string.Empty;
            }
            var desired = enable ? "Live" : "Open";

            // 2) 先寫入記憶體覆寫，立即影響 FAQ 行為；即使 DB 失敗也不會影響使用體驗。
            if (!string.IsNullOrWhiteSpace(roomId))
                _liveOverrides[roomId] = enable;

            // 3) 最佳努力更新 DB（若可解析 memberId，包含由 roomId 解析），但任何 DB 例外都不拋出至前端。
            ChatConversation? conv = null;
            int? dbMid = memberId;
            if (dbMid == null && !string.IsNullOrWhiteSpace(roomId) && roomId.StartsWith("M"))
            {
                try { dbMid = ParseMemberIdFromRoom(roomId); } catch { dbMid = null; }
            }
            if (dbMid != null)
            {
                try
                {
                    conv = await EnsureConversation(dbMid.Value);
                    if (!string.Equals(conv.Status, desired, StringComparison.OrdinalIgnoreCase))
                    {
                        conv.Status = desired;
                        await _db.SaveChangesAsync();
                    }
                }
                catch (Exception dbEx) when (
                    dbEx is DbUpdateException ||
                    dbEx.GetType().Name.Contains("SqlException", StringComparison.OrdinalIgnoreCase) ||
                    (dbEx.InnerException != null && dbEx.InnerException.GetType().Name.Contains("SqlException", StringComparison.OrdinalIgnoreCase))
                )
                {
                    _logger?.LogError(dbEx, "ChatHub.SetLive DB failure (ignored), memberId={MemberId}, roomId={Room}, enable={Enable}", dbMid, roomId, enable);
                    // Ignore DB errors for demo; rely on in-memory override
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "ChatHub.SetLive unexpected during DB ops (ignored), memberId={MemberId}, roomId={Room}, enable={Enable}", dbMid, roomId, enable);
                }
            }

            // 4) 嘗試將呼叫端加入群組並廣播 liveStatusChanged；任何錯誤都不拋出。
            try { await Groups.AddToGroupAsync(Context.ConnectionId, roomId); } catch (Exception ex) { _logger?.LogWarning(ex, "SetLive AddToGroup warn, roomId={Room}", roomId); }
            var statusPayload = new { status = desired };
            try { await Clients.Group(roomId).SendAsync("liveStatusChanged", statusPayload); }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "SetLive broadcast group failed, fallback to caller. roomId={Room}", roomId);
                try { await Clients.Caller.SendAsync("liveStatusChanged", statusPayload); } catch { /* ignore */ }
            }

            // 5) 廣播給管理端（Admins 群組）：讓後台清單即時同步『Live/Open』狀態
            try
            {
                int? mid = memberId;
                if (mid == null && !string.IsNullOrWhiteSpace(roomId) && roomId.StartsWith("M"))
                {
                    try { mid = ParseMemberIdFromRoom(roomId); } catch { /* ignore */ }
                }
                int? convId = null;
                if (mid != null)
                {
                    try
                    {
                        var c = await EnsureConversation(mid.Value);
                        convId = c?.Id;
                    }
                    catch { /* ignore */ }
                }
                var kind = enable ? "Live" : "LiveEnded";
                await Clients.Group(AdminGroup).SendAsync("adminConvHint", new
                {
                    conversationId = convId ?? 0,
                    memberId = mid ?? 0,
                    display = (mid != null) ? await BuildMemberDisplay(mid.Value) : null,
                    lastMessageAt = TaipeiTime.Now,
                    from = "System",
                    kind
                });
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "SetLive admin broadcast failed, roomId={Room}", roomId);
            }
            // 不拋出任何例外到前端。
            return;
        }

        // 後台坐席：切換真人客服
        public async Task SetLiveAsAgent(string roomId, bool enable, string agentKey)
        {
            // Demo：不驗證 agentKey，roomId 若異常則靜默忽略，不丟例外。
            if (string.IsNullOrWhiteSpace(roomId) || !roomId.StartsWith("M"))
            {
                _logger?.LogWarning("SetLiveAsAgent called with invalid roomId: {Room}", roomId);
                return;
            }
            // 寫入覆寫（優先影響 FAQ）
            _liveOverrides[roomId] = enable;
            // 最佳努力更新 DB，但失敗不影響前端
            int? memberId = null;
            int? conversationId = null;
            try
            {
                memberId = ParseMemberIdFromRoom(roomId);
                var conv = await EnsureConversation(memberId.Value);
                var desired = enable ? "Live" : "Open";
                if (!string.Equals(conv.Status, desired, StringComparison.OrdinalIgnoreCase))
                {
                    conv.Status = desired;
                    await _db.SaveChangesAsync();
                }
                conversationId = conv.Id;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "SetLiveAsAgent DB update failed (ignored), roomId={Room}, enable={Enable}", roomId, enable);
            }
            // 廣播狀態（使用期望值）
            var payload = new { status = enable ? "Live" : "Open" };
            try { await Clients.Group(roomId).SendAsync("liveStatusChanged", payload); } catch { /* ignore */ }

            // 同步通知所有管理端清單更新（避免需要開啟會話才收到 liveStatusChanged）
            try
            {
                await Clients.Group(AdminGroup).SendAsync("adminConvHint", new
                {
                    conversationId = conversationId ?? 0,
                    memberId = memberId ?? 0,
                    display = (memberId != null) ? await BuildMemberDisplay(memberId.Value) : null,
                    lastMessageAt = TaipeiTime.Now,
                    from = "System",
                    kind = enable ? "Live" : "LiveEnded"
                });
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "SetLiveAsAgent admin broadcast failed, roomId={Room}", roomId);
            }
        }

        // 以關鍵詞規則 + SQL Keywords 欄位，回傳最佳 FAQ；亦處理「改地址」情境轉人工
        private async Task<(string Answer, string? Sender)?> FindFaqAutoReply(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            var q = input.Trim();
            var low = q.ToLowerInvariant();

            // 規則命中（高優先）
            // 1) 改地址 -> 轉人工
            string[] addrTokens = new[] { "改地址", "地址變更", "變更地址", "更改地址", "換地址" };
            if (addrTokens.Any(t => q.Contains(t, StringComparison.OrdinalIgnoreCase)))
            {
                // 系統先回覆，再由後台接案處理
                var tip = "已為您轉接人工客服，請稍候，我們將儘速協助您更改收件地址。";
                return (tip, "System");
            }

            // 2) /faq 指令：去除前綴直接搜尋（讓會員也能試玩）
            if (low.StartsWith("/faq"))
            {
                q = q.Substring(4).Trim();
                if (string.IsNullOrEmpty(q)) return ("使用方式：/faq 關鍵詞，例如 /faq 運費、/faq 付款", "System");
            }

            // SQL 檢索：比對 Keywords or Question 以 LIKE，計算命中分數
            var faqs = await _db.Faqs.Where(f => f.IsActive).Select(f => new { f.Id, f.Question, f.Answer, f.Keywords }).ToListAsync();
            if (faqs.Count == 0) return null;

            int Score(string text, string? keywords)
            {
                int s = 0;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    if (q.Contains(text, StringComparison.OrdinalIgnoreCase)) s += 2;
                }
                if (!string.IsNullOrWhiteSpace(keywords))
                {
                    var ks = keywords.Split(new[] { ',', '，', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var k in ks)
                    {
                        if (k.Length == 0) continue;
                        if (q.Contains(k, StringComparison.OrdinalIgnoreCase)) s += 3; // 關鍵字權重高一點
                    }
                }
                return s;
            }

            var best = faqs
                .Select(f => new { f.Answer, Score = Score(f.Question ?? string.Empty, f.Keywords) })
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            if (best != null && best.Score > 0)
            {
                return (best.Answer, "System");
            }

            // 後備規則（確保 demo 問句一定命中）
            if (q.Contains("運費"))
                return ("台灣本島單筆滿 NT$2,000 免運，未達收超商 NT$50 或宅配 NT$100；離島依物流報價。", "System");
            if (q.Contains("貨到付款") || q.Contains("到貨付款"))
                return ("可以，亦支援信用卡、超商代碼/條碼、ATM 轉帳等，實際可用方式以結帳頁為準。", "System");

            return null;
        }
    }
}
