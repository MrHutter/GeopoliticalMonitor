namespace GeopoliticalMonitor.Application.XBrowser;

public enum XBrowserNavigationDecision
{
    AllowInPane,
    OpenExternally,
    Block,
}

public static class XBrowserNavigationPolicy
{
    public static XBrowserNavigationDecision ClassifyTopLevelNavigation(string? uriText)
    {
        if (!Uri.TryCreate(uriText, UriKind.Absolute, out var uri))
        {
            return XBrowserNavigationDecision.Block;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return XBrowserNavigationDecision.Block;
        }

        return IsAllowedXHost(uri.Host)
            ? XBrowserNavigationDecision.AllowInPane
            : XBrowserNavigationDecision.OpenExternally;
    }

    public static bool IsAllowedInPane(string? uriText)
    {
        return ClassifyTopLevelNavigation(uriText) == XBrowserNavigationDecision.AllowInPane;
    }

    public static bool IsSafeExternalNavigation(string? uriText)
    {
        return Uri.TryCreate(uriText, UriKind.Absolute, out var uri)
            && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAllowedXHost(string host)
    {
        return string.Equals(host, XBrowserDefaults.PrimaryHost, StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, XBrowserDefaults.WwwHost, StringComparison.OrdinalIgnoreCase);
    }
}