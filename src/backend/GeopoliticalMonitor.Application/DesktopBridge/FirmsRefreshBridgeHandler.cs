using GeopoliticalMonitor.Application.Firms;

namespace GeopoliticalMonitor.Application.DesktopBridge;

public sealed class FirmsRefreshBridgeHandler(FirmsRefreshService refreshService)
{
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

    private static string Message(FirmsRefreshStatus status) => status switch
    {
        FirmsRefreshStatus.AlreadyInProgress => "A NASA FIRMS refresh is already in progress.",
        FirmsRefreshStatus.ConfigurationUnavailable => "NASA FIRMS is not configured.",
        FirmsRefreshStatus.InvalidViewport => "The current map viewport is invalid.",
        FirmsRefreshStatus.AreaTooLarge => "Zoom in to a smaller area before refreshing NASA FIRMS.",
        FirmsRefreshStatus.ResponseTooLarge => "NASA FIRMS returned too much data for this area. Zoom in and refresh again.",
        FirmsRefreshStatus.InvalidProviderResponse => "NASA FIRMS returned an unsupported response. Existing layer data was preserved.",
        FirmsRefreshStatus.TooManyObservations => "Too many FIRMS detections for this area. Zoom in and refresh again.",
        FirmsRefreshStatus.Cancelled => "NASA FIRMS refresh was cancelled.",
        _ => "NASA FIRMS refresh failed. Existing layer data was preserved.",
    };
}
