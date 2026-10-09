using System.Security.Cryptography;
using GeopoliticalMonitor.Application.XBrowser;
using GeopoliticalMonitor.Infrastructure.Persistence;

namespace GeopoliticalMonitor.Infrastructure.Tests;

public sealed class BlueskyAdBlockStorageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"gpm-bluesky-adblock-{Guid.NewGuid():N}");

    [Fact]
    public void BlueskyPayloadAndBindingHaveTheirOwnPathsBesideUnchangedXPaths()
    {
        var provider = new LocalApplicationDataPathProvider(() => root);

        Assert.Equal(Path.Combine(root, "GeopoliticalMonitor", "Data", "bluesky-adblock-binding.json"), provider.GetBlueskyAdBlockBindingPath());
        Assert.Equal(Path.Combine(root, "GeopoliticalMonitor", "Extensions", "AdBlockBluesky", "Current"), provider.GetBlueskyAdBlockManagedSourceDirectory());
        Assert.Equal(Path.Combine(root, "GeopoliticalMonitor", "Data", "x-adblock-binding.json"), provider.GetProductionAdBlockBindingPath());
        Assert.Equal(Path.Combine(root, "GeopoliticalMonitor", "Extensions", "AdBlock", "Current"), provider.GetProductionAdBlockManagedSourceDirectory());

        var blueskyPayload = provider.GetBlueskyAdBlockManagedSourceDirectory();
        var xPayload = provider.GetProductionAdBlockManagedSourceDirectory();
        Assert.False(IsSameOrBelow(blueskyPayload, Path.GetDirectoryName(xPayload)!));
        Assert.False(IsSameOrBelow(xPayload, Path.GetDirectoryName(blueskyPayload)!));
        Assert.False(IsSameOrBelow(blueskyPayload, provider.GetBlueskyBrowserUserDataFolder()));
        Assert.False(IsSameOrBelow(blueskyPayload, provider.GetXBrowserUserDataFolder()));
        Assert.NotEqual(provider.GetProductionAdBlockBindingPath(), provider.GetBlueskyAdBlockBindingPath());
        Assert.NotEqual(provider.GetDiagnosticAdBlockBindingPath(), provider.GetBlueskyAdBlockBindingPath());
    }

    [Fact]
    public async Task VerifiedXCopyIsStagedIntoTheBlueskyFolderAndLeavesTheXFolderUntouched()
    {
        var xPayload = CreatePayload(Path.Combine("app", "Extensions", "AdBlock", "Current"), "trusted");
        var authority = await CreateAuthorityAsync(xPayload);
        var before = Fingerprint(xPayload);
        var blueskyManaged = Path.Combine(root, "app", "Extensions", "AdBlockBluesky", "Current");

        var validation = await new VerifiedXAdBlockCopySource(xPayload, authority.Inspector, [authority.Trust]).AuthenticateAsync();
        Assert.True(validation.IsValid, validation.ErrorMessage);
        var prepared = await new FileXAdBlockManagedPayloadStore(
            blueskyManaged,
            authority.Inspector,
            new SystemXAdBlockManagedPayloadDirectoryOperations(),
            [authority.Trust]).PrepareAsync(validation.Identity!);

        Assert.True(prepared.Success, prepared.ErrorMessage);
        Assert.Equal(Path.GetFullPath(blueskyManaged), prepared.Identity?.SourceFolderPath);
        Assert.Equal(authority.Source.Payload, prepared.Identity?.Payload);
        Assert.Equal("trusted", File.ReadAllText(Path.Combine(blueskyManaged, "worker.js")));
        Assert.Equal(before, Fingerprint(xPayload));
        Assert.False(Directory.Exists($"{xPayload}.staging"));
        Assert.False(Directory.Exists($"{xPayload}.previous"));
        Assert.False(Directory.Exists($"{xPayload}.rejected"));
    }

    [Fact]
    public async Task VerifiedXCopyIsAuthenticatedAgainOnEveryUse()
    {
        var xPayload = CreatePayload(Path.Combine("app", "Extensions", "AdBlock", "Current"), "trusted");
        var authority = await CreateAuthorityAsync(xPayload);
        var counting = new CountingInspector(authority.Inspector);
        var source = new VerifiedXAdBlockCopySource(xPayload, counting, [authority.Trust]);

        Assert.True((await source.AuthenticateAsync()).IsValid);
        Assert.True((await source.AuthenticateAsync()).IsValid);

        Assert.Equal(2, counting.Inspections.Count(path => string.Equals(path, xPayload, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task ChangedXCopyIsRefusedAndNothingIsCopied()
    {
        var xPayload = CreatePayload(Path.Combine("app", "Extensions", "AdBlock", "Current"), "trusted");
        var authority = await CreateAuthorityAsync(xPayload);
        File.WriteAllText(Path.Combine(xPayload, "worker.js"), "changed after review");
        var before = Fingerprint(xPayload);
        var blueskyManaged = Path.Combine(root, "app", "Extensions", "AdBlockBluesky", "Current");

        var validation = await new VerifiedXAdBlockCopySource(xPayload, authority.Inspector, [authority.Trust]).AuthenticateAsync();

        Assert.False(validation.IsValid);
        Assert.Null(validation.Identity);
        Assert.Equal(before, Fingerprint(xPayload));
        Assert.False(Directory.Exists(blueskyManaged));
    }

    [Fact]
    public async Task UnreviewedXCopyIsRefused()
    {
        var xPayload = CreatePayload(Path.Combine("app", "Extensions", "AdBlock", "Current"), "trusted");
        var authority = await CreateAuthorityAsync(xPayload);

        // Authenticated by the inspector, but the copy source accepts only its own reviewed records.
        var validation = await new VerifiedXAdBlockCopySource(xPayload, authority.Inspector, []).AuthenticateAsync();

        Assert.False(validation.IsValid);
    }

    [Fact]
    public async Task MissingXCopyIsRefused()
    {
        var source = new VerifiedXAdBlockCopySource(Path.Combine(root, "missing", "Current"), new FileXDiagnosticAdBlockSourceInspector([]));

        Assert.False(source.Exists);
        var validation = await source.AuthenticateAsync();
        Assert.False(validation.IsValid);
        Assert.Contains("No verified X copy", validation.ErrorMessage, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static bool IsSameOrBelow(string candidate, string directory)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        return string.Equals(path, full, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(full + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string Fingerprint(string directory) =>
        string.Join(
            "\n",
            Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(path => $"{Path.GetRelativePath(directory, path)}|{File.GetLastWriteTimeUtc(path):O}|{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))}"));

    private async Task<TestAuthority> CreateAuthorityAsync(string sourcePath)
    {
        var preliminary = await new FileXDiagnosticAdBlockSourceInspector([]).InspectAsync(sourcePath);
        Assert.True(preliminary.IsValid, preliminary.ErrorMessage);
        var identity = preliminary.Identity!;
        var payload = identity.Payload!;
        var trust = new OfficialAdBlockTrustRecord(
            1,
            "test-adblock",
            "test-store",
            identity.Manifest.OfficialStoreExtensionId!,
            identity.Manifest.Version,
            payload.DigestFormat,
            payload.Sha256,
            "Synthetic test authority.");
        var inspector = new FileXDiagnosticAdBlockSourceInspector([trust]);
        var trusted = await inspector.InspectAsync(sourcePath);
        Assert.True(trusted.IsValid, trusted.ErrorMessage);
        return new(trusted.Identity!, trust, inspector);
    }

    private string CreatePayload(string relativePath, string workerBody)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.Combine(path, "_locales", "en_US"));
        var key = Convert.ToBase64String(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());
        File.WriteAllText(
            Path.Combine(path, "manifest.json"),
            $$"""
            {
              "manifest_version": 3,
              "version": "1.2.3",
              "name": "__MSG_name__",
              "default_locale": "en_US",
              "author": "Adblock, Inc.",
              "key": "{{key}}",
              "externally_connectable": {
                "ids": [
                  "{{XAdBlockExtensionPolicy.ChromeExtensionId}}",
                  "{{XAdBlockExtensionPolicy.EdgeExtensionId}}"
                ]
              }
            }
            """);
        File.WriteAllText(
            Path.Combine(path, "_locales", "en_US", "messages.json"),
            """
            { "name": { "message": "AdBlock - block ads across the web" } }
            """);
        File.WriteAllText(Path.Combine(path, "worker.js"), workerBody);
        return path;
    }

    private sealed record TestAuthority(
        XDiagnosticAdBlockSourceIdentity Source,
        OfficialAdBlockTrustRecord Trust,
        FileXDiagnosticAdBlockSourceInspector Inspector);

    private sealed class CountingInspector(IXDiagnosticAdBlockSourceInspector inner) : IXDiagnosticAdBlockSourceInspector
    {
        public List<string> Inspections { get; } = [];

        public Task<XDiagnosticAdBlockSourceValidation> InspectAsync(string sourceFolderPath, CancellationToken cancellationToken = default)
        {
            Inspections.Add(sourceFolderPath);
            return inner.InspectAsync(sourceFolderPath, cancellationToken);
        }
    }
}
