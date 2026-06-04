using System;

namespace System
{
    public static class DateTimeExtensions
    {
        public static DateTime ToTurkeyTime(this DateTime dateTime)
        {
            try
            {
                // Europe/Istanbul is the standard IANA timezone ID for Turkey (works on Linux/Render)
                var turkeyZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
                return TimeZoneInfo.ConvertTime(dateTime, turkeyZone);
            }
            catch
            {
                try
                {
                    // Turkey Standard Time is the Windows timezone ID (fallback)
                    var turkeyZoneWindows = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
                    return TimeZoneInfo.ConvertTime(dateTime, turkeyZoneWindows);
                }
                catch
                {
                    // If timezone database is not available, default to adding 3 hours as Turkey is permanently UTC+3
                    return dateTime.Kind == DateTimeKind.Utc 
                        ? dateTime.AddHours(3) 
                        : DateTime.SpecifyKind(dateTime, DateTimeKind.Utc).AddHours(3);
                }
            }
        }
    }
}
