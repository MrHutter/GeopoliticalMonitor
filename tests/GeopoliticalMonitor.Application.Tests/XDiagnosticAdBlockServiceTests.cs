using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class XDiagnosticAdBlockServiceTests
{
    private const string MicrosoftClipboardExtensionId = "dgiklkfkllikcanfonkcabmbdfmgleag";
    private const string MicrosoftPdfViewerExtensionId = "mhjfbmdgcfjbbpaeojofohoefgiehjai";
    private const string SourcePath = @"C:\Official\AdBlock";
    private static readonly XDiagnosticAdBlockSourceIdentity Source = CreateSource(SourcePath);

    [Fact]
    public async Task ArbitraryExistingExtensionsAreNeverManagedWithoutBinding()
    {
        var profile = new FakeExtensionProfile(
            MicrosoftExtensions().Append(new XBrowserExtensionInfo("unrelated-id", "Unrelated", IsEnabled: true)));
        var context = CreateContext(profile);

        var snapshot = await context.Service.GetSnapshotAsync();
        await context.Service.SetEnabledAsync(enabled: false);
        await context.Service.RemoveAsync();

        Assert.Equal(XDiagnosticAdBlockState.NoManagedInstance, snapshot.AdBlockStatus.State);
        Assert.Empty(profile.EnabledTargets);
        Assert.Empty(profile.RemovedTargets);
        AssertMicrosoftComponentsRemainEnabled(profile.Extensions);
    }

    [Fact]
    public async Task KnownStoreIdentityIsMetadataAndNotManagementAuthority()
    {
        var storeExtension = new XBrowserExtensionInfo(
            XAdBlockExtensionPolicy.ChromeExtensionId,
            "AdBlock",
            IsEnabled: true);
        var profile = new FakeExtensionProfile(MicrosoftExtensions().Append(storeExtension));
        var context = CreateContext(profile);

        var snapshot = await context.Service.GetSnapshotAsync();

        Assert.Equal(XDiagnosticAdBlockState.InstalledUnmanaged, snapshot.AdBlockStatus.State);
        Assert.True(XAdBlockExtensionPolicy.HasKnownStoreIdentity(storeExtension));
        Assert.False(snapshot.AdBlockStatus.IsManagedInstance);
    }

    [Fact]
    public async Task ExplicitBindSelectsOnlyPathDerivedInstalledInstance()
    {
        var installed = new XBrowserExtensionInfo(Source.Manifest.ExpectedRuntimeId, "AdBlock", IsEnabled: true);
        var profile = new FakeExtensionProfile(MicrosoftExtensions().Append(installed));
        var context = CreateContext(profile);

        var snapshot = await context.Service.BindInstalledAsync(SourcePath);

        Assert.Equal(XDiagnosticAdBlockState.InstalledManagedEnabled, snapshot.AdBlockStatus.State);
        Assert.Equal(installed.Id, context.Store.Binding?.RuntimeExtensionId);
        Assert.Equal(SourcePath, context.Store.Binding?.SourceFolderPath);
        AssertMicrosoftComponentsRemainEnabled(snapshot.Extensions);
    }

    [Fact]
    public async Task ExplicitBindDoesNotAdoptUnrelatedOrMicrosoftExtension()
    {
        var unrelatedProfile = new FakeExtensionProfile(
            MicrosoftExtensions().Append(new XBrowserExtensionInfo("unrelated-id", "AdBlock", IsEnabled: true)));
        var unrelatedContext = CreateContext(unrelatedProfile);

        var unrelated = await unrelatedContext.Service.BindInstalledAsync(SourcePath);

        Assert.Equal(XDiagnosticAdBlockState.OperationFailed, unrelated.AdBlockStatus.State);
        Assert.Null(unrelatedContext.Store.Binding);

        var microsoftSource = CreateSource(SourcePath, MicrosoftClipboardExtensionId);
        var microsoftProfile = new FakeExtensionProfile(MicrosoftExtensions());
        var microsoftContext = CreateContext(microsoftProfile, microsoftSource);

        var microsoft = await microsoftContext.Service.BindInstalledAsync(SourcePath);

        Assert.Equal(XDiagnosticAdBlockState.OperationFailed, microsoft.AdBlockStatus.State);
        Assert.Null(microsoftContext.Store.Binding);
        AssertMicrosoftComponentsRemainEnabled(microsoft.Extensions);
    }

    [Fact]
    public async Task InstallUsesBaselineDeltaReturnedIdAndPostInstallEnumeration()
    {
        var installed = new XBrowserExtensionInfo(Source.Manifest.ExpectedRuntimeId, "AdBlock", IsEnabled: true);
        var unrelated = new XBrowserExtensionInfo("unrelated-id", "Unrelated", IsEnabled: true);
        var profile = new FakeExtensionProfile(MicrosoftExtensions().Append(unrelated)) { ExtensionToAdd = installed };
        var context = CreateContext(profile);

        var snapshot = await context.Service.InstallAsync(SourcePath);

        Assert.Equal(1, profile.AddCount);
        Assert.True(profile.EnumerationCount >= 2);
        Assert.Equal(XDiagnosticAdBlockState.InstalledManagedEnabled, snapshot.AdBlockStatus.State);
        Assert.Equal(installed.Id, context.Store.Binding?.RuntimeExtensionId);
        Assert.Contains(unrelated, snapshot.Extensions);
        AssertMicrosoftComponentsRemainEnabled(snapshot.Extensions);
    }

    [Fact]
    public async Task InstallDoesNotReinstallExistingUnboundPathInstance()
    {
        var installed = new XBrowserExtensionInfo(Source.Manifest.ExpectedRuntimeId, "AdBlock", IsEnabled: true);
        var profile = new FakeExtensionProfile(MicrosoftExtensions().Append(installed));
        var context = CreateContext(profile);

        var snapshot = await context.Service.InstallAsync(SourcePath);

        Assert.Equal(0, profile.AddCount);
        Assert.Equal(XDiagnosticAdBlockState.InstalledUnmanaged, snapshot.AdBlockStatus.State);
        Assert.Contains("Bind Installed AdBlock", snapshot.AdBlockStatus.Message);
        Assert.Null(context.Store.Binding);
    }

    [Fact]
    public async Task InstallMismatchRemainsUnmanagedAndDoesNotPersistBinding()
    {
        var unexpected = new XBrowserExtensionInfo("unexpected-runtime-id", "Unexpected", IsEnabled: true);
        var profile = new FakeExtensionProfile(MicrosoftExtensions()) { ExtensionToAdd = unexpected };
        var context = CreateContext(profile);

        var snapshot = await context.Service.InstallAsync(SourcePath);

        Assert.Equal(XDiagnosticAdBlockState.InstalledUnmanaged, snapshot.AdBlockStatus.State);
        Assert.Equal(unexpected.Id, snapshot.AdBlockStatus.Extension?.Id);
        Assert.Null(context.Store.Binding);
    }

    [Fact]
    public async Task PersistedBindingIsRevalidatedAgainstSourceAndProfile()
    {
        var installed = new XBrowserExtensionInfo(Source.Manifest.ExpectedRuntimeId, "AdBlock", IsEnabled: false);
        var binding = XDiagnosticAdBlockBinding.Create(installed.Id, Source);
        var profile = new FakeExtensionProfile(MicrosoftExtensions().Append(installed));
        var context = CreateContext(profile, binding: binding);

        var snapshot = await context.Service.GetSnapshotAsync();

        Assert.Equal(XDiagnosticAdBlockState.InstalledManagedDisabled, snapshot.AdBlockStatus.State);
        Assert.Equal(installed.Id, snapshot.AdBlockStatus.Extension?.Id);
        Assert.Equal(1, context.Inspector.InspectionCount);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task StateChangeTargetsOnlyRevalidatedManagedInstance(bool initialState, bool requestedState)
    {
        var installed = new XBrowserExtensionInfo(Source.Manifest.ExpectedRuntimeId, "AdBlock", initialState);
        var profile = new FakeExtensionProfile(
            MicrosoftExtensions()
                .Append(new XBrowserExtensionInfo("unrelated-id", "Unrelated", IsEnabled: true))
                .Append(installed));
        var context = CreateContext(profile, binding: XDiagnosticAdBlockBinding.Create(installed.Id, Source));

        var snapshot = await context.Service.SetEnabledAsync(requestedState);

        Assert.Equal([installed.Id], profile.EnabledTargets);
        Assert.Equal(requestedState, snapshot.AdBlockStatus.Extension?.IsEnabled);
        AssertMicrosoftComponentsRemainEnabled(snapshot.Extensions);
    }

    [Fact]
    public async Task RemoveTargetsOnlyManagedInstanceAndClearsBinding()
    {
        var installed = new XBrowserExtensionInfo(Source.Manifest.ExpectedRuntimeId, "AdBlock", IsEnabled: true);
        var profile = new FakeExtensionProfile(MicrosoftExtensions().Append(installed));
        var context = CreateContext(profile, binding: XDiagnosticAdBlockBinding.Create(installed.Id, Source));

        var snapshot = await context.Service.RemoveAsync();

        Assert.Equal([installed.Id], profile.RemovedTargets);
        Assert.Equal(XDiagnosticAdBlockState.NoManagedInstance, snapshot.AdBlockStatus.State);
        Assert.Null(context.Store.Binding);
        Assert.True(context.Store.WasCleared);
        AssertMicrosoftComponentsRemainEnabled(snapshot.Extensions);
    }

    [Fact]
    public async Task StaleOrMissingBindingNeverRetargetsAnotherExtension()
    {
        var binding = XDiagnosticAdBlockBinding.Create(Source.Manifest.ExpectedRuntimeId, Source);
        var profile = new FakeExtensionProfile(
            MicrosoftExtensions().Append(new XBrowserExtensionInfo("different-runtime-id", "AdBlock", IsEnabled: true)));
        var context = CreateContext(profile, Source, binding);

        var snapshot = await context.Service.GetSnapshotAsync();
        await context.Service.SetEnabledAsync(enabled: false);
        await context.Service.RemoveAsync();

        Assert.Equal(XDiagnosticAdBlockState.BindingStale, snapshot.AdBlockStatus.State);
        Assert.Empty(profile.EnabledTargets);
        Assert.Empty(profile.RemovedTargets);
        AssertMicrosoftComponentsRemainEnabled(profile.Extensions);
    }

    [Fact]
    public async Task InvalidProfileScopeDisablesManagement()
    {
        var installed = new XBrowserExtensionInfo(Source.Manifest.ExpectedRuntimeId, "AdBlock", IsEnabled: true);
        var binding = XDiagnosticAdBlockBinding.Create(installed.Id, Source) with { ProfileScope = "ProductionX" };
        var profile = new FakeExtensionProfile(MicrosoftExtensions().Append(installed));
        var context = CreateContext(profile, binding: binding);

        var snapshot = await context.Service.GetSnapshotAsync();

        Assert.Equal(XDiagnosticAdBlockState.BindingStale, snapshot.AdBlockStatus.State);
        Assert.False(snapshot.AdBlockStatus.IsManagedInstance);
    }

    [Fact]
    public async Task ProductionProfilePersistsProductionScopeAndRejectsDiagnosticBinding()
    {
        var installed = new XBrowserExtensionInfo(Source.Manifest.ExpectedRuntimeId, "AdBlock", IsEnabled: true);
        var profile = new FakeExtensionProfile(MicrosoftExtensions().Append(installed));
        var production = CreateContext(
            profile,
            profileScope: XDiagnosticAdBlockBinding.ProductionXProfileScope);

        var bound = await production.Service.BindInstalledAsync(SourcePath);

        Assert.Equal(XDiagnosticAdBlockState.InstalledManagedEnabled, bound.AdBlockStatus.State);
        Assert.Equal([installed.Id], profile.RemovedTargets);
        Assert.Equal(1, profile.AddCount);
        Assert.Equal(XDiagnosticAdBlockBinding.ProductionXProfileScope, production.Store.Binding?.ProfileScope);

        production.Store.Binding = XDiagnosticAdBlockBinding.Create(installed.Id, Source);
        var rejected = await production.Service.GetSnapshotAsync();

        Assert.Equal(XDiagnosticAdBlockState.BindingStale, rejected.AdBlockStatus.State);
        Assert.False(rejected.AdBlockStatus.IsManagedInstance);
    }

    [Fact]
    public async Task ProductionCandidateReportsExactManagedPathInstanceAsInstalledUnmanaged()
    {
        var installed = new XBrowserExtensionInfo(Source.Manifest.ExpectedRuntimeId, "AdBlock", IsEnabled: true);
        var profile = new FakeExtensionProfile(MicrosoftExtensions().Append(installed));
        var production = CreateContext(
            profile,
            profileScope: XDiagnosticAdBlockBinding.ProductionXProfileScope,
            unmanagedCandidateSourcePath: SourcePath);

        var snapshot = await production.Service.GetSnapshotAsync();

        Assert.Equal(XDiagnosticAdBlockState.InstalledUnmanaged, snapshot.AdBlockStatus.State);
        Assert.Equal(installed.Id, snapshot.AdBlockStatus.Extension?.Id);
        Assert.Contains("Use Reconcile", snapshot.AdBlockStatus.Message);
        Assert.Null(production.Store.Binding);
    }

    [Fact]
    public async Task AuthenticatedNavigationDisablesExactUnboundRuntimeEvenWithUnrelatedName()
    {
        var source = CreateSource(SourcePath, "managed-path-runtime-id");
        var installed = new XBrowserExtensionInfo(source.Manifest.ExpectedRuntimeId, "Renamed Extension", IsEnabled: true);
        var profile = new FakeExtensionProfile(MicrosoftExtensions().Append(installed));
        var production = CreateContext(
            profile,
            source,
            profileScope: XDiagnosticAdBlockBinding.ProductionXProfileScope,
            unmanagedCandidateSourcePath: SourcePath);

        await production.Service.SecureForAuthenticatedNavigationAsync();

        Assert.Equal([installed.Id], profile.EnabledTargets);
        Assert.False(profile.Extensions.Single(extension => extension.Id == installed.Id).IsEnabled);
    }

    [Fact]
    public async Task InstallationFailureIsPropagatedForBoundedUiMapping()
    {
        var profile = new FakeExtensionProfile(MicrosoftExtensions())
        {
            AddException = new InvalidOperationException("synthetic install failure"),
        };
        var context = CreateContext(profile);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Service.InstallAsync(SourcePath));

        Assert.Equal("synthetic install failure", exception.Message);
        Assert.Equal(1, profile.AddCount);
        AssertMicrosoftComponentsRemainEnabled(profile.Extensions);
    }

    private static ServiceContext CreateContext(
        FakeExtensionProfile profile,
        XDiagnosticAdBlockSourceIdentity? source = null,
        XDiagnosticAdBlockBinding? binding = null,
        string profileScope = XDiagnosticAdBlockBinding.DiagnosticProfileScope,
        string? unmanagedCandidateSourcePath = null)
    {
        var store = new FakeBindingStore { Binding = binding };
        var inspector = new FakeSourceInspector(source ?? Source);
        return new(
            new XDiagnosticAdBlockService(profile, store, inspector, profileScope, unmanagedCandidateSourcePath),
            store,
            inspector);
    }

    private static XDiagnosticAdBlockSourceIdentity CreateSource(
        string sourcePath,
        string? runtimeId = null)
    {
        var trust = OfficialAdBlockTrustRecords.ChromeWebStore6470;
        return new(
            sourcePath,
            new XDiagnosticAdBlockManifestIdentity(
            ManifestVersion: 3,
            Version: trust.ExtensionVersion,
            RawName: "__MSG_name__",
            DefaultLocale: "en_US",
            DefaultLocaleName: "AdBlock - block ads across the web",
            Author: "Adblock, Inc.",
            HasKey: true,
            ManifestSha256: "manifest-hash",
            ExpectedRuntimeId: runtimeId ?? trust.OfficialStoreExtensionId)
            {
                OfficialStoreExtensionId = trust.OfficialStoreExtensionId,
            },
            new XAdBlockPayloadIdentity(
                trust.DigestFormat,
                trust.PayloadSha256,
                FileCount: 443,
                TotalBytes: 326_555_718,
                DirectoryCount: 36,
                MaximumDirectoryDepth: 7,
                LongestPathUtf8Bytes: 128),
            trust);
    }

    private static IEnumerable<XBrowserExtensionInfo> MicrosoftExtensions()
    {
        yield return new(MicrosoftClipboardExtensionId, "Microsoft Clipboard Extension", IsEnabled: true);
        yield return new(MicrosoftPdfViewerExtensionId, "Microsoft Edge PDF Viewer", IsEnabled: true);
    }

    private static void AssertMicrosoftComponentsRemainEnabled(IReadOnlyList<XBrowserExtensionInfo> extensions)
    {
        Assert.Contains(extensions, extension => extension.Id == MicrosoftClipboardExtensionId && extension.IsEnabled);
        Assert.Contains(extensions, extension => extension.Id == MicrosoftPdfViewerExtensionId && extension.IsEnabled);
    }

    private sealed record ServiceContext(
        XDiagnosticAdBlockService Service,
        FakeBindingStore Store,
        FakeSourceInspector Inspector);

    private sealed class FakeBindingStore : IXDiagnosticAdBlockBindingStore
    {
        public XDiagnosticAdBlockBinding? Binding { get; set; }

        public bool WasCleared { get; private set; }

        public Task<XDiagnosticAdBlockBinding?> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Binding);

        public Task SaveAsync(XDiagnosticAdBlockBinding binding, CancellationToken cancellationToken = default)
        {
            Binding = binding;
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            Binding = null;
            WasCleared = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSourceInspector(XDiagnosticAdBlockSourceIdentity source) : IXDiagnosticAdBlockSourceInspector
    {
        public int InspectionCount { get; private set; }

        public Task<XDiagnosticAdBlockSourceValidation> InspectAsync(
            string sourceFolderPath,
            CancellationToken cancellationToken = default)
        {
            InspectionCount++;
            return Task.FromResult(XDiagnosticAdBlockSourceValidation.Valid(source));
        }
    }

    private sealed class FakeExtensionProfile : IXBrowserExtensionProfile
    {
        public FakeExtensionProfile(IEnumerable<XBrowserExtensionInfo> extensions)
        {
            Extensions = extensions.ToList();
        }

        public List<XBrowserExtensionInfo> Extensions { get; }

        public XBrowserExtensionInfo ExtensionToAdd { get; set; } = new(
            Source.Manifest.ExpectedRuntimeId,
            "AdBlock",
            IsEnabled: true);

        public Exception? AddException { get; set; }

        public int AddCount { get; private set; }

        public int EnumerationCount { get; private set; }

        public List<string> EnabledTargets { get; } = [];

        public List<string> RemovedTargets { get; } = [];

        public Task<IReadOnlyList<XBrowserExtensionInfo>> GetInstalledExtensionsAsync(CancellationToken cancellationToken = default)
        {
            EnumerationCount++;
            return Task.FromResult<IReadOnlyList<XBrowserExtensionInfo>>(Extensions.ToArray());
        }

        public Task<XBrowserExtensionInfo> AddBrowserExtensionAsync(
            string extensionFolderPath,
            CancellationToken cancellationToken = default)
        {
            AddCount++;
            if (AddException is not null)
            {
                return Task.FromException<XBrowserExtensionInfo>(AddException);
            }

            Extensions.Add(ExtensionToAdd);
            return Task.FromResult(ExtensionToAdd);
        }

        public Task SetExtensionEnabledAsync(
            string extensionId,
            bool enabled,
            CancellationToken cancellationToken = default)
        {
            EnabledTargets.Add(extensionId);
            var index = Extensions.FindIndex(extension => extension.Id == extensionId);
            if (index >= 0)
            {
                Extensions[index] = Extensions[index] with { IsEnabled = enabled };
            }

            return Task.CompletedTask;
        }

        public Task RemoveExtensionAsync(string extensionId, CancellationToken cancellationToken = default)
        {
            RemovedTargets.Add(extensionId);
            Extensions.RemoveAll(extension => extension.Id == extensionId);
            return Task.CompletedTask;
        }
    }
}
