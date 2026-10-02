using GeopoliticalMonitor.Application.Ucdp;

namespace GeopoliticalMonitor.Application.Tests;

internal static class UcdpTestData
{
    public static readonly UcdpRelease August2026 = new(2026, 8);
    public static readonly DateTimeOffset RetrievedAt = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

    public static UcdpCandidateRecord Record(
        long id = 1001,
        int typeOfViolence = 1,
        int wherePrecision = 1,
        double latitude = 15.5,
        double longitude = 32.5,
        int best = 12,
        int low = 10,
        int high = 15,
        string dateStart = "2026-08-03",
        string? dateEnd = null,
        string whereCoordinates = "Khartoum town",
        string country = "Sudan",
        string region = "Africa",
        string sideA = "Government of Sudan",
        string sideB = "RSF",
        string dyadName = "Government of Sudan - RSF",
        string codeStatus = "Clear")
    {
        var start = DateOnly.Parse(dateStart);
        var end = dateEnd is null ? start : DateOnly.Parse(dateEnd);
        return new UcdpCandidateRecord(
            id,
            codeStatus,
            typeOfViolence,
            dyadName,
            sideA,
            sideB,
            wherePrecision,
            whereCoordinates,
            latitude,
            longitude,
            country,
            region,
            1,
            start,
            end,
            best,
            high,
            low,
            "1",
            "3");
    }
}
