using System;

namespace FUEN42Team3.Backend.Models.Services
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
                var utc = dt.ToUniversalTime();
                return TimeZoneInfo.ConvertTimeFromUtc(utc, TZ);
            }
            if (dt.Kind == DateTimeKind.Unspecified)
            {
                var utc = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                return TimeZoneInfo.ConvertTimeFromUtc(utc, TZ);
            }
            return TimeZoneInfo.ConvertTimeFromUtc(dt, TZ);
        }
    }
}
