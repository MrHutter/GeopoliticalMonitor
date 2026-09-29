using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class XWebViewEnvironmentPolicyTests
{
    [Fact]
    public void PrivilegedAppEnvironmentDoesNotEnableExtensionsOrOverlayScrollbar()
    {
        var configuration = XWebViewEnvironmentPolicy.ForPrivilegedApp();

        Assert.False(configuration.BrowserExtensionsEnabled);
        Assert.Equal(WebViewScrollbarStyle.Default, configuration.ScrollbarStyle);
    }

    [Fact]
    public void ExtensionCapableHostUsesExtensionEnvironmentWithoutFluentOverlayScrollbar()
    {
        var decision = XWebViewEnvironmentPolicy.ForIsolatedXBrowser(XBrowserExtensionHostCapability.Supported);

        Assert.Equal(XBrowserExtensionStartupPath.ExtensionEnabled, decision.Path);
        Assert.True(decision.AttemptExtensionEnvironment);
        Assert.True(decision.Environment.BrowserExtensionsEnabled);
        Assert.Equal(WebViewScrollbarStyle.Default, decision.Environment.ScrollbarStyle);
    }

    [Fact]
    public void CurrentValidatedWinUiHostUsesExtensionEnabledEnvironment()
    {
        var decision = XWebViewEnvironmentPolicy.ForCurrentWinUi3Host();

        Assert.Equal(XBrowserExtensionStartupPath.ExtensionEnabled, decision.Path);
        Assert.True(decision.AttemptExtensionEnvironment);
        Assert.True(decision.Environment.BrowserExtensionsEnabled);
        Assert.Equal(WebViewScrollbarStyle.Default, decision.Environment.ScrollbarStyle);
    }

    [Fact]
    public void UnsupportedHostAlsoAvoidsExtensionProbeAndFluentOverlay()
    {
        var decision = XWebViewEnvironmentPolicy.ForIsolatedXBrowser(XBrowserExtensionHostCapability.Unsupported);

        Assert.Equal(XBrowserExtensionStartupPath.UnsupportedHost, decision.Path);
        Assert.False(decision.AttemptExtensionEnvironment);
        Assert.False(decision.Environment.BrowserExtensionsEnabled);
        Assert.Equal(WebViewScrollbarStyle.Default, decision.Environment.ScrollbarStyle);
    }

    [Fact]
    public void IsolatedXFallbackKeepsDefaultEnvironmentAndScrollbar()
    {
        var decision = XWebViewEnvironmentPolicy.ForIsolatedXBrowserFallback();

        Assert.Equal(XBrowserExtensionStartupPath.FallbackAfterFailure, decision.Path);
        Assert.False(decision.AttemptExtensionEnvironment);
        Assert.False(decision.Environment.BrowserExtensionsEnabled);
        Assert.Equal(WebViewScrollbarStyle.Default, decision.Environment.ScrollbarStyle);
    }
}
