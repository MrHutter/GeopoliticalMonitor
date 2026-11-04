using GeopoliticalMonitor.Application.Firms;

namespace GeopoliticalMonitor.Application.DesktopBridge;

public sealed class FirmsRefreshBridgeHandler(FirmsRefreshService refreshService, Func<FirmsKeyStatus>? keyStatus = null)
{
    public const string NotConfiguredMessage = "NASA FIRMS is not configured. Set a key in Settings, Data sources.";
    public const string KeyRejectedMessage = "NASA FIRMS rejected the key. Check it in Settings, Data sources. Existing layer data was preserved.";
    public const string MalformedKeyRejectedMessage = "NASA FIRMS rejected the key, which is not 32 hexadecimal characters. Check it in Settings, Data sources. Existing layer data was preserved.";

    public async Task<string> RefreshAsync(
        DesktopBridgeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.FirmsWest is null
            || request.FirmsSouth is null
            || request.FirmsEast is null
            || request.FirmsNorth is null)
        {
            return DesktopBridgeProtocol.CreateFirmsRefreshErrorResponseJson(
                FirmsRefreshStatus.InvalidViewport,
                Message(FirmsRefreshStatus.InvalidViewport));
        }

        var result = await refreshService.RefreshAsync(
            new FirmsViewport(
                request.FirmsWest.Value,
                request.FirmsSouth.Value,
                request.FirmsEast.Value,
                request.FirmsNorth.Value),
            cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? DesktopBridgeProtocol.CreateFirmsRefreshResultResponseJson(result.Snapshot!)
            : DesktopBridgeProtocol.CreateFirmsRefreshErrorResponseJson(result.Status, Message(result.Status));
    }

    private string Message(FirmsRefreshStatus status) => status switch
    {
        FirmsRefreshStatus.KeyRejected => KeyLooksMalformed() ? MalformedKeyRejectedMessage : KeyRejectedMessage,
        FirmsRefreshStatus.RateLimited => "NASA FIRMS reported too many requests for this key. Wait a few minutes and refresh again. Existing layer data was preserved.",
        FirmsRefreshStatus.TimedOut => "NASA FIRMS did not respond in time. Refresh again later. Existing layer data was preserved.",
        FirmsRefreshStatus.ProductUnavailable => "NASA FIRMS did not accept a requested VIIRS product. Refresh again later. Existing layer data was preserved.",
        FirmsRefreshStatus.AlreadyInProgress => "A NASA FIRMS refresh is already in progress.",
        FirmsRefreshStatus.ConfigurationUnavailable => NotConfiguredMessage,
        FirmsRefreshStatus.InvalidViewport => "The current map viewport is invalid.",
        FirmsRefreshStatus.AreaTooLarge => "Zoom in to a smaller area before refreshing NASA FIRMS.",
        FirmsRefreshStatus.ResponseTooLarge => "NASA FIRMS returned too much data for this area. Zoom in and refresh again.",
        FirmsRefreshStatus.InvalidProviderResponse => "NASA FIRMS returned an unsupported response. Existing layer data was preserved.",
        FirmsRefreshStatus.TooManyObservations => "Too many FIRMS detections for this area. Zoom in and refresh again.",
        FirmsRefreshStatus.Cancelled => "NASA FIRMS refresh was cancelled.",
        _ => "NASA FIRMS refresh failed. Existing layer data was preserved.",
    };

    private bool KeyLooksMalformed()
    {
        try
        {
            return keyStatus?.Invoke().FormatWarning == true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
