namespace FerryWinget.Core.Helpers;

using System;

public static class TaipeiTimeHelper
{
    private static readonly TimeZoneInfo TaipeiZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");

    public static DateTimeOffset Now =>
        TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TaipeiZone);

    public static DateTimeOffset Convert(DateTimeOffset utc) =>
        TimeZoneInfo.ConvertTime(utc, TaipeiZone);

    public static string FormatTimestamp(DateTimeOffset? time = null) =>
        (time ?? Now).ToString("yyyy-MM-dd HH:mm:ss zzz");
}
