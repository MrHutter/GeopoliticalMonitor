using System.Text.Json;
using System.Text.RegularExpressions;
using GeopoliticalMonitor.Application.DesktopBridge;
using GeopoliticalMonitor.Application.Firms;

namespace GeopoliticalMonitor.Application.Tests;

public class FirmsKeyTests
{
    // Obviously fake test values: one well-formed 32-hex string and one distinctive non-hex string.
    private const string FakeHexKey = "0123456789abcdef0123456789abcdef";
    private const string DistinctFakeKey = "fake-key-zz-never-real-zz-0000";

    [Theory]
    [InlineData("  " + FakeHexKey + "  ", FakeHexKey)]
    [InlineData("\"" + FakeHexKey + "\"", FakeHexKey)]
    [InlineData("'" + FakeHexKey + "'", FakeHexKey)]
    [InlineData(" \" " + FakeHexKey + " \" ", FakeHexKey)]
    [InlineData("\"" + FakeHexKey + "'", "\"" + FakeHexKey + "'")]
    [InlineData("\"", "\"")]
    [InlineData("hello", "hello")]
    public void NormalizationTrimsAndStripsOneMatchingQuotePair(string input, string expected)
    {
        Assert.Equal(expected, FirmsMapKey.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"\"")]
    [InlineData(" '  ' ")]
    public void EmptyAfterNormalizationIsNotAKey(string? input)
    {
        Assert.Null(FirmsMapKey.Normalize(input));
        Assert.Null(FirmsMapKey.FromInput(input));
    }

    [Theory]
    [InlineData(FakeHexKey, true)]
    [InlineData("0123456789ABCDEF0123456789ABCDEF", true)]
    [InlineData("0123456789abcdef0123456789abcde", false)]
    [InlineData("0123456789abcdef0123456789abcdef0", false)]
    [InlineData("0123456789abcdef0123456789abcdeg", false)]
    [InlineData(DistinctFakeKey, false)]
    public void ExpectedFormatIsExactly32HexCharacters(string input, bool expected)
    {
        Assert.Equal(expected, FirmsMapKey.FromInput(input)!.HasExpectedFormat);
        Assert.Equal(!expected, FirmsKeyService.ShouldWarn(input));
    }

    [Fact]
    public void KeyNeverPrintsItsValue()
    {
        var key = FirmsMapKey.FromInput(DistinctFakeKey)!;
        var resolved = new FirmsResolvedKey(FirmsKeySource.App, key, false);

        Assert.Equal(FirmsMapKey.RedactedText, key.ToString());
        Assert.DoesNotContain(DistinctFakeKey, $"{key}", StringComparison.Ordinal);
        Assert.DoesNotContain(DistinctFakeKey, resolved.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(DistinctFakeKey, new FirmsKeyStoreRead(FirmsKeyStoreOutcome.Succeeded, key).ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void AppKeyWinsOverEnvironment()
    {
        var store = new FakeKeyStore(FakeHexKey);
        var resolved = new FirmsKeyResolver(store, () => DistinctFakeKey).Resolve();

        Assert.Equal(FirmsKeySource.App, resolved.Source);
        Assert.Equal(FakeHexKey, resolved.Key!.RevealForProviderRequest());
    }

    [Fact]
    public void EnvironmentIsTheFallbackAndIsNormalizedTheSameWay()
    {
        var resolved = new FirmsKeyResolver(new FakeKeyStore(), () => $"  \"{FakeHexKey}\"  ").Resolve();

        Assert.Equal(FirmsKeySource.Environment, resolved.Source);
        Assert.Equal(FakeHexKey, resolved.Key!.RevealForProviderRequest());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("''")]
    public void NeitherMeansNotConfigured(string? environment)
    {
        var resolved = new FirmsKeyResolver(new FakeKeyStore(), () => environment).Resolve();

        Assert.Equal(FirmsKeySource.None, resolved.Source);
        Assert.Null(resolved.Key);
    }

    [Fact]
    public void ResolvesOnEveryCallSoSetAndRemoveApplyWithoutRestart()
    {
        var store = new FakeKeyStore();
        var environment = (string?)null;
        var resolver = new FirmsKeyResolver(store, () => environment);
        var service = new FirmsKeyService(store, resolver);

        Assert.Equal(FirmsKeySource.None, resolver.Resolve().Source);
        Assert.Equal(FirmsKeyChangeOutcome.Saved, service.SetKey(FakeHexKey));
        Assert.Equal(FirmsKeySource.App, resolver.Resolve().Source);
        environment = DistinctFakeKey;
        Assert.Equal(FirmsKeySource.App, resolver.Resolve().Source);
        Assert.Equal(FirmsKeyChangeOutcome.Removed, service.RemoveAppKey());
        Assert.Equal(FirmsKeySource.Environment, resolver.Resolve().Source);
        Assert.Equal(DistinctFakeKey, resolver.Resolve().Key!.RevealForProviderRequest());
    }

    [Fact]
    public void StoreRoundTripStoresOnlyTheNormalizedKey()
    {
        var store = new FakeKeyStore();
        var service = new FirmsKeyService(store, new FirmsKeyResolver(store, () => null));

        Assert.Equal(FirmsKeyChangeOutcome.Saved, service.SetKey($"  '{FakeHexKey}' "));
        Assert.Equal(FakeHexKey, store.Value);
        Assert.Equal(FakeHexKey, store.Read().Key!.RevealForProviderRequest());
        Assert.Equal(FirmsKeyChangeOutcome.Removed, service.RemoveAppKey());
        Assert.Null(store.Value);
        Assert.Equal(FirmsKeyChangeOutcome.NotStored, service.RemoveAppKey());
    }

    [Theory]
    [InlineData("hello")]
    [InlineData(DistinctFakeKey)]
    [InlineData("0123456789abcdef")]
    public void MalformedKeyIsSavedWithAWarningNeverBlocked(string input)
    {
        var store = new FakeKeyStore();
        var service = new FirmsKeyService(store, new FirmsKeyResolver(store, () => null));

        Assert.True(FirmsKeyService.ShouldWarn(input));
        Assert.Equal(FirmsKeyChangeOutcome.Saved, service.SetKey(input));
        Assert.Equal(new FirmsKeyStatus(true, FirmsKeySource.App, true, false), service.GetStatus());
    }

    [Fact]
    public void WellFormedKeyHasNoWarning()
    {
        var store = new FakeKeyStore(FakeHexKey);
        var service = new FirmsKeyService(store, new FirmsKeyResolver(store, () => null));

        Assert.Equal(new FirmsKeyStatus(true, FirmsKeySource.App, false, false), service.GetStatus());
    }

    [Theory]
    [InlineData(null, FirmsKeyChangeOutcome.EmptyKey)]
    [InlineData("  \"\" ", FirmsKeyChangeOutcome.EmptyKey)]
    public void EmptyInputIsNotSaved(string? input, FirmsKeyChangeOutcome expected)
    {
        var store = new FakeKeyStore();
        var service = new FirmsKeyService(store, new FirmsKeyResolver(store, () => null));

        Assert.Equal(expected, service.SetKey(input));
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void OverlongInputIsNotSaved()
    {
        var store = new FakeKeyStore();
        var service = new FirmsKeyService(store, new FirmsKeyResolver(store, () => null));

        Assert.Equal(FirmsKeyChangeOutcome.KeyTooLong, service.SetKey(new string('a', FirmsMapKey.MaximumLength + 1)));
        Assert.Equal(FirmsKeyChangeOutcome.Saved, service.SetKey(new string('a', FirmsMapKey.MaximumLength)));
    }

    [Fact]
    public void StoreFailuresBecomeBoundedOutcomesAndFallBackToEnvironment()
    {
        var store = new ThrowingKeyStore();
        var resolver = new FirmsKeyResolver(store, () => FakeHexKey);
        var service = new FirmsKeyService(store, resolver);

        var resolved = resolver.Resolve();
        Assert.Equal(FirmsKeySource.Environment, resolved.Source);
        Assert.True(resolved.StoreUnavailable);
        Assert.Equal(FirmsKeyChangeOutcome.StoreUnavailable, service.SetKey(FakeHexKey));
        Assert.Equal(FirmsKeyChangeOutcome.StoreUnavailable, service.RemoveAppKey());
        Assert.Equal(new FirmsKeyStatus(true, FirmsKeySource.Environment, false, true), service.GetStatus());
    }

    [Fact]
    public void ThrowingEnvironmentReaderMeansNotConfigured()
    {
        var resolved = new FirmsKeyResolver(new FakeKeyStore(), () => throw new InvalidOperationException(DistinctFakeKey)).Resolve();

        Assert.Equal(FirmsKeySource.None, resolved.Source);
    }

    [Theory]
    [InlineData(DesktopBridgeProtocol.FirmsKeyStatusGetMessageType)]
    [InlineData(DesktopBridgeProtocol.FirmsKeySetMessageType)]
    [InlineData(DesktopBridgeProtocol.FirmsKeyRemoveMessageType)]
    public void KeyRequestsAcceptExactlyZeroFields(string type)
    {
        Assert.True(DesktopBridgeProtocol.TryParseRequest(Message(type, ",\"payload\":{}"), out var request, out var error), error);
        Assert.Equal(type, request!.Type);
        Assert.True(DesktopBridgeProtocol.TryParseRequest(Message(type, ""), out _, out _));
        Assert.True(DesktopBridgeProtocol.TryParseRequest(Message(type, ",\"payload\":null"), out _, out _));

        Assert.False(DesktopBridgeProtocol.TryParseRequest(Message(type, ",\"payload\":{\"key\":\"" + DistinctFakeKey + "\"}"), out _, out _));
        Assert.False(DesktopBridgeProtocol.TryParseRequest(Message(type, ",\"payload\":{\"mapKey\":null}"), out _, out _));
        Assert.False(DesktopBridgeProtocol.TryParseRequest(Message(type, ",\"payload\":[]"), out _, out _));
        Assert.False(DesktopBridgeProtocol.TryParseRequest(Message(type, ",\"payload\":\"" + DistinctFakeKey + "\""), out _, out _));
        Assert.False(DesktopBridgeProtocol.TryParseRequest(Message(type, ",\"payload\":{},\"key\":\"" + DistinctFakeKey + "\""), out _, out _));
        Assert.False(DesktopBridgeProtocol.TryParseRequest(Message(type, ",\"type\":\"" + type + "\",\"payload\":{}"), out _, out _));
    }

    [Theory]
    [InlineData(DesktopBridgeProtocol.FirmsKeyStatusGetMessageType)]
    [InlineData(DesktopBridgeProtocol.FirmsKeySetMessageType)]
    [InlineData(DesktopBridgeProtocol.FirmsKeyRemoveMessageType)]
    public async Task KeyRequestsPassOnlyThroughTheTrustedOriginProcessor(string type)
    {
        var calls = 0;
        var json = Message(type, ",\"payload\":{}");

        Assert.False(await PrivilegedBridgeMessageProcessor.TryProcessAsync("https://evil.example", json, _ => { calls++; return Task.CompletedTask; }));
        Assert.False(await PrivilegedBridgeMessageProcessor.TryProcessAsync("https://x.com/home", json, _ => { calls++; return Task.CompletedTask; }));
        Assert.True(await PrivilegedBridgeMessageProcessor.TryProcessAsync(PrivilegedWebViewSecurityPolicy.TrustedOrigin, json, _ => { calls++; return Task.CompletedTask; }));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(FirmsKeyChangeOutcome.Saved)]
    [InlineData(FirmsKeyChangeOutcome.StoreUnavailable)]
    public void StatusResultHasOnlyStatusFieldsAndNeverTheKey(FirmsKeyChangeOutcome? outcome)
    {
        var store = new FakeKeyStore(DistinctFakeKey);
        var status = new FirmsKeyService(store, new FirmsKeyResolver(store, () => FakeHexKey)).GetStatus();

        var json = DesktopBridgeProtocol.CreateFirmsKeyStatusResponseJson(status, outcome);
        using var document = JsonDocument.Parse(json);
        var payload = document.RootElement.GetProperty("payload");

        Assert.Equal(DesktopBridgeProtocol.FirmsKeyStatusMessageType, document.RootElement.GetProperty("type").GetString());
        Assert.Equal(
            ["configured", "formatWarning", "outcome", "source", "storeUnavailable"],
            payload.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.True(payload.GetProperty("configured").GetBoolean());
        Assert.Equal("App", payload.GetProperty("source").GetString());
        Assert.True(payload.GetProperty("formatWarning").GetBoolean());
        Assert.DoesNotContain(DistinctFakeKey, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(FakeHexKey, json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BridgeRequestRecordHasNoKeyField()
    {
        var names = typeof(DesktopBridgeRequest).GetProperties().Select(property => property.Name).ToArray();

        Assert.DoesNotContain(names, name => name.Contains("MapKey", StringComparison.OrdinalIgnoreCase)
            || name.Contains("FirmsKey", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Secret", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(FirmsFeedFailureKind.KeyRejected, FirmsRefreshStatus.KeyRejected)]
    [InlineData(FirmsFeedFailureKind.RateLimited, FirmsRefreshStatus.RateLimited)]
    [InlineData(FirmsFeedFailureKind.TimedOut, FirmsRefreshStatus.TimedOut)]
    [InlineData(FirmsFeedFailureKind.ProductUnavailable, FirmsRefreshStatus.ProductUnavailable)]
    [InlineData(FirmsFeedFailureKind.ConfigurationUnavailable, FirmsRefreshStatus.ConfigurationUnavailable)]
    [InlineData(FirmsFeedFailureKind.RequestFailed, FirmsRefreshStatus.RequestFailed)]
    public async Task FeedFailuresMapToTheirRefreshCategoryWithoutASnapshot(FirmsFeedFailureKind kind, FirmsRefreshStatus expected)
    {
        var service = new FirmsRefreshService(new ThrowingFeed(kind));

        var result = await service.RefreshAsync(new FirmsViewport(0, 0, 10, 10));

        Assert.Equal(expected, result.Status);
        Assert.Null(result.Snapshot);
    }

    [Theory]
    [InlineData(FirmsFeedFailureKind.ConfigurationUnavailable, "ConfigurationUnavailable", "Settings, Data sources")]
    [InlineData(FirmsFeedFailureKind.KeyRejected, "KeyRejected", "rejected the key")]
    [InlineData(FirmsFeedFailureKind.RateLimited, "RateLimited", "too many requests")]
    [InlineData(FirmsFeedFailureKind.TimedOut, "TimedOut", "did not respond in time")]
    [InlineData(FirmsFeedFailureKind.ProductUnavailable, "ProductUnavailable", "VIIRS product")]
    public async Task HandlerReportsEachCategoryWithAClearBoundedMessage(FirmsFeedFailureKind kind, string status, string expectedText)
    {
        var store = new FakeKeyStore(FakeHexKey);
        var keys = new FirmsKeyService(store, new FirmsKeyResolver(store, () => null));
        var handler = new FirmsRefreshBridgeHandler(new FirmsRefreshService(new ThrowingFeed(kind)), keys.GetStatus);

        var json = await handler.RefreshAsync(ViewportRequest());
        using var document = JsonDocument.Parse(json);
        var payload = document.RootElement.GetProperty("payload");

        Assert.Equal(DesktopBridgeProtocol.FirmsRefreshErrorMessageType, document.RootElement.GetProperty("type").GetString());
        Assert.Equal(status, payload.GetProperty("status").GetString());
        var message = payload.GetProperty("message").GetString()!;
        Assert.Contains(expectedText, message, StringComparison.Ordinal);
        Assert.True(message.Length <= 256);
        Assert.DoesNotContain(FakeHexKey, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api/area", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RejectedMalformedKeyMentionsTheFormat()
    {
        var store = new FakeKeyStore(DistinctFakeKey);
        var keys = new FirmsKeyService(store, new FirmsKeyResolver(store, () => null));
        var handler = new FirmsRefreshBridgeHandler(new FirmsRefreshService(new ThrowingFeed(FirmsFeedFailureKind.KeyRejected)), keys.GetStatus);

        var json = await handler.RefreshAsync(ViewportRequest());

        Assert.Contains(FirmsRefreshBridgeHandler.MalformedKeyRejectedMessage, json, StringComparison.Ordinal);
        Assert.DoesNotContain(DistinctFakeKey, json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NotConfiguredMessagePointsToSettings()
    {
        var handler = new FirmsRefreshBridgeHandler(new FirmsRefreshService(new ThrowingFeed(FirmsFeedFailureKind.ConfigurationUnavailable)));

        var json = await handler.RefreshAsync(ViewportRequest());

        Assert.Contains("NASA FIRMS is not configured. Set a key in Settings, Data sources.", json, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyTheFirmsClientAndTheKeyStoreRevealTheKey()
    {
        var backend = Path.Combine(SocialPaneSourceGuard.FindRepositoryRoot(), "src", "backend");
        var revealing = BackendSources(backend)
            .Where(path => File.ReadAllText(path).Contains("RevealForProviderRequest(", StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["FirmsMapKey.cs", "FirmsObservationFeed.cs", "WindowsCredentialFirmsKeyStore.cs"], revealing);
    }

    [Fact]
    public void OnlyTheCompositionRootReadsTheEnvironmentVariable()
    {
        var backend = Path.Combine(SocialPaneSourceGuard.FindRepositoryRoot(), "src", "backend");
        var readers = BackendSources(backend)
            .Where(path => Regex.IsMatch(File.ReadAllText(path), @"GetEnvironmentVariable\(\s*""GEOPOLITICALMONITOR_FIRMS_MAP_KEY"""))
            .Select(path => Path.GetFileName(path))
            .ToArray();

        Assert.Equal(["MainWindow.xaml.cs"], readers);
    }

    [Fact]
    public void KeyCodeWritesNoDebugConsoleOrTraceOutput()
    {
        var root = SocialPaneSourceGuard.FindRepositoryRoot();
        var files = new[]
        {
            Path.Combine(root, "src", "backend", "GeopoliticalMonitor.Application", "Firms", "FirmsMapKey.cs"),
            Path.Combine(root, "src", "backend", "GeopoliticalMonitor.Infrastructure", "Firms", "FirmsObservationFeed.cs"),
            Path.Combine(root, "src", "backend", "GeopoliticalMonitor.Infrastructure", "Firms", "WindowsCredentialFirmsKeyStore.cs"),
            Path.Combine(root, "src", "backend", "GeopoliticalMonitor.Desktop", "MainWindow.FirmsKey.cs"),
        };

        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            foreach (var forbidden in new[] { "Debug.", "Console.", "Trace.", "Logger", "File.Write", "Clipboard" })
            {
                Assert.False(source.Contains(forbidden, StringComparison.Ordinal), $"{Path.GetFileName(file)} uses {forbidden}.");
            }
        }
    }

    [Fact]
    public void KeyDialogHidesAndClearsThePasswordBox()
    {
        var source = File.ReadAllText(Path.Combine(
            SocialPaneSourceGuard.FindRepositoryRoot(), "src", "backend", "GeopoliticalMonitor.Desktop", "MainWindow.FirmsKey.cs"));

        Assert.Contains("PasswordRevealMode = PasswordRevealMode.Hidden", source, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"finally\s*\{\s*passwordBox\.Password = string\.Empty;"), source);
        // Every use of the entered text is one of these: enable Save, show the warning, save it, or clear it.
        string[] allowedUses =
        [
            "FirmsMapKey.Normalize(passwordBox.Password)",
            "FirmsKeyService.ShouldWarn(passwordBox.Password)",
            "service.SetKey(passwordBox.Password)",
            "passwordBox.Password = string.Empty;",
        ];
        var allowed = allowedUses.Sum(use => Regex.Matches(source, Regex.Escape(use)).Count);
        Assert.Equal(allowed, Regex.Matches(source, @"passwordBox\.Password\b").Count);
        Assert.Single(Regex.Matches(source, Regex.Escape("service.SetKey(passwordBox.Password)")));
    }

    [Fact]
    public void NoPackageReferenceWasAddedForKeyStorage()
    {
        var backend = Path.Combine(SocialPaneSourceGuard.FindRepositoryRoot(), "src", "backend");
        var packages = Directory.EnumerateFiles(backend, "*.csproj", SearchOption.AllDirectories)
            .SelectMany(project => Regex.Matches(File.ReadAllText(project), @"PackageReference Include=""([^""]+)""").Select(match => match.Groups[1].Value))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["Microsoft.EntityFrameworkCore.Design", "Microsoft.EntityFrameworkCore.Sqlite", "Microsoft.Web.WebView2", "Microsoft.WindowsAppSDK"],
            packages);
    }

    private static IEnumerable<string> BackendSources(string backend)
        => Directory.EnumerateFiles(backend, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string Message(string type, string rest) => "{\"type\":\"" + type + "\"" + rest + "}";

    private static DesktopBridgeRequest ViewportRequest()
        => new(DesktopBridgeProtocol.FirmsRefreshMessageType, FirmsWest: 0, FirmsSouth: 0, FirmsEast: 10, FirmsNorth: 10);

    private sealed class FakeKeyStore(string? value = null) : IFirmsKeyStore
    {
        public string? Value { get; private set; } = value;

        public int Writes { get; private set; }

        public FirmsKeyStoreRead Read()
        {
            var key = FirmsMapKey.FromInput(Value);
            return key is null ? new FirmsKeyStoreRead(FirmsKeyStoreOutcome.NotFound) : new FirmsKeyStoreRead(FirmsKeyStoreOutcome.Succeeded, key);
        }

        public FirmsKeyStoreOutcome Write(FirmsMapKey key)
        {
            Writes++;
            Value = key.RevealForProviderRequest();
            return FirmsKeyStoreOutcome.Succeeded;
        }

        public FirmsKeyStoreOutcome Delete()
        {
            if (Value is null)
            {
                return FirmsKeyStoreOutcome.NotFound;
            }

            Value = null;
            return FirmsKeyStoreOutcome.Succeeded;
        }
    }

    private sealed class ThrowingKeyStore : IFirmsKeyStore
    {
        public FirmsKeyStoreRead Read() => throw new InvalidOperationException("store failed");

        public FirmsKeyStoreOutcome Write(FirmsMapKey key) => throw new InvalidOperationException("store failed");

        public FirmsKeyStoreOutcome Delete() => throw new InvalidOperationException("store failed");
    }

    private sealed class ThrowingFeed(FirmsFeedFailureKind kind) : IFirmsObservationFeed
    {
        public Task<FirmsObservationBatch> FetchAsync(FirmsSource source, FirmsBoundingBox bounds, CancellationToken cancellationToken = default)
            => throw new FirmsFeedException(kind, "NASA FIRMS request failed.");
    }
}
