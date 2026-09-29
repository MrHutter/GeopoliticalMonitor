namespace GeopoliticalMonitor.Application.XBrowser;

public sealed record XBrowserExtensionActions(
    bool CanInstall,
    bool CanEnable,
    bool CanDisable,
    bool CanRemove);

public static class XBrowserExtensionStatusPresentation
{
    public static string Format(XBrowserExtensionStatus status)
    {
        return status.Kind switch
        {
            XBrowserExtensionStatusKind.Installed => $"Status: {status.Name} - {(status.IsEnabled ? "Enabled" : "Disabled")}",
            XBrowserExtensionStatusKind.Unavailable => $"Status: {status.Message}",
            _ => "Status: AdBlock is not installed",
        };
    }

    public static XBrowserExtensionActions GetActions(XBrowserExtensionStatus status, bool extensionRuntimeAvailable)
    {
        var installed = status.Kind == XBrowserExtensionStatusKind.Installed;
        return new XBrowserExtensionActions(
            CanInstall: extensionRuntimeAvailable && status.Kind == XBrowserExtensionStatusKind.NotInstalled,
            CanEnable: extensionRuntimeAvailable && installed && !status.IsEnabled,
            CanDisable: extensionRuntimeAvailable && installed && status.IsEnabled,
            CanRemove: extensionRuntimeAvailable && installed);
    }
}
