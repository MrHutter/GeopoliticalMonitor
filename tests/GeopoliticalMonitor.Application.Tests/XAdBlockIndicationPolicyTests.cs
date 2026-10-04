using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class XAdBlockIndicationPolicyTests
{
    private const string BoundId = "bound-runtime-id";

    public static TheoryData<XDiagnosticAdBlockState?> AllSnapshotStates()
    {
        var data = new TheoryData<XDiagnosticAdBlockState?> { (XDiagnosticAdBlockState?)null };
        foreach (var state in Enum.GetValues<XDiagnosticAdBlockState>())
        {
            data.Add(state);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllSnapshotStates))]
    public void ShowsNothingWhenTheXPaneIsUnavailable(XDiagnosticAdBlockState? state)
    {
        foreach (var payload in new[] { false, true })
        {
            foreach (var live in Enum.GetValues<XAdBlockLiveCheck>())
            {
                Assert.Equal(XAdBlockIndication.None, XAdBlockIndicationPolicy.Evaluate(XProductionExtensionHostState.XUnavailable, state, payload, live));
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllSnapshotStates))]
    public void ShowsInactiveWhenTheExtensionHostFellBack(XDiagnosticAdBlockState? state)
    {
        foreach (var payload in new[] { false, true })
        {
            foreach (var live in Enum.GetValues<XAdBlockLiveCheck>())
            {
                Assert.Equal(XAdBlockIndication.Inactive, XAdBlockIndicationPolicy.Evaluate(XProductionExtensionHostState.FallbackUnavailable, state, payload, live));
            }
        }
    }

    [Theory]
    [InlineData(null, false, XAdBlockIndication.None)]
    [InlineData(null, true, XAdBlockIndication.None)]
    [InlineData(XDiagnosticAdBlockState.NotTested, false, XAdBlockIndication.None)]
    [InlineData(XDiagnosticAdBlockState.NotTested, true, XAdBlockIndication.None)]
    [InlineData(XDiagnosticAdBlockState.InstalledManagedEnabled, false, XAdBlockIndication.None)]
    [InlineData(XDiagnosticAdBlockState.InstalledManagedEnabled, true, XAdBlockIndication.None)]
    [InlineData(XDiagnosticAdBlockState.InstalledManagedDisabled, false, XAdBlockIndication.Off)]
    [InlineData(XDiagnosticAdBlockState.InstalledManagedDisabled, true, XAdBlockIndication.Off)]
    [InlineData(XDiagnosticAdBlockState.NoManagedInstance, false, XAdBlockIndication.NotSetUp)]
    [InlineData(XDiagnosticAdBlockState.NoManagedInstance, true, XAdBlockIndication.Inactive)]
    [InlineData(XDiagnosticAdBlockState.InstalledUnmanaged, false, XAdBlockIndication.Inactive)]
    [InlineData(XDiagnosticAdBlockState.InstalledUnmanaged, true, XAdBlockIndication.Inactive)]
    [InlineData(XDiagnosticAdBlockState.BindingStale, false, XAdBlockIndication.Inactive)]
    [InlineData(XDiagnosticAdBlockState.BindingStale, true, XAdBlockIndication.Inactive)]
    [InlineData(XDiagnosticAdBlockState.InvalidFolder, false, XAdBlockIndication.Inactive)]
    [InlineData(XDiagnosticAdBlockState.InvalidFolder, true, XAdBlockIndication.Inactive)]
    [InlineData(XDiagnosticAdBlockState.InstallationFailed, false, XAdBlockIndication.Inactive)]
    [InlineData(XDiagnosticAdBlockState.InstallationFailed, true, XAdBlockIndication.Inactive)]
    [InlineData(XDiagnosticAdBlockState.OperationFailed, false, XAdBlockIndication.Inactive)]
    [InlineData(XDiagnosticAdBlockState.OperationFailed, true, XAdBlockIndication.Inactive)]
    public void MapsEverySnapshotStateWhenTheHostIsAvailable(XDiagnosticAdBlockState? state, bool payload, XAdBlockIndication expected)
    {
        Assert.Equal(expected, XAdBlockIndicationPolicy.Evaluate(XProductionExtensionHostState.Available, state, payload, XAdBlockLiveCheck.Unknown));
    }

    [Theory]
    [InlineData(XDiagnosticAdBlockState.InstalledManagedEnabled, XAdBlockLiveCheck.BoundInstanceEnabled, XAdBlockIndication.None)]
    [InlineData(XDiagnosticAdBlockState.InstalledManagedEnabled, XAdBlockLiveCheck.BoundInstanceDisabled, XAdBlockIndication.Off)]
    [InlineData(XDiagnosticAdBlockState.InstalledManagedEnabled, XAdBlockLiveCheck.BoundInstanceMissing, XAdBlockIndication.Inactive)]
    [InlineData(XDiagnosticAdBlockState.InstalledManagedEnabled, XAdBlockLiveCheck.Unknown, XAdBlockIndication.None)]
    [InlineData(XDiagnosticAdBlockState.InstalledManagedDisabled, XAdBlockLiveCheck.BoundInstanceEnabled, XAdBlockIndication.None)]
    [InlineData(XDiagnosticAdBlockState.InstalledManagedDisabled, XAdBlockLiveCheck.BoundInstanceDisabled, XAdBlockIndication.Off)]
    [InlineData(XDiagnosticAdBlockState.InstalledManagedDisabled, XAdBlockLiveCheck.BoundInstanceMissing, XAdBlockIndication.Inactive)]
    [InlineData(XDiagnosticAdBlockState.InstalledManagedDisabled, XAdBlockLiveCheck.Unknown, XAdBlockIndication.Off)]
    public void LetsANewerLiveCheckWinOverAManagedSnapshot(XDiagnosticAdBlockState state, XAdBlockLiveCheck live, XAdBlockIndication expected)
    {
        Assert.Equal(expected, XAdBlockIndicationPolicy.Evaluate(XProductionExtensionHostState.Available, state, managedPayloadExists: true, live));
    }

    [Theory]
    [MemberData(nameof(AllSnapshotStates))]
    public void NeverLetsAnUnknownLiveCheckChangeTheSnapshotResult(XDiagnosticAdBlockState? state)
    {
        foreach (var payload in new[] { false, true })
        {
            var withoutCheck = XAdBlockIndicationPolicy.Evaluate(XProductionExtensionHostState.Available, state, payload, XAdBlockLiveCheck.Unknown);
            if (state is XDiagnosticAdBlockState.InstalledManagedEnabled or XDiagnosticAdBlockState.InstalledManagedDisabled)
            {
                continue;
            }

            // Without a bound managed instance a live check has nothing to compare with and is ignored.
            foreach (var live in Enum.GetValues<XAdBlockLiveCheck>())
            {
                Assert.Equal(withoutCheck, XAdBlockIndicationPolicy.Evaluate(XProductionExtensionHostState.Available, state, payload, live));
            }
        }
    }

    [Fact]
    public void NeverCreatesAWarningFromAnUnknownState()
    {
        foreach (var live in Enum.GetValues<XAdBlockLiveCheck>())
        {
            Assert.Equal(XAdBlockIndication.None, XAdBlockIndicationPolicy.Evaluate(XProductionExtensionHostState.Available, null, false, live));
        }
    }

    [Fact]
    public void UsesExactlyTheFixedTexts()
    {
        Assert.Null(XAdBlockIndicationPolicy.GetText(XAdBlockIndication.None));
        Assert.Equal("Ad blocker inactive: open Ad Blocker... for details", XAdBlockIndicationPolicy.GetText(XAdBlockIndication.Inactive));
        Assert.Equal("Ad blocker off: open Ad Blocker... to turn it on", XAdBlockIndicationPolicy.GetText(XAdBlockIndication.Off));
        Assert.Equal("Ad blocker not set up", XAdBlockIndicationPolicy.GetText(XAdBlockIndication.NotSetUp));
        foreach (var indication in Enum.GetValues<XAdBlockIndication>())
        {
            var text = XAdBlockIndicationPolicy.GetText(indication) ?? string.Empty;
            Assert.DoesNotMatch("[0-9]|\\\\|/", text);
            Assert.DoesNotContain("hybrid", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void TakesTheBoundRuntimeIdOnlyFromAManagedSnapshot()
    {
        Assert.Equal(BoundId, XAdBlockIndicationPolicy.GetBoundRuntimeId(Snapshot(XDiagnosticAdBlockState.InstalledManagedEnabled, BoundId)));
        Assert.Equal(BoundId, XAdBlockIndicationPolicy.GetBoundRuntimeId(Snapshot(XDiagnosticAdBlockState.InstalledManagedDisabled, BoundId)));
        Assert.Null(XAdBlockIndicationPolicy.GetBoundRuntimeId(Snapshot(XDiagnosticAdBlockState.InstalledUnmanaged, BoundId)));
        Assert.Null(XAdBlockIndicationPolicy.GetBoundRuntimeId(Snapshot(XDiagnosticAdBlockState.BindingStale, BoundId)));
        Assert.Null(XAdBlockIndicationPolicy.GetBoundRuntimeId(Snapshot(XDiagnosticAdBlockState.NoManagedInstance, null)));
        Assert.Null(XAdBlockIndicationPolicy.GetBoundRuntimeId(null));
    }

    [Fact]
    public void ComparesTheInstalledListWithTheBoundRuntimeId()
    {
        var enabled = new XBrowserExtensionInfo(BoundId, "AdBlock", true);
        var disabled = enabled with { IsEnabled = false };
        var other = new XBrowserExtensionInfo("other-id", "Other", true);

        Assert.Equal(XAdBlockLiveCheck.BoundInstanceEnabled, XAdBlockIndicationPolicy.EvaluateLiveCheck(BoundId, [other, enabled]));
        Assert.Equal(XAdBlockLiveCheck.BoundInstanceDisabled, XAdBlockIndicationPolicy.EvaluateLiveCheck(BoundId, [disabled]));
        Assert.Equal(XAdBlockLiveCheck.BoundInstanceMissing, XAdBlockIndicationPolicy.EvaluateLiveCheck(BoundId, [other]));
        Assert.Equal(XAdBlockLiveCheck.BoundInstanceMissing, XAdBlockIndicationPolicy.EvaluateLiveCheck(BoundId, []));
        Assert.Equal(XAdBlockLiveCheck.Unknown, XAdBlockIndicationPolicy.EvaluateLiveCheck(BoundId, [enabled, disabled]));
        Assert.Equal(XAdBlockLiveCheck.Unknown, XAdBlockIndicationPolicy.EvaluateLiveCheck(BoundId, null));
        Assert.Equal(XAdBlockLiveCheck.Unknown, XAdBlockIndicationPolicy.EvaluateLiveCheck(null, [enabled]));
        Assert.Equal(XAdBlockLiveCheck.Unknown, XAdBlockIndicationPolicy.EvaluateLiveCheck(string.Empty, [enabled]));
    }

    [Fact]
    public void IndicationCodeCallsNoMutatingAdBlockApi()
    {
        var root = FindRepositoryRoot();
        var files = new[]
        {
            Path.Combine(root, "src", "backend", "GeopoliticalMonitor.Application", "XBrowser", "XAdBlockIndicationPolicy.cs"),
            Path.Combine(root, "src", "backend", "GeopoliticalMonitor.Desktop", "MainWindow.AdBlockIndication.cs"),
        };
        var forbidden = new[]
        {
            "SecureForAuthenticatedNavigationAsync", "SetExtensionEnabledAsync", "AddBrowserExtensionAsync", "RemoveExtensionAsync",
            "SetEnabledAsync", "EnableAsync", "InstallAsync", "BindInstalledAsync", "RemoveAsync", "Reconcile", "GetSnapshotAsync",
            "IXDiagnosticAdBlockBindingStore", "FileXDiagnosticAdBlockBindingStore", "SaveAsync", "ClearAsync",
            "XDiagnosticAdBlockService", "InspectAsync", "PrepareAsync", "CopyAsync", "RecoverInterruptedActivationAsync",
            "XAdBlockPayloadDigest", "FileXAdBlockManagedPayloadStore", "FileXDiagnosticAdBlockSourceInspector",
        };

        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("hybrid", source, StringComparison.OrdinalIgnoreCase);
            foreach (var api in forbidden)
            {
                Assert.False(source.Contains(api, StringComparison.Ordinal), $"{Path.GetFileName(file)} references {api}.");
            }
        }
    }

    private static XDiagnosticAdBlockSnapshot Snapshot(XDiagnosticAdBlockState state, string? extensionId)
    {
        var extension = extensionId is null ? null : new XBrowserExtensionInfo(extensionId, "AdBlock", state != XDiagnosticAdBlockState.InstalledManagedDisabled);
        return new XDiagnosticAdBlockSnapshot(
            extension is null ? [] : [extension],
            new XDiagnosticAdBlockStatus(state, extension, Manifest: null, Stage: null, ExceptionType: null, HResult: null, Message: null));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "backend", "GeopoliticalMonitor.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the GeopoliticalMonitor repository root from the test output directory.");
    }
}
