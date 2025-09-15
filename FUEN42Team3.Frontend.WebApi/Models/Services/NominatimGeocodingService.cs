using System.Security.Cryptography;
using System.Text;
using FUEN42Team3.Backend.Models.EfModels;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace FUEN42Team3.Frontend.WebApi.Models.Services
{
    /// <summary>
    /// 使用 Nominatim 進行地址 -> 座標的地理編碼，並將結果快取到 LogisticsStoreGeocodes。
    /// 僅在缺少或地址變更時才呼叫外部 API；其餘走 DB 快取。
    /// </summary>
    public class NominatimGeocodingService
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<NominatimGeocodingService>? _logger;

        public NominatimGeocodingService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<NominatimGeocodingService>? logger = null)
        {
            _httpFactory = httpFactory;
            _config = config;
            _logger = logger;
        }

        public record GeocodeResult(double Lat, double Lng, string RawJson);

        public async Task<Dictionary<string, (double lat, double lng)>> EnsureStoreGeocodesAsync(
            AppDbContext db,
            string provider,
            string logisticsSubType,
            IEnumerable<EcpayLogisticsService.StoreInfo> stores,
            CancellationToken ct = default)
        {
            var map = new Dictionary<string, (double lat, double lng)>(StringComparer.OrdinalIgnoreCase);
            if (stores == null) return map;

            foreach (var s in stores)
            {
                if (ct.IsCancellationRequested) break;
                var storeId = s.StoreId?.Trim() ?? string.Empty;
                var storeName = s.StoreName?.Trim() ?? string.Empty;
                var addressRaw = s.Address?.Trim() ?? string.Empty;
                var normalized = NormalizeAddress(addressRaw);
                var addrHash = Sha256Hex(normalized);

                // 先以 (Provider, SubType, StoreId) 找 DB
                var row = await db.LogisticsStoreGeocodes
                    .FirstOrDefaultAsync(x => x.Provider == provider && x.LogisticsSubType == logisticsSubType && x.StoreId == storeId, ct);

                if (row != null)
                {
                    // 地址未變：直接使用
                    if (!string.IsNullOrWhiteSpace(row.AddressHash) && string.Equals(row.AddressHash, addrHash, StringComparison.OrdinalIgnoreCase))
                    {
                        if (row.Latitude != 0m || row.Longitude != 0m)
                            map[storeId] = ((double)row.Latitude, (double)row.Longitude);
                        continue;
                    }
                }
                else
                {
                    // 沒有此 store，嘗試以地址快取複用
                    row = await db.LogisticsStoreGeocodes
                        .FirstOrDefaultAsync(x => x.AddressHash == addrHash, ct);
                    if (row != null)
                    {
                        // 共用地址的座標，新增一筆屬於此門市的列
                        var cloned = new LogisticsStoreGeocode
                        {
                            Provider = provider,
                            LogisticsSubType = logisticsSubType,
                            StoreId = storeId,
                            StoreName = storeName,
                            Address = addressRaw,
                            AddressNormalized = normalized,
                            AddressHash = addrHash,
                            Latitude = row.Latitude,
                            Longitude = row.Longitude,
                            Geocoder = row.Geocoder ?? "Nominatim",
                            RawJson = null,
                            Status = "OK",
                            ErrorMessage = null,
                            City = row.City,
                            District = row.District,
                            PostalCode = row.PostalCode,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            LastGeocodedAt = DateTime.UtcNow
                        };
                        db.LogisticsStoreGeocodes.Add(cloned);
                        await db.SaveChangesAsync(ct);
                        if (cloned.Latitude != 0m || cloned.Longitude != 0m)
                            map[storeId] = ((double)cloned.Latitude, (double)cloned.Longitude);
                        continue;
                    }
                }

                // 需要重跑 geocoding（新門市或地址變更）
                try
                {
                    var geo = await GeocodeAsync(addressRaw, ct);
                    if (geo != null)
                    {
                        var lat = geo.Lat;
                        var lng = geo.Lng;
                        var raw = geo.RawJson;

                        if (row == null)
                        {
                            row = new LogisticsStoreGeocode
                            {
                                Provider = provider,
                                LogisticsSubType = logisticsSubType,
                                StoreId = storeId,
                                StoreName = storeName,
                                Address = addressRaw,
                                AddressNormalized = normalized,
                                AddressHash = addrHash,
                                Latitude = (decimal)lat,
                                Longitude = (decimal)lng,
                                Geocoder = "Nominatim",
                                RawJson = raw,
                                Status = "OK",
                                ErrorMessage = null,
                                City = ExtractCity(addressRaw),
                                District = ExtractDistrict(addressRaw),
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow,
                                LastGeocodedAt = DateTime.UtcNow
                            };
                            db.LogisticsStoreGeocodes.Add(row);
                        }
                        else
                        {
                            row.StoreName = storeName;
                            row.Address = addressRaw;
                            row.AddressNormalized = normalized;
                            row.AddressHash = addrHash;
                            row.Latitude = (decimal)lat;
                            row.Longitude = (decimal)lng;
                            row.RawJson = raw;
                            row.Status = "OK";
                            row.ErrorMessage = null;
                            row.City = ExtractCity(addressRaw);
                            row.District = ExtractDistrict(addressRaw);
                            row.UpdatedAt = DateTime.UtcNow;
                            row.LastGeocodedAt = DateTime.UtcNow;
                        }

                        await db.SaveChangesAsync(ct);
                        map[storeId] = (lat, lng);
                    }
                    else
                    {
                        await MarkFailedAsync(db, row, provider, logisticsSubType, storeId, storeName, addressRaw, normalized, addrHash, "NO_RESULT", ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Geocode failed for {storeId} {addr}", storeId, addressRaw);
                    await MarkFailedAsync(db, row, provider, logisticsSubType, storeId, storeName, addressRaw, normalized, addrHash, ex.Message, ct);
                }

                // Nominatim 限流，粗略 sleep
                await Task.Delay(1100, ct);
            }

            return map;
        }

        private async Task MarkFailedAsync(
            AppDbContext db,
            LogisticsStoreGeocode? existing,
            string provider,
            string subType,
            string storeId,
            string storeName,
            string address,
            string normalized,
            string addrHash,
            string error,
            CancellationToken ct)
        {
            if (existing == null)
            {
                existing = new LogisticsStoreGeocode
                {
                    Provider = provider,
                    LogisticsSubType = subType,
                    StoreId = storeId,
                    StoreName = storeName,
                    Address = address,
                    AddressNormalized = normalized,
                    AddressHash = addrHash,
                    Geocoder = "Nominatim",
                    Status = "FAILED",
                    ErrorMessage = error,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    LastGeocodedAt = DateTime.UtcNow
                };
                db.LogisticsStoreGeocodes.Add(existing);
            }
            else
            {
                existing.Address = address;
                existing.AddressNormalized = normalized;
                existing.AddressHash = addrHash;
                existing.Status = "FAILED";
                existing.ErrorMessage = error;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.LastGeocodedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct);
        }

        public async Task<GeocodeResult?> GeocodeAsync(string address, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(address)) return null;
            var client = _httpFactory.CreateClient();
            client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
            var ua = _config["Geocoding:UserAgent"] ?? "FUEN42Team3/1.0 (contact@example.com)";
            client.DefaultRequestHeaders.UserAgent.ParseAdd(ua);

            var url = $"search?format=json&q={Uri.EscapeDataString(address)}&limit=1";
            var text = await client.GetStringAsync(url, ct);
            if (string.IsNullOrWhiteSpace(text)) return null;

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(text);
                var arr = doc.RootElement;
                if (arr.ValueKind != System.Text.Json.JsonValueKind.Array) return null;
                var first = arr.EnumerateArray().FirstOrDefault();
                if (first.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
                var latStr = first.TryGetProperty("lat", out var latEl) ? latEl.GetString() : null;
                var lonStr = first.TryGetProperty("lon", out var lonEl) ? lonEl.GetString() : null;
                if (double.TryParse(latStr, out var lat) && double.TryParse(lonStr, out var lng))
                {
                    if (!(Math.Abs(lat) < 1e-9 && Math.Abs(lng) < 1e-9))
                        return new GeocodeResult(lat, lng, text);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Parse Nominatim response failed: {snippet}", text.Substring(0, Math.Min(200, text.Length)));
            }
            return null;
        }

        private static string NormalizeAddress(string addr)
        {
            if (string.IsNullOrWhiteSpace(addr)) return string.Empty;
            var s = addr.Trim();
            s = s.Replace('台', '臺'); // 統一為「臺」
            s = s.Replace("（", "(").Replace("）", ")");
            s = System.Text.RegularExpressions.Regex.Replace(s, "\\s+", "");
            return s;
        }

        private static string Sha256Hex(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input ?? string.Empty));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        // 公開給其他端點使用（不觸發對外 API 的純規格化/雜湊工具）
        public static string NormalizeForCache(string addr) => NormalizeAddress(addr);
        public static string HashForCache(string input) => Sha256Hex(input);

        private static string? ExtractCity(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return null;
            // 粗略擷取：到「市/縣」
            var idx = address.IndexOf('市');
            if (idx < 0) idx = address.IndexOf('縣');
            if (idx > 0) return address.Substring(0, idx + 1);
            return null;
        }

        private static string? ExtractDistrict(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return null;
            var s = address;
            var pos = s.IndexOf('區');
            if (pos > 0)
            {
                // 嘗試從市/縣後取到 區
                var cityPos = Math.Max(s.IndexOf('市'), s.IndexOf('縣'));
                if (cityPos >= 0 && pos > cityPos) return s.Substring(cityPos + 1, pos - cityPos);
                return s.Substring(0, pos + 1);
            }
            return null;
        }
    }
}
