using System.Globalization;

namespace GeopoliticalMonitor.Application.XBrowser;

public static class XPostSnowflakeTimestamp
{
    public const long EpochUnixMilliseconds = 1_288_834_974_657;
    public static readonly TimeSpan FutureClockSkewTolerance = TimeSpan.FromMinutes(5);

    private static readonly DateTimeOffset EpochUtc = DateTimeOffset.FromUnixTimeMilliseconds(EpochUnixMilliseconds);

    public static bool TryDecode(string? statusId, DateTimeOffset nowUtc, out DateTimeOffset publishedAtUtc)
    {
        publishedAtUtc = default;
        if (string.IsNullOrWhiteSpace(statusId)
            || statusId.Any(character => character is < '0' or > '9')
            || !ulong.TryParse(statusId, NumberStyles.None, CultureInfo.InvariantCulture, out var numericStatusId))
        {
            return false;
        }

        var unixMilliseconds = (numericStatusId >> 22) + (ulong)EpochUnixMilliseconds;
        if (unixMilliseconds > long.MaxValue)
        {
            return false;
        }

        DateTimeOffset decoded;
        try
        {
            decoded = DateTimeOffset.FromUnixTimeMilliseconds((long)unixMilliseconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        var futureBoundary = nowUtc.ToUniversalTime() + FutureClockSkewTolerance;
        if (decoded < EpochUtc || decoded > futureBoundary)
        {
            return false;
        }

        publishedAtUtc = decoded;
        return true;
    }
}
