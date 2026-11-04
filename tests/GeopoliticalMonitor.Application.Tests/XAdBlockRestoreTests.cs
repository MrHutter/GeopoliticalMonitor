using System.Reflection;
using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class XAdBlockRestoreServiceTests
{
    private const string ManagedPath = @"D:\AppData\GeopoliticalMonitor\Extensions\AdBlock\Current";
    private const string OtherPath = @"D:\Downloads\adblock\6.47.0_0";
    private const string BlueskyManagedPath = @"D:\AppData\GeopoliticalMonitor\Extensions\AdBlockBluesky\Current";

    [Fact]
    public async Task LostInstanceWithValidBindingAndVerifiedPayloadIsRestorable()
    {
        var context = Context(XBinding(ManagedPath));

        var snapshot = await context.Service.GetSnapshotAsync();

        Assert.Equal(XDiagnosticAdBlockState.BindingStale, snapshot.AdBlockStatus.State);
        Assert.True(snapshot.AdBlockStatus.BoundInstanceMissing);
        Assert.True(XProductionAdBlockPolicy.IsRestorableState(snapshot));
    }

    [Fact]
    public async Task RestoreReverifiesAddsFromTheManagedFolderAndRebindsToX()
    {
        var context = Context(XBinding(ManagedPath));
        Assert.True(XProductionAdBlockPolicy.IsRestorableState(await context.Service.GetSnapshotAsync()));
        var inspectionsBeforeClick = context.Inspector.Inspected.Count;

        var snapshot = await context.Service.RestoreBoundInstanceAsync();

        Assert.Equal(XDiagnosticAdBlockState.InstalledManagedEnabled, snapshot.AdBlockStatus.State);
        Assert.Equal([ManagedPath], context.Profile.AddedFrom);
        Assert.Equal(XDiagnosticAdBlockBinding.ProductionXProfileScope, context.Store.Binding?.ProfileScope);
        Assert.Equal(ManagedPath, context.Store.Binding?.SourceFolderPath);
        Assert.True(context.Store.Binding!.IsStructurallyValid());
        // The click authenticates the managed payload again; an earlier check is never trusted.
        Assert.True(context.Inspector.Inspected.Skip(inspectionsBeforeClick).Count(path => path == ManagedPath) >= 2);
    }

    [Fact]
    public async Task RestoreDoesNothingWithoutABinding()
    {
        var context = Context(binding: null);

        var snapshot = await context.Service.RestoreBoundInstanceAsync();

        AssertNotPerformed(context, snapshot, expectedBinding: null);
    }

    [Fact]
    public async Task RestoreDoesNothingForABlueskyScopedBindingInTheXProfile()
    {
        var blueskyScoped = XDiagnosticAdBlockBinding.Create(
            Trusted(ManagedPath).Manifest.ExpectedRuntimeId,
            Trusted(ManagedPath),
            XDiagnosticAdBlockBinding.ProductionBlueskyProfileScope);
        var context = Context(blueskyScoped);

        var snapshot = await context.Service.RestoreBoundInstanceAsync();

        Assert.False(snapshot.AdBlockStatus.BoundInstanceMissing);
        AssertNotPerformed(context, snapshot, blueskyScoped);
    }

    [Fact]
    public async Task RestoreDoesNothingForAnUntrustedOrStructurallyInvalidBinding()
    {
        foreach (var binding in new[]
                 {
                     XBinding(ManagedPath) with { TrustRecord = null },
                     XBinding(ManagedPath) with { SchemaVersion = 1 },
                     XBinding(ManagedPath) with { Payload = null },
                 })
        {
            var context = Context(binding);

            var snapshot = await context.Service.RestoreBoundInstanceAsync();

            Assert.False(snapshot.AdBlockStatus.BoundInstanceMissing);
            AssertNotPerformed(context, snapshot, binding);
        }
    }

    [Fact]
    public async Task RestoreDoesNothingWhenTheManagedPayloadWasTampered()
    {
        var binding = XBinding(ManagedPath);
        var context = Context(binding);
        context.Inspector.Tampered = true;

        var snapshot = await context.Service.RestoreBoundInstanceAsync();

        Assert.Equal(XDiagnosticAdBlockState.BindingStale, snapshot.AdBlockStatus.State);
        Assert.False(snapshot.AdBlockStatus.BoundInstanceMissing);
        AssertNotPerformed(context, snapshot, binding);
    }

    [Fact]
    public async Task RestoreRechecksThePayloadAtClickTime()
    {
        var binding = XBinding(ManagedPath);
        var context = Context(binding);
        Assert.True(XProductionAdBlockPolicy.IsRestorableState(await context.Service.GetSnapshotAsync()));
        context.Inspector.Tampered = true;

        var snapshot = await context.Service.RestoreBoundInstanceAsync();

        AssertNotPerformed(context, snapshot, binding);
    }

    [Fact]
    public async Task RestoreDoesNothingWhileTheBoundInstanceIsInstalled()
    {
        var binding = XBinding(ManagedPath);
        var context = Context(binding, new XBrowserExtensionInfo(Trusted(ManagedPath).Manifest.ExpectedRuntimeId, "AdBlock", IsEnabled: true));

        var snapshot = await context.Service.RestoreBoundInstanceAsync();

        Assert.True(snapshot.AdBlockStatus.IsManagedInstance);
        Assert.Empty(context.Profile.AddedFrom);
        Assert.Same(binding, context.Store.Binding);
    }

    [Fact]
    public async Task RestoreDoesNothingWhenTheBindingNamesAnotherFolder()
    {
        var binding = XBinding(OtherPath);
        var context = Context(binding);
        Assert.True((await context.Service.GetSnapshotAsync()).AdBlockStatus.BoundInstanceMissing);

        var snapshot = await context.Service.RestoreBoundInstanceAsync();

        AssertNotPerformed(context, snapshot, binding);
        Assert.DoesNotContain(OtherPath, context.Profile.AddedFrom);
    }

    [Fact]
    public async Task RestoreIsXOnlyAndRefusesInABlueskyScopedService()
    {
        var source = Trusted(BlueskyManagedPath);
        var binding = XDiagnosticAdBlockBinding.Create(source.Manifest.ExpectedRuntimeId, source, XDiagnosticAdBlockBinding.ProductionBlueskyProfileScope);
        var context = Context(binding, scope: XDiagnosticAdBlockBinding.ProductionBlueskyProfileScope, managedPath: BlueskyManagedPath);

        var snapshot = await context.Service.RestoreBoundInstanceAsync();

        AssertNotPerformed(context, snapshot, binding);
    }

    [Fact]
    public void RestoreTakesNoSourceFolder()
    {
        var method = typeof(XDiagnosticAdBlockService).GetMethod(nameof(XDiagnosticAdBlockService.RestoreBoundInstanceAsync));

        Assert.NotNull(method);
        var parameter = Assert.Single(method!.GetParameters());
        Assert.Equal(typeof(CancellationToken), parameter.ParameterType);
    }

    private static void AssertNotPerformed(TestContext context, XDiagnosticAdBlockSnapshot snapshot, XDiagnosticAdBlockBinding? expectedBinding)
    {
        Assert.Empty(context.Profile.AddedFrom);
        Assert.Equal(0, context.Store.SaveCount);
        Assert.Same(expectedBinding, context.Store.Binding);
        Assert.NotNull(snapshot.AdBlockStatus.Message);
        Assert.StartsWith(XProductionAdBlockPolicy.RestoreNotPerformedMessage, snapshot.AdBlockStatus.Message, StringComparison.Ordinal);
        Assert.True(snapshot.AdBlockStatus.Message!.Length < 400);
        Assert.False(snapshot.AdBlockStatus.IsManagedInstance);
    }

    private static XDiagnosticAdBlockBinding XBinding(string path) =>
        XDiagnosticAdBlockBinding.Create(Trusted(path).Manifest.ExpectedRuntimeId, Trusted(path), XDiagnosticAdBlockBinding.ProductionXProfileScope);

    private static XDiagnosticAdBlockSourceIdentity Trusted(string path) => BlueskyAdBlockScopeTests.TrustedSource(path);

    private static TestContext Context(
        XDiagnosticAdBlockBinding? binding,
        XBrowserExtensionInfo? installed = null,
        string scope = XDiagnosticAdBlockBinding.ProductionXProfileScope,
        string managedPath = ManagedPath)
    {
        var profile = new RecordingProfile(installed is null ? [] : [installed])
        {
            ToAdd = new XBrowserExtensionInfo(Trusted(managedPath).Manifest.ExpectedRuntimeId, "AdBlock", IsEnabled: true),
        };
        var store = new CountingBindingStore { Binding = binding };
        var inspector = new SwitchableInspector();
        return new(new XDiagnosticAdBlockService(profile, store, inspector, scope, managedPath), profile, store, inspector);
    }

    private sealed record TestContext(
        XDiagnosticAdBlockService Service,
        RecordingProfile Profile,
        CountingBindingStore Store,
        SwitchableInspector Inspector);

    private sealed class CountingBindingStore : IXDiagnosticAdBlockBindingStore
    {
        public XDiagnosticAdBlockBinding? Binding { get; set; }

        public int SaveCount { get; private set; }

        public Task<XDiagnosticAdBlockBinding?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Binding);

        public Task SaveAsync(XDiagnosticAdBlockBinding binding, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            Binding = binding;
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            Binding = null;
            return Task.CompletedTask;
        }
    }

    /// <summary>Authenticates any folder as the reviewed payload, or as a changed payload once <see cref="Tampered"/> is set.</summary>
    private sealed class SwitchableInspector : IXDiagnosticAdBlockSourceInspector
    {
        public bool Tampered { get; set; }

        public List<string> Inspected { get; } = [];

        public Task<XDiagnosticAdBlockSourceValidation> InspectAsync(string sourceFolderPath, CancellationToken cancellationToken = default)
        {
            Inspected.Add(sourceFolderPath);
            var trusted = Trusted(sourceFolderPath);
            var identity = Tampered
                ? trusted with { Payload = trusted.Payload! with { Sha256 = new string('0', 64) }, TrustRecord = null }
                : trusted;
            return Task.FromResult(XDiagnosticAdBlockSourceValidation.Valid(identity));
        }
    }

    private sealed class RecordingProfile(IEnumerable<XBrowserExtensionInfo> extensions) : IXBrowserExtensionProfile
    {
        private readonly List<XBrowserExtensionInfo> installed = extensions.ToList();

        public XBrowserExtensionInfo? ToAdd { get; init; }

        public List<string> AddedFrom { get; } = [];

        public Task<IReadOnlyList<XBrowserExtensionInfo>> GetInstalledExtensionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<XBrowserExtensionInfo>>(installed.ToArray());

        public Task<XBrowserExtensionInfo> AddBrowserExtensionAsync(string extensionFolderPath, CancellationToken cancellationToken = default)
        {
            AddedFrom.Add(extensionFolderPath);
            var added = ToAdd ?? throw new InvalidOperationException("No extension to add.");
            installed.Add(added);
            return Task.FromResult(added);
        }

        public Task SetExtensionEnabledAsync(string extensionId, bool enabled, CancellationToken cancellationToken = default)
        {
            var index = installed.FindIndex(item => item.Id == extensionId);
            installed[index] = installed[index] with { IsEnabled = enabled };
            return Task.CompletedTask;
        }

        public Task RemoveExtensionAsync(string extensionId, CancellationToken cancellationToken = default)
        {
            installed.RemoveAll(item => item.Id == extensionId);
            return Task.CompletedTask;
        }
    }
}

public sealed class XAdBlockRestorePolicyTests
{
    private static readonly XDiagnosticAdBlockSnapshot Restorable = new(
        [],
        XDiagnosticAdBlockStatus.BindingStale("The bound production X AdBlock instance is no longer installed; management is disabled.") with
        {
            BoundInstanceMissing = true,
        });

    private static readonly XDiagnosticAdBlockSnapshot StaleOnly = new(
        [],
        XDiagnosticAdBlockStatus.BindingStale("The saved production X binding is invalid; management is disabled."));

    [Fact]
    public void RestoreIsOfferedOnlyInTheLostButRestorableState()
    {
        Assert.True(XProductionAdBlockPolicy.CanRestoreTrustedInstance(XProductionExtensionHostState.Available, Restorable, hasManagedSource: true, isBusy: false));

        Assert.False(XProductionAdBlockPolicy.CanRestoreTrustedInstance(XProductionExtensionHostState.Available, Restorable, true, isBusy: true));
        Assert.False(XProductionAdBlockPolicy.CanRestoreTrustedInstance(XProductionExtensionHostState.FallbackUnavailable, Restorable, true, false));
        Assert.False(XProductionAdBlockPolicy.CanRestoreTrustedInstance(XProductionExtensionHostState.XUnavailable, Restorable, true, false));
        Assert.False(XProductionAdBlockPolicy.CanRestoreTrustedInstance(XProductionExtensionHostState.Available, Restorable, hasManagedSource: false, false));
        Assert.False(XProductionAdBlockPolicy.CanRestoreTrustedInstance(XProductionExtensionHostState.Available, StaleOnly, true, false));
        Assert.False(XProductionAdBlockPolicy.CanRestoreTrustedInstance(XProductionExtensionHostState.Available, null, true, false));
    }

    [Theory]
    [InlineData(XDiagnosticAdBlockState.NoManagedInstance)]
    [InlineData(XDiagnosticAdBlockState.InstalledManagedEnabled)]
    [InlineData(XDiagnosticAdBlockState.InstalledManagedDisabled)]
    [InlineData(XDiagnosticAdBlockState.InstalledUnmanaged)]
    [InlineData(XDiagnosticAdBlockState.InvalidFolder)]
    [InlineData(XDiagnosticAdBlockState.OperationFailed)]
    public void RestoreIsNotOfferedInOtherStatesEvenWithTheFlag(XDiagnosticAdBlockState state)
    {
        var snapshot = new XDiagnosticAdBlockSnapshot(
            [],
            new XDiagnosticAdBlockStatus(state, null, null, null, null, null, "state") { BoundInstanceMissing = true });

        Assert.False(XProductionAdBlockPolicy.IsRestorableState(snapshot));
        Assert.False(XProductionAdBlockPolicy.CanRestoreTrustedInstance(XProductionExtensionHostState.Available, snapshot, true, false));
    }

    [Fact]
    public void TheRestorableMarkerLeavesExistingActionsTextsAndIndicationUnchanged()
    {
        Assert.Equal(
            XProductionAdBlockPolicy.GetActions(XProductionExtensionHostState.Available, StaleOnly, true, false),
            XProductionAdBlockPolicy.GetActions(XProductionExtensionHostState.Available, Restorable, true, false));
        Assert.Equal(
            XProductionAdBlockPolicy.Format(XProductionExtensionHostState.Available, Restorable with { AdBlockStatus = Restorable.AdBlockStatus with { BoundInstanceMissing = false } }, true),
            XProductionAdBlockPolicy.Format(XProductionExtensionHostState.Available, Restorable, true));
        Assert.Equal(
            XAdBlockIndication.Inactive,
            XAdBlockIndicationPolicy.Evaluate(XProductionExtensionHostState.Available, Restorable.AdBlockStatus.State, true, XAdBlockLiveCheck.Unknown));
    }

    [Fact]
    public void RestoreTextsAreBoundedAndPlain()
    {
        Assert.Equal("Restore trusted instance", XProductionAdBlockPolicy.RestoreActionText);
        Assert.Contains("missing", XProductionAdBlockPolicy.RestoreHint, StringComparison.Ordinal);
        Assert.Contains("no folder is chosen", XProductionAdBlockPolicy.RestoreHint, StringComparison.Ordinal);
        Assert.Contains("nothing was changed", XProductionAdBlockPolicy.RestoreNotPerformedMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactTextMatchesTheExceptionRedaction()
    {
        const string text = @"Failed at C:\Users\owner\AppData\Local\GeopoliticalMonitor\Extensions using https://x.com/private?token=secret";

        Assert.Equal(
            SafeDiagnosticExceptionFormatter.GetSafeMessage(new InvalidOperationException(text)),
            SafeDiagnosticExceptionFormatter.RedactText(text));
        Assert.DoesNotContain("owner", SafeDiagnosticExceptionFormatter.RedactText(text), StringComparison.Ordinal);
        Assert.DoesNotContain("https://", SafeDiagnosticExceptionFormatter.RedactText(text), StringComparison.Ordinal);
    }
}

public sealed class XAdBlockRestoreSourceGuardTests
{
    private static readonly string Root = SocialPaneSourceGuard.FindRepositoryRoot();
    private static readonly string Desktop = Path.Combine(Root, "src", "backend", "GeopoliticalMonitor.Desktop");

    [Fact]
    public void RestoreMethodAddsOnlyFromTheConfiguredManagedFolderAndNeverTouchesBluesky()
    {
        var source = File.ReadAllText(Path.Combine(Root, "src", "backend", "GeopoliticalMonitor.Application", "XBrowser", "XDiagnosticAdBlock.cs"));
        var body = Between(source, "public async Task<XDiagnosticAdBlockSnapshot> RestoreBoundInstanceAsync(", "public async Task<XDiagnosticAdBlockSnapshot> BindInstalledAsync(");

        Assert.Contains("return await InstallAsync(unmanagedCandidateSourcePath, cancellationToken)", body, StringComparison.Ordinal);
        Assert.Contains("XDiagnosticAdBlockBinding.ProductionXProfileScope", body, StringComparison.Ordinal);
        foreach (var forbidden in new[] { "Bluesky", "PrepareAsync", "Copy", "Delete", "Move", "SaveAsync", "ClearAsync", "RemoveExtensionAsync" })
        {
            Assert.False(body.Contains(forbidden, StringComparison.Ordinal), $"RestoreBoundInstanceAsync references {forbidden}.");
        }
    }

    [Fact]
    public void XDialogRestoreIsUserInvokedWithoutAFolderPicker()
    {
        var source = File.ReadAllText(Path.Combine(Desktop, "MainWindow.xaml.cs"));
        var handler = Between(source, "restoreButton.Click +=", "dialog.Opened +=");

        Assert.Contains("service => service.RestoreBoundInstanceAsync()", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("FolderPicker", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("Bluesky", handler, StringComparison.OrdinalIgnoreCase);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(source, @"RestoreBoundInstanceAsync\("));
        Assert.DoesNotContain("RestoreBoundInstanceAsync", File.ReadAllText(Path.Combine(Desktop, "MainWindow.BlueskyAdBlock.cs")), StringComparison.Ordinal);
    }

    private static string Between(string source, string start, string end)
    {
        var from = source.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"Missing '{start}'.");
        var to = source.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        Assert.True(to > from, $"Missing '{end}' after '{start}'.");
        return source[(from + start.Length)..to];
    }
}
