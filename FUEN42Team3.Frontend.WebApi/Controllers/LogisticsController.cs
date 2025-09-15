using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Frontend.WebApi.Models.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace FUEN42Team3.Frontend.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LogisticsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly EcpayLogisticsService _logi;
        private readonly IConfiguration _config;
        private readonly ILogger<LogisticsController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly NominatimGeocodingService _geocoder;
        private readonly IEmailQueue _emailQueue;

        public LogisticsController(AppDbContext db, EcpayLogisticsService logi, IConfiguration config, ILogger<LogisticsController> logger, IHttpClientFactory httpClientFactory, NominatimGeocodingService geocoder, IEmailQueue emailQueue)
        {
            _db = db;
            _logi = logi;
            _config = config;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _geocoder = geocoder;
            _emailQueue = emailQueue;
        }

        private int? GetCurrentMemberId()
        {
            var val = User.FindFirst("MemberId")?.Value;
            if (int.TryParse(val, out var id)) return id;
            return null;
        }

        private static string MapBrand(string? logisticsSubType)
        {
            var s = (logisticsSubType ?? string.Empty).Trim().ToUpperInvariant();
            return s switch
            {
                "UNIMARTC2C" => "7-11",
                "FAMIC2C" => "全家",
                "HILIFEC2C" => "萊爾富",
                "OKMARTC2C" => "OK",
                _ => "超商"
            };
        }

        private static string ComposeCvsAddress(string? logisticsSubType, string? storeName, string? address)
        {
            var brand = MapBrand(logisticsSubType);
            var name = (storeName ?? string.Empty).Trim();
            var addr = (address ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(addr)) return $"{brand} {name} {addr}";
            if (!string.IsNullOrEmpty(addr)) return $"{brand} {addr}";
            if (!string.IsNullOrEmpty(name)) return $"{brand} {name}";
            return brand;
        }

        public record MapRequest(int OrderId, string LogisticsSubType, int? Device);

        [HttpPost("map")]
        public IActionResult OpenMap([FromBody] MapRequest req)
        {
            // 開啟綠界超商地圖不一定需要先建立訂單；
            // 這裡不強制檢查 order 是否存在，若 ExtraData 帶 0/空值，後續 callback 將以 postMessage 回傳前端自行保存。
            try
            {
                var merchantTradeNo = $"LG{DateTime.UtcNow:yyMMddHHmmss}{Random.Shared.Next(0, 999999):D6}";
                var serverReplyURL = _config["Ecpay:LogisticsMapCallback"] ?? string.Empty;
                // 再次保險：移除公網網域上的自訂連接埠（例如 trycloudflare.com:7262）
                serverReplyURL = SanitizePublicUrl(serverReplyURL);
                var extra = (req.OrderId > 0) ? req.OrderId.ToString() : "0";

                var device = req.Device.GetValueOrDefault(0);
                var (url, fields) = _logi.BuildCvsMapPost(req.LogisticsSubType, merchantTradeNo, serverReplyURL, extra, device);
                // 若服務層仍回傳帶埠號（舊版程序尚未重啟等），這裡再保險一次
                if (fields.ContainsKey("ServerReplyURL"))
                {
                    fields["ServerReplyURL"] = SanitizePublicUrl(fields["ServerReplyURL"] ?? string.Empty);
                }
                _logger.LogInformation("ECPay Map Request -> URL: {url}, Callback: {cb}, Fields: {@fields}", url, fields.GetValueOrDefault("ServerReplyURL"), fields);
                return Ok(new { url, fields });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenMap failed");
                return Problem("open map failed");
            }
        }

        // 與服務層相同邏輯的精簡版，避免舊進程造成的帶埠號問題
        private static string SanitizePublicUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            try
            {
                var uri = new Uri(url);
                var isPublic = uri.Host.EndsWith("trycloudflare.com", StringComparison.OrdinalIgnoreCase) || !uri.IsLoopback;
                if (isPublic && !uri.IsDefaultPort)
                {
                    var builder = new UriBuilder(uri.Scheme, uri.Host)
                    {
                        Path = uri.AbsolutePath,
                        Query = uri.Query.TrimStart('?')
                    };
                    return builder.Uri.ToString();
                }
                return url;
            }
            catch
            {
                return url ?? string.Empty;
            }
        }

        [HttpPost("map-callback")]
        [Consumes("application/x-www-form-urlencoded")]
        public async Task<IActionResult> MapCallback()
        {
            var dict = Request.Form.Keys.ToDictionary(k => k, k => Request.Form[k].ToString());
            _logger.LogInformation("ECPay Map Callback: {@dict}", dict);

            // 經驗上 Map 回傳不一定包含 CheckMacValue，若有就驗證
            if (dict.ContainsKey("CheckMacValue") && !_logi.VerifyCheckMac(dict))
                return Content("0|CheckMacValue verify failed");

            var extraData = dict.GetValueOrDefault("ExtraData"); // 我方 orderId 或 0
            var hasOrderId = int.TryParse(extraData, out var orderId) && orderId > 0;

            // 組合要回傳前端的門市資訊（供 window.opener postMessage 使用）
            var payload = new Dictionary<string, string?>
            {
                ["LogisticsSubType"] = dict.GetValueOrDefault("LogisticsSubType"),
                ["CVSStoreID"] = dict.GetValueOrDefault("CVSStoreID"),
                ["CVSStoreName"] = dict.GetValueOrDefault("CVSStoreName"),
                ["CVSAddress"] = dict.GetValueOrDefault("CVSAddress"),
                ["CVSTelephone"] = dict.GetValueOrDefault("CVSTelephone")
            };

            if (hasOrderId)
            {
                var order = await _db.Orders.Include(o => o.OrderLogistic).FirstOrDefaultAsync(o => o.Id == orderId);
                if (order != null)
                {
                    var logistic = order.OrderLogistic ?? new OrderLogistic
                    {
                        OrderId = order.Id,
                        Provider = "ECPAY",
                        LogisticsType = "CVS",
                        LogisticsSubType = dict.GetValueOrDefault("LogisticsSubType") ?? string.Empty,
                        CreatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now
                    };

                    logistic.PickupStoreId = dict.GetValueOrDefault("CVSStoreID");
                    logistic.PickupStoreName = dict.GetValueOrDefault("CVSStoreName");
                    logistic.PickupAddress = dict.GetValueOrDefault("CVSAddress");
                    logistic.PickupTelephone = dict.GetValueOrDefault("CVSTelephone");
                    logistic.PickupExtra = System.Text.Json.JsonSerializer.Serialize(dict);
                    logistic.UpdatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;

                    if (order.OrderLogistic == null)
                        _db.OrderLogistics.Add(logistic);

                    // 同步把訂單 ShippingAddress 存成「品牌 門市名 地址」
                    order.ShippingAddress = ComposeCvsAddress(logistic.LogisticsSubType, logistic.PickupStoreName, logistic.PickupAddress);
                    order.UpdatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;

                    await _db.SaveChangesAsync();
                }
            }

            // 無論是否已成功保存到 DB，皆回傳一段 HTML：
            // 1) 若有 opener，postMessage 回傳並嘗試自動關閉彈窗
            // 2) 若無 opener（同頁開啟），顯示完成頁並提供返回上一頁/回到網站按鈕
            var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload);
            var clientReturnUrl = _config["Ecpay:ClientReturnUrl"] ?? "/";
            var html = @$"<!DOCTYPE html>
            <html lang=""zh-Hant""><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1""><title>門市選擇完成</title>
            <style>
                body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, 'Noto Sans', 'Microsoft JhengHei', sans-serif; padding: 24px; color: #333; }}
                .card {{ max-width: 520px; margin: 10vh auto; border: 1px solid #e5e7eb; border-radius: 12px; box-shadow: 0 6px 20px rgba(0,0,0,.06); overflow: hidden; }}
                .card h1 {{ font-size: 20px; margin: 0; padding: 16px 20px; background:#f9fafb; border-bottom:1px solid #eee; }}
                .card .content {{ padding: 20px; line-height: 1.6; }}
                .actions {{ margin-top: 16px; display:flex; gap:10px; flex-wrap: wrap; }}
                .btn {{ padding: 10px 14px; border-radius: 8px; border: 1px solid #d1d5db; background:#fff; cursor:pointer; }}
                .btn.primary {{ background:#2563eb; border-color:#2563eb; color:#fff; }}
            </style>
            </head>
            <body>
                <div class=""card"" id=""app"">
                    <h1>門市選擇完成</h1>
                    <div class=""content"">
                        <div id=""storeInfo"" style=""display:none; color:#555;""></div>
                        <div class=""actions"">
                            <button class=""btn primary"" id=""btnBack"">回上一頁</button>
                            <a class=""btn"" id=""btnHome"" href=""{clientReturnUrl}"">回到網站</a>
                        </div>
                    </div>
                </div>
                <script>
                    (function() {{
                        var data = {payloadJson};
                        try {{
                            if (window.opener && typeof window.opener.postMessage === 'function') {{
                                window.opener.postMessage({{ type: 'ECPAY_CVS_SELECTED', data: data }}, '*');
                                setTimeout(function() {{ window.close(); }}, 800);
                                return; // 已回傳並嘗試關閉彈窗
                            }}
                        }} catch (e) {{ /* ignore */ }}

                        // 無 opener：顯示資訊與返回按鈕
                        try {{
                            var info = document.getElementById('storeInfo');
                            if (info) {{
                                var name = data && (data.CVSStoreName || data.StoreName || '');
                                var addr = data && (data.CVSAddress || data.Address || '');
                                info.style.display = 'block';
                                info.innerHTML = name ? ('您已選擇：<strong>' + name + '</strong><br/>' + addr) : '已完成選擇，請返回上一頁繼續。';
                            }}
                        }} catch (e) {{ /* ignore */ }}
                        var back = document.getElementById('btnBack');
                        if (back) back.addEventListener('click', function() {{ history.back(); }});
                    }})();
                </script>
                </body></html>";
            return Content(html, "text/html; charset=utf-8");
        }

        public record CreateRequest(int OrderId, string LogisticsSubType, bool IsCollection);

        /// <summary>
        /// 建立 CVS 託運單（需先完成選店）
        /// </summary>
        [HttpPost("c2c/create")]
        [Authorize]
        public async Task<IActionResult> CreateShippingOrder([FromBody] CreateRequest req)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized(new { message = "未登入" });
            var order = await _db.Orders.Include(o => o.OrderLogistic).FirstOrDefaultAsync(o => o.Id == req.OrderId);
            if (order == null) return NotFound("order not found");
            if (order.MemberId != mid.Value) return Forbid();
            if (order.OrderLogistic == null || string.IsNullOrEmpty(order.OrderLogistic.PickupStoreId))
                return BadRequest("store not selected");

            var merchantTradeNo = $"SL{DateTime.UtcNow:yyMMddHHmmss}{Random.Shared.Next(0, 999999):D6}";

            var (url, fields) = _logi.BuildCreateC2CFields(
                merchantTradeNo,
                req.LogisticsSubType,
                req.IsCollection,
                goodsAmount: (int)order.TotalAmount,
                receiverName: order.RecipientName ?? "Receiver",
                receiverPhone: order.RecipientPhone ?? string.Empty,
                receiverEmail: string.Empty,
                receiverStoreId: order.OrderLogistic.PickupStoreId ?? string.Empty
            );

            // 直接由後端代為呼叫 ECPay /Express/Create（application/x-www-form-urlencoded）
            var client = _httpClientFactory.CreateClient();
            var form = new FormUrlEncodedContent(fields);
            var resp = await client.PostAsync(url, form);
            var resultText = await resp.Content.ReadAsStringAsync();
            _logger.LogInformation("ECPay Create Response: {text}", resultText);

            // 綠界回傳是 key=value&key2=value2 格式
            var resultDict = resultText.Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Split('=', 2))
                .Where(arr => arr.Length == 2)
                .ToDictionary(arr => arr[0], arr => Uri.UnescapeDataString(arr[1]));

            if (!resultDict.TryGetValue("RtnCode", out var rtn) || rtn != "1")
            {
                var msg = resultDict.GetValueOrDefault("RtnMsg") ?? "create failed";
                return BadRequest(new { success = false, message = msg, raw = resultText });
            }

            // 成功：保存 AllPayLogisticsID / ShipmentNo 等資訊
            var logi = order.OrderLogistic;
            logi.AllPayLogisticsId = resultDict.GetValueOrDefault("AllPayLogisticsID");
            logi.ShipmentNo = resultDict.GetValueOrDefault("ShipmentNo");
            logi.RtnCode = 1;
            logi.RtnMsg = resultDict.GetValueOrDefault("RtnMsg");
            logi.Status = "Created";
            // 保存是否代收與金額，供後續通知判斷
            logi.IsCollection = req.IsCollection;
            if (req.IsCollection)
            {
                logi.CollectionAmount = order.TotalAmount;
            }
            logi.UpdatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;
            await _db.SaveChangesAsync();

            // 若為超商取貨付款（COD），嘗試更新訂單狀態為「待取貨付款」（若資料庫有此狀態）
            if (req.IsCollection)
            {
                var codStatus = await _db.OrderStatuses
                    .Where(s => s.StatusName == "待取貨付款" || s.StatusName == "待付款")
                    .Select(s => new { s.Id, s.StatusName })
                    .FirstOrDefaultAsync();
                if (codStatus != null && order.StatusId != codStatus.Id)
                {
                    order.StatusId = codStatus.Id;
                    order.UpdatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;
                    await _db.SaveChangesAsync();
                }
            }

            // 嘗試寄送「物流建立」通知信（背景投遞，不阻斷流程）
            try
            {
                var member = await _db.Members.FirstOrDefaultAsync(m => m.Id == order.MemberId);
                var toEmail = member?.Email;
                var toName = member?.UserName ?? "會員";
                if (!string.IsNullOrWhiteSpace(toEmail) && order.OrderLogistic != null)
                {
                    var html = OrderNotificationEmailBuilder.BuildLogisticsCreatedEmail(toName, order, order.OrderLogistic);
                    _emailQueue.Enqueue(new EmailMessage(
                        SenderName: "魔型仔官方團隊",
                        SenderEmail: "Ghosttoy0905@gmail.com",
                        ToName: toName,
                        ToEmail: toEmail!,
                        Subject: "[魔型仔Ghost Toys] 物流建立通知",
                        Html: html
                    ));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "send logistics created email failed for OrderId={OrderId}", order.Id);
            }

            return Ok(new
            {
                success = true,
                logisticsId = logi.AllPayLogisticsId,
                shipmentNo = logi.ShipmentNo
            });
        }

        public record StoreSelectRequest(int OrderId, string LogisticsSubType, string StoreId, string StoreName, string Address, string Telephone);

        /// <summary>
        /// 直接保存選店資訊（若未使用綠界地圖，可使用此端點）
        /// </summary>
        [HttpPost("store-select")]
        [Authorize]
        public async Task<IActionResult> StoreSelect([FromBody] StoreSelectRequest req)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized(new { message = "未登入" });
            var order = await _db.Orders.Include(o => o.OrderLogistic).FirstOrDefaultAsync(o => o.Id == req.OrderId);
            if (order == null) return NotFound("order not found");
            if (order.MemberId != mid.Value) return Forbid();

            var logistic = order.OrderLogistic ?? new OrderLogistic
            {
                OrderId = order.Id,
                Provider = "ECPAY",
                LogisticsType = "CVS",
                CreatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now
            };

            logistic.LogisticsSubType = req.LogisticsSubType;
            logistic.PickupStoreId = req.StoreId;
            logistic.PickupStoreName = req.StoreName;
            logistic.PickupAddress = req.Address;
            logistic.PickupTelephone = req.Telephone;
            logistic.UpdatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;

            // 同步把訂單 ShippingAddress 存成「品牌 門市名 地址」
            order.ShippingAddress = ComposeCvsAddress(logistic.LogisticsSubType, logistic.PickupStoreName, logistic.PickupAddress);
            order.UpdatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;

            if (order.OrderLogistic == null)
                _db.OrderLogistics.Add(logistic);

            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        public record CancelRequest(int OrderId);
        /// <summary>
        /// 取消已建立的 CVS C2C 託運單（若存在）。
        /// </summary>
        [HttpPost("c2c/cancel")]
        [Authorize]
        public async Task<IActionResult> CancelC2C([FromBody] CancelRequest req)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized(new { message = "未登入" });
            var order = await _db.Orders.Include(o => o.OrderLogistic).FirstOrDefaultAsync(o => o.Id == req.OrderId);
            if (order == null) return NotFound("order not found");
            if (order.MemberId != mid.Value) return Forbid();
            var logi = order.OrderLogistic;
            if (logi == null || string.IsNullOrWhiteSpace(logi.AllPayLogisticsId)) return Ok(new { success = true, message = "no shipment" });

            var (url, fields) = _logi.BuildCancelC2CFields(logi.AllPayLogisticsId);
            var client = _httpClientFactory.CreateClient();
            var resp = await client.PostAsync(url, new FormUrlEncodedContent(fields));
            var txt = await resp.Content.ReadAsStringAsync();
            _logger.LogInformation("ECPay Cancel Response: {text}", txt);
            var result = txt.Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Split('=', 2)).Where(a => a.Length == 2)
                .ToDictionary(a => a[0], a => Uri.UnescapeDataString(a[1]));

            if (!result.TryGetValue("RtnCode", out var rtn) || rtn != "1")
            {
                return BadRequest(new { success = false, message = result.GetValueOrDefault("RtnMsg") ?? "cancel failed", raw = txt });
            }

            // 清除已建立託運資訊，但保留選店，方便後續重建
            logi.AllPayLogisticsId = null;
            logi.ShipmentNo = null;
            logi.Status = null;
            logi.RtnCode = null;
            logi.RtnMsg = null;
            logi.UpdatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;
            await _db.SaveChangesAsync();

            return Ok(new { success = true });
        }

        public record ChangeStoreRequest(int OrderId, string LogisticsSubType, string StoreId, string StoreName, string Address, string Telephone, bool RecreateShipment);
        /// <summary>
        /// 變更門市：必要時會先取消既有託運單，再覆寫門市資訊；若 RecreateShipment=true，並自動重建託運單。
        /// </summary>
        [HttpPost("c2c/change-store")]
        [Authorize]
        public async Task<IActionResult> ChangeStore([FromBody] ChangeStoreRequest req)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized(new { message = "未登入" });
            var order = await _db.Orders.Include(o => o.OrderLogistic).FirstOrDefaultAsync(o => o.Id == req.OrderId);
            if (order == null) return NotFound("order not found");
            if (order.MemberId != mid.Value) return Forbid();
            var logi = order.OrderLogistic;
            if (logi == null)
            {
                logi = new OrderLogistic
                {
                    OrderId = order.Id,
                    Provider = "ECPAY",
                    LogisticsType = "CVS",
                    CreatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now
                };
                _db.OrderLogistics.Add(logi);
            }

            // 若已有託運單，先取消
            if (!string.IsNullOrWhiteSpace(logi.AllPayLogisticsId))
            {
                var (url, fields) = _logi.BuildCancelC2CFields(logi.AllPayLogisticsId);
                var client = _httpClientFactory.CreateClient();
                var resp = await client.PostAsync(url, new FormUrlEncodedContent(fields));
                var txt = await resp.Content.ReadAsStringAsync();
                var result = txt.Split('&', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Split('=', 2)).Where(a => a.Length == 2)
                    .ToDictionary(a => a[0], a => Uri.UnescapeDataString(a[1]));
                if (!result.TryGetValue("RtnCode", out var rtn) || rtn != "1")
                {
                    return BadRequest(new { success = false, message = result.GetValueOrDefault("RtnMsg") ?? "cancel failed", raw = txt });
                }
                // 清除託運單欄位
                logi.AllPayLogisticsId = null;
                logi.ShipmentNo = null;
                logi.Status = null;
                logi.RtnCode = null;
                logi.RtnMsg = null;
            }

            // 覆寫門市
            logi.LogisticsSubType = req.LogisticsSubType;
            logi.PickupStoreId = req.StoreId;
            logi.PickupStoreName = req.StoreName;
            logi.PickupAddress = req.Address;
            logi.PickupTelephone = req.Telephone;
            logi.UpdatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;
            // 同步把訂單 ShippingAddress 存成「品牌 門市名 地址」
            order.ShippingAddress = ComposeCvsAddress(logi.LogisticsSubType, logi.PickupStoreName, logi.PickupAddress);
            order.UpdatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;
            await _db.SaveChangesAsync();

            // 需要重建託運單
            if (req.RecreateShipment)
            {
                var merchantTradeNo = $"SL{DateTime.UtcNow:yyMMddHHmmss}{Random.Shared.Next(0, 999999):D6}";
                var (url, fields) = _logi.BuildCreateC2CFields(
                    merchantTradeNo,
                    req.LogisticsSubType,
                    logi.IsCollection,
                    goodsAmount: (int)order.TotalAmount,
                    receiverName: order.RecipientName ?? "Receiver",
                    receiverPhone: order.RecipientPhone ?? string.Empty,
                    receiverEmail: string.Empty,
                    receiverStoreId: logi.PickupStoreId ?? string.Empty
                );
                var client = _httpClientFactory.CreateClient();
                var resp = await client.PostAsync(url, new FormUrlEncodedContent(fields));
                var resultText = await resp.Content.ReadAsStringAsync();
                var resultDict = resultText.Split('&', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Split('=', 2))
                    .Where(arr => arr.Length == 2)
                    .ToDictionary(arr => arr[0], arr => Uri.UnescapeDataString(arr[1]));
                if (!resultDict.TryGetValue("RtnCode", out var rtn) || rtn != "1")
                {
                    var msg = resultDict.GetValueOrDefault("RtnMsg") ?? "create failed";
                    return BadRequest(new { success = false, message = msg, raw = resultText });
                }
                logi.AllPayLogisticsId = resultDict.GetValueOrDefault("AllPayLogisticsID");
                logi.ShipmentNo = resultDict.GetValueOrDefault("ShipmentNo");
                logi.RtnCode = 1;
                logi.RtnMsg = resultDict.GetValueOrDefault("RtnMsg");
                logi.Status = "Created";
                logi.UpdatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;
                await _db.SaveChangesAsync();
            }

            return Ok(new { success = true, shipmentNo = logi.ShipmentNo });
        }

        /// <summary>
        /// 綠界物流狀態通知（ServerReplyURL）
        /// </summary>
        [HttpPost("notify")]
        [Consumes("application/x-www-form-urlencoded")]
        public async Task<IActionResult> Notify()
        {
            var dict = Request.Form.Keys.ToDictionary(k => k, k => Request.Form[k].ToString());
            _logger.LogInformation("ECPay Logistics Notify: {@dict}", dict);

            if (dict.ContainsKey("CheckMacValue") && !_logi.VerifyCheckMac(dict))
                return Content("0|CheckMacValue verify failed");

            var allPayLogisticsId = dict.GetValueOrDefault("AllPayLogisticsID");
            var rtnCodeStr = dict.GetValueOrDefault("RtnCode");
            int.TryParse(rtnCodeStr, out var rtnCode);
            var rtnMsg = dict.GetValueOrDefault("RtnMsg");
            var shipmentNo = dict.GetValueOrDefault("ShipmentNo");

            var ol = await _db.OrderLogistics
                .FirstOrDefaultAsync(x => x.AllPayLogisticsId == allPayLogisticsId || x.ShipmentNo == shipmentNo);
            if (ol != null)
            {
                ol.RtnCode = rtnCode;
                ol.RtnMsg = rtnMsg;
                ol.ShipmentNo = string.IsNullOrEmpty(ol.ShipmentNo) ? shipmentNo : ol.ShipmentNo;
                ol.Status = rtnCode == 1 ? "InTransit" : ol.Status;
                ol.UpdatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;
                await _db.SaveChangesAsync();

                // 若為超商取貨付款（代收），嘗試於取貨/付款完成時更新訂單為「已付款」
                try
                {
                    if (ol.IsCollection)
                    {
                        // 經驗法則：RtnMsg 可能包含「取貨」、「取貨成功」、「已領取」等字樣
                        var msg = (rtnMsg ?? string.Empty);
                        if (msg.Contains("取貨", StringComparison.OrdinalIgnoreCase) ||
                            msg.Contains("付款", StringComparison.OrdinalIgnoreCase) ||
                            msg.Contains("已領取", StringComparison.OrdinalIgnoreCase))
                        {
                            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == ol.OrderId);
                            if (order != null)
                            {
                                // 以名稱查詢狀態 Id（已付款）
                                var paidStatus = await _db.OrderStatuses
                                    .Where(s => s.StatusName == "已付款")
                                    .Select(s => new { s.Id })
                                    .FirstOrDefaultAsync();
                                if (paidStatus != null)
                                {
                                    order.StatusId = paidStatus.Id;
                                }
                                order.PaymentDate = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;
                                order.UpdatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;
                                await _db.SaveChangesAsync();

                                // 發送付款成功通知信（COD 完成付款，背景投遞）
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
                                    _logger.LogError(ex, "send COD payment success email failed for OrderId={OrderId}", order.Id);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "更新 COD 訂單付款狀態時發生錯誤");
                }
            }

            return Content("1|OK");
        }

        /// <summary>
        /// 取得訂單物流資訊
        /// </summary>
        [HttpGet("order/{orderId:int}")]
        [Authorize]
        public async Task<IActionResult> GetOrderLogistic(int orderId)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized(new { message = "未登入" });
            var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null || order.MemberId != mid.Value)
                return NotFound(new { exists = false, orderId });

            var ol = await _db.OrderLogistics.AsNoTracking().FirstOrDefaultAsync(x => x.OrderId == orderId);
            if (ol == null) return Ok(new { exists = false, orderId });
            return Ok(new
            {
                exists = true,
                orderId = ol.OrderId,
                provider = ol.Provider,
                type = ol.LogisticsType,
                subType = ol.LogisticsSubType,
                storeId = ol.PickupStoreId,
                storeName = ol.PickupStoreName,
                address = ol.PickupAddress,
                telephone = ol.PickupTelephone,
                logisticsId = ol.AllPayLogisticsId,
                shipmentNo = ol.ShipmentNo,
                rtnCode = ol.RtnCode,
                rtnMsg = ol.RtnMsg,
                status = ol.Status
            });
        }

        /// <summary>
        /// 取得超商門市清單（供自製 UI 使用）。
        /// query: subType=UNIMARTC2C|FAMIC2C|HILIFEC2C, keyword=關鍵字, city=縣市, district=區
        /// </summary>
        [HttpGet("stores")]
        public async Task<IActionResult> GetStores(
            [FromQuery] string subType = "UNIMARTC2C",
            [FromQuery] string? keyword = null,
            [FromQuery] string? city = null,
            [FromQuery] string? district = null,
            [FromQuery] int debug = 0,
            [FromQuery] bool withGeo = false)
        {
            _logger.LogInformation("GetStores query -> subType={subType}, keyword={keyword}, city={city}, district={district}", subType, keyword, city, district);
            // 將城市/區一併傳給服務層，便於樣本資料符合過濾條件
            var list = await _logi.GetStoreListAsync(subType, keyword, city, district);

            // 依地址本地過濾（ECPay 測試 API 未必提供地區過濾）- 加入台/臺容錯
            bool ContainsAny(string? text, IEnumerable<string> kws)
            {
                if (string.IsNullOrWhiteSpace(text)) return false;
                foreach (var k in kws) if (!string.IsNullOrWhiteSpace(k) && text.Contains(k, StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }
            IEnumerable<string> Variants(string? s)
            {
                if (string.IsNullOrWhiteSpace(s)) yield break;
                var t = s.Trim();
                yield return t;
                yield return t.Replace('台', '臺');
                yield return t.Replace('臺', '台');
            }
            if (!string.IsNullOrWhiteSpace(city))
                list = list.Where(s => ContainsAny(s.Address, Variants(city))).ToList();
            if (!string.IsNullOrWhiteSpace(district))
                list = list.Where(s => ContainsAny(s.Address, Variants(district))).ToList();

            _logger.LogInformation("GetStores result count: {count}", list?.Count ?? 0);

            // 先嘗試用 DB 既有座標補齊（不對外呼叫），避免 0,0 與首次無座標的地圖空白
            if (list != null && list.Count > 0)
            {
                var ids = list.Select(s => (s.StoreId ?? string.Empty).Trim()).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (ids.Count > 0)
                {
                    var rows = await _db.LogisticsStoreGeocodes
                        .Where(x => x.Provider == "ECPAY" && x.LogisticsSubType == subType && ids.Contains(x.StoreId))
                        .Select(x => new { x.StoreId, x.Latitude, x.Longitude })
                        .ToListAsync();
                    var geoDict = rows
                        .Where(r => r.Latitude != 0m && r.Longitude != 0m)
                        .ToDictionary(r => r.StoreId!, r => (lat: (double)r.Latitude, lng: (double)r.Longitude), StringComparer.OrdinalIgnoreCase);
                    foreach (var s in list)
                    {
                        if (s.Lat.HasValue && s.Lng.HasValue && !(s.Lat.Value == 0 && s.Lng.Value == 0)) continue;
                        var sid = s.StoreId?.Trim() ?? string.Empty;
                        if (geoDict.TryGetValue(sid, out var pos))
                        {
                            s.Lat = pos.lat;
                            s.Lng = pos.lng;
                        }
                        else
                        {
                            // 避免前端繪到 (0,0)
                            if (s.Lat.HasValue && s.Lng.HasValue && (s.Lat.Value == 0 && s.Lng.Value == 0))
                            {
                                s.Lat = null; s.Lng = null;
                            }
                        }
                    }
                }
            }

            if (withGeo && (list?.Count > 0))
            {
                // 確保每筆都有 lat/lng（缺的才會打 Nominatim 並寫入 DB）
                var dict = await _geocoder.EnsureStoreGeocodesAsync(_db, provider: "ECPAY", logisticsSubType: subType, stores: list);
                foreach (var s in list)
                {
                    // 僅在沒有或為 (0,0) 時才覆寫
                    if (s.Lat.HasValue && s.Lng.HasValue && !(Math.Abs(s.Lat.Value) < 1e-6 && Math.Abs(s.Lng.Value) < 1e-6)) continue;
                    if (dict.TryGetValue(s.StoreId ?? string.Empty, out var pos))
                    {
                        s.Lat = pos.lat;
                        s.Lng = pos.lng;
                    }
                }
            }
            if (debug == 1)
            {
                // 同步回傳簡易診斷資訊
                var probe = await _logi.ProbeStoreListAsync(subType);
                return Ok(new { query = new { subType, keyword, city, district, withGeo }, count = list?.Count ?? 0, list, probe });
            }
            return Ok(list);
        }

        /// <summary>
        /// 偵錯：直接探測綠界 Helper/GetStoreList（僅供開發用途）。
        /// </summary>
        [HttpGet("stores/probe")]
        public async Task<IActionResult> ProbeStores([FromQuery] string subType = "UNIMARTC2C")
        {
            var probe = await _logi.ProbeStoreListAsync(subType);
            return Ok(probe);
        }

        /// <summary>
        /// 將綠界門市清單直接快取到 LogisticsStoreGeocodes（不做座標轉換）。
        /// 用於先建立資料，之後再補地理編碼。
        /// </summary>
        [HttpPost("stores/cache")]
        public async Task<IActionResult> CacheStores(
            [FromQuery] string subType = "UNIMARTC2C",
            [FromQuery] string? keyword = null,
            [FromQuery] string? city = null,
            [FromQuery] string? district = null)
        {
            try
            {
                var stores = await _logi.GetStoreListAsync(subType, keyword, city, district);
                var now = DateTime.UtcNow;
                int inserted = 0, updated = 0;
                foreach (var s in stores)
                {
                    var sid = s.StoreId?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(sid)) continue;
                    var row = await _db.LogisticsStoreGeocodes
                        .FirstOrDefaultAsync(x => x.Provider == "ECPAY" && x.LogisticsSubType == subType && x.StoreId == sid);
                    if (row == null)
                    {
                        row = new LogisticsStoreGeocode
                        {
                            Provider = "ECPAY",
                            LogisticsSubType = subType,
                            StoreId = sid,
                            StoreName = s.StoreName?.Trim() ?? string.Empty,
                            Address = s.Address?.Trim() ?? string.Empty,
                            AddressNormalized = null,
                            AddressHash = null,
                            Latitude = 0m,
                            Longitude = 0m,
                            Geocoder = "None",
                            RawJson = null,
                            Status = "PENDING",
                            ErrorMessage = null,
                            City = ExtractCitySimple(s.Address),
                            District = ExtractDistrictSimple(s.Address),
                            PostalCode = null,
                            CreatedAt = now,
                            UpdatedAt = now,
                            LastGeocodedAt = now
                        };
                        _db.LogisticsStoreGeocodes.Add(row);
                        inserted++;
                    }
                    else
                    {
                        // 僅同步名稱與地址等基本欄位，不變更座標
                        row.StoreName = s.StoreName?.Trim() ?? row.StoreName;
                        row.Address = s.Address?.Trim() ?? row.Address;
                        row.City = ExtractCitySimple(row.Address);
                        row.District = ExtractDistrictSimple(row.Address);
                        row.UpdatedAt = now;
                        updated++;
                    }
                }
                await _db.SaveChangesAsync();
                return Ok(new { success = true, subType, count = stores.Count, inserted, updated });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CacheStores failed");
                return Problem("cache stores failed");
            }
        }

        private static string? ExtractCitySimple(string? address)
        {
            var s = address ?? string.Empty;
            if (string.IsNullOrWhiteSpace(s)) return null;
            var i = s.IndexOf('市');
            if (i < 0) i = s.IndexOf('縣');
            return i > 0 ? s.Substring(0, i + 1) : null;
        }

        private static string? ExtractDistrictSimple(string? address)
        {
            var s = address ?? string.Empty;
            if (string.IsNullOrWhiteSpace(s)) return null;
            var pos = s.IndexOf('區');
            if (pos > 0)
            {
                var cityPos = Math.Max(s.IndexOf('市'), s.IndexOf('縣'));
                if (cityPos >= 0 && pos > cityPos) return s.Substring(cityPos + 1, pos - cityPos);
                return s.Substring(0, pos + 1);
            }
            return null;
        }

        /// <summary>
        /// 一次抓取（預設）所有品牌的全量門市（不加城市/區篩選），直接快取到 DB，不做地理編碼。
        /// subTypes 以逗號分隔，例如：UNIMARTC2C,FAMIC2C,HILIFEC2C
        /// </summary>
        [HttpPost("stores/cache/all")]
        public async Task<IActionResult> CacheAllStores([FromQuery] string subTypes = "UNIMARTC2C,FAMIC2C,HILIFEC2C")
        {
            var list = (subTypes ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (list.Count == 0) return BadRequest("no subTypes provided");

            var overall = new List<object>();
            int grandInserted = 0, grandUpdated = 0, grandCount = 0;
            var now = DateTime.UtcNow;

            foreach (var subType in list)
            {
                try
                {
                    var stores = await _logi.GetStoreListAsync(subType, keyword: null, city: null, district: null);
                    int inserted = 0, updated = 0, total = stores.Count;
                    int batch = 0;
                    foreach (var s in stores)
                    {
                        var sid = s.StoreId?.Trim() ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(sid)) continue;
                        var row = await _db.LogisticsStoreGeocodes
                            .FirstOrDefaultAsync(x => x.Provider == "ECPAY" && x.LogisticsSubType == subType && x.StoreId == sid);
                        if (row == null)
                        {
                            row = new LogisticsStoreGeocode
                            {
                                Provider = "ECPAY",
                                LogisticsSubType = subType,
                                StoreId = sid,
                                StoreName = s.StoreName?.Trim() ?? string.Empty,
                                Address = s.Address?.Trim() ?? string.Empty,
                                AddressNormalized = null,
                                AddressHash = null,
                                Latitude = 0m,
                                Longitude = 0m,
                                Geocoder = "None",
                                RawJson = null,
                                Status = "PENDING",
                                ErrorMessage = null,
                                City = ExtractCitySimple(s.Address),
                                District = ExtractDistrictSimple(s.Address),
                                PostalCode = null,
                                CreatedAt = now,
                                UpdatedAt = now,
                                LastGeocodedAt = now
                            };
                            _db.LogisticsStoreGeocodes.Add(row);
                            inserted++;
                        }
                        else
                        {
                            row.StoreName = s.StoreName?.Trim() ?? row.StoreName;
                            row.Address = s.Address?.Trim() ?? row.Address;
                            row.City = ExtractCitySimple(row.Address);
                            row.District = ExtractDistrictSimple(row.Address);
                            row.UpdatedAt = now;
                            // 不改動座標與 geocoding 欄位
                            updated++;
                        }

                        batch++;
                        if (batch % 500 == 0)
                        {
                            await _db.SaveChangesAsync();
                        }
                    }
                    await _db.SaveChangesAsync();

                    overall.Add(new { subType, total, inserted, updated });
                    grandInserted += inserted;
                    grandUpdated += updated;
                    grandCount += total;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "CacheAllStores failed for {subType}", subType);
                    overall.Add(new { subType, error = "failed" });
                }
            }

            return Ok(new { success = true, subTypes = list, summary = overall, totals = new { count = grandCount, inserted = grandInserted, updated = grandUpdated } });
        }

        /// <summary>
        /// 批次回填 AddressNormalized/AddressHash（不呼叫外部 geocoding）。
        /// 可選擇篩選品牌（subTypes，逗號分隔）。
        /// </summary>
        [HttpPost("stores/backfill-address-hash")]
        public async Task<IActionResult> BackfillAddressHash([FromQuery] string? subTypes = null, [FromQuery] int batchSize = 1000)
        {
            if (batchSize <= 0 || batchSize > 5000) batchSize = 1000;

            var subTypeSet = (subTypes ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var q = _db.LogisticsStoreGeocodes.AsQueryable();
            if (subTypeSet.Count > 0)
                q = q.Where(x => subTypeSet.Contains(x.LogisticsSubType));

            // 僅針對 AddressNormalized/AddressHash 為空或 Address 有變動者
            q = q.Where(x => string.IsNullOrEmpty(x.AddressNormalized) || string.IsNullOrEmpty(x.AddressHash));

            int total = await q.CountAsync();
            int updated = 0;
            var now = DateTime.UtcNow;

            _logger.LogInformation("BackfillAddressHash start: total={total}, batchSize={batchSize}, subTypes=[{subTypes}]", total, batchSize, string.Join(',', subTypeSet));

            // 由於資料庫對 AddressHash 有唯一索引（且非 NULL），必須避免重複雜湊寫入。
            // 先載入現有的非空雜湊到集合中，以便跳過重複。
            var existingHashes = await _db.LogisticsStoreGeocodes
                .Where(x => x.AddressHash != null && x.AddressHash != "")
                .Select(x => x.AddressHash!)
                .AsNoTracking()
                .ToListAsync();
            var usedHashes = new HashSet<string>(existingHashes, StringComparer.OrdinalIgnoreCase);

            for (int skip = 0; skip < total; skip += batchSize)
            {
                var batch = await q
                    .OrderBy(x => x.Id)
                    .Skip(skip)
                    .Take(batchSize)
                    .ToListAsync();
                if (batch.Count == 0) break;

                foreach (var row in batch)
                {
                    var addr = row.Address?.Trim() ?? string.Empty;
                    var norm = NominatimGeocodingService.NormalizeForCache(addr);
                    row.AddressNormalized = norm;

                    // 空白地址：略過雜湊，避免產生相同空字串雜湊造成唯一索引衝突
                    if (!string.IsNullOrWhiteSpace(norm))
                    {
                        var hash = NominatimGeocodingService.HashForCache(norm);
                        // 若雜湊已存在於其他列，為避免唯一索引衝突，則保留為 NULL（待後續人工處理或以地址群組化處理）
                        if (!usedHashes.Contains(hash))
                        {
                            row.AddressHash = hash;
                            usedHashes.Add(hash);
                        }
                        else
                        {
                            row.AddressHash = null; // 讓 stats 顯示仍缺少，避免寫入失敗
                        }
                    }
                    else
                    {
                        row.AddressHash = null;
                    }
                    // 也順手補 City/District（若為空）
                    row.City ??= ExtractCitySimple(addr);
                    row.District ??= ExtractDistrictSimple(addr);
                    row.UpdatedAt = now;
                }

                try
                {
                    await _db.SaveChangesAsync();
                    updated += batch.Count;
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "BackfillAddressHash SaveChanges failed (skip={skip}, batchSize={batchSize}) due to possible unique constraint on AddressHash.", skip, batchSize);
                    // 若仍有個別資料造成衝突，改以逐筆嘗試寫入並略過失敗的紀錄，盡量完成其餘更新
                    foreach (var row in batch)
                    {
                        try
                        {
                            _db.Entry(row).State = EntityState.Modified;
                            await _db.SaveChangesAsync();
                            updated++;
                        }
                        catch
                        {
                            // 還原衝突列的 AddressHash 以避免卡死
                            row.AddressHash = null;
                            try { await _db.SaveChangesAsync(); } catch { /* ignore */ }
                        }
                    }
                }
            }

            _logger.LogInformation("BackfillAddressHash done: total={total}, updated={updated}, subTypes=[{subTypes}]", total, updated, string.Join(',', subTypeSet));
            return Ok(new { success = true, total, updated });
        }

        /// <summary>
        /// 查詢 AddressNormalized/AddressHash 的補齊狀態（可用 subTypes 過濾）。
        /// </summary>
        [HttpGet("stores/hash-stats")]
        public async Task<IActionResult> GetHashStats([FromQuery] string? subTypes = null)
        {
            var subTypeSet = (subTypes ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var q = _db.LogisticsStoreGeocodes.AsQueryable();
            if (subTypeSet.Count > 0)
                q = q.Where(x => subTypeSet.Contains(x.LogisticsSubType));

            var total = await q.CountAsync();
            var missingHash = await q.Where(x => string.IsNullOrEmpty(x.AddressHash)).CountAsync();
            var missingNormalized = await q.Where(x => string.IsNullOrEmpty(x.AddressNormalized)).CountAsync();

            _logger.LogInformation("HashStats: total={total}, missingHash={missingHash}, missingNormalized={missingNormalized}, subTypes=[{subTypes}]", total, missingHash, missingNormalized, string.Join(',', subTypeSet));
            return Ok(new { total, missingHash, missingNormalized, subTypes = subTypeSet.ToArray() });
        }
    }
}