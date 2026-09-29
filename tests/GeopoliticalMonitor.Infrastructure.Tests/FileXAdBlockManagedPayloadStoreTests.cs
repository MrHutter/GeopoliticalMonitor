using GeopoliticalMonitor.Application.XBrowser;
using GeopoliticalMonitor.Infrastructure.Persistence;

namespace GeopoliticalMonitor.Infrastructure.Tests;

public sealed class FileXAdBlockManagedPayloadStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"gpm-adblock-{Guid.NewGuid():N}");

    [Fact]
    public async Task CopiesOnlyAuthenticatedRuntimeTreeToStableManagedPath()
    {
        var source = CreatePayload("source", "1.2.3", "payload");
        Directory.CreateDirectory(Path.Combine(source, "_metadata"));
        File.WriteAllText(Path.Combine(source, "_metadata", "verified_contents.json"), "untrusted metadata");
        var authority = await CreateAuthorityAsync(source);
        var managed = ManagedPath();

        var result = await CreateStore(managed, authority).PrepareAsync(authority.Source);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.False(result.ReusedExisting);
        Assert.Equal(Path.GetFullPath(managed), result.Identity?.SourceFolderPath);
        Assert.Equal(authority.Source.Payload, result.Identity?.Payload);
        Assert.Equal("payload", File.ReadAllText(Path.Combine(managed, "worker.js")));
        Assert.False(Directory.Exists(Path.Combine(managed, "_metadata")));
    }

    [Fact]
    public async Task ReusesOnlyExactAuthenticatedPayload()
    {
        var authority = await CreateAuthorityAsync(CreatePayload("source", "1.2.3", "payload"));
        var managed = ManagedPath();
        var store = CreateStore(managed, authority);
        Assert.True((await store.PrepareAsync(authority.Source)).Success);
        File.SetLastWriteTimeUtc(Path.Combine(managed, "worker.js"), DateTime.UtcNow.AddMinutes(-10));

        var second = await store.PrepareAsync(authority.Source);

        Assert.True(second.Success, second.ErrorMessage);
        Assert.True(second.ReusedExisting);
        Assert.Equal(authority.Source.Payload, second.Identity?.Payload);
    }

    [Fact]
    public async Task DifferentExistingPayloadIsAtomicallyReplacedAfterFinalVerification()
    {
        var authority = await CreateAuthorityAsync(CreatePayload("source", "1.2.3", "trusted"));
        var managed = CreatePayload(Path.Combine("app", "Extensions", "AdBlock", "Current"), "0.9.0", "old");

        var result = await CreateStore(managed, authority).PrepareAsync(authority.Source);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.False(result.ReusedExisting);
        Assert.Equal("trusted", File.ReadAllText(Path.Combine(managed, "worker.js")));
        AssertNoTransactionDirectories(managed);
    }

    [Fact]
    public async Task StagingVerificationFailurePreservesExistingPayload()
    {
        var authority = await CreateAuthorityAsync(CreatePayload("source", "1.2.3", "trusted"));
        var managed = CreatePayload(Path.Combine("app", "Extensions", "AdBlock", "Current"), "0.9.0", "old");
        var rejecting = new RejectingInspector(authority.Inspector, path => path.EndsWith(".staging", StringComparison.Ordinal));
        var store = CreateStore(managed, authority, rejecting);

        var result = await store.PrepareAsync(authority.Source);

        Assert.False(result.Success);
        Assert.Equal("old", File.ReadAllText(Path.Combine(managed, "worker.js")));
        AssertNoTransactionDirectories(managed);
    }

    [Fact]
    public async Task StagingCopyFailurePreservesExistingPayloadAndCleansPartialStaging()
    {
        var authority = await CreateAuthorityAsync(CreatePayload("source", "1.2.3", "trusted"));
        var managed = CreatePayload(Path.Combine("app", "Extensions", "AdBlock", "Current"), "0.9.0", "old");
        var store = new FileXAdBlockManagedPayloadStore(
            managed,
            authority.Inspector,
            new SystemXAdBlockManagedPayloadDirectoryOperations(),
            new FailingFileCopier(),
            [authority.Trust]);

        var result = await store.PrepareAsync(authority.Source);

        Assert.False(result.Success);
        Assert.Equal("old", File.ReadAllText(Path.Combine(managed, "worker.js")));
        AssertNoTransactionDirectories(managed);
    }

    [Fact]
    public async Task FinalVerificationFailureRestoresPreviousPayload()
    {
        var authority = await CreateAuthorityAsync(CreatePayload("source", "1.2.3", "trusted"));
        var managed = CreatePayload(Path.Combine("app", "Extensions", "AdBlock", "Current"), "0.9.0", "old");
        var managedInspections = 0;
        var rejecting = new RejectingInspector(authority.Inspector, path =>
        {
            if (!string.Equals(Path.GetFullPath(path), Path.GetFullPath(managed), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            managedInspections++;
            return managedInspections >= 2;
        });
        var store = CreateStore(managed, authority, rejecting);

        var result = await store.PrepareAsync(authority.Source);

        Assert.False(result.Success);
        Assert.Equal("old", File.ReadAllText(Path.Combine(managed, "worker.js")));
        AssertNoTransactionDirectories(managed);
    }

    [Fact]
    public async Task ActivationFailureRestoresPreviousPayload()
    {
        var authority = await CreateAuthorityAsync(CreatePayload("source", "1.2.3", "trusted"));
        var managed = CreatePayload(Path.Combine("app", "Extensions", "AdBlock", "Current"), "0.9.0", "old");
        var store = new FileXAdBlockManagedPayloadStore(
            managed,
            authority.Inspector,
            new ThrowingActivationOperations(managed),
            [authority.Trust]);

        var result = await store.PrepareAsync(authority.Source);

        Assert.False(result.Success);
        Assert.Equal("old", File.ReadAllText(Path.Combine(managed, "worker.js")));
        AssertNoTransactionDirectories(managed);
    }

    [Fact]
    public async Task InterruptedActivationRestoresDeterministicPreviousPayload()
    {
        var authority = await CreateAuthorityAsync(CreatePayload("source", "1.2.3", "trusted"));
        var managed = CreatePayload(Path.Combine("app", "Extensions", "AdBlock", "Current"), "0.9.0", "old");
        var previous = $"{managed}.previous";
        Directory.Move(managed, previous);
        var store = CreateStore(managed, authority);

        var recovered = await store.RecoverInterruptedActivationAsync();

        Assert.True(recovered);
        Assert.Equal("old", File.ReadAllText(Path.Combine(managed, "worker.js")));
        Assert.False(Directory.Exists(previous));
    }

    [Fact]
    public async Task BackupCleanupFailureNeverRollsBackVerifiedCurrent()
    {
        var authority = await CreateAuthorityAsync(CreatePayload("source", "1.2.3", "trusted"));
        var managed = CreatePayload(Path.Combine("app", "Extensions", "AdBlock", "Current"), "0.9.0", "old");
        var operations = new FailingBackupDeleteOperations();
        var store = new FileXAdBlockManagedPayloadStore(
            managed,
            authority.Inspector,
            operations,
            [authority.Trust]);

        var result = await store.PrepareAsync(authority.Source);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("trusted", File.ReadAllText(Path.Combine(managed, "worker.js")));
        Assert.True(Directory.Exists($"{managed}.previous"));

        Assert.True(await CreateStore(managed, authority).RecoverInterruptedActivationAsync());
        Assert.Equal("trusted", File.ReadAllText(Path.Combine(managed, "worker.js")));
        Assert.False(Directory.Exists($"{managed}.previous"));
    }

    [Fact]
    public async Task UntrustedCandidateIsRejectedBeforeCopy()
    {
        var sourcePath = CreatePayload("source", "1.2.3", "payload");
        var inspector = new FileXDiagnosticAdBlockSourceInspector([]);
        var validation = await inspector.InspectAsync(sourcePath);
        var source = Assert.IsType<XDiagnosticAdBlockSourceIdentity>(validation.Identity);
        var managed = ManagedPath();

        var result = await new FileXAdBlockManagedPayloadStore(managed, inspector).PrepareAsync(source);

        Assert.False(result.Success);
        Assert.False(Directory.Exists(managed));
    }

    [Fact]
    public async Task CleanupRemovesOnlyManagedPayload()
    {
        var authority = await CreateAuthorityAsync(CreatePayload("source", "1.2.3", "payload"));
        var managed = ManagedPath();
        var store = CreateStore(managed, authority);
        Assert.True((await store.PrepareAsync(authority.Source)).Success);

        var cleanup = await store.RemoveAsync();

        Assert.True(cleanup.Success, cleanup.ErrorMessage);
        Assert.True(cleanup.Removed);
        Assert.False(Directory.Exists(managed));
        Assert.True(Directory.Exists(authority.SourcePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private FileXAdBlockManagedPayloadStore CreateStore(
        string managed,
        TestAuthority authority,
        IXDiagnosticAdBlockSourceInspector? inspector = null) => new(
        managed,
        inspector ?? authority.Inspector,
        new SystemXAdBlockManagedPayloadDirectoryOperations(),
        [authority.Trust]);

    private async Task<TestAuthority> CreateAuthorityAsync(string sourcePath)
    {
        var preliminary = await new FileXDiagnosticAdBlockSourceInspector([]).InspectAsync(sourcePath);
        Assert.True(preliminary.IsValid, preliminary.ErrorMessage);
        var identity = Assert.IsType<XDiagnosticAdBlockSourceIdentity>(preliminary.Identity);
        var payload = Assert.IsType<XAdBlockPayloadIdentity>(identity.Payload);
        var storeId = Assert.IsType<string>(identity.Manifest.OfficialStoreExtensionId);
        var trust = new OfficialAdBlockTrustRecord(
            1,
            "test-adblock",
            "test-store",
            storeId,
            identity.Manifest.Version,
            payload.DigestFormat,
            payload.Sha256,
            "Synthetic test authority.");
        var inspector = new FileXDiagnosticAdBlockSourceInspector([trust]);
        var trusted = await inspector.InspectAsync(sourcePath);
        Assert.True(trusted.IsValid, trusted.ErrorMessage);
        return new(sourcePath, Assert.IsType<XDiagnosticAdBlockSourceIdentity>(trusted.Identity), trust, inspector);
    }

    private string CreatePayload(string relativePath, string version, string workerBody)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.Combine(path, "_locales", "en_US"));
        var key = Convert.ToBase64String(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());
        File.WriteAllText(
            Path.Combine(path, "manifest.json"),
            $$"""
            {
              "manifest_version": 3,
              "version": "{{version}}",
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

    private string ManagedPath() => Path.Combine(root, "app", "Extensions", "AdBlock", "Current");

    private static void AssertNoTransactionDirectories(string managed)
    {
        var parent = Path.GetDirectoryName(managed)!;
        Assert.False(Directory.Exists($"{managed}.staging"));
        Assert.False(Directory.Exists($"{managed}.previous"));
        Assert.False(Directory.Exists($"{managed}.rejected"));
        Assert.Empty(Directory.GetDirectories(parent, "Current.staging-*"));
        Assert.Empty(Directory.GetDirectories(parent, "Current.previous-*"));
        Assert.Empty(Directory.GetDirectories(parent, "Current.rejected-*"));
    }

    private sealed record TestAuthority(
        string SourcePath,
        XDiagnosticAdBlockSourceIdentity Source,
        OfficialAdBlockTrustRecord Trust,
        FileXDiagnosticAdBlockSourceInspector Inspector);

    private sealed class RejectingInspector(
        IXDiagnosticAdBlockSourceInspector inner,
        Func<string, bool> shouldReject) : IXDiagnosticAdBlockSourceInspector
    {
        public async Task<XDiagnosticAdBlockSourceValidation> InspectAsync(
            string sourceFolderPath,
            CancellationToken cancellationToken = default) =>
            shouldReject(sourceFolderPath)
                ? XDiagnosticAdBlockSourceValidation.Invalid("Synthetic verification failure.")
                : await inner.InspectAsync(sourceFolderPath, cancellationToken);
    }

    private sealed class ThrowingActivationOperations(string managedPath) : IXAdBlockManagedPayloadDirectoryOperations
    {
        public bool Exists(string path) => Directory.Exists(path);

        public void Move(string source, string destination)
        {
            if (source.EndsWith(".staging", StringComparison.Ordinal)
                && string.Equals(destination, managedPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Synthetic activation failure.");
            }

            Directory.Move(source, destination);
        }

        public void Delete(string path, bool recursive) => Directory.Delete(path, recursive);
    }

    private sealed class FailingFileCopier : IXAdBlockManagedPayloadFileCopier
    {
        public Task CopyAsync(
            string sourcePath,
            long expectedLength,
            byte[] expectedSha256,
            string destinationPath,
            CancellationToken cancellationToken) =>
            Task.FromException(new IOException("Synthetic staging copy failure."));
    }

    private sealed class FailingBackupDeleteOperations : IXAdBlockManagedPayloadDirectoryOperations
    {
        public bool Exists(string path) => Directory.Exists(path);

        public void Move(string source, string destination) => Directory.Move(source, destination);

        public void Delete(string path, bool recursive)
        {
            if (path.EndsWith(".previous", StringComparison.Ordinal))
            {
                throw new IOException("Synthetic backup cleanup failure.");
            }

            Directory.Delete(path, recursive);
        }
    }
}
