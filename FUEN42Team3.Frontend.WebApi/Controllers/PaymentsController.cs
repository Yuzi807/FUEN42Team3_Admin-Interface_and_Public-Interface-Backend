using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Frontend.WebApi.Models.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;

namespace FUEN42Team3.Frontend.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly AppDbContext _db;                 // 資料庫存取
        private readonly EcpayService _ecpay;               // 綠界服務，用來產生表單欄位 & 驗證 CheckMacValue
        private readonly ILogger<PaymentsController> _logger; // 紀錄 Log
        private readonly IConfiguration _config;            // 設定
        private readonly FUEN42Team3.Frontend.WebApi.Models.Services.PointsEventsClient _pointsEvents;
        private readonly IEmailQueue _emailQueue;          // 寄信佇列

        private int? GetCurrentMemberId()
        {
            var val = User.FindFirst("MemberId")?.Value;
            if (int.TryParse(val, out var id)) return id;
            return null;
        }

        // 產生 20 碼且確認未在 DB 使用過的 MerchantTradeNo
        private async Task<string> GenerateTradeNumberAsync()
        {
            for (var i = 0; i < 5; i++)
            {
                // GT + 時間(12) + base36 隨機6 = 20 碼
                var timePart = DateTime.UtcNow.ToString("yyMMddHHmmss");
                var rnd = Random.Shared.NextInt64(0, 36L * 36 * 36 * 36 * 36 * 36); // 36^6
                var base36 = ToBase36(rnd).PadLeft(6, '0').ToUpperInvariant();
                var no = $"GT{timePart}{base36}";
                var exists = await _db.EcpayPayments.AnyAsync(x => x.MerchantTradeNo == no);
                if (!exists) return no;
                await Task.Delay(5);
            }
            // 極少數情況仍撞，退回完全隨機但仍符合 20 碼限制
            return $"GT{DateTime.UtcNow:yyMMddHHmmss}{Random.Shared.Next(0, 999999):D6}";
        }

        private static DateTime ToTaipeiTime(DateTime utcOrUnknown)
        {
            try
            {
                TimeZoneInfo tz;
                try { tz = TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time"); }
                catch { tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei"); }

                var src = utcOrUnknown;
                if (src.Kind == DateTimeKind.Unspecified) src = DateTime.SpecifyKind(src, DateTimeKind.Utc);
                if (src.Kind == DateTimeKind.Local) src = src.ToUniversalTime();
                return TimeZoneInfo.ConvertTimeFromUtc(src, tz);
            }
            catch { return utcOrUnknown; }
        }

        private static DateTime? ToTaipeiTime(DateTime? utcOrUnknown)
            => utcOrUnknown.HasValue ? ToTaipeiTime(utcOrUnknown.Value) : (DateTime?)null;

        /// <summary>
        /// 除錯：抓取最新一次通知內容並驗證簽章。輸入 merchantTradeNo 或 orderId。
        /// 僅回傳資訊，不改動資料。
        /// </summary>
        [HttpGet("debug/last-notify")]
        [Authorize]
        public async Task<IActionResult> DebugLastNotify([FromQuery] string? merchantTradeNo, [FromQuery] int? orderId)
        {
            // 僅允許開發環境使用
            var env = HttpContext.RequestServices.GetService(typeof(IWebHostEnvironment)) as IWebHostEnvironment;
            if (env != null && !env.IsDevelopment()) return NotFound();
            if (string.IsNullOrWhiteSpace(merchantTradeNo) && orderId == null)
                return BadRequest("missing key");

            var q = _db.EcpayPayments.AsQueryable();
            if (!string.IsNullOrWhiteSpace(merchantTradeNo)) q = q.Where(x => x.MerchantTradeNo == merchantTradeNo);
            if (orderId != null) q = q.Where(x => x.OrderId == orderId);

            var tx = await q.OrderByDescending(x => x.UpdatedAt).FirstOrDefaultAsync();
            if (tx == null) return NotFound();

            Dictionary<string, object>? raw = null;
            try
            {
                raw = string.IsNullOrWhiteSpace(tx.ExtraInfo)
                    ? null
                    : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(tx.ExtraInfo);
            }
            catch { /* ignore */ }

            // 嘗試用字串字典重算 MAC（ExtraInfo 可能同時混有 PaymentInfo 與 Notify 的欄位）
            var asStrings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (raw != null)
            {
                foreach (var kv in raw)
                {
                    asStrings[kv.Key] = kv.Value?.ToString() ?? string.Empty;
                }
            }

            // 常見欄位白名單（綠界 AIO Notify）
            string[] notifyWhitelist = new[]
            {
                "CustomField1","CustomField2","CustomField3","CustomField4",
                "MerchantID","MerchantTradeNo","PaymentDate","PaymentType",
                "RtnCode","RtnMsg","SimulatePaid","StoreID","TradeAmt","TradeDate","TradeNo","CheckMacValue"
            };
            // PaymentInfo（ATM/CVS/條碼）常見欄位白名單
            string[] paymentInfoWhitelist = new[]
            {
                "MerchantID","MerchantTradeNo","RtnCode","RtnMsg","TradeNo","TradeAmt","BankCode","vAccount",
                "ExpireDate","PaymentNo","Barcode1","Barcode2","Barcode3","PaymentType","CheckMacValue"
            };

            Dictionary<string, string> pick(IDictionary<string, string> src, string[] keys)
            {
                var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var k in keys)
                    if (src.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v)) d[k] = v;
                return d;
            }

            var vAll = _ecpay.VerifyCheckMacDetailed(asStrings);
            var vNotify = _ecpay.VerifyCheckMacDetailed(pick(asStrings, notifyWhitelist));
            var vPayInfo = _ecpay.VerifyCheckMacDetailed(pick(asStrings, paymentInfoWhitelist));

            return Ok(new
            {
                orderId = tx.OrderId,
                merchantTradeNo = tx.MerchantTradeNo,
                payStatus = tx.PayStatus,
                updatedAt = tx.UpdatedAt,
                hasExtra = !string.IsNullOrWhiteSpace(tx.ExtraInfo),
                notifyKeys = asStrings.Keys.OrderBy(k => k).ToArray(),
                verifyAll = new { vAll.isValid, vAll.provided, vAll.expected, vAll.rawNormalized },
                verifyNotifyOnly = new { vNotify.isValid, vNotify.provided, vNotify.expected, vNotify.rawNormalized },
                verifyPaymentInfoOnly = new { vPayInfo.isValid, vPayInfo.provided, vPayInfo.expected, vPayInfo.rawNormalized }
            });
        }

        private static string ToBase36(long value)
        {
            const string chars = "0123456789abcdefghijklmnopqrstuvwxyz";
            if (value <= 0) return "0";
            var sb = new System.Text.StringBuilder();
            while (value > 0)
            {
                var idx = (int)(value % 36);
                sb.Insert(0, chars[idx]);
                value /= 36;
            }
            return sb.ToString();
        }


        public PaymentsController(
            AppDbContext db,
            EcpayService ecpay,
            ILogger<PaymentsController> logger,
            IConfiguration config,
            FUEN42Team3.Frontend.WebApi.Models.Services.PointsEventsClient pointsEvents,
            IEmailQueue emailQueue)
        {
            _db = db;
            _ecpay = ecpay;
            _logger = logger;
            _config = config;
            _pointsEvents = pointsEvents;
            _emailQueue = emailQueue;
        }

        // 前端送來的資料格式
        // OrderId = 訂單ID
        // UserId  = 會員ID
        // Amount  = 總金額
        // ItemName = 商品名稱（多品項可用#串接）
        // Email    = 付款人Email（綠界需要）
        public record CheckoutRequest(int OrderId, int UserId, int Amount, string ItemName, string Email, string PaymentMethod);

        /// <summary>
        /// 前端呼叫結帳 API → 產生交易編號 → 存入交易紀錄 → 回傳綠界所需的表單欄位給前端
        /// </summary>
        [HttpPost("checkout")]
        [Authorize]
        public async Task<IActionResult> Checkout([FromBody] CheckoutRequest req)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized("未登入");
            // 1) 檢查訂單是否存在
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == req.OrderId);
            if (order == null)
            {
                return BadRequest("Order not found");
            }
            if (order.MemberId != mid.Value || req.UserId != mid.Value)
            {
                return Forbid();
            }

            // 2) 產生 20 碼唯一交易編號（符合綠界 MerchantTradeNo 長度限制）
            var merchantTradeNo = await GenerateTradeNumberAsync();


            // 3) 建立或更新交易記錄（狀態為 Pending）
            var now = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;
            var normalizedMethod = _ecpay.NormalizePaymentMethod(req.PaymentMethod);

            var existed = await _db.EcpayPayments.FirstOrDefaultAsync(x => x.OrderId == req.OrderId);
            if (existed == null)
            {
                var tx = new EcpayPayment
                {
                    OrderId = req.OrderId,
                    MerchantTradeNo = merchantTradeNo,
                    PayAmount = req.Amount,
                    PayStatus = "Pending",
                    PaymentType = string.IsNullOrWhiteSpace(normalizedMethod) ? "Pending" : normalizedMethod,
                    // 為避免資料庫 datetime 下限問題與 NotNull 限制，先帶安全值
                    PayTime = now, // 若 DB 允許 null 可改為 null
                    // 若 EcpayTradeNo 在 DB 設為 NOT NULL + UNIQUE，暫以 MerchantTradeNo 代入，待 notify 更新
                    EcpayTradeNo = merchantTradeNo,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                _db.EcpayPayments.Add(tx);
            }
            else
            {
                // 重複結帳：沿用唯一 OrderId 的記錄，更新成新的交易編號與金額，重置狀態為 Pending
                existed.MerchantTradeNo = merchantTradeNo;
                existed.EcpayTradeNo = merchantTradeNo; // 待 notify 更新為正式 TradeNo
                existed.PayAmount = req.Amount;
                existed.PayStatus = "Pending";
                existed.PaymentType = string.IsNullOrWhiteSpace(normalizedMethod) ? existed.PaymentType : normalizedMethod;
                existed.PayTime = now;
                existed.UpdatedAt = now;
            }
            await _db.SaveChangesAsync();

            //var tx = new EcpayPayment
            //{
            //    OrderId = req.OrderId,
            //    MerchantTradeNo = merchantTradeNo,
            //    PayAmount = req.Amount,
            //    PayStatus = "Pending", // 預設為待付款
            //    CreatedAt = DateTime.UtcNow
            //};
            //_db.EcpayPayments.Add(tx);
            //await _db.SaveChangesAsync();

            // 4) 呼叫 EcpayService 產生結帳表單欄位（包含簽名 CheckMacValue）
            var (url, fields) = _ecpay.BuildCheckoutFormFields(
                merchantTradeNo,
                req.Amount,
                req.ItemName,
                req.Email,
                req.PaymentMethod,
                customField1: req.OrderId.ToString()
            );

            // 5) 回傳給前端，前端可用 form submit 跳轉到綠界
            return Ok(new { gatewayUrl = url, fields });
        }

        /// <summary>
        /// 綠界付款完成後會呼叫此 API (ReturnURL) 通知後端交易結果
        /// </summary>
        [HttpPost("ecpay/notify")]
        [Consumes("application/x-www-form-urlencoded")]
        public async Task<IActionResult> Notify()
        {
            // 1) 把綠界回傳的 form 資料轉成字典
            var dict = Request.Form.Keys.ToDictionary(k => k, k => Request.Form[k].ToString());
            _logger.LogInformation("ECPay Notify Raw: {@dict}", dict);

            // 2) 驗證 CheckMacValue（避免被偽造）
            if (!_ecpay.VerifyCheckMac(dict))
            {
                _logger.LogWarning("ECPay CheckMacValue verify failed");
                return Content("0|CheckMacValue verify failed"); // 驗證失敗 → 回 0|xxx
            }

            // 3) 擷取回傳欄位
            var rtnCode = dict.GetValueOrDefault("RtnCode");           // "1" 表示成功
            var rtnMsg = dict.GetValueOrDefault("RtnMsg");
            var merchantTradeNo = dict.GetValueOrDefault("MerchantTradeNo");   // 我們產生的單號
            var tradeNo = dict.GetValueOrDefault("TradeNo");           // 綠界交易號
            var tradeAmtStr = dict.GetValueOrDefault("TradeAmt");          // 金額
            var paymentDateStr = dict.GetValueOrDefault("PaymentDate");       // 付款時間
            var customField1 = dict.GetValueOrDefault("CustomField1");       // 我方 orderId（可選）

            int.TryParse(tradeAmtStr, out var tradeAmt);
            DateTime? paidAt = DateTime.TryParse(paymentDateStr, out var dt) ? dt : null;
            var paid = rtnCode == "1"; // 是否付款成功

            // 4) 找回我們資料庫的交易紀錄
            var tx = await _db.EcpayPayments
                .FirstOrDefaultAsync(x => x.MerchantTradeNo == merchantTradeNo);

            if (tx == null)
            {
                _logger.LogWarning("EcpayPayment not found: {MNo}", merchantTradeNo);
                return Content("0|Tx not found");
            }


            // 冪等處理：如果已經是 Paid，仍檢查是否需要補發回饋點數（避免先前版本未入帳）
            if (tx.PayStatus == "Paid")
            {
                // 嘗試補發（若尚未發放）
                var existOrder = await _db.Orders
                    .Include(o => o.OrderDetails)
                    .FirstOrDefaultAsync(o => o.Id == tx.OrderId);
                if (existOrder != null)
                {
                    await TryAwardOrderPointsAsync(existOrder);
                }
                return Content("1|OK");
            }

            // 5) 驗證金額是否一致（避免金額被竄改）
            if (tradeAmt > 0 && tx.PayAmount != tradeAmt)
            {
                _logger.LogWarning("Amount mismatch: tx={txAmt}, rtn={rtnAmt}", tx.PayAmount, tradeAmt);
                // 可視情況標記為 Failed 或暫緩處理
            }

            // 6) 更新交易紀錄內容
            tx.EcpayTradeNo = tradeNo;
            tx.PayStatus = paid ? "Paid" : "Failed";
            tx.PayAmount = tradeAmt;
            tx.PayTime = paid ? (paidAt ?? FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now) : tx.PayTime;
            tx.PaymentType = dict.GetValueOrDefault("PaymentType"); // 如果需要
            tx.ExtraInfo = System.Text.Json.JsonSerializer.Serialize(dict);
            tx.UpdatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;

            // 7) 同步更新訂單主檔付款狀態
            // ⚠ TODO: 依照你的 Orders 表結構更新
            var oid = tx.OrderId;
            if (!string.IsNullOrEmpty(customField1) && int.TryParse(customField1, out var oidFromCustom))
            {
                oid = oidFromCustom;
            }
            var orderToUpdate = await _db.Orders
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.Id == oid);
            if (orderToUpdate != null)
            {
                // 付款成功 → 設定已付款狀態與付款時間
                if (paid)
                {
                    // 依 StatusName 尋找 Id，避免把 Id 寫死
                    var paidStatus = await _db.OrderStatuses.FirstOrDefaultAsync(s => s.StatusName == "已付款");
                    if (paidStatus != null)
                    {
                        orderToUpdate.StatusId = paidStatus.Id;
                    }
                    orderToUpdate.PaymentDate = paidAt ?? FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;

                    // 發放回饋點數（滿 100 元回饋 1 點），以(商品小計-優惠-使用點數)為基礎，不含運費
                    await TryAwardOrderPointsAsync(orderToUpdate);

                    // 同步通知後台點數規則事件（首購/達標/百分比）
                    try
                    {
                        decimal subtotal = orderToUpdate.OrderDetails?.Sum(d => d.Subtotal) ?? 0m;
                        decimal discount = orderToUpdate.DiscountAmount ?? 0m;
                        decimal usedPoints = orderToUpdate.UsedPoints;
                        decimal eligible = subtotal - discount - usedPoints;
                        if (eligible < 0m) eligible = 0m;

                        bool isFirstPurchase = !await _db.Orders
                            .AnyAsync(o => o.MemberId == orderToUpdate.MemberId && o.Id != orderToUpdate.Id && o.PaymentDate != null);

                        _ = _pointsEvents.SendOrderCompletedAsync(orderToUpdate.MemberId, orderToUpdate.Id, eligible, isFirstPurchase);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "send points events failed for OrderId={OrderId}", orderToUpdate.Id);
                    }

                    // 嘗試寄送付款成功通知信（改為背景投遞，不阻擋主流程）
                    try
                    {
                        var member = await _db.Members.FirstOrDefaultAsync(m => m.Id == orderToUpdate.MemberId);
                        var toEmail = member?.Email;
                        var toName = member?.UserName ?? "會員";
                        if (!string.IsNullOrWhiteSpace(toEmail))
                        {
                            var html = OrderNotificationEmailBuilder.BuildPaymentSuccessEmail(toName, orderToUpdate);
                            _emailQueue.Enqueue(new EmailMessage(
                                SenderName: "魔型仔官方團隊",
                                SenderEmail: "Ghosttoy0905@gmail.com",
                                ToName: toName,
                                ToEmail: toEmail!,
                                Subject: "[魔型仔Ghost Toys] 付款成功通知",
                                Html: html
                            ));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "send payment success email failed for OrderId={OrderId}", orderToUpdate.Id);
                    }
                }
                orderToUpdate.UpdatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;
            }

            await _db.SaveChangesAsync();

            // 8) 回覆綠界必須是 "1|OK" 才會被認定接收成功
            return Content("1|OK");
        }

        /// <summary>
        /// 嘗試為訂單發放回饋點數：規則=每滿100元回饋1點；基礎金額=(明細小計合計 - 折扣 - 使用點數)，不含運費。
        /// 具冪等性：若已發放或點數為0則不動作。
        /// </summary>
        private async Task TryAwardOrderPointsAsync(Order order)
        {
            try
            {
                // 已發放過則跳過（以 Order.PointsEarned 或既有點數紀錄為準）
                if (order.PointsEarned > 0)
                {
                    return;
                }

                var hasLog = await _db.MemberPointLogs
                    .AnyAsync(x => x.OrderId == order.Id && x.MemberId == order.MemberId && x.ChangeAmount > 0);
                if (hasLog)
                {
                    return;
                }

                // 基礎金額：商品小計 - 優惠 - 使用點數（避免負值）
                decimal subtotal = order.OrderDetails?.Sum(d => d.Subtotal) ?? 0m;
                decimal discount = order.DiscountAmount ?? 0m;
                decimal usedPoints = order.UsedPoints;
                decimal eligible = subtotal - discount - usedPoints;
                if (eligible < 0m) eligible = 0m;

                var earned = (int)Math.Floor(eligible / 100m);
                if (earned <= 0)
                {
                    return;
                }

                order.PointsEarned = earned;
                _db.MemberPointLogs.Add(new MemberPointLog
                {
                    MemberId = order.MemberId,
                    OrderId = order.Id,
                    ChangeAmount = earned,
                    Reason = "購物回饋",
                    CreatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now
                });

                // 僅保存與點數相關的異動，不影響外部流程
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // 不中斷主要付款流程；記錄錯誤以便後續補發
                _logger.LogError(ex, "TryAwardOrderPointsAsync failed for OrderId={OrderId}", order?.Id);
            }
        }

        /// <summary>
        /// 綠界針對 ATM/超商代碼/條碼 發碼等資訊的回呼 (PaymentInfoURL)
        /// 這些回呼通常先於付款完成，需保存代碼/條碼與到期時間供用戶繳費。
        /// </summary>
        [HttpPost("ecpay/payment-info")]
        [Consumes("application/x-www-form-urlencoded")]
        public async Task<IActionResult> PaymentInfo()
        {
            var dict = Request.Form.Keys.ToDictionary(k => k, k => Request.Form[k].ToString());
            _logger.LogInformation("ECPay PaymentInfo Raw: {@dict}", dict);

            // 若帶有 CheckMacValue 則驗證（有些情境不一定帶）
            if (dict.ContainsKey("CheckMacValue") && !_ecpay.VerifyCheckMac(dict))
            {
                return Content("0|CheckMacValue verify failed");
            }

            var merchantTradeNo = dict.GetValueOrDefault("MerchantTradeNo");
            if (string.IsNullOrWhiteSpace(merchantTradeNo))
                return Content("0|missing MerchantTradeNo");

            var tx = await _db.EcpayPayments.FirstOrDefaultAsync(x => x.MerchantTradeNo == merchantTradeNo);
            if (tx == null)
                return Content("0|Tx not found");

            // 將目前回傳欄位併入 ExtraInfo（保留既有內容）
            try
            {
                Dictionary<string, string> all = new();
                if (!string.IsNullOrWhiteSpace(tx.ExtraInfo))
                {
                    try
                    {
                        var old = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(tx.ExtraInfo) ?? new();
                        foreach (var kv in old) all[kv.Key] = kv.Value?.ToString() ?? string.Empty;
                    }
                    catch { /* ignore corrupt json */ }
                }
                foreach (var kv in dict) all[kv.Key] = kv.Value;
                tx.ExtraInfo = System.Text.Json.JsonSerializer.Serialize(all);
                tx.UpdatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "save payment-info failed");
            }

            return Content("1|OK");
        }

        /// <summary>
        /// 提供前端查詢付款結果用（依 MerchantTradeNo 或 OrderId）
        /// </summary>
        [HttpGet("result")]
        [Authorize]
        public async Task<IActionResult> GetResult([FromQuery] string? merchantTradeNo, [FromQuery] int? orderId)
        {
            if (string.IsNullOrWhiteSpace(merchantTradeNo) && orderId == null)
                return BadRequest("missing key");
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized("未登入");
            var query = _db.EcpayPayments.AsQueryable();
            if (!string.IsNullOrWhiteSpace(merchantTradeNo))
                query = query.Where(x => x.MerchantTradeNo == merchantTradeNo);
            if (orderId != null)
                query = query.Where(x => x.OrderId == orderId);

            var tx = await query.FirstOrDefaultAsync();
            if (tx == null) return NotFound();

            // 擁有者檢查
            var orderForOwnerCheck = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == tx.OrderId);
            if (orderForOwnerCheck == null || orderForOwnerCheck.MemberId != mid.Value)
            {
                return Forbid();
            }

            // 若仍為 Pending，主動向綠界查詢一次，成功則即時同步為 Paid
            if (string.Equals(tx.PayStatus, "Pending", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(tx.MerchantTradeNo))
            {
                var dict = await _ecpay.QueryTradeInfoAsync(tx.MerchantTradeNo);
                // 綠界查詢成功且顯示付款成功：Status=SUCCESS 或 RtnCode=1（不同環節鍵名可能不同）
                if (dict != null)
                {
                    var rtnCode = dict.GetValueOrDefault("RtnCode");
                    var tradeAmt = dict.GetValueOrDefault("TradeAmt");
                    var tradeNo = dict.GetValueOrDefault("TradeNo");
                    var paymentDateStr = dict.GetValueOrDefault("PaymentDate");
                    var tradeStatus = dict.GetValueOrDefault("TradeStatus");

                    bool paidByQuery = rtnCode == "1"
                        || string.Equals(tradeStatus, "SUCCESS", StringComparison.OrdinalIgnoreCase)
                        || tradeStatus == "1"; // 綠界 QueryTradeInfo 成功常見為 1
                    if (paidByQuery)
                    {
                        int.TryParse(tradeAmt, out var amt);
                        DateTime? paidAt = DateTime.TryParse(paymentDateStr, out var dt) ? dt : FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;

                        tx.PayStatus = "Paid";
                        tx.EcpayTradeNo = string.IsNullOrWhiteSpace(tradeNo) ? tx.EcpayTradeNo : tradeNo;
                        tx.PayTime = paidAt ?? FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;
                        tx.PayAmount = amt > 0 ? amt : tx.PayAmount;
                        tx.UpdatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;

                        // 同步訂單主檔
                        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == tx.OrderId);
                        if (order != null)
                        {
                            var paidStatus = await _db.OrderStatuses.FirstOrDefaultAsync(s => s.StatusName == "已付款");
                            if (paidStatus != null) order.StatusId = paidStatus.Id;
                            order.PaymentDate = paidAt ?? FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;
                            order.UpdatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;

                            // 冪等發放點數
                            await TryAwardOrderPointsAsync(order);
                        }
                        await _db.SaveChangesAsync();
                    }
                }
            }

            // 夾帶訂單編號供前端導向詳情頁使用
            string? orderNumber = null;
            try
            {
                orderNumber = await _db.Orders.Where(o => o.Id == tx.OrderId).Select(o => o.OrderNumber).FirstOrDefaultAsync();
            }
            catch { /* ignore */ }

            return Ok(new
            {
                orderId = tx.OrderId,
                orderNumber,
                merchantTradeNo = tx.MerchantTradeNo,
                ecpayTradeNo = tx.EcpayTradeNo,
                payStatus = tx.PayStatus,
                payAmount = tx.PayAmount,
                payTime = tx.PayTime,
                payTimeTaipei = ToTaipeiTime(tx.PayTime),
                paymentType = tx.PaymentType,
                extraInfo = tx.ExtraInfo
            });
        }

        /// <summary>
        /// 手動同步：立即向綠界 QueryTradeInfo 查詢並更新狀態（除錯/救援用）
        /// </summary>
        [HttpPost("ecpay/sync")]
        [Authorize]
        public Task<IActionResult> Sync([FromQuery] string? merchantTradeNo, [FromQuery] int? orderId)
                => SyncCore(merchantTradeNo, orderId);

        // 僅供本機/開發除錯：GET 版同步（方便用瀏覽器直接點）
        [HttpGet("ecpay/sync")]
        public async Task<IActionResult> SyncGet([FromQuery] string? merchantTradeNo, [FromQuery] int? orderId, [FromServices] IWebHostEnvironment env)
        {
            if (!env.IsDevelopment()) return NotFound();
            // 開發用也需登入避免暴露資料
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized("未登入");
            return await SyncCore(merchantTradeNo, orderId);
        }

        private async Task<IActionResult> SyncCore(string? merchantTradeNo, int? orderId)
        {
            if (string.IsNullOrWhiteSpace(merchantTradeNo) && orderId == null)
                return BadRequest("missing key");

            var query = _db.EcpayPayments.AsQueryable();
            if (!string.IsNullOrWhiteSpace(merchantTradeNo))
                query = query.Where(x => x.MerchantTradeNo == merchantTradeNo);
            if (orderId != null)
                query = query.Where(x => x.OrderId == orderId);

            var tx = await query.FirstOrDefaultAsync();
            if (tx == null) return NotFound();

            var dict = await _ecpay.QueryTradeInfoAsync(tx.MerchantTradeNo);
            if (dict == null)
                return Ok(new { success = false, message = "query failed", current = tx });

            var rtnCode = dict.GetValueOrDefault("RtnCode");
            var tradeAmt = dict.GetValueOrDefault("TradeAmt");
            var tradeNo = dict.GetValueOrDefault("TradeNo");
            var paymentDateStr = dict.GetValueOrDefault("PaymentDate");
            var tradeStatus = dict.GetValueOrDefault("TradeStatus");

            bool paidByQuery = rtnCode == "1" || string.Equals(tradeStatus, "SUCCESS", StringComparison.OrdinalIgnoreCase) || tradeStatus == "1";

            if (paidByQuery)
            {
                int.TryParse(tradeAmt, out var amt);
                DateTime? paidAt = DateTime.TryParse(paymentDateStr, out var dt) ? dt : FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;

                tx.PayStatus = "Paid";
                tx.EcpayTradeNo = string.IsNullOrWhiteSpace(tradeNo) ? tx.EcpayTradeNo : tradeNo;
                tx.PayTime = paidAt ?? FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;
                tx.PayAmount = amt > 0 ? amt : tx.PayAmount;
                tx.UpdatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;

                var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == tx.OrderId);
                if (order != null)
                {
                    var paidStatus = await _db.OrderStatuses.FirstOrDefaultAsync(s => s.StatusName == "已付款");
                    if (paidStatus != null) order.StatusId = paidStatus.Id;
                    order.PaymentDate = paidAt ?? FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;
                    order.UpdatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;
                    await TryAwardOrderPointsAsync(order);
                }

                await _db.SaveChangesAsync();
            }

            return Ok(new
            {
                success = true,
                tx,
                queryResult = dict,
                payTimeTaipei = ToTaipeiTime(tx.PayTime)
            });
        }

        /// <summary>
        /// 供綠界前端（OrderResultURL）POST 回來的頁面端點。
        /// 這裡會驗證回傳、必要時回寫付款成功，以作為 ReturnURL 未達時的備援，
        /// 最後 302 轉址：
        /// - 成功 → 導向前端訂單詳情頁 /order/{orderNumber}
        /// - 其他 → 導向付款結果頁 /payment-result?merchantTradeNo=...&orderId=...
        /// </summary>
        [HttpPost("order-result")]
        [Consumes("application/x-www-form-urlencoded")]
        public async Task<IActionResult> OrderResultRedirect()
        {
            var dict = Request.Form.Keys.ToDictionary(k => k, k => Request.Form[k].ToString());
            _logger.LogInformation("ECPay OrderResult Raw: {@dict}", dict);

            // 取常用欄位
            var rtnCode = dict.GetValueOrDefault("RtnCode");
            var merchantTradeNo = dict.GetValueOrDefault("MerchantTradeNo");
            var tradeNo = dict.GetValueOrDefault("TradeNo");
            var tradeAmtStr = dict.GetValueOrDefault("TradeAmt");
            var paymentDateStr = dict.GetValueOrDefault("PaymentDate");
            var customOrderId = dict.GetValueOrDefault("CustomField1");

            int.TryParse(tradeAmtStr, out var tradeAmt);
            DateTime? paidAt = DateTime.TryParse(paymentDateStr, out var dt) ? dt : null;

            // 先準備導向的預設（付款結果頁）
            var frontResultUrl = _config["Ecpay:OrderResultFrontUrl"] ?? "http://localhost:5173/payment-result";
            string BuildResultUrl()
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(merchantTradeNo))
                    parts.Add($"merchantTradeNo={Uri.EscapeDataString(merchantTradeNo)}");
                if (int.TryParse(customOrderId, out var oid1))
                    parts.Add($"orderId={oid1}");
                return parts.Count == 0 ? frontResultUrl : ($"{frontResultUrl}{(frontResultUrl.Contains("?") ? "&" : "?")}{string.Join("&", parts)}");
            }

            // 若帶有 CheckMacValue，先驗證；失敗則直接導到付款結果頁
            if (dict.ContainsKey("CheckMacValue") && !_ecpay.VerifyCheckMac(dict))
            {
                _logger.LogWarning("ECPay OrderResult CheckMacValue verify failed");
                return Redirect(BuildResultUrl());
            }

            // 找交易紀錄
            if (string.IsNullOrWhiteSpace(merchantTradeNo))
                return Redirect(BuildResultUrl());

            var tx = await _db.EcpayPayments.FirstOrDefaultAsync(x => x.MerchantTradeNo == merchantTradeNo);
            if (tx == null)
            {
                _logger.LogWarning("EcpayPayment not found in OrderResult: {MNo}", merchantTradeNo);
                return Redirect(BuildResultUrl());
            }

            // ReturnURL 已處理過（已付款）則直接嘗試導向訂單詳情
            var paidAlready = string.Equals(tx.PayStatus, "Paid", StringComparison.OrdinalIgnoreCase);
            var paidNow = rtnCode == "1";

            // 備援更新：若尚未 Paid 且此次回傳成功，則比照 notify 更新資料
            if (!paidAlready && paidNow)
            {
                // 金額不一致僅記錄警告，不阻擋導向
                if (tradeAmt > 0 && tx.PayAmount != tradeAmt)
                {
                    _logger.LogWarning("[OrderResult] Amount mismatch: tx={txAmt}, rtn={rtnAmt}", tx.PayAmount, tradeAmt);
                }

                tx.EcpayTradeNo = string.IsNullOrWhiteSpace(tradeNo) ? tx.EcpayTradeNo : tradeNo;
                tx.PayStatus = "Paid";
                tx.PayAmount = tradeAmt > 0 ? tradeAmt : tx.PayAmount;
                tx.PayTime = paidAt ?? FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;
                tx.PaymentType = dict.GetValueOrDefault("PaymentType") ?? tx.PaymentType;
                tx.ExtraInfo = System.Text.Json.JsonSerializer.Serialize(dict);
                tx.UpdatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;

                // 同步訂單主檔
                var oid = tx.OrderId;
                if (!string.IsNullOrEmpty(customOrderId) && int.TryParse(customOrderId, out var oidFromCustom))
                    oid = oidFromCustom;

                var order = await _db.Orders
                    .Include(o => o.OrderDetails)
                    .FirstOrDefaultAsync(o => o.Id == oid);
                if (order != null)
                {
                    var paidStatus = await _db.OrderStatuses.FirstOrDefaultAsync(s => s.StatusName == "已付款");
                    if (paidStatus != null) order.StatusId = paidStatus.Id;
                    order.PaymentDate = paidAt ?? FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;
                    order.UpdatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;

                    // 發放回饋點數（冪等）
                    await TryAwardOrderPointsAsync(order);

                    // 備援也寄付款成功通知信（與 Notify 路徑一致），避免 ReturnURL 漏送時沒有信件
                    try
                    {
                        var member = await _db.Members.FirstOrDefaultAsync(m => m.Id == order.MemberId);
                        var toEmail = member?.Email;
                        var toName = member?.UserName ?? "會員";
                        if (!string.IsNullOrWhiteSpace(toEmail))
                        {
                            var html = OrderNotificationEmailBuilder.BuildPaymentSuccessEmail(toName, order);
                            _emailQueue.Enqueue(new EmailMessage(
                                SenderName: "魔型仔官方團隊",
                                SenderEmail: "Ghosttoy0905@gmail.com",
                                ToName: toName,
                                ToEmail: toEmail!,
                                Subject: "[魔型仔Ghost Toys] 付款成功通知",
                                Html: html
                            ));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "send payment success email (backup) failed for OrderId={OrderId}", order.Id);
                    }
                }

                await _db.SaveChangesAsync();
            }

            // 新需求：所有付款方式（包含非信用卡）也導向訂單詳情頁
            try
            {
                // 取訂單編號
                var orderId = tx.OrderId;
                if (!string.IsNullOrEmpty(customOrderId) && int.TryParse(customOrderId, out var oidFromCustom2))
                    orderId = oidFromCustom2;

                var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
                var orderNumber = order?.OrderNumber;

                // 從設定取前端 Base（可能是 http://localhost:5173/payment-result 或根網址）
                var frontBase = _config["Ecpay:OrderResultFrontUrl"] ?? "http://localhost:5173/payment-result";
                // 嘗試取出根網址
                string baseOrigin;
                try
                {
                    var uri = new Uri(frontBase, UriKind.Absolute);
                    baseOrigin = uri.GetLeftPart(UriPartial.Authority);
                }
                catch
                {
                    // 若不是絕對位址，退回預設
                    baseOrigin = "http://localhost:5173";
                }

                if (!string.IsNullOrWhiteSpace(orderNumber))
                {
                    var detailUrl = $"{baseOrigin}/order/{Uri.EscapeDataString(orderNumber)}";
                    return Redirect(detailUrl);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OrderResult redirect to detail failed");
            }

            // 後備：資料不足 → 回付款結果頁
            return Redirect(BuildResultUrl());
        }
    }
}

