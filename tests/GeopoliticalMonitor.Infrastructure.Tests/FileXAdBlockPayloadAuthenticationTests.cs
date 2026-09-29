using System.Diagnostics;
using GeopoliticalMonitor.Application.XBrowser;
using GeopoliticalMonitor.Infrastructure.Persistence;

namespace GeopoliticalMonitor.Infrastructure.Tests;

public sealed class FileXAdBlockPayloadAuthenticationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"gpm-payload-auth-{Guid.NewGuid():N}");

    [Fact]
    public async Task DigestIsIndependentOfRootTimestampsAndBrowserMetadata()
    {
        var first = CreatePayload("first", "payload");
        var second = CreatePayload("second", "payload");
        var baseline = await InspectAsync(first);
        var alternateRoot = await InspectAsync(second);
        Directory.CreateDirectory(Path.Combine(first, "_metadata", "generated_indexed_rulesets"));
        File.WriteAllText(Path.Combine(first, "_metadata", "verified_contents.json"), "one");
        File.WriteAllText(Path.Combine(first, "_metadata", "generated_indexed_rulesets", "cache"), "two");
        File.SetLastWriteTimeUtc(Path.Combine(first, "worker.js"), DateTime.UtcNow.AddYears(-1));
        var changedMetadata = await InspectAsync(first);

        Assert.Equal(baseline.Payload, alternateRoot.Payload);
        Assert.Equal(baseline.Payload, changedMetadata.Payload);
    }

    [Fact]
    public async Task EveryFilesystemRuntimeMutationBreaksTheTrustedDigest()
    {
        var source = CreatePayload("source", "payload");
        var baseline = await InspectAsync(source);
        var trust = CreateTrust(baseline);

        File.WriteAllText(Path.Combine(source, "worker.js"), "payloae");
        Assert.Null((await InspectAsync(source, trust)).TrustRecord);

        source = CreatePayload("added", "payload");
        File.WriteAllText(Path.Combine(source, "added.txt"), "x");
        Assert.Null((await InspectAsync(source, trust)).TrustRecord);

        source = CreatePayload("removed", "payload");
        File.Delete(Path.Combine(source, "worker.js"));
        Assert.Null((await InspectAsync(source, trust)).TrustRecord);

        source = CreatePayload("renamed", "payload");
        File.Move(Path.Combine(source, "worker.js"), Path.Combine(source, "renamed.js"));
        Assert.Null((await InspectAsync(source, trust)).TrustRecord);
    }

    [Fact]
    public async Task ManifestOnlySpoofCannotMatchFullPayloadAuthority()
    {
        var source = CreatePayload("source", "payload");
        var baseline = await InspectAsync(source);
        var trust = CreateTrust(baseline);
        File.Delete(Path.Combine(source, "worker.js"));

        var spoof = await InspectAsync(source, trust);

        Assert.Null(spoof.TrustRecord);
        Assert.NotEqual(baseline.Payload?.Sha256, spoof.Payload?.Sha256);
    }

    [Fact]
    public async Task PerFileLimitIsAcceptedExactlyAndOverflowRejectedBeforeAllocation()
    {
        var source = CreatePayload("source", "payload");
        var largeFile = Path.Combine(source, "large.bin");
        await using (var stream = new FileStream(largeFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.SetLength(XAdBlockPayloadLimits.MaximumIndividualFileBytes);
        }

        var exact = await new FileXDiagnosticAdBlockSourceInspector([]).InspectAsync(source);
        Assert.True(exact.IsValid, exact.ErrorMessage);

        await using (var stream = new FileStream(largeFile, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            stream.SetLength(XAdBlockPayloadLimits.MaximumIndividualFileBytes + 1);
        }

        var overflow = await new FileXDiagnosticAdBlockSourceInspector([]).InspectAsync(source);
        Assert.False(overflow.IsValid);
        Assert.Contains("larger", overflow.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReparsePointIsRejectedWithoutFollowingTarget()
    {
        var source = CreatePayload("source", "payload");
        var target = Path.Combine(root, "outside");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "secret.txt"), "must not be read");
        var link = Path.Combine(source, "linked");
        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("/d");
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("mklink");
        startInfo.ArgumentList.Add("/J");
        startInfo.ArgumentList.Add(link);
        startInfo.ArgumentList.Add(target);
        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        await process!.WaitForExitAsync();
        Assert.Equal(0, process.ExitCode);

        var validation = await new FileXDiagnosticAdBlockSourceInspector([]).InspectAsync(source);
        Directory.Delete(link);

        Assert.False(validation.IsValid);
        Assert.Contains("reparse", validation.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private async Task<XDiagnosticAdBlockSourceIdentity> InspectAsync(
        string path,
        OfficialAdBlockTrustRecord? trust = null)
    {
        var inspector = new FileXDiagnosticAdBlockSourceInspector(
            trust is null ? [] : [trust]);
        var validation = await inspector.InspectAsync(path);
        Assert.True(validation.IsValid, validation.ErrorMessage);
        return Assert.IsType<XDiagnosticAdBlockSourceIdentity>(validation.Identity);
    }

    private static OfficialAdBlockTrustRecord CreateTrust(XDiagnosticAdBlockSourceIdentity source) => new(
        1,
        "test-adblock",
        "test-store",
        source.Manifest.OfficialStoreExtensionId!,
        source.Manifest.Version,
        source.Payload!.DigestFormat,
        source.Payload.Sha256,
        "Synthetic test authority.");

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
}
