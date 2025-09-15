using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace FUEN42Team3.Frontend.WebApi.Models.Services
{
    public class EcpayService
    {
        /// <summary>
        /// 綠界服務：負責產生結帳表單欄位、驗證 CheckMacValue
        /// </summary>
        private readonly IConfiguration _config;
        private readonly ILogger<EcpayService>? _logger;

        // 透過 DI 注入設定檔（appsettings.json）
        public EcpayService(IConfiguration config, ILogger<EcpayService>? logger = null)
        {
            _config = config;
            _logger = logger;
        }

        /// <summary>
        /// 產生綠界結帳表單需要的欄位與網址
        /// </summary>
        public (string gatewayUrl, Dictionary<string, string> fields) BuildCheckoutFormFields(
            string merchantTradeNo, // 我方訂單編號（必須唯一）
            int totalAmount,        // 訂單金額
            string itemName,        // 商品名稱（多品項用 # 分隔）
            string email,           // 客戶 Email（選填）
            string paymentMethod = "Credit", // Credit|WebATM|ATM|CVS|BARCODE
            string? customField1 = null, // 可帶 orderId 供回傳比對
            string? customField2 = null,
            string? customField3 = null,
            string? customField4 = null
        )
        {
            // 從設定檔讀取綠界參數
            var merchantId = _config["Ecpay:MerchantID"] ?? "";
            var hashKey = _config["Ecpay:HashKey"] ?? "";
            var hashIV = _config["Ecpay:HashIV"] ?? "";
            // 回呼網址：允許以環境變數或設定的 PublicBaseUrl 動態組裝，避免硬編 ngrok 網域失效
            var publicBase = Environment.GetEnvironmentVariable("ECPAY_PUBLIC_BASE")
                              ?? _config["Ecpay:PublicBaseUrl"];
            string? returnURL = _config["Ecpay:ReturnURL"];
            string? paymentInfoURL = _config["Ecpay:PaymentInfoURL"];
            string? orderResultURL = _config["Ecpay:OrderResultURL"];
            if (!string.IsNullOrWhiteSpace(publicBase))
            {
                var baseUrl = publicBase.TrimEnd('/');
                // 只要提供了 PublicBase，就以它覆蓋（避免用到過期的 ngrok 網域）
                returnURL = $"{baseUrl}/api/payments/ecpay/notify";
                paymentInfoURL = $"{baseUrl}/api/payments/ecpay/payment-info";
                orderResultURL = $"{baseUrl}/api/payments/order-result";
            }
            // 前端返回頁（使用者取消或完成後返回）仍沿用設定值
            var clientBackURL = _config["Ecpay:ClientBackURL"] ?? "";   // 前端取消返回頁
            var gatewayUrl = _config["Ecpay:GatewayUrl"]
                                ?? "https://payment-stage.ecpay.com.tw/Cashier/AioCheckOut/V5";

            // 按照綠界規格建立必填欄位
            var fields = new Dictionary<string, string>
            {
                ["MerchantID"] = merchantId,
                ["MerchantTradeNo"] = merchantTradeNo,                       // 唯一單號(<=20)
                ["MerchantTradeDate"] = TaipeiTime.Now.ToString("yyyy/MM/dd HH:mm:ss"),
                ["PaymentType"] = "aio",                                  // 固定值
                ["TotalAmount"] = totalAmount.ToString(),
                // TradeDesc 需先做 UrlEncode（依綠界規範）
                ["TradeDesc"] = WebUtility.UrlEncode("購物結帳"),           // 訂單描述（已 URL Encode）
                ["ItemName"] = itemName,                              // 商品名稱
                ["ReturnURL"] = returnURL ?? string.Empty,                             // 後端回呼（必填）
                ["ChoosePayment"] = MapPaymentMethod(paymentMethod),        // 付款方式
                ["EncryptType"] = "1",                                   // SHA256
                ["NeedExtraPaidInfo"] = "Y",                                   // 是否回傳額外資訊
                ["Language"] = "zh-TW"                                // 語系
            };

            // 僅在有值時加入選填網址，避免空值造成 CheckMacValue 不一致
            if (!string.IsNullOrWhiteSpace(paymentInfoURL)) fields["PaymentInfoURL"] = paymentInfoURL;
            if (!string.IsNullOrWhiteSpace(orderResultURL)) fields["OrderResultURL"] = orderResultURL;
            if (!string.IsNullOrWhiteSpace(clientBackURL)) fields["ClientBackURL"] = clientBackURL;

            if (!string.IsNullOrWhiteSpace(email))
                fields["Email"] = email;

            // 付款方式特定參數（採用合適的安全預設值）
            var pm = fields["ChoosePayment"];
            if (pm == "ATM")
            {
                // ATM 繳費期限（天，1-60），預設 3 天
                fields["ExpireDate"] = _config["Ecpay:AtmExpireDays"] ?? "3";
            }
            else if (pm == "CVS" || pm == "BARCODE")
            {
                // 超商代碼/條碼 繳費期限（分鐘或小時，依綠界規格常見為分鐘 1-4320；部分文件亦見 StoreExpireDate 小時 1-168）
                // 這裡採分鐘 1440(=24 小時) 做預設；可用設定覆寫
                var defaultMinutes = _config["Ecpay:StoreExpireMinutes"] ?? "1440";
                fields["StoreExpireDate"] = defaultMinutes;
                // 資訊描述（選填）
                fields["Desc_1"] = _config["Ecpay:StoreDesc1"] ?? "至超商繳費";
            }

            // 自訂欄位（綠界會原樣回傳），常用於帶 orderId
            if (!string.IsNullOrWhiteSpace(customField1)) fields["CustomField1"] = customField1;
            if (!string.IsNullOrWhiteSpace(customField2)) fields["CustomField2"] = customField2;
            if (!string.IsNullOrWhiteSpace(customField3)) fields["CustomField3"] = customField3;
            if (!string.IsNullOrWhiteSpace(customField4)) fields["CustomField4"] = customField4;

            // 先移除空值欄位，避免送出空值導致綠界重算欄位不一致
            var nonEmpty = fields
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            // 計算 CheckMacValue 驗證碼
            // 簽章
            var mac = MakeCheckMacValue(nonEmpty, hashKey, hashIV, out var debugRaw);
            nonEmpty["CheckMacValue"] = mac;

            // 開發時可開啟除錯記錄（appsettings: Ecpay:DebugHash = true）
            if (bool.TryParse(_config["Ecpay:DebugHash"], out var debug) && debug)
            {
                _logger?.LogInformation("ECPay AIO Fields: {@fields}", nonEmpty);
                _logger?.LogInformation("ECPay AIO MAC Raw String: {raw}", debugRaw);
                _logger?.LogInformation("ECPay AIO CheckMacValue: {mac}", mac);
            }

            return (gatewayUrl, nonEmpty);
        }

        public string NormalizePaymentMethod(string input) => MapPaymentMethod(input);

        /// <summary>
        /// 主動查詢綠界交易狀態（QueryTradeInfo）。成功時回傳鍵值字典並已驗證 CheckMacValue；失敗回傳 null。
        /// </summary>
        public async Task<Dictionary<string, string>?> QueryTradeInfoAsync(string merchantTradeNo, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(merchantTradeNo)) return null;

            var merchantId = _config["Ecpay:MerchantID"] ?? string.Empty;
            var hashKey = _config["Ecpay:HashKey"] ?? string.Empty;
            var hashIV = _config["Ecpay:HashIV"] ?? string.Empty;
            var queryUrl = _config["Ecpay:QueryTradeInfoUrl"] ?? "https://payment-stage.ecpay.com.tw/Cashier/QueryTradeInfo/V5";

            var fields = new Dictionary<string, string>
            {
                ["MerchantID"] = merchantId,
                ["MerchantTradeNo"] = merchantTradeNo,
                ["TimeStamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()
            };

            // 計算簽章並送出查詢
            fields["CheckMacValue"] = MakeCheckMacValue(fields, hashKey, hashIV, out var raw);

            try
            {
                using var client = new HttpClient();
                using var content = new FormUrlEncodedContent(fields);
                using var resp = await client.PostAsync(queryUrl, content, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger?.LogWarning("ECPay QueryTradeInfo http {status}", resp.StatusCode);
                    return null;
                }
                var text = await resp.Content.ReadAsStringAsync(ct);
                var dict = ParseFormUrlEncoded(text);
                if (dict.Count == 0) return null;

                // 驗證回應簽章
                if (!dict.TryGetValue("CheckMacValue", out var mac) || string.IsNullOrEmpty(mac))
                    return null;

                // 注意：QueryTradeInfo 的回應常包含大量空字串欄位，綠界簽章計算會將這些欄位一併納入。
                // 因此此處必須包含空值欄位一同計算，否則會驗證失敗。
                var calc = MakeCheckMacValue(dict, hashKey, hashIV, out var respRaw, includeEmptyValues: true, sortIgnoreCase: true);
                if (!mac.Equals(calc, StringComparison.OrdinalIgnoreCase))
                {
                    var relax = false;
                    bool.TryParse(_config["Ecpay:RelaxedQueryVerify"], out relax);
                    _logger?.LogWarning("ECPay QueryTradeInfo MAC verify failed. Provided={provided}, Calc={calc}, Relaxed={relax}, resp={resp}", mac, calc, relax, text);
                    if (!relax)
                    {
                        return null;
                    }
                    // 開啟 RelaxedQueryVerify 時，放行回應（僅限開發/除錯環境），以便手動同步訂單狀態。
                }
                return dict;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ECPay QueryTradeInfo error");
                return null;
            }
        }

        private static Dictionary<string, string> ParseFormUrlEncoded(string s)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(s)) return result;
            var pairs = s.Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in pairs)
            {
                var idx = p.IndexOf('=');
                if (idx <= 0)
                {
                    var k = WebUtility.UrlDecode(p);
                    if (!string.IsNullOrEmpty(k) && !result.ContainsKey(k)) result[k] = string.Empty;
                    continue;
                }
                var key = WebUtility.UrlDecode(p.Substring(0, idx));
                var val = WebUtility.UrlDecode(p[(idx + 1)..]);
                if (!string.IsNullOrEmpty(key)) result[key] = val ?? string.Empty;
            }
            return result;
        }

        private static string MapPaymentMethod(string input)
        {
            // 接受多種命名，統一轉為綠界規格
            var key = (input ?? "").Trim().ToLowerInvariant();
            return key switch
            {
                // 信用卡
                "credit" or "credit-card" or "信用卡" => "Credit",
                // 網路ATM/銀行轉帳（使用綠界虛擬帳號）
                "webatm" or "網路atm" => "WebATM",
                "atm" or "銀行轉帳" => "ATM",
                // 超商代碼 / 超商條碼
                "cvs" or "超商代碼" => "CVS",
                "barcode" or "超商條碼" => "BARCODE",
                // 模糊/總稱
                "ecpay" or "ecpay綠界" or "綠界" => "Credit",
                // 其他支付（目前未在本流程實作，先回退信用卡避免壞流程；建議分開路由至各自流程）
                "line pay" or "linepay" or "街口支付" or "悠遊付" or "apple pay" or "google pay" or "台灣pay" => "Credit",
                _ => "Credit"
            };
        }

        /// <summary>
        /// 驗證綠界回傳的 CheckMacValue
        /// </summary>
        public bool VerifyCheckMac(IDictionary<string, string> dict)
        {
            var hashKey = _config["Ecpay:HashKey"] ?? "";
            var hashIV = _config["Ecpay:HashIV"] ?? "";

            // 沒有 CheckMacValue 就直接失敗
            if (!dict.TryGetValue("CheckMacValue", out var mac) || string.IsNullOrEmpty(mac))
                return false;

            var calc = MakeCheckMacValue(dict, hashKey, hashIV, out var raw);
            if (!mac.Equals(calc, StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogWarning("ECPay VerifyCheckMac failed. Provided={provided}, Calc={calc}, Raw={raw}", mac, calc, raw);
            }
            return mac.Equals(calc, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 提供更完整的 CheckMac 驗證資訊，用於除錯：回傳是否通過、原始提供與計算值，以及雜湊前的原始字串。
        /// </summary>
        public (bool isValid, string? provided, string expected, string rawNormalized) VerifyCheckMacDetailed(IDictionary<string, string> dict)
        {
            var hashKey = _config["Ecpay:HashKey"] ?? "";
            var hashIV = _config["Ecpay:HashIV"] ?? "";

            dict.TryGetValue("CheckMacValue", out var provided);
            var expected = MakeCheckMacValue(dict, hashKey, hashIV, out var raw);
            var ok = !string.IsNullOrEmpty(provided) && provided.Equals(expected, StringComparison.OrdinalIgnoreCase);
            if (!ok)
            {
                _logger?.LogWarning("ECPay VerifyCheckMacDetailed failed. Provided={provided}, Expected={expected}, Raw={raw}", provided, expected, raw);
            }
            return (ok, provided, expected, raw);
        }

        /// <summary>
        /// 依綠界規範計算 CheckMacValue
        /// </summary>
        private static string MakeCheckMacValue(IDictionary<string, string> dict, string hashKey, string hashIV, out string rawNormalized, bool includeEmptyValues = false, bool sortIgnoreCase = false)
        {
            // 1) 排序欄位（依鍵名）、排除空值與 CheckMacValue
            var comparer = sortIgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var raw = dict
                .Where(kv => kv.Key != "CheckMacValue" && (includeEmptyValues || !string.IsNullOrWhiteSpace(kv.Value)))
                .OrderBy(kv => kv.Key, comparer)
                .Select(kv => $"{kv.Key}={kv.Value}");

            // 2) 前後加上 HashKey/HashIV
            var s = $"HashKey={hashKey}&{string.Join("&", raw)}&HashIV={hashIV}";

            // 3) URL Encode → 轉小寫
            var encodedLower = UrlEncodeLower(s);

            // 4) 按綠界規則還原部分字元
            var normalized = EcpayNormalize(encodedLower);

            rawNormalized = normalized;

            // 5) SHA256 → 大寫
            var sha = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
            return BitConverter.ToString(sha).Replace("-", "").ToUpperInvariant();
        }

        private static string UrlEncodeLower(string input)
        {
            // .NET 的 WebUtility.UrlEncode 會將空白編碼為 %20，
            // 但綠界規範要求空白需轉為 '+' 後再進行字元還原與雜湊。
            var encoded = WebUtility.UrlEncode(input) ?? string.Empty;
            encoded = encoded.ToLowerInvariant();
            encoded = encoded.Replace("%20", "+");
            return encoded;
        }

        private static string EcpayNormalize(string s)
        {
            // 依官方文件將部分編碼還原
            return s
                .Replace("%2d", "-")
                .Replace("%5f", "_")
                .Replace("%2e", ".")
                .Replace("%21", "!")
                .Replace("%2a", "*")
                .Replace("%28", "(")
                .Replace("%29", ")");
        }
    }

}

