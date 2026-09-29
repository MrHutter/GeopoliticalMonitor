namespace GeopoliticalMonitor.Application.DesktopBridge;

public static class PrivilegedBridgeMessageProcessor
{
    public static async Task<bool> TryProcessAsync(
        string? sourceUri,
        string json,
        Func<DesktopBridgeRequest, Task> dispatchAsync)
    {
        ArgumentNullException.ThrowIfNull(dispatchAsync);

        if (!PrivilegedWebViewSecurityPolicy.TryParseTrustedBridgeRequest(
                sourceUri,
                json,
                out var request,
                out _)
            || request is null)
        {
            return false;
        }

        await dispatchAsync(request);
        return true;
    }
}
