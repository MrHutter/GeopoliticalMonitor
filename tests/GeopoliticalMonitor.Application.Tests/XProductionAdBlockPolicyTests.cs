using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class XProductionAdBlockPolicyTests
{
    [Fact]
    public void UnavailableHostDisablesProductionManagementButKeepsDiagnosticExplicit()
    {
        var actions = XProductionAdBlockPolicy.GetActions(
            XProductionExtensionHostState.FallbackUnavailable,
            snapshot: null,
            hasManagedSource: false,
            isBusy: false);

        Assert.False(actions.CanRefresh);
        Assert.False(actions.CanInstall);
        Assert.False(actions.CanBind);
        Assert.False(actions.CanEnable);
        Assert.False(actions.CanDisable);
        Assert.False(actions.CanRemove);
        Assert.True(actions.CanRunDiagnostic);
    }

    [Fact]
    public void ManagedEnabledInstanceCanBeDisabledOrRemovedOnly()
    {
        var extension = new XBrowserExtensionInfo("managed-id", "AdBlock", IsEnabled: true);
        var snapshot = new XDiagnosticAdBlockSnapshot(
            [extension],
            XDiagnosticAdBlockStatus.Managed(
                extension,
                Manifest(),
                trustRecord: OfficialAdBlockTrustRecords.ChromeWebStore6470));

        var actions = XProductionAdBlockPolicy.GetActions(
            XProductionExtensionHostState.Available,
            snapshot,
            hasManagedSource: true,
            isBusy: false);

        Assert.True(actions.CanRefresh);
        Assert.False(actions.CanInstall);
        Assert.False(actions.CanBind);
        Assert.False(actions.CanEnable);
        Assert.True(actions.CanDisable);
        Assert.True(actions.CanRemove);
    }

    [Fact]
    public void StatusSeparatesHostInstallBindingEnabledAndManagedSourceFacts()
    {
        var text = XProductionAdBlockPolicy.Format(
            XProductionExtensionHostState.Available,
            snapshot: null,
            hasManagedSource: true,
            operationMessage: "Ready for reconciliation.");

        Assert.Contains("Extension host: Available", text);
        Assert.Contains("Installed: Not established", text);
        Assert.Contains("Managed: No", text);
        Assert.Contains("Enabled: Not established", text);
        Assert.Contains("Source managed by application: Yes", text);
        Assert.Contains("Payload trusted: No", text);
        Assert.Contains("Ready for reconciliation.", text);
    }

    private static XDiagnosticAdBlockManifestIdentity Manifest() => new(
        3,
        "6.47.0",
        "__MSG_name__",
        "en_US",
        "AdBlock",
        "Adblock, Inc.",
        HasKey: true,
        "hash",
        "managed-id")
    {
        OfficialStoreExtensionId = XAdBlockExtensionPolicy.ChromeExtensionId,
    };
}
