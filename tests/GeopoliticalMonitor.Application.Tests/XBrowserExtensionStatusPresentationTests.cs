using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class XBrowserExtensionStatusPresentationTests
{
    [Fact]
    public void NotInstalledAllowsOnlyInstallWhenRuntimeIsAvailable()
    {
        var actions = XBrowserExtensionStatusPresentation.GetActions(
            XBrowserExtensionStatus.NotInstalled,
            extensionRuntimeAvailable: true);

        Assert.Equal("Status: AdBlock is not installed", XBrowserExtensionStatusPresentation.Format(XBrowserExtensionStatus.NotInstalled));
        Assert.True(actions.CanInstall);
        Assert.False(actions.CanEnable);
        Assert.False(actions.CanDisable);
        Assert.False(actions.CanRemove);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    public void InstalledStateMapsToTruthfulEnableDisableActions(bool isEnabled, bool canEnable, bool canDisable)
    {
        var status = XBrowserExtensionStatus.Installed(new XBrowserExtensionInfo(
            XAdBlockExtensionPolicy.ChromeExtensionId,
            "AdBlock",
            isEnabled));

        var actions = XBrowserExtensionStatusPresentation.GetActions(status, extensionRuntimeAvailable: true);

        Assert.Contains(isEnabled ? "Enabled" : "Disabled", XBrowserExtensionStatusPresentation.Format(status));
        Assert.False(actions.CanInstall);
        Assert.Equal(canEnable, actions.CanEnable);
        Assert.Equal(canDisable, actions.CanDisable);
        Assert.True(actions.CanRemove);
    }

    [Fact]
    public void UnavailableStateCannotReportEnabledOrOfferActions()
    {
        var status = XBrowserExtensionStatus.Unavailable("Extension API failed. X browsing remains available.");

        var actions = XBrowserExtensionStatusPresentation.GetActions(status, extensionRuntimeAvailable: false);

        Assert.Equal("Status: Extension API failed. X browsing remains available.", XBrowserExtensionStatusPresentation.Format(status));
        Assert.Equal(XBrowserExtensionStatusKind.Unavailable, status.Kind);
        Assert.False(status.IsEnabled);
        Assert.False(actions.CanInstall);
        Assert.False(actions.CanEnable);
        Assert.False(actions.CanDisable);
        Assert.False(actions.CanRemove);
    }

    [Fact]
    public void UnavailableRuntimeDisablesActionsEvenForPersistedInstalledState()
    {
        var status = XBrowserExtensionStatus.Installed(new XBrowserExtensionInfo(
            XAdBlockExtensionPolicy.ChromeExtensionId,
            "AdBlock",
            IsEnabled: true));

        var actions = XBrowserExtensionStatusPresentation.GetActions(status, extensionRuntimeAvailable: false);

        Assert.False(actions.CanInstall);
        Assert.False(actions.CanEnable);
        Assert.False(actions.CanDisable);
        Assert.False(actions.CanRemove);
    }
}
