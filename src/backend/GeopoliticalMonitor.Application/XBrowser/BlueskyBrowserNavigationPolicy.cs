namespace GeopoliticalMonitor.Application.XBrowser;

public static class BlueskyBrowserDefaults
{
    public const string HomeUrl = "https://bsky.app/";
    public const string Host = "bsky.app";
}

/// <summary>
/// Bluesky pane navigation, mirroring <see cref="XBrowserNavigationPolicy"/>: top-level HTTPS to the exact host bsky.app
/// stays in the pane, other HTTPS opens in the system browser, and everything else is blocked.
/// </summary>
public static class BlueskyBrowserNavigationPolicy
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

        return string.Equals(uri.Host, BlueskyBrowserDefaults.Host, StringComparison.OrdinalIgnoreCase)
            ? XBrowserNavigationDecision.AllowInPane
            : XBrowserNavigationDecision.OpenExternally;
    }

    public static bool IsAllowedInPane(string? uriText)
    {
        return ClassifyTopLevelNavigation(uriText) == XBrowserNavigationDecision.AllowInPane;
    }
}
