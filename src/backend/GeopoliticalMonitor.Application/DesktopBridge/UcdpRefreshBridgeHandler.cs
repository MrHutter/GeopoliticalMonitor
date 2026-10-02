using GeopoliticalMonitor.Application.Ucdp;

namespace GeopoliticalMonitor.Application.DesktopBridge;

public sealed class UcdpRefreshBridgeHandler
{
    private readonly Func<CancellationToken, Task<UcdpRefreshResult>> refresh;

    /// <summary>Uses a refresh delegate routed through the refresh scheduler's manual tracking and the shared service.</summary>
    public UcdpRefreshBridgeHandler(Func<CancellationToken, Task<UcdpRefreshResult>> refresh)
    {
        this.refresh = refresh;
    }

    public async Task<string> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var result = await refresh(cancellationToken).ConfigureAwait(false);
        return result.IsSuccess || result.Status == UcdpRefreshStatus.AlreadyInProgress
            ? DesktopBridgeProtocol.CreateUcdpRefreshResultResponseJson(result)
            : DesktopBridgeProtocol.CreateUcdpRefreshErrorResponseJson(result.Status, GetMessage(result.Status));
    }

    public static string GetMessage(UcdpRefreshStatus status)
    {
        return status switch
        {
            UcdpRefreshStatus.ProviderUnavailable => "UCDP is currently unavailable. Existing events were preserved.",
            UcdpRefreshStatus.TimedOut => "The UCDP request timed out. Existing events were preserved.",
            UcdpRefreshStatus.InvalidResponse => "UCDP returned an unsupported response. No provider data was changed.",
            UcdpRefreshStatus.Cancelled => "The UCDP refresh was cancelled.",
            _ => "UCDP data could not be saved. Existing events were preserved.",
        };
    }
}
