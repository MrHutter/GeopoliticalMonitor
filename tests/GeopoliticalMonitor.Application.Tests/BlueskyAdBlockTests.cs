using System.Text.Json;
using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class BlueskyAdBlockScopeTests
{
    private const string XManagedPath = @"D:\AppData\GeopoliticalMonitor\Extensions\AdBlock\Current";
    private const string BlueskyManagedPath = @"D:\AppData\GeopoliticalMonitor\Extensions\AdBlockBluesky\Current";

    // A production X binding exactly as the current build writes it (FileXDiagnosticAdBlockBindingStore serializes the
    // record with System.Text.Json defaults). It must keep validating after the scope generalization.
    private const string ExistingXBindingJson =
        """
        {"SchemaVersion":2,"ProfileScope":"XBrowser","RuntimeExtensionId":"gighmmpiobklfepjocnamgkkbiglidom","SourceFolderPath":"D:\\AppData\\GeopoliticalMonitor\\Extensions\\AdBlock\\Current","Manifest":{"ManifestVersion":3,"Version":"6.47.0","RawName":"__MSG_name__","DefaultLocale":"en_US","DefaultLocaleName":"AdBlock - block ads across the web","Author":"Adblock, Inc.","HasKey":true,"ManifestSha256":"manifest-hash","ExpectedRuntimeId":"gighmmpiobklfepjocnamgkkbiglidom","OfficialStoreExtensionId":"gighmmpiobklfepjocnamgkkbiglidom"},"Payload":{"DigestFormat":"gpm-extension-payload-v1","Sha256":"9022cbf484913e8c45cbeae3828c7072de51ffa1369013e7e0aa105172a822ae","FileCount":443,"TotalBytes":326555718,"DirectoryCount":36,"MaximumDirectoryDepth":7,"LongestPathUtf8Bytes":128},"TrustRecord":{"TrustSchemaVersion":1,"ProductKey":"adblock-by-eyeo","StoreLabel":"chrome-web-store","OfficialStoreExtensionId":"gighmmpiobklfepjocnamgkkbiglidom","ExtensionVersion":"6.47.0","DigestFormat":"gpm-extension-payload-v1","PayloadSha256":"9022cbf484913e8c45cbeae3828c7072de51ffa1369013e7e0aa105172a822ae","ApprovalNote":"Initial reviewed browser-store enrollment authority."}}
        """;

    [Fact]
    public void BlueskyIsAFirstClassProductionScopeBesideAnUnchangedXScope()
    {
        Assert.Equal("XBrowser", XDiagnosticAdBlockBinding.ProductionXProfileScope);
        Assert.Equal("BlueskyBrowser", XDiagnosticAdBlockBinding.ProductionBlueskyProfileScope);
        Assert.Equal("ExtensionCapabilityDiagnostic", XDiagnosticAdBlockBinding.DiagnosticProfileScope);
        Assert.True(XDiagnosticAdBlockBinding.IsProductionScope(XDiagnosticAdBlockBinding.ProductionXProfileScope));
        Assert.True(XDiagnosticAdBlockBinding.IsProductionScope(XDiagnosticAdBlockBinding.ProductionBlueskyProfileScope));
        Assert.False(XDiagnosticAdBlockBinding.IsProductionScope(XDiagnosticAdBlockBinding.DiagnosticProfileScope));
        Assert.False(XDiagnosticAdBlockBinding.IsProductionScope("blueskybrowser"));
        Assert.False(XDiagnosticAdBlockBinding.IsProductionScope("ProductionX"));
        Assert.False(XDiagnosticAdBlockBinding.IsProductionScope(null));
    }

    [Fact]
    public void ExistingXBindingFileStillValidatesAndMatchesOnlyTheXScope()
    {
        var binding = JsonSerializer.Deserialize<XDiagnosticAdBlockBinding>(ExistingXBindingJson);
        var source = TrustedSource(XManagedPath);

        Assert.NotNull(binding);
        Assert.True(binding!.IsStructurallyValid());
        Assert.True(binding.Matches(source, XDiagnosticAdBlockBinding.ProductionXProfileScope));
        Assert.False(binding.Matches(source, XDiagnosticAdBlockBinding.ProductionBlueskyProfileScope));
        Assert.False(binding.Matches(source));
        Assert.True(XProductionAdBlockStartupTrustPolicy.Evaluate(true, true, binding, XDiagnosticAdBlockSourceValidation.Valid(source)).AttemptExtensionEnvironment);
    }

    [Fact]
    public void XBindingWrittenByThisBuildHasTheSameFormat()
    {
        var source = TrustedSource(XManagedPath);
        var written = XDiagnosticAdBlockBinding.Create(source.Manifest.ExpectedRuntimeId, source, XDiagnosticAdBlockBinding.ProductionXProfileScope);

        Assert.Equal(ExistingXBindingJson, JsonSerializer.Serialize(written));
    }

    [Fact]
    public void BlueskyBindingIsStructurallyValidOnlyWithTheReviewedPayload()
    {
        var source = TrustedSource(BlueskyManagedPath);
        var binding = XDiagnosticAdBlockBinding.Create(source.Manifest.ExpectedRuntimeId, source, XDiagnosticAdBlockBinding.ProductionBlueskyProfileScope);
        var roundTrip = JsonSerializer.Deserialize<XDiagnosticAdBlockBinding>(JsonSerializer.Serialize(binding))!;

        Assert.True(roundTrip.IsStructurallyValid());
        Assert.Equal(XDiagnosticAdBlockBinding.CurrentSchemaVersion, roundTrip.SchemaVersion);
        Assert.Equal(OfficialAdBlockTrustRecords.ChromeWebStore6470, roundTrip.TrustRecord);
        Assert.False((roundTrip with { TrustRecord = null }).IsStructurallyValid());
        Assert.False((roundTrip with { Payload = null }).IsStructurallyValid());
        Assert.False((roundTrip with { Payload = roundTrip.Payload! with { Sha256 = new string('0', 64) } }).IsStructurallyValid());
        Assert.False((roundTrip with { TrustRecord = roundTrip.TrustRecord! with { ProductKey = "unreviewed-product" } }).IsStructurallyValid());
    }

    [Fact]
    public void BindingsNeverCrossMatchBetweenXAndBluesky()
    {
        var xSource = TrustedSource(XManagedPath);
        var blueskySource = TrustedSource(BlueskyManagedPath);
        var xBinding = XDiagnosticAdBlockBinding.Create(xSource.Manifest.ExpectedRuntimeId, xSource, XDiagnosticAdBlockBinding.ProductionXProfileScope);
        var blueskyBinding = XDiagnosticAdBlockBinding.Create(blueskySource.Manifest.ExpectedRuntimeId, blueskySource, XDiagnosticAdBlockBinding.ProductionBlueskyProfileScope);

        Assert.True(blueskyBinding.Matches(blueskySource, XDiagnosticAdBlockBinding.ProductionBlueskyProfileScope));
        Assert.False(blueskyBinding.Matches(blueskySource, XDiagnosticAdBlockBinding.ProductionXProfileScope));
        Assert.False(xBinding.Matches(xSource, XDiagnosticAdBlockBinding.ProductionBlueskyProfileScope));
        // An X binding pointed at the Bluesky copy still fails: scope and folder both differ.
        Assert.False(xBinding.Matches(blueskySource, XDiagnosticAdBlockBinding.ProductionBlueskyProfileScope));
        Assert.False(blueskyBinding.Matches(xSource, XDiagnosticAdBlockBinding.ProductionBlueskyProfileScope));
    }

    [Fact]
    public void BlueskyMatchingRequiresTheReviewedPayload()
    {
        var untrusted = TrustedSource(BlueskyManagedPath) with { TrustRecord = null };
        var binding = XDiagnosticAdBlockBinding.Create(untrusted.Manifest.ExpectedRuntimeId, untrusted, XDiagnosticAdBlockBinding.ProductionBlueskyProfileScope);

        Assert.False(binding.Matches(untrusted, XDiagnosticAdBlockBinding.ProductionBlueskyProfileScope));
    }

    [Fact]
    public void BlueskyGateStaysExtensionDisabledUntilSetUp()
    {
        var notSetUp = Evaluate(false, false, null, null);

        Assert.False(notSetUp.AttemptExtensionEnvironment);
        Assert.False(notSetUp.HasTrustedManagedPayload);
        Assert.Null(notSetUp.Message);
        // X's clean state is unchanged.
        Assert.True(XProductionAdBlockStartupTrustPolicy.Evaluate(false, false, null, null).AttemptExtensionEnvironment);
    }

    [Fact]
    public void BlueskyGateAllowsATrustedBindingOrAFreshlyEnrolledTrustedPayload()
    {
        var source = TrustedSource(BlueskyManagedPath);
        var binding = XDiagnosticAdBlockBinding.Create(source.Manifest.ExpectedRuntimeId, source, XDiagnosticAdBlockBinding.ProductionBlueskyProfileScope);

        var trusted = Evaluate(true, true, binding, XDiagnosticAdBlockSourceValidation.Valid(source));
        var enrolled = Evaluate(true, false, null, XDiagnosticAdBlockSourceValidation.Valid(source));

        Assert.True(trusted.AttemptExtensionEnvironment);
        Assert.True(trusted.HasTrustedManagedPayload);
        Assert.True(enrolled.AttemptExtensionEnvironment);
        Assert.True(enrolled.HasTrustedManagedPayload);
    }

    [Fact]
    public void BlueskyGateRefusesStaleOrInvalidState()
    {
        var source = TrustedSource(BlueskyManagedPath);
        var binding = XDiagnosticAdBlockBinding.Create(source.Manifest.ExpectedRuntimeId, source, XDiagnosticAdBlockBinding.ProductionBlueskyProfileScope);
        var mutated = source with { Payload = source.Payload! with { Sha256 = new string('0', 64) } };

        // Stale: the binding remains but the payload is gone, or the binding file is unreadable.
        Assert.False(Evaluate(false, true, binding, null).AttemptExtensionEnvironment);
        Assert.False(Evaluate(false, true, null, null).AttemptExtensionEnvironment);
        // Invalid: the payload changed, failed authentication, or is not the reviewed one.
        Assert.False(Evaluate(true, true, binding, XDiagnosticAdBlockSourceValidation.Valid(mutated)).AttemptExtensionEnvironment);
        Assert.False(Evaluate(true, true, binding, XDiagnosticAdBlockSourceValidation.Invalid("changed")).AttemptExtensionEnvironment);
        Assert.False(Evaluate(true, false, null, XDiagnosticAdBlockSourceValidation.Valid(source with { TrustRecord = null })).AttemptExtensionEnvironment);
        Assert.False(Evaluate(true, false, null, null).AttemptExtensionEnvironment);
        Assert.NotNull(Evaluate(true, true, binding, XDiagnosticAdBlockSourceValidation.Valid(mutated)).Message);
    }

    [Fact]
    public void GatesDoNotAcceptTheOtherFeedsBinding()
    {
        var xSource = TrustedSource(XManagedPath);
        var xBinding = XDiagnosticAdBlockBinding.Create(xSource.Manifest.ExpectedRuntimeId, xSource, XDiagnosticAdBlockBinding.ProductionXProfileScope);
        var blueskySource = TrustedSource(BlueskyManagedPath);
        var blueskyBinding = XDiagnosticAdBlockBinding.Create(blueskySource.Manifest.ExpectedRuntimeId, blueskySource, XDiagnosticAdBlockBinding.ProductionBlueskyProfileScope);

        Assert.False(Evaluate(true, true, xBinding, XDiagnosticAdBlockSourceValidation.Valid(xSource)).AttemptExtensionEnvironment);
        Assert.False(XProductionAdBlockStartupTrustPolicy.Evaluate(true, true, blueskyBinding, XDiagnosticAdBlockSourceValidation.Valid(blueskySource)).AttemptExtensionEnvironment);
    }

    [Fact]
    public void OnlyProductionScopesHaveAStartupGate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => XProductionAdBlockStartupTrustPolicy.Evaluate(
            false, false, null, null, XDiagnosticAdBlockBinding.DiagnosticProfileScope));
        Assert.Throws<ArgumentOutOfRangeException>(() => XProductionAdBlockStartupTrustPolicy.Evaluate(
            false, false, null, null, "Bluesky"));
    }

    private static XProductionAdBlockStartupDecision Evaluate(
        bool managedPayloadExists,
        bool persistedBindingExists,
        XDiagnosticAdBlockBinding? binding,
        XDiagnosticAdBlockSourceValidation? validation) =>
        XProductionAdBlockStartupTrustPolicy.Evaluate(
            managedPayloadExists,
            persistedBindingExists,
            binding,
            validation,
            XDiagnosticAdBlockBinding.ProductionBlueskyProfileScope);

    internal static XDiagnosticAdBlockSourceIdentity TrustedSource(string path)
    {
        var trust = OfficialAdBlockTrustRecords.ChromeWebStore6470;
        return new(
            path,
            new XDiagnosticAdBlockManifestIdentity(
                ManifestVersion: 3,
                Version: trust.ExtensionVersion,
                RawName: "__MSG_name__",
                DefaultLocale: "en_US",
                DefaultLocaleName: "AdBlock - block ads across the web",
                Author: "Adblock, Inc.",
                HasKey: true,
                ManifestSha256: "manifest-hash",
                ExpectedRuntimeId: trust.OfficialStoreExtensionId)
            {
                OfficialStoreExtensionId = trust.OfficialStoreExtensionId,
            },
            new XAdBlockPayloadIdentity(trust.DigestFormat, trust.PayloadSha256, 443, 326_555_718, 36, 7, 128),
            trust);
    }
}

public sealed class BlueskyAdBlockServiceTests
{
    private const string BlueskyManagedPath = @"D:\AppData\GeopoliticalMonitor\Extensions\AdBlockBluesky\Current";

    [Fact]
    public async Task InstallBindsTheInstanceToTheBlueskyScope()
    {
        var source = BlueskyAdBlockScopeTests.TrustedSource(BlueskyManagedPath);
        var profile = new FakeProfile([]) { ToAdd = new(source.Manifest.ExpectedRuntimeId, "AdBlock", IsEnabled: true) };
        var store = new FakeBindingStore();
        var service = new XDiagnosticAdBlockService(profile, store, new FakeInspector(source), XDiagnosticAdBlockBinding.ProductionBlueskyProfileScope, BlueskyManagedPath);

        var snapshot = await service.InstallAsync(BlueskyManagedPath);

        Assert.Equal(XDiagnosticAdBlockState.InstalledManagedEnabled, snapshot.AdBlockStatus.State);
        Assert.Equal(XDiagnosticAdBlockBinding.ProductionBlueskyProfileScope, store.Binding?.ProfileScope);
        Assert.True(store.Binding!.IsStructurallyValid());
        Assert.Contains("production Bluesky", (await service.GetSnapshotAsync()).AdBlockStatus.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InstallRefusesAnUnreviewedPayloadForBluesky()
    {
        var source = BlueskyAdBlockScopeTests.TrustedSource(BlueskyManagedPath) with { TrustRecord = null };
        var profile = new FakeProfile([]);
        var store = new FakeBindingStore();
        var service = new XDiagnosticAdBlockService(profile, store, new FakeInspector(source), XDiagnosticAdBlockBinding.ProductionBlueskyProfileScope);

        var snapshot = await service.InstallAsync(BlueskyManagedPath);

        Assert.Equal(XDiagnosticAdBlockState.InvalidFolder, snapshot.AdBlockStatus.State);
        Assert.Equal(0, profile.AddCount);
        Assert.Null(store.Binding);
    }

    [Fact]
    public async Task SecuringTheBlueskyProfileDisablesAnUnmanagedAdBlockLikeExtension()
    {
        var source = BlueskyAdBlockScopeTests.TrustedSource(BlueskyManagedPath);
        var profile = new FakeProfile([new XBrowserExtensionInfo("abcdefghijklmnopabcdefghijklmnop", "AdBlock Plus", IsEnabled: true)]);
        var service = new XDiagnosticAdBlockService(profile, new FakeBindingStore(), new FakeInspector(source), XDiagnosticAdBlockBinding.ProductionBlueskyProfileScope);

        await service.SecureForAuthenticatedNavigationAsync();

        Assert.False(Assert.Single(profile.Extensions).IsEnabled);
    }

    [Fact]
    public async Task AnXBindingInTheBlueskyStoreIsStale()
    {
        var source = BlueskyAdBlockScopeTests.TrustedSource(BlueskyManagedPath);
        var xBinding = XDiagnosticAdBlockBinding.Create(source.Manifest.ExpectedRuntimeId, source, XDiagnosticAdBlockBinding.ProductionXProfileScope);
        var profile = new FakeProfile([new XBrowserExtensionInfo(source.Manifest.ExpectedRuntimeId, "AdBlock", IsEnabled: true)]);
        var service = new XDiagnosticAdBlockService(profile, new FakeBindingStore { Binding = xBinding }, new FakeInspector(source), XDiagnosticAdBlockBinding.ProductionBlueskyProfileScope);

        var snapshot = await service.GetSnapshotAsync();

        Assert.Equal(XDiagnosticAdBlockState.BindingStale, snapshot.AdBlockStatus.State);
    }

    private sealed class FakeBindingStore : IXDiagnosticAdBlockBindingStore
    {
        public XDiagnosticAdBlockBinding? Binding { get; set; }

        public Task<XDiagnosticAdBlockBinding?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Binding);

        public Task SaveAsync(XDiagnosticAdBlockBinding binding, CancellationToken cancellationToken = default)
        {
            Binding = binding;
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            Binding = null;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeInspector(XDiagnosticAdBlockSourceIdentity source) : IXDiagnosticAdBlockSourceInspector
    {
        public Task<XDiagnosticAdBlockSourceValidation> InspectAsync(string sourceFolderPath, CancellationToken cancellationToken = default) =>
            Task.FromResult(XDiagnosticAdBlockSourceValidation.Valid(source));
    }

    private sealed class FakeProfile(IEnumerable<XBrowserExtensionInfo> extensions) : IXBrowserExtensionProfile
    {
        public List<XBrowserExtensionInfo> Extensions { get; } = extensions.ToList();

        public XBrowserExtensionInfo? ToAdd { get; init; }

        public int AddCount { get; private set; }

        public Task<IReadOnlyList<XBrowserExtensionInfo>> GetInstalledExtensionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<XBrowserExtensionInfo>>(Extensions.ToArray());

        public Task<XBrowserExtensionInfo> AddBrowserExtensionAsync(string extensionFolderPath, CancellationToken cancellationToken = default)
        {
            AddCount++;
            var added = ToAdd ?? throw new InvalidOperationException("No extension to add.");
            Extensions.Add(added);
            return Task.FromResult(added);
        }

        public Task SetExtensionEnabledAsync(string extensionId, bool enabled, CancellationToken cancellationToken = default)
        {
            var index = Extensions.FindIndex(item => item.Id == extensionId);
            Extensions[index] = Extensions[index] with { IsEnabled = enabled };
            return Task.CompletedTask;
        }

        public Task RemoveExtensionAsync(string extensionId, CancellationToken cancellationToken = default)
        {
            Extensions.RemoveAll(item => item.Id == extensionId);
            return Task.CompletedTask;
        }
    }
}

public sealed class BlueskyAdBlockPolicyTests
{
    [Fact]
    public void NeverSetUpShowsNoLineWhateverTheState()
    {
        foreach (var host in Enum.GetValues<XProductionExtensionHostState>())
        {
            foreach (var state in Enum.GetValues<XDiagnosticAdBlockState>())
            {
                Assert.Equal(XAdBlockIndication.None, BlueskyAdBlockPolicy.GetIndication(false, host, state, false, XAdBlockLiveCheck.Unknown));
            }
        }

        Assert.False(BlueskyAdBlockPolicy.IsSetUp(false, false));
        Assert.True(BlueskyAdBlockPolicy.IsSetUp(true, false));
        Assert.True(BlueskyAdBlockPolicy.IsSetUp(false, true));
    }

    [Fact]
    public void SetUpBlueskyUsesTheXTextsExceptNotSetUp()
    {
        Assert.Equal(XAdBlockIndication.None, BlueskyAdBlockPolicy.GetIndication(true, XProductionExtensionHostState.Available, XDiagnosticAdBlockState.InstalledManagedEnabled, true, XAdBlockLiveCheck.Unknown));
        Assert.Equal(XAdBlockIndication.Off, BlueskyAdBlockPolicy.GetIndication(true, XProductionExtensionHostState.Available, XDiagnosticAdBlockState.InstalledManagedDisabled, true, XAdBlockLiveCheck.Unknown));
        Assert.Equal(XAdBlockIndication.Inactive, BlueskyAdBlockPolicy.GetIndication(true, XProductionExtensionHostState.Available, XDiagnosticAdBlockState.BindingStale, true, XAdBlockLiveCheck.Unknown));
        Assert.Equal(XAdBlockIndication.Inactive, BlueskyAdBlockPolicy.GetIndication(true, XProductionExtensionHostState.Available, XDiagnosticAdBlockState.InstalledManagedEnabled, true, XAdBlockLiveCheck.BoundInstanceMissing));
        Assert.Equal(XAdBlockIndication.Inactive, BlueskyAdBlockPolicy.GetIndication(true, XProductionExtensionHostState.FallbackUnavailable, null, true, XAdBlockLiveCheck.Unknown));
        Assert.Equal(XAdBlockIndication.None, BlueskyAdBlockPolicy.GetIndication(true, XProductionExtensionHostState.XUnavailable, XDiagnosticAdBlockState.BindingStale, true, XAdBlockLiveCheck.Unknown));
        // Fully removed through the Bluesky dialog: no line, unlike X's "Ad blocker not set up".
        Assert.Equal(XAdBlockIndication.None, BlueskyAdBlockPolicy.GetIndication(true, XProductionExtensionHostState.Available, XDiagnosticAdBlockState.NoManagedInstance, false, XAdBlockLiveCheck.Unknown));
        Assert.Equal(XAdBlockIndication.NotSetUp, XAdBlockIndicationPolicy.Evaluate(XProductionExtensionHostState.Available, XDiagnosticAdBlockState.NoManagedInstance, false, XAdBlockLiveCheck.Unknown));
    }

    [Fact]
    public void ExtensionDisabledBlueskyAllowsEnrollmentOnly()
    {
        var actions = BlueskyAdBlockPolicy.GetActions(XProductionExtensionHostState.FallbackUnavailable, null, false, false, enrollmentAllowed: true, verifiedXCopyExists: true);

        Assert.True(actions.CanInstall);
        Assert.True(actions.CanUseVerifiedXCopy);
        Assert.False(actions.CanBind);
        Assert.False(actions.CanEnable);
        Assert.False(actions.CanDisable);
        Assert.False(actions.CanRemove);
        Assert.False(actions.CanRefresh);
    }

    [Fact]
    public void NoEnrollmentWhileBusyFailedOrWithoutPermission()
    {
        Assert.False(BlueskyAdBlockPolicy.GetActions(XProductionExtensionHostState.FallbackUnavailable, null, false, true, true, true).CanInstall);
        Assert.False(BlueskyAdBlockPolicy.GetActions(XProductionExtensionHostState.FallbackUnavailable, null, false, false, false, true).CanInstall);
        Assert.False(BlueskyAdBlockPolicy.GetActions(XProductionExtensionHostState.XUnavailable, null, false, false, true, true).CanInstall);
        Assert.False(BlueskyAdBlockPolicy.GetActions(XProductionExtensionHostState.FallbackUnavailable, null, false, false, true, false).CanUseVerifiedXCopy);
    }

    [Fact]
    public void AvailableBlueskyFollowsTheXActionRules()
    {
        var snapshot = new XDiagnosticAdBlockSnapshot([], XDiagnosticAdBlockStatus.NoManagedInstance("none"));
        var x = XProductionAdBlockPolicy.GetActions(XProductionExtensionHostState.Available, snapshot, true, false);
        var bluesky = BlueskyAdBlockPolicy.GetActions(XProductionExtensionHostState.Available, snapshot, true, false, false, true);

        Assert.Equal(x.CanRefresh, bluesky.CanRefresh);
        Assert.Equal(x.CanInstall, bluesky.CanInstall);
        Assert.Equal(x.CanBind, bluesky.CanBind);
        Assert.Equal(x.CanEnable, bluesky.CanEnable);
        Assert.Equal(x.CanDisable, bluesky.CanDisable);
        Assert.Equal(x.CanRemove, bluesky.CanRemove);
        Assert.True(bluesky.CanUseVerifiedXCopy);
    }

    [Fact]
    public void DialogTitleAndTextsAreBlueskySpecific()
    {
        Assert.Equal("Bluesky Ad Blocker", BlueskyAdBlockPolicy.DialogTitle);
        Assert.Contains("6.47.0", BlueskyAdBlockPolicy.Explanation, StringComparison.Ordinal);
        Assert.Contains("X copy", BlueskyAdBlockPolicy.Explanation, StringComparison.Ordinal);
        Assert.DoesNotContain("hybrid", BlueskyAdBlockPolicy.Explanation + BlueskyAdBlockPolicy.NotSetUpMessage, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class BlueskyAdBlockSourceGuardTests
{
    private static readonly string Desktop = Path.Combine(SocialPaneSourceGuard.FindRepositoryRoot(), "src", "backend", "GeopoliticalMonitor.Desktop");

    [Fact]
    public void BlueskyAdBlockFilesDoNotUseTheXInstallRemoveReconcileOrBindingPaths()
    {
        var forbidden = new[]
        {
            "xProductionAdBlockService", "xProductionExtensionHostState", "xProductionRequiresTrustedReenrollment",
            "xProductionExtensionStartupFailure", "ShowXProductionAdBlockDialogAsync", "ShowXExtensionDiagnosticDialogAsync",
            "RunXExtensionCapabilityTestAsync", "GetProductionAdBlockBindingPath", "GetDiagnosticAdBlockBindingPath",
            "ProductionXProfileScope", "ObserveXAdBlockSnapshot", "UpdateXAdBlockIndication", "xAdBlockIndication",
            "XWebView", "Test Extension Support",
        };
        foreach (var name in new[] { "MainWindow.BlueskyAdBlock.cs", "MainWindow.BlueskyAdBlockIndication.cs" })
        {
            var source = File.ReadAllText(Path.Combine(Desktop, name));
            Assert.DoesNotContain("hybrid", source, StringComparison.OrdinalIgnoreCase);
            foreach (var item in forbidden)
            {
                // Ordinal: the camel-case X fields must not appear, while shared type names such as XProductionExtensionHostState may.
                Assert.False(source.Contains(item, StringComparison.Ordinal), $"{name} references {item}.");
            }
        }
    }

    [Fact]
    public void TheXManagedCopyIsReachedOnlyThroughTheReadOnlyVerifiedCopySource()
    {
        var source = File.ReadAllText(Path.Combine(Desktop, "MainWindow.BlueskyAdBlock.cs"));
        var lines = source.Split('\n');

        var reference = Assert.Single(lines, line => line.Contains("GetProductionAdBlockManagedSourceDirectory", StringComparison.Ordinal));
        var index = Array.IndexOf(lines, reference);
        Assert.Contains("new VerifiedXAdBlockCopySource(", lines[index - 1], StringComparison.Ordinal);

        var copySource = File.ReadAllText(Path.Combine(SocialPaneSourceGuard.FindRepositoryRoot(), "src", "backend", "GeopoliticalMonitor.Infrastructure", "Persistence", "VerifiedXAdBlockCopySource.cs"));
        foreach (var write in new[] { "Delete", "Move", "Copy", "Create", "Write", "SaveAsync", "ClearAsync", "PrepareAsync", "RemoveAsync" })
        {
            Assert.False(copySource.Contains(write + "(", StringComparison.Ordinal) || copySource.Contains("." + write, StringComparison.Ordinal), $"VerifiedXAdBlockCopySource uses {write}.");
        }
    }

    [Fact]
    public void BlueskyIndicationCodeCallsNoMutatingAdBlockApi()
    {
        var forbidden = new[]
        {
            "SecureForAuthenticatedNavigationAsync", "SetExtensionEnabledAsync", "AddBrowserExtensionAsync", "RemoveExtensionAsync",
            "SetEnabledAsync", "EnableAsync", "InstallAsync", "BindInstalledAsync", "RemoveAsync", "Reconcile", "GetSnapshotAsync",
            "IXDiagnosticAdBlockBindingStore", "FileXDiagnosticAdBlockBindingStore", "SaveAsync", "ClearAsync",
            "XDiagnosticAdBlockService", "InspectAsync", "PrepareAsync", "CopyAsync", "RecoverInterruptedActivationAsync",
            "XAdBlockPayloadDigest", "FileXAdBlockManagedPayloadStore", "FileXDiagnosticAdBlockSourceInspector",
        };
        var source = File.ReadAllText(Path.Combine(Desktop, "MainWindow.BlueskyAdBlockIndication.cs"));
        foreach (var api in forbidden)
        {
            Assert.False(source.Contains(api, StringComparison.Ordinal), $"MainWindow.BlueskyAdBlockIndication.cs references {api}.");
        }
    }

    [Fact]
    public void XAdBlockFilesDoNotReferenceTheBlueskyAdBlock()
    {
        var forbidden = new[] { "BlueskyAdBlock", "AdBlockBluesky", "bluesky-adblock", "ProductionBlueskyProfileScope", "blueskyExtension", "VerifiedXAdBlockCopySource" };
        foreach (var name in new[] { "MainWindow.xaml.cs", "MainWindow.AdBlockIndication.cs" })
        {
            var source = File.ReadAllText(Path.Combine(Desktop, name));
            foreach (var item in forbidden)
            {
                Assert.False(source.Contains(item, StringComparison.OrdinalIgnoreCase), $"{name} references {item}.");
            }
        }
    }

    [Fact]
    public void XStartupStillEvaluatesItsGateWithTheDefaultXScope()
    {
        var source = File.ReadAllText(Path.Combine(Desktop, "MainWindow.xaml.cs"));

        Assert.Contains(
            """
                        var trustDecision = XProductionAdBlockStartupTrustPolicy.Evaluate(
                            managedPayloadExists,
                            bindingStore.HasPersistedState || !activationRecovered,
                            binding,
                            managedValidation);
            """.ReplaceLineEndings("\n"),
            source.ReplaceLineEndings("\n"),
            StringComparison.Ordinal);
        Assert.Contains("XDiagnosticAdBlockBinding.ProductionXProfileScope,", source, StringComparison.Ordinal);
        Assert.Contains("pathProvider.GetProductionAdBlockManagedSourceDirectory()", source, StringComparison.Ordinal);
        Assert.Contains("pathProvider.GetProductionAdBlockBindingPath()", source, StringComparison.Ordinal);
    }
}
