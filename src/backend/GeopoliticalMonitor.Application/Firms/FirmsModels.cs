using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace GeopoliticalMonitor.Application.Firms;

public enum FirmsSource
{
    ViirsNoaa20Nrt,
    ViirsNoaa21Nrt,
}

public static class FirmsSources
{
    public const string ViirsNoaa20Nrt = "VIIRS_NOAA20_NRT";
    public const string ViirsNoaa21Nrt = "VIIRS_NOAA21_NRT";

    public static readonly IReadOnlyList<FirmsSource> All =
    [
        FirmsSource.ViirsNoaa20Nrt,
        FirmsSource.ViirsNoaa21Nrt,
    ];

    public static string ToProviderValue(this FirmsSource source) => source switch
    {
        FirmsSource.ViirsNoaa20Nrt => ViirsNoaa20Nrt,
        FirmsSource.ViirsNoaa21Nrt => ViirsNoaa21Nrt,
        _ => throw new ArgumentOutOfRangeException(nameof(source)),
    };
}

public sealed record FirmsViewport(double West, double South, double East, double North);

public sealed record FirmsBoundingBox(double West, double South, double East, double North);

public sealed record FirmsObservation(
    string Id,
    double Latitude,
    double Longitude,
    DateTimeOffset AcquiredAtUtc,
    string Source,
    string Satellite,
    string Confidence,
    double Frp,
    double BrightnessTemperatureI4,
    double? BrightnessTemperatureI5,
    double Scan,
    double Track,
    string ProcessingVersion,
    string DayNight);

public sealed record FirmsObservationBatch(
    IReadOnlyList<FirmsObservation> Observations,
    int SkippedMalformedRecords);

public sealed record FirmsSnapshot(
    IReadOnlyList<FirmsObservation> Observations,
    DateTimeOffset RefreshedAtUtc,
    DateTimeOffset? OldestAcquiredAtUtc,
    DateTimeOffset? NewestAcquiredAtUtc,
    FirmsViewport Coverage,
    int SkippedMalformedRecords);

public enum FirmsFeedFailureKind
{
    ConfigurationUnavailable,
    RequestFailed,
    TimedOut,
    ResponseTooLarge,
    InvalidProviderResponse,
    TooManyObservations,
    KeyRejected,
    RateLimited,
    ProductUnavailable,
}

public sealed class FirmsFeedException : Exception
{
    public FirmsFeedException(FirmsFeedFailureKind kind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    public FirmsFeedFailureKind Kind { get; }
}

public enum FirmsRefreshStatus
{
    Succeeded,
    AlreadyInProgress,
    ConfigurationUnavailable,
    InvalidViewport,
    AreaTooLarge,
    RequestFailed,
    ResponseTooLarge,
    InvalidProviderResponse,
    TooManyObservations,
    Cancelled,
    KeyRejected,
    RateLimited,
    TimedOut,
    ProductUnavailable,
}

public sealed record FirmsRefreshResult(
    FirmsRefreshStatus Status,
    FirmsSnapshot? Snapshot = null)
{
    public bool IsSuccess => Status == FirmsRefreshStatus.Succeeded && Snapshot is not null;
}

public static class FirmsObservationIdentity
{
    public static string Create(
        string source,
        string satellite,
        DateTimeOffset acquiredAtUtc,
        double latitude,
        double longitude)
    {
        var canonical = string.Join(
            "|",
            source,
            satellite,
            acquiredAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            latitude.ToString("R", CultureInfo.InvariantCulture),
            longitude.ToString("R", CultureInfo.InvariantCulture));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return $"firms-{Convert.ToHexString(hash.AsSpan(0, 16)).ToLowerInvariant()}";
    }
}
