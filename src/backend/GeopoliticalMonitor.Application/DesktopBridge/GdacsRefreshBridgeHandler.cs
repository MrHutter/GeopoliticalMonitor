using GeopoliticalMonitor.Application.Gdacs;

namespace GeopoliticalMonitor.Application.DesktopBridge;

public sealed class GdacsRefreshBridgeHandler(GdacsRefreshService refreshService)
{
    public async Task<string> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var result = await refreshService.RefreshAsync(cancellationToken).ConfigureAwait(false);
        return result.IsSuccess || result.Status == GdacsRefreshStatus.AlreadyInProgress
            ? DesktopBridgeProtocol.CreateGdacsRefreshResultResponseJson(result)
            : DesktopBridgeProtocol.CreateGdacsRefreshErrorResponseJson(result.Status, GetMessage(result.Status));
    }

    private static string GetMessage(GdacsRefreshStatus status)
    {
        return status switch
        {
            GdacsRefreshStatus.ProviderUnavailable => "GDACS is currently unavailable. Existing events were preserved.",
            GdacsRefreshStatus.TimedOut => "The GDACS request timed out. Existing events were preserved.",
            GdacsRefreshStatus.InvalidResponse => "GDACS returned an unsupported response. No provider data was changed.",
            GdacsRefreshStatus.Cancelled => "The GDACS refresh was cancelled.",
            _ => "GDACS data could not be saved. Existing events were preserved.",
        };
    }
}
