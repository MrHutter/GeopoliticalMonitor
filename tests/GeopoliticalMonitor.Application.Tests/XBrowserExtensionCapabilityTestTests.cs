using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class XBrowserExtensionCapabilityTestTests
{
    [Fact]
    public void NormalStartupNeverRunsCapabilityTest()
    {
        Assert.False(XBrowserExtensionCapabilityTestPolicy.ShouldRun(XBrowserExtensionCapabilityTestTrigger.ApplicationStartup));
        Assert.False(XBrowserExtensionCapabilityTestPolicy.ShouldRun(XBrowserExtensionCapabilityTestTrigger.DialogOpened));
        Assert.True(XBrowserExtensionCapabilityTestPolicy.ShouldRun(XBrowserExtensionCapabilityTestTrigger.UserRequested));
    }

    [Fact]
    public void NotTestedAndTestingStatesRemainNonActionable()
    {
        var notTested = XBrowserExtensionCapabilityTestResult.NotTested("sdk", "runtime");
        var testing = XBrowserExtensionCapabilityTestResult.Testing("sdk", "runtime");

        Assert.Equal(XBrowserExtensionCapabilityTestState.NotTested, notTested.State);
        Assert.Equal(XBrowserExtensionCapabilityTestState.Testing, testing.State);
        Assert.True(XBrowserExtensionCapabilityTestPolicy.GetActions(notTested).CanRunTest);
        Assert.False(XBrowserExtensionCapabilityTestPolicy.GetActions(testing).CanRunTest);
        AssertAdBlockActionsDisabled(notTested);
        AssertAdBlockActionsDisabled(testing);
    }

    [Theory]
    [InlineData(XBrowserExtensionCapabilityTestState.EnvironmentCreationFailed, "CoreWebView2Environment.CreateWithOptionsAsync")]
    [InlineData(XBrowserExtensionCapabilityTestState.WinUiInitializationFailed, "WebView2.EnsureCoreWebView2Async")]
    [InlineData(XBrowserExtensionCapabilityTestState.ExtensionApiFailed, "Profile.GetBrowserExtensionsAsync")]
    public void FailureStatesKeepExactStageAndBoundedError(
        XBrowserExtensionCapabilityTestState expectedState,
        string expectedStage)
    {
        var exception = new HResultException(unchecked((int)0x80004002));
        var result = expectedState switch
        {
            XBrowserExtensionCapabilityTestState.EnvironmentCreationFailed =>
                XBrowserExtensionCapabilityTestResult.EnvironmentCreationFailed("sdk", "runtime", exception, "safe message"),
            XBrowserExtensionCapabilityTestState.WinUiInitializationFailed =>
                XBrowserExtensionCapabilityTestResult.WinUiInitializationFailed("sdk", "runtime", exception, "safe message"),
            _ => XBrowserExtensionCapabilityTestResult.ExtensionApiFailed("sdk", "runtime", exception, "safe message"),
        };

        Assert.Equal(expectedState, result.State);
        Assert.Equal(expectedStage, result.Stage);
        Assert.Equal("0x80004002", result.HResult);
        Assert.Equal("safe message", result.ExceptionMessage);
        Assert.Contains(expectedStage, XBrowserExtensionCapabilityTestPolicy.Format(result));
        AssertAdBlockActionsDisabled(result);
    }

    [Fact]
    public void ConfirmedHostWithoutAdBlockAllowsOnlyInstall()
    {
        var result = XBrowserExtensionCapabilityTestResult.Confirmed("sdk", "runtime", []);
        var formatted = XBrowserExtensionCapabilityTestPolicy.Format(result);
        var actions = XBrowserExtensionCapabilityTestPolicy.GetActions(result, hasLiveDiagnosticSession: true);

        Assert.True(result.IsConfirmed);
        Assert.Equal(0, result.ExtensionCount);
        Assert.Contains("Host capability: Confirmed", formatted);
        Assert.Contains("Diagnostic profile extensions: 0", formatted);
        Assert.Contains("Diagnostic AdBlock: No managed instance", formatted);
        Assert.Contains("Diagnostic managed instance: No", formatted);
        Assert.True(actions.CanInstall);
        Assert.True(actions.CanBind);
        Assert.False(actions.CanEnable);
        Assert.False(actions.CanDisable);
        Assert.False(actions.CanRemove);
    }

    [Fact]
    public void ConfirmedHostPresentsOneUnknownEnabledExtensionWithoutMislabelingIt()
    {
        var result = XBrowserExtensionCapabilityTestResult.Confirmed(
            "sdk",
            "runtime",
            [new XBrowserExtensionInfo("unknown-extension-id", "Unknown Extension", IsEnabled: true)]);

        var formatted = XBrowserExtensionCapabilityTestPolicy.Format(result);

        Assert.Equal(1, result.ExtensionCount);
        Assert.Contains("1. Unknown Extension - unknown-extension-id - Enabled - Store identity: Different - Diagnostic managed instance: No", formatted);
        Assert.True(XBrowserExtensionCapabilityTestPolicy.GetActions(result, hasLiveDiagnosticSession: true).CanInstall);
    }

    [Fact]
    public void ConfirmedHostSeparatesKnownStoreIdentityFromManagedStatus()
    {
        var result = XBrowserExtensionCapabilityTestResult.Confirmed(
            "sdk",
            "runtime",
            [
                new XBrowserExtensionInfo("first-id", "First Extension", IsEnabled: true),
                new XBrowserExtensionInfo(XAdBlockExtensionPolicy.ChromeExtensionId, "AdBlock", IsEnabled: false),
            ]);

        var formatted = XBrowserExtensionCapabilityTestPolicy.Format(result);

        Assert.Equal(2, result.ExtensionCount);
        Assert.Contains("1. First Extension - first-id - Enabled - Store identity: Different - Diagnostic managed instance: No", formatted);
        Assert.Contains($"2. AdBlock - {XAdBlockExtensionPolicy.ChromeExtensionId} - Disabled - Store identity: Known - Diagnostic managed instance: No", formatted);
        var actions = XBrowserExtensionCapabilityTestPolicy.GetActions(result, hasLiveDiagnosticSession: true);
        Assert.True(actions.CanInstall);
        Assert.True(actions.CanBind);
        Assert.False(actions.CanEnable);
        Assert.False(actions.CanDisable);
        Assert.False(actions.CanRemove);
    }

    [Fact]
    public void ConfirmedHostBoundsExtensionIdentityFieldsAndMakesMissingValuesExplicit()
    {
        var longId = new string('i', 200);
        var longName = new string('n', 300);
        var result = XBrowserExtensionCapabilityTestResult.Confirmed(
            "sdk",
            "runtime",
            [
                new XBrowserExtensionInfo(longId, longName, IsEnabled: true),
                new XBrowserExtensionInfo(string.Empty, " ", IsEnabled: false),
            ]);

        Assert.Equal(131, result.Extensions![0].Id.Length);
        Assert.Equal(203, result.Extensions[0].Name.Length);
        Assert.Equal("[ID unavailable]", result.Extensions[1].Id);
        Assert.Equal("[name unavailable]", result.Extensions[1].Name);
    }

    [Fact]
    public void EnabledManagedAdBlockAllowsOnlyDisableAndRemove()
    {
        var extension = new XBrowserExtensionInfo("path-derived-id", "AdBlock", IsEnabled: true);
        var result = XBrowserExtensionCapabilityTestResult.Confirmed(
            "sdk",
            "runtime",
            [extension],
            XDiagnosticAdBlockStatus.Managed(extension, CreateManifest("path-derived-id")));

        var actions = XBrowserExtensionCapabilityTestPolicy.GetActions(result, hasLiveDiagnosticSession: true);

        Assert.False(actions.CanInstall);
        Assert.False(actions.CanBind);
        Assert.False(actions.CanEnable);
        Assert.True(actions.CanDisable);
        Assert.True(actions.CanRemove);
        var formatted = XBrowserExtensionCapabilityTestPolicy.Format(result);
        Assert.Contains("Diagnostic AdBlock: Installed, managed, and enabled", formatted);
        Assert.Contains("Store identity: Different", formatted);
        Assert.Contains("Diagnostic managed instance: Yes", formatted);
        Assert.DoesNotContain("approval", formatted, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DifferentStoreIdentityCanRemainInstalledButUnmanaged()
    {
        var extension = new XBrowserExtensionInfo("unapproved-id", "Unexpected Extension", IsEnabled: true);
        var result = XBrowserExtensionCapabilityTestResult.Confirmed(
            "sdk",
            "runtime",
            [extension],
            XDiagnosticAdBlockStatus.InstalledUnmanaged(
                extension,
                CreateManifest("unapproved-id"),
                "Installed but not bound."));

        var formatted = XBrowserExtensionCapabilityTestPolicy.Format(result);

        Assert.Contains("Diagnostic AdBlock: Installed but not managed", formatted);
        Assert.Contains("Installed: Yes - Enabled: Yes - Store identity: Different", formatted);
        var actions = XBrowserExtensionCapabilityTestPolicy.GetActions(result, hasLiveDiagnosticSession: true);
        Assert.True(actions.CanBind);
        Assert.False(actions.CanEnable);
        Assert.False(actions.CanDisable);
        Assert.False(actions.CanRemove);
    }

    [Fact]
    public void InvalidFolderAndInstallFailureRemainRetryableWithoutEnablingManagement()
    {
        var invalid = XBrowserExtensionCapabilityTestResult.Confirmed(
            "sdk",
            "runtime",
            [],
            XDiagnosticAdBlockStatus.InvalidFolder("manifest.json is required"));
        var exception = new HResultException(unchecked((int)0x80070002));
        var failed = XBrowserExtensionCapabilityTestResult.Confirmed(
            "sdk",
            "runtime",
            [],
            XDiagnosticAdBlockStatus.Failed(
                XDiagnosticAdBlockState.InstallationFailed,
                "Profile.AddBrowserExtensionAsync",
                exception,
                "safe install error"));

        Assert.True(XBrowserExtensionCapabilityTestPolicy.GetActions(invalid, hasLiveDiagnosticSession: true).CanInstall);
        Assert.True(XBrowserExtensionCapabilityTestPolicy.GetActions(failed, hasLiveDiagnosticSession: true).CanInstall);
        Assert.Contains("Diagnostic AdBlock: Invalid selected folder", XBrowserExtensionCapabilityTestPolicy.Format(invalid));
        var failedText = XBrowserExtensionCapabilityTestPolicy.Format(failed);
        Assert.Contains("Diagnostic AdBlock: Installation failed", failedText);
        Assert.Contains("0x80070002", failedText);
        Assert.Contains("safe install error", failedText);
    }

    [Fact]
    public void ExtensionActionsRequireLiveDiagnosticSession()
    {
        var notInstalled = XBrowserExtensionCapabilityTestResult.Confirmed("sdk", "runtime", []);
        var extension = new XBrowserExtensionInfo("path-derived-id", "AdBlock", IsEnabled: true);
        var installed = XBrowserExtensionCapabilityTestResult.Confirmed(
            "sdk",
            "runtime",
            [extension],
            XDiagnosticAdBlockStatus.Managed(extension, CreateManifest(extension.Id)));

        AssertAdBlockActionsDisabled(notInstalled);
        AssertAdBlockActionsDisabled(installed);
        Assert.True(XBrowserExtensionCapabilityTestPolicy.GetActions(notInstalled, hasLiveDiagnosticSession: true).CanInstall);
        Assert.True(XBrowserExtensionCapabilityTestPolicy.GetActions(notInstalled, hasLiveDiagnosticSession: true).CanBind);
        var liveInstalledActions = XBrowserExtensionCapabilityTestPolicy.GetActions(installed, hasLiveDiagnosticSession: true);
        Assert.True(liveInstalledActions.CanDisable);
        Assert.True(liveInstalledActions.CanRemove);
    }

    private static void AssertAdBlockActionsDisabled(XBrowserExtensionCapabilityTestResult result)
    {
        var actions = XBrowserExtensionCapabilityTestPolicy.GetActions(result);
        Assert.False(actions.CanInstall);
        Assert.False(actions.CanBind);
        Assert.False(actions.CanEnable);
        Assert.False(actions.CanDisable);
        Assert.False(actions.CanRemove);
    }

    private static XDiagnosticAdBlockManifestIdentity CreateManifest(string expectedRuntimeId) => new(
        ManifestVersion: 3,
        Version: "6.46.0",
        RawName: "__MSG_name__",
        DefaultLocale: "en_US",
        DefaultLocaleName: "AdBlock - block ads across the web",
        Author: "Adblock, Inc.",
        HasKey: false,
        ManifestSha256: "manifest-hash",
        ExpectedRuntimeId: expectedRuntimeId);

    private sealed class HResultException : Exception
    {
        public HResultException(int hresult)
            : base("Synthetic capability failure")
        {
            HResult = hresult;
        }
    }
}
