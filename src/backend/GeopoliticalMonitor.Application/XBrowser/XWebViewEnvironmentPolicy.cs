namespace GeopoliticalMonitor.Application.XBrowser;

public enum WebViewScrollbarStyle
{
    Default,
    FluentOverlay,
}

public sealed record WebViewEnvironmentConfiguration(bool BrowserExtensionsEnabled, WebViewScrollbarStyle ScrollbarStyle);

public enum XBrowserExtensionHostCapability
{
    Supported,
    Unverified,
    Unsupported,
}

public enum XBrowserExtensionStartupPath
{
    ExtensionEnabled,
    DefaultEnvironment,
    UnsupportedHost,
    FallbackAfterFailure,
}

public sealed record XWebViewStartupDecision(
    XBrowserExtensionStartupPath Path,
    WebViewEnvironmentConfiguration Environment,
    bool AttemptExtensionEnvironment);

public static class XWebViewEnvironmentPolicy
{
    public static WebViewEnvironmentConfiguration ForPrivilegedApp() => new(
        BrowserExtensionsEnabled: false,
        ScrollbarStyle: WebViewScrollbarStyle.Default);

    public static XWebViewStartupDecision ForCurrentWinUi3Host() =>
        ForIsolatedXBrowser(XBrowserExtensionHostCapability.Supported);

    public static XWebViewStartupDecision ForIsolatedXBrowser(XBrowserExtensionHostCapability hostCapability)
    {
        var supported = hostCapability == XBrowserExtensionHostCapability.Supported;
        var path = hostCapability switch
        {
            XBrowserExtensionHostCapability.Supported => XBrowserExtensionStartupPath.ExtensionEnabled,
            XBrowserExtensionHostCapability.Unsupported => XBrowserExtensionStartupPath.UnsupportedHost,
            _ => XBrowserExtensionStartupPath.DefaultEnvironment,
        };
        return new XWebViewStartupDecision(
            path,
            new WebViewEnvironmentConfiguration(
                BrowserExtensionsEnabled: supported,
                ScrollbarStyle: WebViewScrollbarStyle.Default),
            AttemptExtensionEnvironment: supported);
    }

    public static XWebViewStartupDecision ForIsolatedXBrowserFallback() => new(
        XBrowserExtensionStartupPath.FallbackAfterFailure,
        new WebViewEnvironmentConfiguration(
            BrowserExtensionsEnabled: false,
            ScrollbarStyle: WebViewScrollbarStyle.Default),
        AttemptExtensionEnvironment: false);
}
