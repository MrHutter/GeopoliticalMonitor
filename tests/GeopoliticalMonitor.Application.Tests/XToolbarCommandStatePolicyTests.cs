using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class XToolbarCommandStatePolicyTests
{
    [Fact]
    public void DisablesInitializedCommandsBeforeXWebViewIsReady()
    {
        var state = new XToolbarCommandState(XBrowserRuntimeState.NotStarted, CanGoBack: false, CanGoForward: false, CurrentUrl: null);

        Assert.False(XToolbarCommandStatePolicy.IsEnabled(XToolbarCommand.Home, state));
        Assert.False(XToolbarCommandStatePolicy.IsEnabled(XToolbarCommand.Refresh, state));
        Assert.False(XToolbarCommandStatePolicy.IsEnabled(XToolbarCommand.ClearSession, state));
        Assert.False(XToolbarCommandStatePolicy.IsEnabled(XToolbarCommand.AdBlocker, state));
    }

    [Fact]
    public void EnablesBaselineCommandsAtXHomeButNotCopyPostLink()
    {
        var state = new XToolbarCommandState(XBrowserRuntimeState.Ready, CanGoBack: false, CanGoForward: false, CurrentUrl: "https://x.com/home");

        Assert.True(XToolbarCommandStatePolicy.IsEnabled(XToolbarCommand.Home, state));
        Assert.True(XToolbarCommandStatePolicy.IsEnabled(XToolbarCommand.Refresh, state));
        Assert.True(XToolbarCommandStatePolicy.IsEnabled(XToolbarCommand.OpenExternal, state));
        Assert.True(XToolbarCommandStatePolicy.IsEnabled(XToolbarCommand.ClearSession, state));
        Assert.True(XToolbarCommandStatePolicy.IsEnabled(XToolbarCommand.AdBlocker, state));
        Assert.False(XToolbarCommandStatePolicy.IsEnabled(XToolbarCommand.CopyPostLink, state));
    }

    [Fact]
    public void EnablesCopyPostLinkOnlyForStatusUrls()
    {
        var state = new XToolbarCommandState(XBrowserRuntimeState.Ready, CanGoBack: false, CanGoForward: false, CurrentUrl: "https://x.com/example/status/12345");

        Assert.True(XToolbarCommandStatePolicy.IsEnabled(XToolbarCommand.CopyPostLink, state));
    }

    [Fact]
    public void BackAndForwardFollowHistoryAvailability()
    {
        var state = new XToolbarCommandState(XBrowserRuntimeState.Ready, CanGoBack: true, CanGoForward: true, CurrentUrl: "https://x.com/home");

        Assert.True(XToolbarCommandStatePolicy.IsEnabled(XToolbarCommand.Back, state));
        Assert.True(XToolbarCommandStatePolicy.IsEnabled(XToolbarCommand.Forward, state));
    }

    [Fact]
    public void EnablesOpenExternalWhenXInitializationFailed()
    {
        var state = new XToolbarCommandState(XBrowserRuntimeState.Failed, CanGoBack: false, CanGoForward: false, CurrentUrl: null);

        Assert.True(XToolbarCommandStatePolicy.IsEnabled(XToolbarCommand.OpenExternal, state));
        Assert.False(XToolbarCommandStatePolicy.IsEnabled(XToolbarCommand.Refresh, state));
        Assert.False(XToolbarCommandStatePolicy.IsEnabled(XToolbarCommand.ClearSession, state));
    }
    [Fact]
    public void OverflowPanelWidthStaysInsideAvailablePane()
    {
        Assert.Equal(190, XOverflowPanelPolicy.GetPanelWidth(420));
        Assert.Equal(160, XOverflowPanelPolicy.GetPanelWidth(180));
        Assert.Equal(150, XOverflowPanelPolicy.GetPanelWidth(100));
    }
}
