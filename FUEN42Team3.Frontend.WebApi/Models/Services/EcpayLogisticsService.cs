using Microsoft.Extensions.Configuration;
using System.Net;
using System.Text;

namespace FUEN42Team3.Frontend.WebApi.Models.Services
{
    public class EcpayLogisticsService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EcpayLogisticsService>? _logger;
        public EcpayLogisticsService(IConfiguration config, ILogger<EcpayLogisticsService>? logger = null)
        {
            _config = config;
            _logger = logger;
        }

        public class StoreInfo
        {
            public string LogisticsSubType { get; set; } = "UNIMARTC2C"; // UNIMARTC2C/FAMIC2C/HILIFEC2C/OKMARTC2C
            public string StoreId { get; set; } = string.Empty;
            public string StoreName { get; set; } = string.Empty;
            public string Address { get; set; } = string.Empty;
            public string Telephone { get; set; } = string.Empty;
            public double? Lat { get; set; }
            public double? Lng { get; set; }
        }

        public class StoreListDebug
        {
            public int HttpStatus { get; set; }
            public string? RtnCode { get; set; }
            public string? RtnMsg { get; set; }
            public string? Snippet { get; set; }
            public int ParsedCount { get; set; }
            public List<StoreInfo> Stores { get; set; } = new();
        }

        // --- Public APIs ---
        public async Task<List<StoreInfo>> GetStoreListAsync(string logisticsSubType, string? keyword = null, string? city = null, string? district = null)
        {
            try
            {
                var allowSample = bool.TryParse(_config["Ecpay:AllowSampleStores"], out var b) && b;
                var apiUrl = "https://logistics-stage.ecpay.com.tw/Helper/GetStoreList";

                var cvsType = MapCvsType(logisticsSubType);
                var logisticsMerchantId = _config["Ecpay:LogisticsMerchantID"] ?? "2000933";

                var form = new Dictionary<string, string>
                {
                    ["MerchantID"] = logisticsMerchantId,
                    ["CvsType"] = cvsType
                };
                if (!string.IsNullOrWhiteSpace(city)) form["City"] = city!.Trim();
                if (!string.IsNullOrWhiteSpace(district)) form["Town"] = district!.Trim();
                if (!string.IsNullOrWhiteSpace(keyword)) form["KeyWord"] = keyword!.Trim();
                // Helper/GetStoreList 依物流規格使用 MD5 簽章
                form["CheckMacValue"] = MakeCheckMacValueMd5(form);

                var responseText = await PostFormAsync(apiUrl, form);

                var parsed = ParseEcpayStoreResponse(responseText ?? string.Empty, logisticsSubType);
                if (parsed == null || parsed.Count == 0)
                {
                    // broad retry (no filters)
                    var slim = new Dictionary<string, string>
                    {
                        ["MerchantID"] = logisticsMerchantId,
                        ["CvsType"] = cvsType,
                    };
                    // 寬鬆重試亦採用 MD5 簽章
                    slim["CheckMacValue"] = MakeCheckMacValueMd5(slim);
                    var slimText = await PostFormAsync(apiUrl, slim);
                    parsed = ParseEcpayStoreResponse(slimText ?? string.Empty, logisticsSubType);
                }

                if (parsed == null || parsed.Count == 0)
                {
                    if (allowSample)
                    {
                        parsed = GetSampleStores(logisticsSubType, keyword, city, district);
                    }
                    else
                    {
                        return new List<StoreInfo>();
                    }
                }

                return ApplyLocalFilters(parsed, city, district, keyword);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetStoreListAsync failed");
                var allowSample = bool.TryParse(_config["Ecpay:AllowSampleStores"], out var b) && b;
                if (allowSample) return ApplyLocalFilters(GetSampleStores(logisticsSubType, keyword, city, district), city, district, keyword);
                return new List<StoreInfo>();
            }
        }

        public async Task<StoreListDebug> ProbeStoreListAsync(string logisticsSubType)
        {
            var result = new StoreListDebug();
            try
            {
                var apiUrl = "https://logistics-stage.ecpay.com.tw/Helper/GetStoreList";
                var cvsType = MapCvsType(logisticsSubType);
                var logisticsMerchantId = _config["Ecpay:LogisticsMerchantID"] ?? "2000933";

                var form = new Dictionary<string, string>
                {
                    ["MerchantID"] = logisticsMerchantId,
                    ["CvsType"] = cvsType,
                };
                // Probe 使用 MD5 簽章
                form["CheckMacValue"] = MakeCheckMacValueMd5(form);

                var (respText, status1) = await PostFormAsyncWithStatus(apiUrl, form);
                result.HttpStatus = status1;
                result.Snippet = respText?.Substring(0, Math.Min(800, respText?.Length ?? 0));


                try
                {
                    if (!string.IsNullOrWhiteSpace(respText) && respText.TrimStart().StartsWith("{"))
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(respText);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("RtnCode", out var rc)) result.RtnCode = rc.ValueKind == System.Text.Json.JsonValueKind.Number ? rc.GetInt32().ToString() : rc.GetString();
                        if (root.TryGetProperty("RtnMsg", out var rm)) result.RtnMsg = rm.GetString();
                    }
                }
                catch { }

                var parsed = ParseEcpayStoreResponse(respText ?? string.Empty, logisticsSubType) ?? new List<StoreInfo>();
                result.Stores = parsed;
                result.ParsedCount = parsed.Count;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ProbeStoreListAsync failed");
                result.RtnMsg = ex.Message;
            }
            return result;
        }

        // --- Helpers ---
        private async Task<(string text, int status)> PostFormAsyncWithStatus(string url, Dictionary<string, string> form)
        {
            using var httpClient = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            })
            {
                Timeout = TimeSpan.FromSeconds(15)
            };
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/json,application/x-www-form-urlencoded;q=0.9,*/*;q=0.8");
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0 Safari/537.36");
            httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-TW,zh;q=0.9,en;q=0.8");
            var refUrl = _config["Ecpay:LogisticsMapUrl"] ?? "https://logistics-stage.ecpay.com.tw/Express/map";
            httpClient.DefaultRequestHeaders.Referrer = new Uri(refUrl);

            _logger?.LogInformation("POST {url} -> {@form}", url, form);
            var resp = await httpClient.PostAsync(url, new FormUrlEncodedContent(form));
            var status = (int)resp.StatusCode;
            var text = await resp.Content.ReadAsStringAsync() ?? string.Empty;
            _logger?.LogInformation("Resp ({status}) {len} chars: {snippet}", status, text?.Length ?? 0, text?.Substring(0, Math.Min(600, text?.Length ?? 0)));
            return (text ?? string.Empty, status);
        }
        private async Task<string> PostFormAsync(string url, Dictionary<string, string> form)
        {
            var (text, _) = await PostFormAsyncWithStatus(url, form);
            return text;
        }

        private static bool LooksLikeMacError(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            try
            {
                var t = text.Trim();
                if (t.StartsWith("{"))
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(t);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("RtnCode", out var rc))
                    {
                        var code = rc.ValueKind == System.Text.Json.JsonValueKind.Number ? rc.GetInt32().ToString() : rc.GetString() ?? string.Empty;
                        var msg = root.TryGetProperty("RtnMsg", out var rm) ? rm.GetString() ?? string.Empty : string.Empty;
                        if (code == "0" && msg.Contains("CheckMacValue", StringComparison.OrdinalIgnoreCase)) return true;
                    }
                }
            }
            catch { }
            return text.Contains("CheckMacValue", StringComparison.OrdinalIgnoreCase);
        }

        private static string MapCvsType(string logisticsSubType)
        {
            if (string.IsNullOrWhiteSpace(logisticsSubType)) return "UNIMART";
            var s = logisticsSubType.Trim().ToUpperInvariant();
            if (s.Contains("UNIMART")) return "UNIMART";
            if (s.Contains("FAMI")) return "FAMI";
            if (s.Contains("HILIFE")) return "HILIFE";
            if (s.Contains("OKMART") || s.Contains("OK")) return "OKMART";
            return s;
        }

        private static List<StoreInfo> ApplyLocalFilters(List<StoreInfo> list, string? city, string? district, string? keyword)
        {
            if (list == null) return new List<StoreInfo>();
            IEnumerable<StoreInfo> q = list;
            if (!string.IsNullOrWhiteSpace(city))
            {
                var cityVariants = Variants(city);
                q = q.Where(s => ContainsAny(s.Address, cityVariants));
            }
            if (!string.IsNullOrWhiteSpace(district))
            {
                var distVariants = Variants(district);
                q = q.Where(s => ContainsAny(s.Address, distVariants));
            }
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim();
                q = q.Where(s => (s.StoreName?.Contains(kw) == true) || (s.Address?.Contains(kw) == true));
            }
            return q.ToList();
        }

        private static bool ContainsAny(string? text, IEnumerable<string> keywords)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            foreach (var k in keywords)
            {
                if (string.IsNullOrWhiteSpace(k)) continue;
                if (text.Contains(k, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static IEnumerable<string> Variants(string input)
        {
            input = input?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(input)) yield break;
            yield return input;
            yield return input.Replace('台', '臺');
            yield return input.Replace('臺', '台');
        }

        private List<StoreInfo> GetSampleStores(string logisticsSubType, string? keyword = null, string? city = null, string? district = null)
        {
            var cityName = string.IsNullOrWhiteSpace(city) ? "台北市" : city!.Trim();
            var districtName = string.IsNullOrWhiteSpace(district) ? (cityName.Contains("台北") || cityName.Contains("臺北") ? "信義區" : "市區") : district!.Trim();
            var telPrefix = cityName.Contains("台北") ? "02" : (cityName.Contains("新竹") ? "03" : (cityName.Contains("高雄") ? "07" : "02"));

            var candidates = new[] { "中央店", "車站店", "市府店", "百貨店", "文化店", "中正店", "體育館店", "東門店", "西門店", "南門店" };
            var sample = new List<StoreInfo>();
            for (int i = 0; i < candidates.Length; i++)
            {
                sample.Add(new StoreInfo
                {
                    LogisticsSubType = logisticsSubType,
                    StoreId = $"{Math.Abs(HashCode.Combine(cityName, districtName)) % 900000 + 100000:D6}{i}".Substring(0, 6),
                    StoreName = candidates[i],
                    Address = $"{cityName}{districtName}中正路{10 + i}號",
                    Telephone = $"{telPrefix}-{1000 + i:0000}-{2000 + i:0000}",
                });
            }
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim();
                sample = sample.Where(s => (s.StoreName?.Contains(kw) == true) || (s.Address?.Contains(kw) == true)).ToList();
            }
            return sample;
        }

        private List<StoreInfo> ParseEcpayStoreResponse(string responseText, string logisticsSubType)
        {
            var stores = new List<StoreInfo>();
            if (string.IsNullOrWhiteSpace(responseText)) return stores;

            try
            {
                if (responseText.TrimStart().StartsWith("{"))
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(responseText);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("RtnCode", out var rtnCodeEl))
                    {
                        var ok = false;
                        if (rtnCodeEl.ValueKind == System.Text.Json.JsonValueKind.String) ok = string.Equals(rtnCodeEl.GetString(), "1", StringComparison.Ordinal);
                        else if (rtnCodeEl.ValueKind == System.Text.Json.JsonValueKind.Number) ok = rtnCodeEl.GetInt32() == 1;

                        System.Text.Json.JsonElement listEl = default;
                        var hasList = false;
                        if (root.TryGetProperty("StoreLIST", out var listEl1) && listEl1.ValueKind == System.Text.Json.JsonValueKind.Array) { listEl = listEl1; hasList = true; }
                        else if (root.TryGetProperty("StoreList", out var listEl2) && listEl2.ValueKind == System.Text.Json.JsonValueKind.Array) { listEl = listEl2; hasList = true; }

                        if (ok && hasList)
                        {
                            var want = MapCvsType(logisticsSubType);
                            foreach (var grp in listEl.EnumerateArray())
                            {
                                var cvs = grp.TryGetProperty("CvsType", out var cvsEl) ? (cvsEl.GetString() ?? string.Empty) : string.Empty;
                                if (!string.IsNullOrEmpty(want) && !string.IsNullOrEmpty(cvs) && !string.Equals(cvs, want, StringComparison.OrdinalIgnoreCase)) continue;
                                if (grp.TryGetProperty("StoreInfo", out var infoArr) && infoArr.ValueKind == System.Text.Json.JsonValueKind.Array)
                                {
                                    foreach (var el in infoArr.EnumerateArray())
                                    {
                                        var s = new StoreInfo { LogisticsSubType = logisticsSubType };
                                        if (el.TryGetProperty("StoreId", out var v1)) s.StoreId = v1.GetString() ?? string.Empty;
                                        if (el.TryGetProperty("StoreName", out var v2)) s.StoreName = v2.GetString() ?? string.Empty;
                                        if (el.TryGetProperty("StoreAddr", out var v3)) s.Address = v3.GetString() ?? string.Empty;
                                        if (string.IsNullOrEmpty(s.Address) && el.TryGetProperty("StoreAddress", out var v3a)) s.Address = v3a.GetString() ?? string.Empty;
                                        if (string.IsNullOrEmpty(s.Address) && el.TryGetProperty("Address", out var v3b)) s.Address = v3b.GetString() ?? string.Empty;
                                        if (el.TryGetProperty("StorePhone", out var v4)) s.Telephone = v4.GetString() ?? string.Empty;
                                        if (string.IsNullOrEmpty(s.Telephone) && el.TryGetProperty("StoreTel", out var v4a)) s.Telephone = v4a.GetString() ?? string.Empty;
                                        if (string.IsNullOrEmpty(s.Telephone) && el.TryGetProperty("Telephone", out var v4b)) s.Telephone = v4b.GetString() ?? string.Empty;
                                        if (!string.IsNullOrEmpty(s.StoreId) || !string.IsNullOrEmpty(s.StoreName) || !string.IsNullOrEmpty(s.Address)) stores.Add(s);
                                    }
                                }
                            }
                            return stores;
                        }
                    }

                    string[] keys = new[] { "Stores", "stores", "Data", "data", "Result", "result" };
                    foreach (var k in keys)
                    {
                        if (root.TryGetProperty(k, out var arr) && arr.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var el in arr.EnumerateArray())
                            {
                                var s = new StoreInfo { LogisticsSubType = logisticsSubType };
                                if (el.TryGetProperty("CVSStoreID", out var v1)) s.StoreId = v1.GetString() ?? string.Empty;
                                if (el.TryGetProperty("CVSStoreName", out var v2)) s.StoreName = v2.GetString() ?? string.Empty;
                                if (el.TryGetProperty("CVSAddress", out var v3)) s.Address = v3.GetString() ?? string.Empty;
                                if (el.TryGetProperty("CVSTelephone", out var v4)) s.Telephone = v4.GetString() ?? string.Empty;
                                if (string.IsNullOrEmpty(s.StoreId) && el.TryGetProperty("StoreID", out var v7)) s.StoreId = v7.GetString() ?? string.Empty;
                                if (string.IsNullOrEmpty(s.StoreName) && el.TryGetProperty("StoreName", out var v8)) s.StoreName = v8.GetString() ?? string.Empty;
                                if (string.IsNullOrEmpty(s.Address) && el.TryGetProperty("Address", out var v9)) s.Address = v9.GetString() ?? string.Empty;
                                if (string.IsNullOrEmpty(s.Telephone) && el.TryGetProperty("Telephone", out var v10)) s.Telephone = v10.GetString() ?? string.Empty;
                                if (!string.IsNullOrEmpty(s.StoreId) || !string.IsNullOrEmpty(s.StoreName) || !string.IsNullOrEmpty(s.Address)) stores.Add(s);
                            }
                            return stores;
                        }
                    }
                }

                if (responseText.TrimStart().StartsWith("["))
                {
                    var jsonStores = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(responseText);
                    if (jsonStores != null)
                    {
                        foreach (var el in jsonStores)
                        {
                            var s = new StoreInfo { LogisticsSubType = logisticsSubType };
                            if (el.TryGetProperty("CVSStoreID", out var v1)) s.StoreId = v1.GetString() ?? string.Empty;
                            if (el.TryGetProperty("CVSStoreName", out var v2)) s.StoreName = v2.GetString() ?? string.Empty;
                            if (el.TryGetProperty("CVSAddress", out var v3)) s.Address = v3.GetString() ?? string.Empty;
                            if (el.TryGetProperty("CVSTelephone", out var v4)) s.Telephone = v4.GetString() ?? string.Empty;
                            if (string.IsNullOrEmpty(s.StoreId) && el.TryGetProperty("StoreID", out var v7)) s.StoreId = v7.GetString() ?? string.Empty;
                            if (string.IsNullOrEmpty(s.StoreName) && el.TryGetProperty("StoreName", out var v8)) s.StoreName = v8.GetString() ?? string.Empty;
                            if (string.IsNullOrEmpty(s.Address) && el.TryGetProperty("Address", out var v9)) s.Address = v9.GetString() ?? string.Empty;
                            if (string.IsNullOrEmpty(s.Telephone) && el.TryGetProperty("Telephone", out var v10)) s.Telephone = v10.GetString() ?? string.Empty;
                            if (!string.IsNullOrEmpty(s.StoreId)) stores.Add(s);
                        }
                    }
                    return stores;
                }

                var lines = responseText.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length == 1 && lines[0].Contains("<"))
                {
                    var text = System.Text.RegularExpressions.Regex.Replace(responseText, "<[^>]+>", " ");
                    lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                }

                string[] headers = Array.Empty<string>();
                int startIndex = 0;
                if (lines.Length > 0 && (lines[0].Contains(",") || lines[0].Contains("\t")))
                {
                    headers = lines[0].Split(lines[0].Contains(',') ? ',' : '\t');
                    startIndex = 1;
                }

                for (int i = startIndex; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    var s = new StoreInfo { LogisticsSubType = logisticsSubType };
                    if (headers.Length > 0)
                    {
                        var parts = line.Split(line.Contains(',') ? ',' : '\t');
                        for (int c = 0; c < Math.Min(parts.Length, headers.Length); c++)
                        {
                            MapField(headers[c], parts[c], s);
                        }
                    }
                    else
                    {
                        var pairs = line.Split('&', StringSplitOptions.RemoveEmptyEntries);
                        if (pairs.Length == 1) pairs = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var pair in pairs)
                        {
                            var kv = pair.Split('=', 2);
                            if (kv.Length == 2) MapField(kv[0], Uri.UnescapeDataString(kv[1]), s);
                        }
                    }
                    if (!string.IsNullOrEmpty(s.StoreId) || !string.IsNullOrEmpty(s.StoreName) || !string.IsNullOrEmpty(s.Address)) stores.Add(s);
                }

                return stores;
            }
            catch
            {
                return new List<StoreInfo>();
            }
        }

        private static void MapField(string name, string value, StoreInfo s)
        {
            if (string.IsNullOrEmpty(name)) return;
            var key = name.Trim();
            switch (key)
            {
                case "CVSStoreID":
                case "StoreID":
                case "store_id":
                    s.StoreId = value; break;
                case "CVSStoreName":
                case "StoreName":
                case "store_name":
                    s.StoreName = value; break;
                case "CVSAddress":
                case "Address":
                case "addr":
                    s.Address = value; break;
                case "CVSTelephone":
                case "Telephone":
                case "tel":
                    s.Telephone = value; break;
                case "Latitude":
                case "lat":
                    if (double.TryParse(value, out var lat)) s.Lat = lat; break;
                case "Longitude":
                case "lng":
                    if (double.TryParse(value, out var lng)) s.Lng = lng; break;
            }
        }

        public (string url, Dictionary<string, string> fields) BuildCvsMapPost(
            string logisticsSubType,
            string merchantTradeNo,
            string serverReplyURL,
            string extraData,
            int device = 0
        )
        {
            var merchantId = _config["Ecpay:LogisticsMerchantID"] ?? _config["Ecpay:MerchantID"] ?? string.Empty;
            var url = _config["Ecpay:LogisticsMapUrl"] ?? "https://logistics-stage.ecpay.com.tw/Express/map";
            var callback = SanitizePublicUrl(serverReplyURL);

            var dict = new Dictionary<string, string>
            {
                ["MerchantID"] = merchantId,
                ["IsCollection"] = "N",
                ["LogisticsType"] = "CVS",
                ["LogisticsSubType"] = logisticsSubType,
                ["ServerReplyURL"] = callback,
                ["Device"] = device == 1 ? "1" : "0",
                ["ExtraData"] = extraData,
                ["MerchantTradeNo"] = merchantTradeNo,
            };

            // Express/map 依物流一般規格使用 SHA256 簽章
            dict["CheckMacValue"] = MakeCheckMacValueSha256(dict);
            return (url, dict);
        }

        public bool VerifyCheckMac(Dictionary<string, string> dict)
        {
            if (!dict.TryGetValue("CheckMacValue", out var mac)) return false;
            var clone = dict.Where(kv => kv.Key != "CheckMacValue").ToDictionary(kv => kv.Key, kv => kv.Value);
            // 物流通知/回傳多為 SHA256；亦容許 MD5 比對一次，任一通過即視為成功
            var expectedSha = MakeCheckMacValueSha256(clone);
            if (string.Equals(expectedSha, mac, StringComparison.OrdinalIgnoreCase)) return true;
            var expectedMd5 = MakeCheckMacValueMd5(clone);
            return string.Equals(expectedMd5, mac, StringComparison.OrdinalIgnoreCase);
        }

        public (string url, Dictionary<string, string> fields) BuildCreateC2CFields(
            string merchantTradeNo,
            string logisticsSubType,
            bool isCollection,
            int goodsAmount,
            string receiverName,
            string receiverPhone,
            string receiverEmail,
            string receiverStoreId
        )
        {
            var merchantId = _config["Ecpay:LogisticsMerchantID"] ?? _config["Ecpay:MerchantID"] ?? string.Empty;
            var url = _config["Ecpay:LogisticsCreateUrl"] ?? "https://logistics-stage.ecpay.com.tw/Express/Create";
            var serverReplyURL = SanitizePublicUrl(_config["Ecpay:LogisticsReturnURL"] ?? string.Empty);

            var senderName = _config["Ecpay:LogisticsSenderName"] ?? "Sender";
            var senderPhone = _config["Ecpay:LogisticsSenderPhone"] ?? "";
            var senderZip = _config["Ecpay:LogisticsSenderZipCode"] ?? "";
            var senderAddress = _config["Ecpay:LogisticsSenderAddress"] ?? "";

            var dict = new Dictionary<string, string>
            {
                ["MerchantID"] = merchantId,
                ["MerchantTradeNo"] = merchantTradeNo,
                ["LogisticsType"] = "CVS",
                ["LogisticsSubType"] = logisticsSubType,
                ["IsCollection"] = isCollection ? "Y" : "N",
                ["GoodsAmount"] = goodsAmount.ToString(),
                ["SenderName"] = senderName,
                ["SenderPhone"] = senderPhone,
                ["SenderZipCode"] = senderZip,
                ["SenderAddress"] = senderAddress,
                ["ReceiverName"] = receiverName,
                ["ReceiverPhone"] = receiverPhone,
                ["ReceiverEmail"] = receiverEmail ?? string.Empty,
                ["ReceiverStoreID"] = receiverStoreId,
                ["ServerReplyURL"] = serverReplyURL,
            };

            if (isCollection) dict["CollectionAmount"] = goodsAmount.ToString();

            // Express/Create 使用 SHA256 簽章
            dict["CheckMacValue"] = MakeCheckMacValueSha256(dict);
            return (url, dict);
        }

        public (string url, Dictionary<string, string> fields) BuildCancelC2CFields(string allPayLogisticsId)
        {
            var merchantId = _config["Ecpay:LogisticsMerchantID"] ?? _config["Ecpay:MerchantID"] ?? string.Empty;
            var url = _config["Ecpay:LogisticsCancelUrl"] ?? "https://logistics-stage.ecpay.com.tw/Express/CancelC2COrder";

            var dict = new Dictionary<string, string>
            {
                ["MerchantID"] = merchantId,
                ["AllPayLogisticsID"] = allPayLogisticsId
            };

            // CancelC2COrder 使用 SHA256 簽章
            dict["CheckMacValue"] = MakeCheckMacValueSha256(dict);
            return (url, dict);
        }

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
            catch { return url ?? string.Empty; }
        }

        // Helper/GetStoreList 專用（MD5）
        private string MakeCheckMacValueMd5(Dictionary<string, string> dict)
        {
            var hashKey = _config["Ecpay:LogisticsHashKey"] ?? string.Empty;
            var hashIV = _config["Ecpay:LogisticsHashIV"] ?? string.Empty;
            var ordered = dict
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key}={kv.Value}");
            var raw = $"HashKey={hashKey}&{string.Join('&', ordered)}&HashIV={hashIV}";

            var encoded = WebUtility.UrlEncode(raw).ToLower();
            encoded = encoded.Replace("%20", "+");
            encoded = encoded
                .Replace("%2d", "-")
                .Replace("%5f", "_")
                .Replace("%2e", ".")
                .Replace("%21", "!")
                .Replace("%2a", "*")
                .Replace("%28", "(")
                .Replace("%29", ")");

            using var md5 = System.Security.Cryptography.MD5.Create();
            var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(encoded));
            var sb = new StringBuilder();
            foreach (var b in bytes) sb.Append(b.ToString("X2"));
            return sb.ToString();
        }

        // 物流 Express 系列（map/create/cancel/notify）一般採用 SHA256
        private string MakeCheckMacValueSha256(Dictionary<string, string> dict)
        {
            var hashKey = _config["Ecpay:LogisticsHashKey"] ?? string.Empty;
            var hashIV = _config["Ecpay:LogisticsHashIV"] ?? string.Empty;
            var ordered = dict
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key}={kv.Value}");
            var raw = $"HashKey={hashKey}&{string.Join('&', ordered)}&HashIV={hashIV}";

            var encoded = WebUtility.UrlEncode(raw).ToLower();
            encoded = encoded.Replace("%20", "+");
            encoded = encoded
                .Replace("%2d", "-")
                .Replace("%5f", "_")
                .Replace("%2e", ".")
                .Replace("%21", "!")
                .Replace("%2a", "*")
                .Replace("%28", "(")
                .Replace("%29", ")");

            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(encoded));
            var sb = new StringBuilder();
            foreach (var b in bytes) sb.Append(b.ToString("X2"));
            return sb.ToString();
        }
    }
}
