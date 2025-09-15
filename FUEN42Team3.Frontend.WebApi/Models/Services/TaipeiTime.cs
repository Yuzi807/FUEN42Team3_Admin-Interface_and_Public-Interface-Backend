using System;

namespace FUEN42Team3.Frontend.WebApi.Models.Services
{
    public static class TaipeiTime
    {
        private static TimeZoneInfo? _tz;
        private static TimeZoneInfo TZ => _tz ??= ResolveTz();

        private static TimeZoneInfo ResolveTz()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time"); }
            catch { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei"); }
        }

        public static DateTime Now => TimeZoneInfo.ConvertTime(DateTime.UtcNow, TZ);

        public static DateTime ToTaipei(DateTime dt)
        {
            if (dt.Kind == DateTimeKind.Local)
            {
                // Assume local is already in machine TZ; convert to Taipei explicitly
                var utc = dt.ToUniversalTime();
                return TimeZoneInfo.ConvertTimeFromUtc(utc, TZ);
            }
            if (dt.Kind == DateTimeKind.Unspecified)
            {
                // Treat as UTC then convert
                var utc = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                return TimeZoneInfo.ConvertTimeFromUtc(utc, TZ);
            }
            // Utc
            return TimeZoneInfo.ConvertTimeFromUtc(dt, TZ);
        }

        public static DateTime ParseAsTaipei(string s)
        {
            if (DateTime.TryParse(s, out var dt))
            {
                if (dt.Kind == DateTimeKind.Unspecified)
                {
                    // Treat parsed value as Taipei local time
                    var unspecified = DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
                    var offset = new DateTimeOffset(unspecified, TZ.GetUtcOffset(DateTime.UtcNow));
                    return offset.DateTime;
                }
                return ToTaipei(dt);
            }
            return Now;
        }
    }
}
