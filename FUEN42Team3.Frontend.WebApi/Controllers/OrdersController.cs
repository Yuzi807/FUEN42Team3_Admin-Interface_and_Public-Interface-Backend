using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Frontend.WebApi.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using FUEN42Team3.Frontend.WebApi.Models.Services;

namespace FUEN42Team3.Frontend.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private int? GetCurrentMemberId()
        {
            var val = User.FindFirst("MemberId")?.Value;
            if (int.TryParse(val, out var id)) return id;
            return null;
        }
        private readonly AppDbContext _db;
        private readonly ILogger<OrdersController> _logger;
        private readonly IEmailQueue _emailQueue;
        public OrdersController(AppDbContext db, ILogger<OrdersController> logger, IEmailQueue emailQueue)
        {
            _db = db;
            _logger = logger;
            _emailQueue = emailQueue;
        }

        // 取得系統使用者（後台 User）ID 以符合 OrderGift.CreatedBy 的外鍵需求
        private async Task<int> GetSystemUserIdAsync()
        {
            try
            {
                var id = await _db.Users.Where(u => u.IsActive).OrderBy(u => u.Id).Select(u => u.Id).FirstOrDefaultAsync();
                if (id > 0) return id;
            }
            catch { }
            return 1; // 後備：假設資料庫有 Id=1 的管理者
        }

        /// <summary>
        /// 依目前時間與訂單條件，從資料庫規則判斷應贈送的贈品清單。
        /// 回傳項目包含 GiftId、Quantity 與來源 GiftRuleId（如有）。
        /// </summary>
        private async Task<List<(int GiftId, int Quantity, int? GiftRuleId)>> ComputeGiftsByRulesAsync(Order order)
        {
            var result = new List<(int, int, int?)>();
            try
            {
                var now = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;

                // 以訂單明細小計為基準（不含運費），與購物車頁一致
                var itemsAmount = order.OrderDetails?.Sum(d => d.Subtotal) ?? 0m;
                var itemsCount = order.OrderDetails?.Sum(d => d.Quantity) ?? 0;

                // 會員點數與生日月（若有）
                var totalPoints = await _db.MemberPointLogs
                    .Where(x => x.MemberId == order.MemberId)
                    .SumAsync(x => (int?)x.ChangeAmount) ?? 0;

                int? birthMonth = await _db.MemberProfiles
                    .Where(mp => mp.MemberId == order.MemberId)
                    .Select(mp => mp.Birthdate.HasValue ? (int?)mp.Birthdate.Value.Month : null)
                    .FirstOrDefaultAsync();

                // 撈取目前有效的贈品規則與對應贈品
                var activeRules = await _db.GiftRules
                    .Where(r => !r.IsDeleted
                        && (r.StartDate == null || r.StartDate <= now)
                        && (r.EndDate == null || r.EndDate >= now))
                    .Select(r => new
                    {
                        r.Id,
                        r.Name,
                        r.ConditionType,
                        r.ConditionValue,
                        Gifts = r.GiftRuleItems
                            .Where(x => !x.IsDeleted)
                            .Select(x => new { x.GiftId, x.Quantity })
                            .ToList()
                    })
                    .AsNoTracking()
                    .ToListAsync();

                bool IsQualified(dynamic p)
                {
                    string type = (p.ConditionType ?? string.Empty).ToString();
                    return type switch
                    {
                        "amount" => itemsAmount >= (decimal)(p.ConditionValue ?? 0m),
                        "quantity" => itemsCount >= Convert.ToInt32(p.ConditionValue ?? 0m),
                        "member_points" => totalPoints >= Convert.ToInt32(p.ConditionValue ?? 0m),
                        "birthday_month" => birthMonth.HasValue && birthMonth.Value == now.Month,
                        _ => false
                    };
                }

                foreach (var rule in activeRules)
                {
                    if (!IsQualified(rule)) continue;
                    foreach (var g in rule.Gifts)
                    {
                        if (g.GiftId <= 0 || g.Quantity <= 0) continue;
                        result.Add((g.GiftId, g.Quantity, rule.Id));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "計算贈品規則時發生例外（忽略並不中斷下單）");
            }

            return result;
        }

        // 注意：前台需求為「直接使用資料庫時間，不做任何時區轉換」。
        // 因此移除所有 ToTaipeiTime 轉換，API 將回傳資料庫欄位原樣的 DateTime。

        private static string NewOrderNumber()
        {
            return $"GT{DateTime.UtcNow:yyMMddHHmmss}{Random.Shared.Next(0, 9999):D4}";
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] OrderCreateDto dto)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized(new { message = "未登入" });
            // 最小可行：以現有必填欄位建立一張主檔與明細
            // 預設 PaymentMethod/DeliveryMethod 可先以 1 帶值（請依你的資料種子）；或從傳入推斷
            var paymentMethodId = 1;
            var deliveryMethodId = 1;
            // 配送方式：優先依 LogisticsSubType（品牌）判斷；否則退回 ShippingMethod（超商/宅配）
            var subType = (dto.LogisticsSubType ?? string.Empty).Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(subType))
            {
                deliveryMethodId = subType switch
                {
                    "UNIMARTC2C" => 2, // 7-11 取貨
                    "FAMIC2C" => 3,     // 全家 取貨
                    "HILIFEC2C" => 4,   // 萊爾富 取貨
                    _ => 1
                };
            }
            else if (string.Equals(dto.ShippingMethod, "convenience-store", StringComparison.OrdinalIgnoreCase))
            {
                deliveryMethodId = 2; // 預設以 7-11 取貨
            }

            var now = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;
            // 準備宅配資料（若為宅配）
            string recipientName = ($"{dto.RecipientFirstName} {dto.RecipientLastName}").Trim();
            string recipientPhone = dto.RecipientPhone ?? string.Empty;
            string? zip = dto.ShippingZipCode;
            string? fullAddress = dto.ShippingAddress;
            if (string.IsNullOrWhiteSpace(fullAddress))
            {
                var parts = new[] { dto.ShippingCity, dto.ShippingDistrict, dto.ShippingAddress }
                    .Where(s => !string.IsNullOrWhiteSpace(s));
                fullAddress = string.Join(string.Empty, parts);
            }
            if (deliveryMethodId == 1 && dto.AddressId.HasValue)
            {
                var a = await _db.MemberAddresses.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == dto.AddressId.Value && x.MemberId == mid.Value);
                if (a != null)
                {
                    if (string.IsNullOrWhiteSpace(recipientName)) recipientName = a.RecipientName ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(recipientPhone)) recipientPhone = a.RecipientPhone ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(zip)) zip = a.PostalCode;
                    if (string.IsNullOrWhiteSpace(fullAddress))
                    {
                        var parts2 = new[] { a.City, a.District, a.Street }.Where(s => !string.IsNullOrWhiteSpace(s));
                        fullAddress = string.Join(string.Empty, parts2);
                    }
                }
            }

            var order = new Order
            {
                MemberId = mid.Value,
                PaymentMethodId = paymentMethodId,
                DeliveryMethodId = deliveryMethodId,
                OrderNumber = NewOrderNumber(),
                TotalAmount = dto.Total,
                ShippingFee = dto.ShippingFee,
                DiscountAmount = dto.TotalDiscount,
                // 由前端已透過 /api/points/redeem 折抵成功後回傳的使用點數
                UsedPoints = Math.Max(0, dto.PointsUsed),
                PointsEarned = 0,
                StatusId = 1, // 1=新訂單，請依你資料庫定義
                OrderType = "NORMAL",
                OrderDate = now,
                RecipientName = recipientName,
                RecipientPhone = recipientPhone,
                ShippingAddress = fullAddress,
                ShippingZipCode = zip,
                Notes = dto.Notes,
                CreatedAt = now
            };

            // 若為超商取貨，優先把 ShippingAddress 存成「品牌 門市名 地址」（如果 DTO 有足夠資訊）
            if (order.DeliveryMethodId != 1)
            {
                // 嘗試從已存在的 OrderLogistic 或 DTO 中取得門市資訊
                string? brandSubtype = subType;
                string? storeName = null;
                string? storeAddr = null;
                try
                {
                    var ol = await _db.OrderLogistics.AsNoTracking().FirstOrDefaultAsync(x => x.OrderId == order.Id);
                    if (ol != null)
                    {
                        if (!string.IsNullOrWhiteSpace(ol.LogisticsSubType)) brandSubtype = ol.LogisticsSubType;
                        storeName = ol.PickupStoreName ?? storeName;
                        storeAddr = ol.PickupAddress ?? storeAddr;
                    }
                }
                catch { }

                // DTO 尚未定義 CvsPickup 類型；若前端把門市名/地址先塞到 ShippingAddress，維持向下相容；否則僅以已有欄位最佳化
                if (string.IsNullOrWhiteSpace(storeAddr) && !string.IsNullOrWhiteSpace(dto.ShippingAddress))
                {
                    storeAddr = dto.ShippingAddress;
                }

                // 組合品牌字串
                string brand = brandSubtype switch
                {
                    "UNIMARTC2C" => "7-11",
                    "FAMIC2C" => "全家",
                    "HILIFEC2C" => "萊爾富",
                    "OKMARTC2C" => "OK",
                    _ => "超商"
                };
                var composed = (!string.IsNullOrWhiteSpace(storeName) && !string.IsNullOrWhiteSpace(storeAddr))
                    ? ($"{brand} {storeName} {storeAddr}")
                    : (!string.IsNullOrWhiteSpace(storeAddr) ? ($"{brand} {storeAddr}") : order.ShippingAddress);
                if (!string.IsNullOrWhiteSpace(composed)) order.ShippingAddress = composed;
            }

            // 明細
            foreach (var i in dto.Items)
            {
                order.OrderDetails.Add(new OrderDetail
                {
                    ProductId = i.ProductId,
                    ProductName = i.ProductName ?? $"P{i.ProductId}",
                    UnitPrice = i.UnitPrice,
                    Quantity = i.Quantity,
                    Discount = 0,
                    Subtotal = i.UnitPrice * i.Quantity,
                    CreatedAt = now
                });
            }

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            // 贈品：合併「前端傳入」與「後端規則計算」後統一寫入 OrderGifts
            try
            {
                // 1) 前端傳入的贈品（容錯：id 可能是字串型別被忽略，嘗試用名稱比對）
                var fromClient = new List<(int GiftId, int Quantity, int? GiftRuleId)>();
                if (dto.Gifts != null && dto.Gifts.Count > 0)
                {
                    foreach (var g in dto.Gifts)
                    {
                        int giftId = 0;
                        if (g.Id.HasValue && g.Id.Value > 0)
                        {
                            giftId = g.Id.Value;
                        }
                        else if (!string.IsNullOrWhiteSpace(g.Name))
                        {
                            // 名稱比對採用資料庫預設 Collation（通常不區分大小寫），同時排除軟刪除
                            giftId = await _db.Gifts
                                .Where(x => !x.IsDeleted && x.Name == g.Name)
                                .Select(x => x.Id)
                                .FirstOrDefaultAsync();
                        }
                        if (giftId > 0)
                        {
                            var qty = g.Quantity > 0 ? g.Quantity : 1;
                            fromClient.Add((giftId, qty, null));
                        }
                    }
                }

                // 2) 依規則計算的贈品
                var fromRules = await ComputeGiftsByRulesAsync(order);

                // 3) 合併：同一 GiftId 的數量累加；優先保留規則來源的 GiftRuleId
                var merged = new Dictionary<int, (int qty, int? ruleId)>();
                foreach (var it in fromClient)
                {
                    if (!merged.ContainsKey(it.GiftId)) merged[it.GiftId] = (0, it.GiftRuleId);
                    var cur = merged[it.GiftId];
                    merged[it.GiftId] = (cur.qty + it.Quantity, cur.ruleId ?? it.GiftRuleId);
                }
                foreach (var it in fromRules)
                {
                    if (!merged.ContainsKey(it.GiftId)) merged[it.GiftId] = (0, it.GiftRuleId);
                    var cur = merged[it.GiftId];
                    merged[it.GiftId] = (cur.qty + it.Quantity, cur.ruleId ?? it.GiftRuleId);
                }

                if (merged.Count > 0)
                {
                    foreach (var kv in merged)
                    {
                        _db.OrderGifts.Add(new OrderGift
                        {
                            OrderId = order.Id,
                            GiftId = kv.Key,
                            // 需求：贈品數量強制為 1（無論規則或前端傳入）
                            Quantity = 1,
                            GiftRuleId = kv.Value.ruleId,
                            CreatedAt = now,
                            CreatedBy = await GetSystemUserIdAsync(),
                            IsDeleted = false
                        });
                    }
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "寫入贈品失敗，OrderNumber={OrderNumber}", order.OrderNumber);
            }

            // 下單成功後清空目前會員購物車（硬刪）
            try
            {
                await _db.ShoppingCartItems.Where(i => i.CartId == mid).ExecuteDeleteAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清空購物車失敗，MemberId={MemberId}", mid);
            }

            // 寄送訂單通知信改為背景投遞（不影響回應，失敗只記錄）
            try
            {
                // 取得收件人
                var toEmail = User.FindFirst("Email")?.Value;
                var toName = User.FindFirst("UserName")?.Value ?? (order.RecipientName ?? "會員");
                if (string.IsNullOrWhiteSpace(toEmail))
                {
                    // 後備：查 DB
                    var m = await _db.Members.AsNoTracking().FirstOrDefaultAsync(x => x.Id == mid);
                    toEmail = m?.Email;
                    toName = toName ?? m?.UserName ?? "會員";
                }
                if (!string.IsNullOrWhiteSpace(toEmail))
                {
                    var subject = $"[魔型仔 Ghost Toys] 訂單成立通知 - {order.OrderNumber}";
                    var html = FUEN42Team3.Frontend.WebApi.Models.Services.OrderNotificationEmailBuilder.BuildOrderCreatedEmail(toName ?? "會員", order);
                    _emailQueue.Enqueue(new EmailMessage(
                        SenderName: "魔型仔官方團隊",
                        SenderEmail: "Ghosttoy0905@gmail.com",
                        ToName: toName ?? "會員",
                        ToEmail: toEmail!,
                        Subject: subject,
                        Html: html
                    ));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "訂單通知信寄送失敗，OrderNumber={OrderNumber}", order.OrderNumber);
            }

            // 若為宅配且要將本次地址寫入地址簿
            if (deliveryMethodId == 1 && dto.SaveAddressToBook)
            {
                if (!string.IsNullOrWhiteSpace(order.RecipientName) &&
                    !string.IsNullOrWhiteSpace(order.RecipientPhone) &&
                    !string.IsNullOrWhiteSpace(order.ShippingAddress))
                {
                    var exists = await _db.MemberAddresses.AnyAsync(a => a.MemberId == mid.Value
                        && a.RecipientName == order.RecipientName
                        && a.RecipientPhone == order.RecipientPhone
                        && a.PostalCode == order.ShippingZipCode
                        && (a.City ?? string.Empty) + (a.District ?? string.Empty) + (a.Street ?? string.Empty) == order.ShippingAddress);
                    if (!exists)
                    {
                        var mAddr = new MemberAddress
                        {
                            MemberId = mid.Value,
                            RecipientName = order.RecipientName,
                            RecipientPhone = order.RecipientPhone,
                            PostalCode = order.ShippingZipCode,
                            City = dto.ShippingCity ?? string.Empty,
                            District = dto.ShippingDistrict ?? string.Empty,
                            Street = (!string.IsNullOrWhiteSpace(dto.ShippingAddress) ? dto.ShippingAddress : order.ShippingAddress) ?? string.Empty,
                            IsDefault = false,
                            UpdatedAt = DateTime.Now
                        };
                        var hasAny = await _db.MemberAddresses.AnyAsync(a => a.MemberId == mid.Value);
                        if (!hasAny) mAddr.IsDefault = true;
                        _db.MemberAddresses.Add(mAddr);
                        await _db.SaveChangesAsync();
                    }
                }
            }

            return Ok(new { orderId = order.Id, orderNumber = order.OrderNumber });
        }

        // 前端會員中心：取目前登入會員的訂單清單
        [HttpGet("my")]
        public async Task<IActionResult> ListMyOrders()
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized(new { success = false, message = "未登入" });

            var orders = await _db.Orders
                .AsNoTracking()
                .Include(o => o.Status)
                .Include(o => o.EcpayPayment)
                .Include(o => o.OrderDetails)
                    .ThenInclude(d => d.Product)
                        .ThenInclude(p => p.ProductImages)
                .Include(o => o.OrderDetails)
                    .ThenInclude(d => d.Product)
                        .ThenInclude(p => p.Category)
                .Where(o => o.MemberId == mid.Value && !o.IsDeleted)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            var result = orders.Select(MapOrderToListDto).ToList();
            return Ok(new { success = true, data = result });
        }

        // 管理情境或指定會員使用
        [HttpGet("user/{userId:int}")]
        public async Task<IActionResult> ListUserOrders(int userId)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized(new { success = false, message = "未登入" });
            if (mid.Value != userId)
            {
                // 若有管理者權限可放行，這裡簡化為禁止
                return Forbid();
            }

            var orders = await _db.Orders
                .AsNoTracking()
                .Include(o => o.Status)
                .Include(o => o.EcpayPayment)
                .Include(o => o.OrderDetails)
                    .ThenInclude(d => d.Product)
                        .ThenInclude(p => p.ProductImages)
                .Include(o => o.OrderDetails)
                    .ThenInclude(d => d.Product)
                        .ThenInclude(p => p.Category)
                .Where(o => o.MemberId == userId && !o.IsDeleted)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            var result = orders.Select(MapOrderToListDto).ToList();
            return Ok(new { success = true, data = result });
        }

        // 會員取消訂單等狀態更新
        [HttpPut("{orderNumber}/status")]
        public async Task<IActionResult> UpdateStatus(string orderNumber, [FromBody] UpdateOrderStatusDto dto)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized(new { success = false, message = "未登入" });

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);
            if (order == null) return NotFound(new { success = false, message = "找不到訂單" });
            if (order.MemberId != mid.Value) return Forbid();

            // 對應狀態名稱到 StatusId（支援英文/中文關鍵字）
            if (!string.IsNullOrWhiteSpace(dto.Status))
            {
                var want = dto.Status.Trim().ToLower();
                var statuses = await _db.OrderStatuses.AsNoTracking().ToListAsync();
                var status = statuses.FirstOrDefault(s => (s.StatusName ?? string.Empty).Trim().ToLower() == want);
                if (status == null)
                {
                    // 關鍵字對應
                    string PickByKeywords(IEnumerable<FUEN42Team3.Backend.Models.EfModels.OrderStatus> all, params string[] keywords)
                        => all.FirstOrDefault(s => keywords.Any(k => (s.StatusName ?? string.Empty).Contains(k, StringComparison.OrdinalIgnoreCase)))?.StatusName ?? string.Empty;

                    string matchedName = want switch
                    {
                        "pending" => PickByKeywords(statuses, "待", "新"),
                        "confirmed" => PickByKeywords(statuses, "確認"),
                        "processing" => PickByKeywords(statuses, "處理"),
                        "shipped" => PickByKeywords(statuses, "出貨", "配送"),
                        "delivered" => PickByKeywords(statuses, "送達", "完成", "結案"),
                        "cancelled" => PickByKeywords(statuses, "取消"),
                        "returned" => PickByKeywords(statuses, "退貨", "退"),
                        _ => string.Empty
                    };
                    if (!string.IsNullOrEmpty(matchedName))
                    {
                        status = statuses.FirstOrDefault(s => s.StatusName == matchedName);
                    }
                }
                if (status == null)
                {
                    return BadRequest(new { success = false, message = $"無效狀態: {dto.Status}" });
                }
                order.StatusId = status.Id;
                order.UpdatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;
                await _db.SaveChangesAsync();
            }

            return Ok(new { success = true });
        }

        private static string NormalizeStatusName(string? name)
        {
            var n = (name ?? string.Empty).Trim().ToLower();
            if (string.IsNullOrEmpty(n)) return "pending";
            // 嘗試將中文名稱對應為英文字串
            if (n.Contains("待") || n.Contains("新")) return "pending";
            if (n.Contains("確認")) return "confirmed";
            if (n.Contains("處理")) return "processing";
            if (n.Contains("出貨") || n.Contains("配送")) return "shipped";
            if (n.Contains("送達") || n.Contains("完成") || n.Contains("結案")) return "delivered";
            if (n.Contains("取消")) return "cancelled";
            if (n.Contains("退貨") || n.Contains("退")) return "returned";
            // 若本來就是英文，直接回傳標準小寫
            switch (n)
            {
                case "pending":
                case "confirmed":
                case "processing":
                case "shipped":
                case "delivered":
                case "cancelled":
                case "returned":
                    return n;
            }
            return n;
        }

        private OrderListDto MapOrderToListDto(Order o)
        {
            // 付款狀態推斷：有 EcpayPayment 且 PayStatus=Paid -> paid；取消 -> refunded；其他 pending
            var paymentStatus = "pending";
            // 若模型關聯未 Include，可再查詢一次（這裡簡化不再額外查表）
            if (o.EcpayPayment != null)
            {
                var ps = (o.EcpayPayment.PayStatus ?? "").ToLower();
                if (ps.Contains("paid") || ps.Contains("success")) paymentStatus = "paid";
                else if (ps.Contains("refund")) paymentStatus = "refunded";
                else if (ps.Contains("fail")) paymentStatus = "failed";
            }

            var items = o.OrderDetails.Select(d =>
            {
                var img = d.Product?.ProductImages?.FirstOrDefault(pi => pi.IsMainImage)?.ImagePath
                          ?? d.Product?.ProductImages?.FirstOrDefault()?.ImagePath;
                var category = d.Product?.Category?.CategoryName ?? string.Empty;
                return new OrderItemDto
                {
                    Id = d.Id,
                    ProductId = d.ProductId,
                    Name = d.ProductName,
                    Category = category,
                    Price = d.UnitPrice,
                    Quantity = d.Quantity,
                    Image = img
                };
            }).ToList();

            return new OrderListDto
            {
                OrderNumber = o.OrderNumber,
                Status = NormalizeStatusName(o.Status?.StatusName),
                PaymentStatus = paymentStatus,
                // 直接使用資料庫時間，不轉換
                CreatedAt = o.CreatedAt,
                Total = o.TotalAmount,
                TotalItems = items.Sum(i => i.Quantity),
                Items = items
            };
        }

        /// <summary>
        /// 以訂單編號查詢目前登入會員的單筆訂單，回傳詳情所需欄位（不與後端 OrdersAPI 衝突）。
        /// </summary>
        [HttpGet("by-number/{orderNumber}")]
        public async Task<IActionResult> GetByOrderNumber(string orderNumber)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized(new { success = false, message = "未登入" });

            var order = await _db.Orders
                .AsNoTracking()
                .Include(o => o.Status)
                .Include(o => o.EcpayPayment)
                .Include(o => o.OrderLogistic)
                .Include(o => o.OrderGifts)
                    .ThenInclude(og => og.Gift)
                .Include(o => o.OrderDetails)
                    .ThenInclude(d => d.Product)
                        .ThenInclude(p => p.ProductImages)
                .Include(o => o.OrderDetails)
                    .ThenInclude(d => d.Product)
                        .ThenInclude(p => p.Category)
                .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber && o.MemberId == mid.Value && !o.IsDeleted);

            if (order == null) return NotFound(new { success = false, message = "找不到訂單" });

            // 付款狀態
            var paymentStatus = "pending";
            if (order.EcpayPayment != null)
            {
                var ps = (order.EcpayPayment.PayStatus ?? "").ToLower();
                if (ps.Contains("paid") || ps.Contains("success")) paymentStatus = "paid";
                else if (ps.Contains("refund")) paymentStatus = "refunded";
                else if (ps.Contains("fail")) paymentStatus = "failed";
            }

            // 商品清單（附原價）：原價優先取產品 BasePrice，找不到則退回 UnitPrice
            var items = order.OrderDetails.Select(d => new
            {
                id = d.Id,
                productId = d.ProductId,
                name = d.ProductName,
                category = d.Product?.Category?.CategoryName ?? string.Empty,
                price = d.UnitPrice,
                originalPrice = (d.Product?.BasePrice ?? d.UnitPrice),
                quantity = d.Quantity,
                image = d.Product?.ProductImages?.FirstOrDefault(pi => pi.IsMainImage)?.ImagePath
                        ?? d.Product?.ProductImages?.FirstOrDefault()?.ImagePath
            }).ToList();

            // 贈品清單（若有）
            var gifts = (order.OrderGifts ?? new List<OrderGift>()).Select(g => new
            {
                id = g.Id,
                giftId = g.GiftId,
                name = g.Gift?.Name ?? $"贈品#{g.GiftId}",
                quantity = g.Quantity,
                image = g.Gift?.ImageUrl
            }).ToList();

            // 配送資訊（若為超商取貨，優先顯示門市地址名稱與電話）
            string address = order.ShippingAddress ?? string.Empty;
            string phone = order.RecipientPhone ?? string.Empty;
            string recipient = order.RecipientName ?? string.Empty;
            if (order.OrderLogistic != null && !string.IsNullOrWhiteSpace(order.OrderLogistic.PickupAddress))
            {
                // 組合「門市名（電話）」+ 地址
                var storeName = order.OrderLogistic.PickupStoreName ?? string.Empty;
                var tel = order.OrderLogistic.PickupTelephone ?? string.Empty;
                var addr = order.OrderLogistic.PickupAddress ?? string.Empty;
                address = string.IsNullOrWhiteSpace(storeName) ? addr : ($"{storeName} - {addr}");
                if (!string.IsNullOrWhiteSpace(tel)) phone = tel;
            }
            // 取配送方式名稱（DeliveryMethods.MethodName）
            string? deliveryName = null;
            try
            {
                deliveryName = await _db.DeliveryMethods.Where(dm => dm.Id == order.DeliveryMethodId)
            .Select(dm => dm.ShippingName)
                    .FirstOrDefaultAsync();
            }
            catch { }

            // 針對超商：若尚無 OrderLogistic，fallback 以 address 作為 storeAddress，避免前端空白
            var isCvs = order.DeliveryMethodId == 2 || order.DeliveryMethodId == 3 || order.DeliveryMethodId == 4;
            var storeNameOut = order.OrderLogistic?.PickupStoreName;
            var storeAddressOut = order.OrderLogistic?.PickupAddress;
            var storeTelOut = order.OrderLogistic?.PickupTelephone;
            if (isCvs && string.IsNullOrWhiteSpace(storeAddressOut))
            {
                storeAddressOut = address;
            }

            var shippingInfo = new
            {
                firstName = string.Empty,
                lastName = recipient,
                phone = phone,
                email = string.Empty,
                address = address,
                city = string.Empty,
                district = string.Empty,
                zipCode = order.ShippingZipCode ?? string.Empty,
                notes = order.Notes ?? string.Empty,
                // 額外回傳以滿足前端顯示需求
                deliveryMethodId = order.DeliveryMethodId,
                deliveryMethodName = deliveryName,
                storeName = storeNameOut,
                storeAddress = storeAddressOut,
                storeTelephone = storeTelOut,
                logisticsSubType = order.OrderLogistic?.LogisticsSubType
            };

            // 付款資訊：優先依 ECPay PaymentType 判斷
            string method = "credit-card";
            var ptype = (order.EcpayPayment?.PaymentType ?? string.Empty).Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(ptype))
            {
                if (ptype.Contains("ATM")) method = "atm";
                else if (ptype.Contains("CVS")) method = "cvs";
                else if (ptype.Contains("BARCODE")) method = "barcode";
                else if (ptype.Contains("CREDIT")) method = "credit-card";
            }
            else
            {
                // 後備：依 PaymentMethodId/配送方式推斷
                if (order.DeliveryMethodId == 2)
                {
                    // 便利商店取貨可能為取貨付款
                    method = "cod";
                }
                else
                {
                    method = order.PaymentMethodId == 1 ? "credit-card" : "bank-transfer";
                }
            }
            // 若需卡號末四碼，請於 EcpayPayment.ExtraInfo 解析；此處先不提供避免模型缺欄位
            var last4 = string.Empty;
            var paymentInfo = new
            {
                method = method,
                creditCard = new { lastFourDigits = last4 }
            };

            // 價格資訊
            decimal originalSubtotal = order.OrderDetails.Sum(d => (d.Product?.BasePrice ?? d.UnitPrice) * d.Quantity);
            decimal itemDiscount = order.OrderDetails.Sum(d =>
            {
                var op = d.Product?.BasePrice ?? d.UnitPrice;
                var diff = op - d.UnitPrice;
                return diff > 0 ? diff * d.Quantity : 0m;
            });
            var pricing = new
            {
                subtotal = originalSubtotal, // 顯示原價小計
                itemDiscount = itemDiscount,
                couponDiscount = 0m,
                pointsDiscount = (decimal)order.UsedPoints,
                totalDiscount = order.DiscountAmount ?? (itemDiscount + order.UsedPoints),
                shipping = order.ShippingFee,
                tax = 0m,
                total = order.TotalAmount
            };

            // 簡易時間軸（台北時間）
            var timeline = new List<object>
            {
                // 直接使用資料庫的 OrderDate
                new { title = "訂單已成立", date = order.OrderDate, completed = true, description = string.Empty },
            };
            var paidAtTw = order.PaymentDate.HasValue ? order.PaymentDate.Value : (DateTime?)null;
            if (paidAtTw.HasValue)
            {
                timeline.Add(new { title = "付款完成", date = paidAtTw.Value, completed = true, description = string.Empty });
            }

            var data = new
            {
                orderNumber = order.OrderNumber,
                status = NormalizeStatusName(order.Status?.StatusName),
                paymentStatus = paymentStatus,
                // 直接回傳資料庫時間
                createdAt = order.CreatedAt,
                paymentDateTaipei = paidAtTw,
                items,
                gifts,
                shippingInfo,
                paymentInfo,
                pricing,
                timeline
            };

            return Ok(new { success = true, data });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized(new { message = "未登入" });
            var order = await _db.Orders.Include(o => o.OrderDetails).FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();
            if (order.MemberId != mid.Value) return Forbid();
            return Ok(new
            {
                order.Id,
                order.OrderNumber,
                order.TotalAmount,
                order.ShippingFee,
                order.DiscountAmount,
                order.RecipientName,
                order.RecipientPhone,
                order.ShippingAddress,
                Items = order.OrderDetails.Select(d => new { d.ProductId, d.ProductName, d.UnitPrice, d.Quantity, d.Subtotal })
            });
        }

        /// <summary>
        /// 更新訂單關鍵資訊（收件人、地址、金額）。用於先建立草稿訂單後在結帳前更新。
        /// </summary>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] OrderCreateDto dto)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized(new { message = "未登入" });
            var order = await _db.Orders.Include(o => o.OrderDetails).Include(o => o.OrderGifts).FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();
            if (order.MemberId != mid.Value) return Forbid();

            var now = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;

            // 依配送方式（字串）調整 DeliveryMethodId（請依實際代碼對應）
            if (!string.IsNullOrWhiteSpace(dto.LogisticsSubType))
            {
                var sub2 = (dto.LogisticsSubType ?? string.Empty).Trim().ToUpperInvariant();
                order.DeliveryMethodId = sub2 switch
                {
                    "UNIMARTC2C" => 2,
                    "FAMIC2C" => 3,
                    "HILIFEC2C" => 4,
                    _ => 1
                };
            }
            else if (!string.IsNullOrWhiteSpace(dto.ShippingMethod))
            {
                if (string.Equals(dto.ShippingMethod, "convenience-store", StringComparison.OrdinalIgnoreCase))
                    order.DeliveryMethodId = 2; // 預設 7-11
                else
                    order.DeliveryMethodId = 1; // 宅配
            }

            // 收件/地址（支援地址簿或手動覆蓋）
            var upName = ($"{dto.RecipientFirstName} {dto.RecipientLastName}").Trim();
            if (!string.IsNullOrWhiteSpace(upName)) order.RecipientName = upName;
            if (!string.IsNullOrWhiteSpace(dto.RecipientPhone)) order.RecipientPhone = dto.RecipientPhone;

            var upFull = dto.ShippingAddress;
            if (string.IsNullOrWhiteSpace(upFull))
            {
                var parts = new[] { dto.ShippingCity, dto.ShippingDistrict, dto.ShippingAddress }
                    .Where(s => !string.IsNullOrWhiteSpace(s));
                upFull = string.Join(string.Empty, parts);
            }
            if (!string.IsNullOrWhiteSpace(upFull)) order.ShippingAddress = upFull;
            if (!string.IsNullOrWhiteSpace(dto.ShippingZipCode)) order.ShippingZipCode = dto.ShippingZipCode;

            if (order.DeliveryMethodId == 1 && dto.AddressId.HasValue && string.IsNullOrWhiteSpace(dto.ShippingAddress)
                && string.IsNullOrWhiteSpace(dto.ShippingCity) && string.IsNullOrWhiteSpace(dto.ShippingDistrict))
            {
                var a2 = await _db.MemberAddresses.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == dto.AddressId.Value && x.MemberId == mid.Value);
                if (a2 != null)
                {
                    if (string.IsNullOrWhiteSpace(upName)) order.RecipientName = a2.RecipientName ?? order.RecipientName;
                    if (string.IsNullOrWhiteSpace(dto.RecipientPhone)) order.RecipientPhone = a2.RecipientPhone ?? order.RecipientPhone;
                    if (string.IsNullOrWhiteSpace(dto.ShippingZipCode)) order.ShippingZipCode = a2.PostalCode ?? order.ShippingZipCode;
                    var parts2 = new[] { a2.City, a2.District, a2.Street }.Where(s => !string.IsNullOrWhiteSpace(s));
                    var addr2 = string.Join(string.Empty, parts2);
                    if (!string.IsNullOrWhiteSpace(addr2)) order.ShippingAddress = addr2;
                }
            }
            order.Notes = dto.Notes ?? order.Notes;

            // 金額
            order.TotalAmount = dto.Total;
            order.ShippingFee = dto.ShippingFee;
            order.DiscountAmount = dto.TotalDiscount;
            // 同步更新使用點數
            order.UsedPoints = Math.Max(0, dto.PointsUsed);

            order.UpdatedAt = now;
            await _db.SaveChangesAsync();

            // 若更新後配送方式為超商，將 ShippingAddress 標準化為「品牌 門市名 地址」（如有門市資訊）
            if (order.DeliveryMethodId != 1)
            {
                string? brandSubtype = dto.LogisticsSubType;
                string? storeName = null;
                string? storeAddr = null;
                try
                {
                    var ol = await _db.OrderLogistics.AsNoTracking().FirstOrDefaultAsync(x => x.OrderId == order.Id);
                    if (ol != null)
                    {
                        if (!string.IsNullOrWhiteSpace(ol.LogisticsSubType)) brandSubtype = ol.LogisticsSubType;
                        storeName = ol.PickupStoreName ?? storeName;
                        storeAddr = ol.PickupAddress ?? storeAddr;
                    }
                }
                catch { }

                if (string.IsNullOrWhiteSpace(storeAddr) && !string.IsNullOrWhiteSpace(order.ShippingAddress))
                {
                    storeAddr = order.ShippingAddress; // 向下相容：若前端已寫入地址
                }

                string brand = (brandSubtype ?? string.Empty).Trim().ToUpperInvariant() switch
                {
                    "UNIMARTC2C" => "7-11",
                    "FAMIC2C" => "全家",
                    "HILIFEC2C" => "萊爾富",
                    "OKMARTC2C" => "OK",
                    _ => "超商"
                };
                var composed = (!string.IsNullOrWhiteSpace(storeName) && !string.IsNullOrWhiteSpace(storeAddr))
                    ? ($"{brand} {storeName} {storeAddr}")
                    : (!string.IsNullOrWhiteSpace(storeAddr) ? ($"{brand} {storeAddr}") : order.ShippingAddress);
                if (!string.IsNullOrWhiteSpace(composed))
                {
                    order.ShippingAddress = composed;
                    order.UpdatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;
                    await _db.SaveChangesAsync();
                }
            }

            // 同步更新贈品（覆蓋重建：合併前端與規則判斷）
            try
            {
                if (order.OrderGifts != null && order.OrderGifts.Count > 0)
                {
                    _db.OrderGifts.RemoveRange(order.OrderGifts);
                    await _db.SaveChangesAsync();
                }

                var merged = new Dictionary<int, (int qty, int? ruleId)>();

                // 1) 前端傳入
                if (dto.Gifts != null && dto.Gifts.Count > 0)
                {
                    foreach (var g in dto.Gifts)
                    {
                        int giftId = 0;
                        if (g.Id.HasValue && g.Id.Value > 0) giftId = g.Id.Value;
                        else if (!string.IsNullOrWhiteSpace(g.Name))
                        {
                            giftId = await _db.Gifts
                                .Where(x => !x.IsDeleted && x.Name == g.Name)
                                .Select(x => x.Id)
                                .FirstOrDefaultAsync();
                        }
                        if (giftId > 0)
                        {
                            var qty = g.Quantity > 0 ? g.Quantity : 1;
                            if (!merged.ContainsKey(giftId)) merged[giftId] = (0, null);
                            var cur = merged[giftId];
                            merged[giftId] = (cur.qty + qty, cur.ruleId);
                        }
                    }
                }

                // 2) 規則判斷
                var fromRules = await ComputeGiftsByRulesAsync(order);
                foreach (var it in fromRules)
                {
                    if (!merged.ContainsKey(it.GiftId)) merged[it.GiftId] = (0, it.GiftRuleId);
                    var cur = merged[it.GiftId];
                    merged[it.GiftId] = (cur.qty + it.Quantity, cur.ruleId ?? it.GiftRuleId);
                }

                if (merged.Count > 0)
                {
                    foreach (var kv in merged)
                    {
                        _db.OrderGifts.Add(new OrderGift
                        {
                            OrderId = order.Id,
                            GiftId = kv.Key,
                            // 需求：贈品數量強制為 1
                            Quantity = 1,
                            GiftRuleId = kv.Value.ruleId,
                            CreatedAt = now,
                            CreatedBy = await GetSystemUserIdAsync(),
                            IsDeleted = false
                        });
                    }
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "更新贈品失敗，OrderId={OrderId}", order.Id);
            }

            return Ok(new { orderId = order.Id, orderNumber = order.OrderNumber });
        }
    }
}
