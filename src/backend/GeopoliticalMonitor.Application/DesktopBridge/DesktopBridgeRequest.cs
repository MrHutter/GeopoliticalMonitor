namespace GeopoliticalMonitor.Application.DesktopBridge;

public sealed record DesktopBridgeRequest(
    string Type,
    string? PreferenceKey = null,
    string? PreferenceValue = null,
    string? EventId = null,
    string? XPostUrl = null,
    string? ReliefWebQueryText = null,
    string? ReliefWebFromDateUtc = null,
    string? ReliefWebToDateUtc = null,
    string? ReliefWebCountryIso3 = null,
    string? ReliefWebReportId = null,
    string? WatchlistId = null,
    string? WatchlistName = null,
    bool? WatchlistIsMember = null,
    string? AlertId = null,
    bool? AlertRuleEnabled = null,
    double? FirmsWest = null,
    double? FirmsSouth = null,
    double? FirmsEast = null,
    double? FirmsNorth = null);
