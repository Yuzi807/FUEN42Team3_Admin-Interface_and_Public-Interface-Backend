using System.Text;
using FUEN42Team3.Backend.Models.EfModels;

namespace FUEN42Team3.Frontend.WebApi.Models.Services
{
    public static class OrderNotificationEmailBuilder
    {
        private const decimal FreeShippingThreshold = 2000m; // 滿 2000 免運（與前端一致）

        public static string BuildOrderCreatedEmail(string recipientName, Order order)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<html><body style='font-family: Arial, sans-serif;'>");
            sb.AppendLine($"<h2>親愛的 {Escape(recipientName)} 您好</h2>");
            sb.AppendLine("<p>感謝您的訂購！以下是您的訂單資訊：</p>");
            sb.AppendLine($"<p><strong>訂單編號：</strong>{Escape(order.OrderNumber)}</p>");
            sb.AppendLine($"<p><strong>下單時間：</strong>{order.CreatedAt:yyyy/MM/dd HH:mm:ss}</p>");
            // 配送資訊與付款資訊（版面與欄位對齊前台詳情頁樣式）
            AppendShippingPanel(sb, order);
            AppendPaymentPanel(sb, order);

            sb.AppendLine("<h3>商品明細</h3>");
            sb.AppendLine("<table cellpadding='8' cellspacing='0' style='border-collapse: collapse; width: 100%;'>");
            sb.AppendLine("<thead><tr style='background:#f5f5f5'>" +
                          "<th align='left'>商品</th>" +
                          "<th align='right'>原價</th>" +
                          "<th align='right'>單價</th>" +
                          "<th align='right'>數量</th>" +
                          "<th align='right'>小計</th>" +
                          "</tr></thead><tbody>");
            decimal originalSubtotal = 0m;
            foreach (var d in order.OrderDetails)
            {
                var originalPrice = d.Product?.BasePrice ?? d.UnitPrice;
                var lineOriginal = originalPrice * d.Quantity;
                originalSubtotal += lineOriginal;
                sb.AppendLine("<tr>" +
                              $"<td>{Escape(d.ProductName)}</td>" +
                              $"<td align='right'>NT$ {originalPrice:N0}</td>" +
                              $"<td align='right'>NT$ {d.UnitPrice:N0}</td>" +
                              $"<td align='right'>{d.Quantity}</td>" +
                              $"<td align='right'>NT$ {lineOriginal:N0}</td>" +
                              "</tr>");
            }
            sb.AppendLine("</tbody></table>");

            sb.AppendLine("<h3>金額摘要</h3>");
            sb.AppendLine("<table cellpadding='6' cellspacing='0' style='width: 100%;'>");
            var discount = order.DiscountAmount ?? 0m;
            var points = (decimal)order.UsedPoints;
            var shipping = order.ShippingFee;
            // 以資料庫保存的總額為主，避免前端/後端計算差異
            var total = order.TotalAmount;
            sb.AppendLine($"<tr><td>商品小計（原價）</td><td align='right'>NT$ {originalSubtotal:N0}</td></tr>");
            if (discount > 0)
                sb.AppendLine($"<tr><td>折扣</td><td align='right'>-NT$ {discount:N0}</td></tr>");
            if (points > 0)
                sb.AppendLine($"<tr><td>點數折抵</td><td align='right'>-NT$ {points:N0}</td></tr>");
            // 滿 2000 免運：以商品小計扣除折扣（不含點數）判斷
            var amountForFreeShipping = originalSubtotal - discount;
            var effectiveShipping = amountForFreeShipping >= FreeShippingThreshold ? 0m : shipping;
            var shippingDisplay = effectiveShipping <= 0 ? "免費" : $"NT$ {effectiveShipping:N0}";
            sb.AppendLine($"<tr><td>運費</td><td align='right'>{shippingDisplay}</td></tr>");
            sb.AppendLine($"<tr><td><strong>應付總額</strong></td><td align='right'><strong>NT$ {total:N0}</strong></td></tr>");
            sb.AppendLine("</table>");

            sb.AppendLine("<p>您可至會員中心查看訂單詳情與後續進度。</p>");
            sb.AppendLine("<p style='font-size: 12px; color: #888;'>此為系統自動通知信，請勿直接回覆。</p>");
            sb.AppendLine("<p style='font-size: 0.9em;'>魔型仔官方團隊敬上</p>");
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        public static string BuildPaymentSuccessEmail(string recipientName, Order order)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<html><body style='font-family: Arial, sans-serif;'>");
            sb.AppendLine($"<h2>付款成功通知</h2>");
            sb.AppendLine($"<p>親愛的 {Escape(recipientName)}，您的付款已成功入帳。</p>");
            sb.AppendLine($"<p><strong>訂單編號：</strong>{Escape(order.OrderNumber)}</p>");
            var payTime = order.PaymentDate ?? FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;
            sb.AppendLine($"<p><strong>付款時間：</strong>{payTime.ToString("yyyy/MM/dd HH:mm:ss")}</p>");
            // 顯示付款資訊面板（含付款狀態與信用卡遮罩）
            AppendPaymentPanel(sb, order);
            // 同步展示配送資訊，與前台保持一致
            AppendShippingPanel(sb, order);
            sb.AppendLine("<h3>金額資訊</h3>");
            sb.AppendLine("<table cellpadding='6' cellspacing='0' style='width: 100%;'>");
            decimal subtotal2 = 0m;
            foreach (var d in order.OrderDetails)
            {
                var originalPrice = d.Product?.BasePrice ?? d.UnitPrice;
                subtotal2 += (originalPrice * d.Quantity);
            }
            var discount2 = order.DiscountAmount ?? 0m;
            var points2 = (decimal)order.UsedPoints;
            var shipping2 = order.ShippingFee;
            // 以資料庫保存的總額為主，避免顯示與實際入帳不一致
            var total2 = order.TotalAmount;
            sb.AppendLine($"<tr><td>商品小計（原價）</td><td align='right'>NT$ {subtotal2:N0}</td></tr>");
            if (discount2 > 0)
                sb.AppendLine($"<tr><td>折扣</td><td align='right'>-NT$ {discount2:N0}</td></tr>");
            if (points2 > 0)
                sb.AppendLine($"<tr><td>點數折抵</td><td align='right'>-NT$ {points2:N0}</td></tr>");
            var amountForFreeShipping2 = subtotal2 - discount2;
            var effectiveShipping2 = amountForFreeShipping2 >= FreeShippingThreshold ? 0m : shipping2;
            var shippingDisplay2 = effectiveShipping2 <= 0 ? "免費" : $"NT$ {effectiveShipping2:N0}";
            sb.AppendLine($"<tr><td>運費</td><td align='right'>{shippingDisplay2}</td></tr>");
            sb.AppendLine($"<tr><td><strong>實付金額</strong></td><td align='right'><strong>NT$ {total2:N0}</strong></td></tr>");
            sb.AppendLine("</table>");

            sb.AppendLine("<p>我們將盡速為您安排出貨，感謝您的支持。</p>");
            sb.AppendLine("<p style='font-size: 12px; color: #888;'>此為系統自動通知信，請勿直接回覆。</p>");
            sb.AppendLine("<p style='font-size: 0.9em;'>魔型仔官方團隊敬上</p>");
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        public static string BuildLogisticsCreatedEmail(string recipientName, Order order, OrderLogistic logistic)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<html><body style='font-family: Arial, sans-serif;'>");
            sb.AppendLine("<h2>物流建立通知</h2>");
            sb.AppendLine($"<p>親愛的 {Escape(recipientName)}，您的訂單已建立物流託運單。</p>");
            sb.AppendLine($"<p><strong>訂單編號：</strong>{Escape(order.OrderNumber)}</p>");
            if (!string.IsNullOrWhiteSpace(logistic.ShipmentNo))
            {
                sb.AppendLine($"<p><strong>託運單號：</strong>{Escape(logistic.ShipmentNo)}</p>");
            }

            sb.AppendLine("<h3>取件資訊</h3>");
            sb.AppendLine("<ul>");
            if (!string.IsNullOrWhiteSpace(logistic.PickupStoreName))
                sb.AppendLine($"<li>門市：{Escape(logistic.PickupStoreName)}</li>");
            if (!string.IsNullOrWhiteSpace(logistic.PickupAddress))
                sb.AppendLine($"<li>地址：{Escape(logistic.PickupAddress)}</li>");
            if (!string.IsNullOrWhiteSpace(logistic.PickupTelephone))
                sb.AppendLine($"<li>電話：{Escape(logistic.PickupTelephone)}</li>");
            if (!string.IsNullOrWhiteSpace(logistic.LogisticsSubType))
            {
                var brand = MapLogisticsSubType(logistic.LogisticsSubType);
                sb.AppendLine($"<li>物流方式：{Escape(brand)}</li>");
            }
            sb.AppendLine("</ul>");

            sb.AppendLine("<p>包裹進入物流流程後，請留意取件通知簡訊或 Email。</p>");
            sb.AppendLine("<p style='font-size: 12px; color: #888;'>此為系統自動通知信，請勿直接回覆。</p>");
            sb.AppendLine("<p style='font-size: 0.9em;'>魔型仔官方團隊敬上</p>");
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private static string Escape(string? s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return System.Net.WebUtility.HtmlEncode(s);
        }

        // 配送資訊面板（支援宅配與超商取貨），標籤與排序對齊前台詳情頁
        private static void AppendShippingPanel(StringBuilder sb, Order order)
        {
            if (order == null) return;

            var ol = order.OrderLogistic;
            bool isCvs = (order.DeliveryMethodId == 2 || order.DeliveryMethodId == 3 || order.DeliveryMethodId == 4 ||
                          (ol != null && (ol.LogisticsSubType ?? string.Empty).ToUpperInvariant().Contains("C2C")));

            sb.AppendLine("<h3 style='margin-top:24px'>配送資訊</h3>");
            sb.AppendLine("<div style='border:1px solid #eee;border-radius:8px;padding:12px;'>");

            var recName = order.RecipientName ?? string.Empty;
            var recPhone = order.RecipientPhone ?? string.Empty;

            if (isCvs)
            {
                var brand = MapLogisticsSubType(ol?.LogisticsSubType);
                var addr = string.IsNullOrWhiteSpace(ol?.PickupAddress) ? order.ShippingAddress : ol!.PickupAddress;
                var addrDisplay = string.IsNullOrWhiteSpace(brand) ? addr : ($"{brand} {addr}").Trim();

                sb.AppendLine($"<div>配送方式：{Escape((brand == string.Empty ? "超商" : brand) + "取貨")}</div>");
                if (!string.IsNullOrWhiteSpace(addrDisplay)) sb.AppendLine($"<div>門市地址：{Escape(addrDisplay)}</div>");
                if (!string.IsNullOrWhiteSpace(recName)) sb.AppendLine($"<div>收件人：{Escape(recName)}</div>");
                if (!string.IsNullOrWhiteSpace(recPhone)) sb.AppendLine($"<div>電話：{Escape(recPhone)}</div>");
            }
            else
            {
                var full = CombineZipAddress(order.ShippingZipCode, order.ShippingAddress);
                sb.AppendLine("<div>配送方式：宅配到府</div>");
                if (!string.IsNullOrWhiteSpace(full)) sb.AppendLine($"<div>地址：{Escape(full)}</div>");
                if (!string.IsNullOrWhiteSpace(recName)) sb.AppendLine($"<div>收件人：{Escape(recName)}</div>");
                if (!string.IsNullOrWhiteSpace(recPhone)) sb.AppendLine($"<div>電話：{Escape(recPhone)}</div>");
            }

            sb.AppendLine("</div>");
        }

        private static string MapLogisticsSubType(string? subType)
        {
            var t = (subType ?? string.Empty).Trim().ToUpperInvariant();
            return t switch
            {
                "UNIMARTC2C" => "7-11",
                "FAMIC2C" => "全家",
                "HILIFEC2C" => "萊爾富",
                "OKMARTC2C" => "OK",
                _ => string.IsNullOrEmpty(t) ? string.Empty : t
            };
        }

        // 嘗試推斷/格式化付款方式（面板用文案）
        private static string GetPaymentMethodDisplay(Order order, string fallbackIfUnknown = "-")
        {
            try
            {
                var ptype = (order?.EcpayPayment?.PaymentType ?? string.Empty).Trim().ToUpperInvariant();
                if (!string.IsNullOrEmpty(ptype))
                {
                    if (ptype.Contains("CREDIT")) return "信用卡付款";
                    if (ptype.Contains("ATM")) return "ATM 轉帳";
                    if (ptype.Contains("CVS")) return "超商代碼繳費";
                    if (ptype.Contains("BARCODE")) return "超商條碼繳費";
                }

                // 後備：若關聯載入，使用資料表名稱
                var name = order?.PaymentMethod?.MethodName;
                if (!string.IsNullOrWhiteSpace(name)) return name;
            }
            catch { }
            return fallbackIfUnknown;
        }

        private static string CombineZipAddress(string? zip, string? address)
        {
            var z = (zip ?? string.Empty).Trim();
            var a = (address ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(z) && a.StartsWith(z))
            {
                a = a.Substring(z.Length).TrimStart();
            }
            var full = ($"{z} {a}").Trim();
            return full;
        }

        // 付款資訊面板：付款方式、付款狀態、信用卡遮罩
        private static void AppendPaymentPanel(StringBuilder sb, Order order)
        {
            if (order == null) return;

            var method = GetPaymentMethodDisplay(order, fallbackIfUnknown: "-");
            var status = GetPaymentStatus(order);
            var badge = RenderPaymentStatusBadge(status);
            var isCredit = method.Contains("信用卡");
            var cardMasked = isCredit ? GetMaskedCard(order) : string.Empty;

            sb.AppendLine("<h3 style='margin-top:24px'>付款資訊</h3>");
            sb.AppendLine("<div style='border:1px solid #eee;border-radius:8px;padding:12px;'>");
            sb.AppendLine($"<div>付款方式：{Escape(method)}{(method == "-" ? "（待選擇）" : string.Empty)}</div>");
            sb.AppendLine($"<div>付款狀態：{badge}</div>");
            if (isCredit)
            {
                sb.AppendLine($"<div>信用卡：{Escape(cardMasked)}</div>");
            }
            sb.AppendLine("</div>");
        }

        private static string GetPaymentStatus(Order order)
        {
            try
            {
                var ps = (order?.EcpayPayment?.PayStatus ?? string.Empty).Trim().ToLowerInvariant();
                if (ps.Contains("paid") || ps.Contains("success")) return "paid";
                if (ps.Contains("refund")) return "refunded";
                if (ps.Contains("fail")) return "failed";
            }
            catch { }
            return "pending";
        }

        private static string RenderPaymentStatusBadge(string status)
        {
            // inline badge style for email clients
            return status switch
            {
                "paid" => "<span style='display:inline-block;background:#c8e6c9;color:#2e7d32;padding:3px 8px;border-radius:4px;'>已付款</span>",
                "failed" => "<span style='display:inline-block;background:#ffcdd2;color:#c62828;padding:3px 8px;border-radius:4px;'>付款失敗</span>",
                "refunded" => "<span style='display:inline-block;background:#e0e0e0;color:#424242;padding:3px 8px;border-radius:4px;'>已退款</span>",
                _ => "<span style='display:inline-block;background:#ffe0b2;color:#e65100;padding:3px 8px;border-radius:4px;'>待付款</span>"
            };
        }

        private static string GetMaskedCard(Order order)
        {
            // 盡力從 ExtraInfo 解析卡號末四碼或遮罩；失敗則回傳通用遮罩
            try
            {
                var json = order?.EcpayPayment?.ExtraInfo;
                if (!string.IsNullOrWhiteSpace(json))
                {
                    // 容錯解析為不區分大小寫的字典
                    var doc = System.Text.Json.JsonDocument.Parse(json);
                    string? tryGet(params string[] keys)
                    {
                        foreach (var k in keys)
                        {
                            if (doc.RootElement.TryGetProperty(k, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String)
                                return v.GetString();
                        }
                        return null;
                    }

                    // 常見鍵：Card4No/card4no、MaskedPan
                    var last4 = tryGet("Card4No", "card4no", "CardNo4", "cardNo4");
                    if (!string.IsNullOrWhiteSpace(last4)) return $"**** **** **** {last4}";

                    var masked = tryGet("MaskedPan", "maskedPan", "masked");
                    if (!string.IsNullOrWhiteSpace(masked)) return masked!;
                }
            }
            catch { }
            return "***** ***** *****";
        }
    }
}