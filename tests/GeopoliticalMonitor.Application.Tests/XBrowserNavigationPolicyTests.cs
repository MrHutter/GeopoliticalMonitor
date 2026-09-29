using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class XBrowserNavigationPolicyTests
{
    [Theory]
    [InlineData("https://x.com/home")]
    [InlineData("https://x.com/user/status/123")]
    [InlineData("https://www.x.com/home")]
    public void AllowsHttpsXPagesInsidePane(string uri)
    {
        Assert.Equal(XBrowserNavigationDecision.AllowInPane, XBrowserNavigationPolicy.ClassifyTopLevelNavigation(uri));
        Assert.True(XBrowserNavigationPolicy.IsAllowedInPane(uri));
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("https://t.co/demo")]
    public void OpensHttpsNonXPagesExternally(string uri)
    {
        Assert.Equal(XBrowserNavigationDecision.OpenExternally, XBrowserNavigationPolicy.ClassifyTopLevelNavigation(uri));
        Assert.False(XBrowserNavigationPolicy.IsAllowedInPane(uri));
        Assert.True(XBrowserNavigationPolicy.IsSafeExternalNavigation(uri));
    }

    [Theory]
    [InlineData("http://x.com/")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/temp/x.html")]
    [InlineData("data:text/html,hello")]
    [InlineData("not a url")]
    public void BlocksNonHttpsAndNonWebSchemes(string uri)
    {
        Assert.Equal(XBrowserNavigationDecision.Block, XBrowserNavigationPolicy.ClassifyTopLevelNavigation(uri));
        Assert.False(XBrowserNavigationPolicy.IsAllowedInPane(uri));
        Assert.False(XBrowserNavigationPolicy.IsSafeExternalNavigation(uri));
    }

    [Fact]
    public void IsolatedSecurityConfigurationDisablesNativeBridgeAndCredentialCapture()
    {
        var settings = XBrowserSecurityConfiguration.IsolatedDefault;

        Assert.False(settings.AreHostObjectsAllowed);
        Assert.False(settings.IsWebMessageEnabled);
        Assert.False(settings.IsPasswordAutosaveEnabled);
        Assert.False(settings.IsGeneralAutofillEnabled);
        Assert.False(settings.InjectsScripts);
        Assert.False(settings.UsesDevToolsProtocol);
    }
}