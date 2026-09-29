using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class AdBlockTrustSecurityTests
{
    [Fact]
    public void ReviewedChrome6470RecordExistsExactlyOnce()
    {
        var record = Assert.Single(OfficialAdBlockTrustRecords.All);

        Assert.Equal(1, record.TrustSchemaVersion);
        Assert.Equal("adblock-by-eyeo", record.ProductKey);
        Assert.Equal("chrome-web-store", record.StoreLabel);
        Assert.Equal("gighmmpiobklfepjocnamgkkbiglidom", record.OfficialStoreExtensionId);
        Assert.Equal("6.47.0", record.ExtensionVersion);
        Assert.Equal("gpm-extension-payload-v1", record.DigestFormat);
        Assert.Equal("9022cbf484913e8c45cbeae3828c7072de51ffa1369013e7e0aa105172a822ae", record.PayloadSha256);
    }

    [Fact]
    public void TrustMatchingRejectsWrongIdentityVersionFormatOrDigest()
    {
        var trusted = OfficialAdBlockTrustRecords.ChromeWebStore6470;
        var payload = Payload(trusted.PayloadSha256);

        Assert.True(trusted.Matches(trusted.OfficialStoreExtensionId, trusted.ExtensionVersion, payload));
        Assert.False(trusted.Matches(XAdBlockExtensionPolicy.EdgeExtensionId, trusted.ExtensionVersion, payload));
        Assert.False(trusted.Matches(trusted.OfficialStoreExtensionId, "6.47.1", payload));
        Assert.False(trusted.Matches(trusted.OfficialStoreExtensionId, trusted.ExtensionVersion, payload with { DigestFormat = "unknown" }));
        Assert.False(trusted.Matches(trusted.OfficialStoreExtensionId, trusted.ExtensionVersion, payload with { Sha256 = new string('0', 64) }));
    }

    [Fact]
    public void CanonicalDigestIsOrdinalAndIndependentOfEnumerationOrSourceRoot()
    {
        var first = File("scripts/a.js", "alpha");
        var second = File("Scripts/B.js", "beta");

        var forward = XAdBlockPayloadDigest.Compute([first, second]);
        var reverse = XAdBlockPayloadDigest.Compute([second, first]);

        Assert.Equal("3df31a2c24574fb0d968192d0ee5c067ff3f9d9a71c19656863223a6aa1a3b9d", forward);
        Assert.Equal(forward, reverse);
    }

    [Fact]
    public void EveryRuntimePathAndByteMutationChangesCanonicalDigest()
    {
        var originalFiles = new[] { File("manifest.json", "abc"), File("worker.js", "payload") };
        var original = XAdBlockPayloadDigest.Compute(originalFiles);

        Assert.NotEqual(original, XAdBlockPayloadDigest.Compute([File("manifest.json", "abc"), File("worker.js", "payloae")]));
        Assert.NotEqual(original, XAdBlockPayloadDigest.Compute([.. originalFiles, File("added.txt", "x")]));
        Assert.NotEqual(original, XAdBlockPayloadDigest.Compute([originalFiles[0]]));
        Assert.NotEqual(original, XAdBlockPayloadDigest.Compute([originalFiles[0], File("renamed.js", "payload")]));
        Assert.NotEqual(original, XAdBlockPayloadDigest.Compute([originalFiles[0], File("Worker.js", "payload")]));
    }

    [Fact]
    public void LengthFramingAndPathFramingAreUnambiguous()
    {
        var contentHash = SHA256.HashData(Encoding.UTF8.GetBytes("same"));
        var first = XAdBlockPayloadDigest.Compute([new("ab", 1, contentHash), new("c", 23, contentHash)]);
        var second = XAdBlockPayloadDigest.Compute([new("a", 12, contentHash), new("bc", 3, contentHash)]);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void DuplicateNormalizedPathIsRejected()
    {
        Assert.Throws<InvalidDataException>(() => XAdBlockPayloadDigest.Compute([
            File("folder/file.js", "one"),
            File("folder\\file.js", "two"),
        ]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/absolute")]
    [InlineData("C:/drive")]
    [InlineData("\\\\server\\share")]
    [InlineData("../escape")]
    [InlineData("folder/../escape")]
    [InlineData("folder//file")]
    [InlineData("folder/./file")]
    [InlineData("folder/")]
    public void InvalidCanonicalPathsAreRejected(string path)
    {
        Assert.Throws<ArgumentException>(() => XAdBlockPayloadDigest.NormalizeRelativePath(path));
    }

    [Fact]
    public void NulAndInvalidUtf16PathsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => XAdBlockPayloadDigest.NormalizeRelativePath("folder/\0/file"));
        Assert.Throws<ArgumentException>(() => XAdBlockPayloadDigest.NormalizeRelativePath("folder/\ud800/file"));
    }

    [Fact]
    public void ResourceBudgetAcceptsExactLimitsAndRejectsEveryOverflowClass()
    {
        var exactBytes = new XAdBlockPayloadResourceBudget();
        for (var index = 0; index < 16; index++)
        {
            exactBytes.AddFile($"file-{index}", XAdBlockPayloadLimits.MaximumIndividualFileBytes);
        }

        Assert.Equal(XAdBlockPayloadLimits.MaximumAggregateRuntimeBytes, exactBytes.TotalBytes);
        Assert.Throws<InvalidDataException>(() => exactBytes.AddFile("overflow", 1));

        var exactFiles = new XAdBlockPayloadResourceBudget();
        for (var index = 0; index < XAdBlockPayloadLimits.MaximumRuntimeFiles; index++)
        {
            exactFiles.AddFile($"f{index}", 0);
        }

        Assert.Throws<InvalidDataException>(() => exactFiles.AddFile("too-many", 0));
        Assert.Throws<InvalidDataException>(() => new XAdBlockPayloadResourceBudget().AddFile(
            "large",
            XAdBlockPayloadLimits.MaximumIndividualFileBytes + 1));

        var exactDirectories = new XAdBlockPayloadResourceBudget();
        for (var index = 0; index < XAdBlockPayloadLimits.MaximumDirectories; index++)
        {
            exactDirectories.AddDirectory(1);
        }

        Assert.Throws<InvalidDataException>(() => exactDirectories.AddDirectory(1));
        Assert.Throws<InvalidDataException>(() => new XAdBlockPayloadResourceBudget().AddDirectory(
            XAdBlockPayloadLimits.MaximumDirectoryDepth + 1));
        Assert.Equal(
            new string('a', XAdBlockPayloadLimits.MaximumNormalizedPathUtf8Bytes),
            XAdBlockPayloadDigest.NormalizeRelativePath(new string('a', XAdBlockPayloadLimits.MaximumNormalizedPathUtf8Bytes)));
        Assert.Throws<ArgumentException>(() => XAdBlockPayloadDigest.NormalizeRelativePath(
            new string('a', XAdBlockPayloadLimits.MaximumNormalizedPathUtf8Bytes + 1)));
    }

    [Fact]
    public void StrictProductionBindingRoundTripsAndOldOrMalformedAuthorityIsRejected()
    {
        var source = TrustedSource(@"C:\managed\Current");
        var binding = XDiagnosticAdBlockBinding.Create(
            source.Manifest.ExpectedRuntimeId,
            source,
            XDiagnosticAdBlockBinding.ProductionXProfileScope);

        var roundTrip = JsonSerializer.Deserialize<XDiagnosticAdBlockBinding>(JsonSerializer.Serialize(binding));

        Assert.NotNull(roundTrip);
        Assert.True(roundTrip!.IsStructurallyValid());
        Assert.Equal(source.TrustRecord, roundTrip.TrustRecord);
        Assert.Equal(source.Payload, roundTrip.Payload);
        Assert.False((roundTrip with { SchemaVersion = 1 }).IsStructurallyValid());
        Assert.False((roundTrip with { Payload = roundTrip.Payload! with { Sha256 = "not-a-digest" } }).IsStructurallyValid());
        Assert.False((roundTrip with { Payload = roundTrip.Payload! with { DigestFormat = "unknown" } }).IsStructurallyValid());
        Assert.False((roundTrip with { TrustRecord = null }).IsStructurallyValid());
        Assert.False((roundTrip with
        {
            TrustRecord = roundTrip.TrustRecord! with { ProductKey = "unreviewed-product" },
        }).IsStructurallyValid());
    }

    [Fact]
    public void StartupAllowsCleanProfileOrExactTrustedBindingOnly()
    {
        var clean = XProductionAdBlockStartupTrustPolicy.Evaluate(false, false, null, null);
        var source = TrustedSource(@"C:\managed\Current");
        var binding = XDiagnosticAdBlockBinding.Create(
            source.Manifest.ExpectedRuntimeId,
            source,
            XDiagnosticAdBlockBinding.ProductionXProfileScope);
        var valid = XProductionAdBlockStartupTrustPolicy.Evaluate(
            true,
            true,
            binding,
            XDiagnosticAdBlockSourceValidation.Valid(source));
        var readyForFreshInstall = XProductionAdBlockStartupTrustPolicy.Evaluate(
            true,
            false,
            binding: null,
            XDiagnosticAdBlockSourceValidation.Valid(source));
        var mutated = XProductionAdBlockStartupTrustPolicy.Evaluate(
            true,
            true,
            binding,
            XDiagnosticAdBlockSourceValidation.Valid(source with { Payload = source.Payload! with { Sha256 = new string('0', 64) } }));

        Assert.True(clean.AttemptExtensionEnvironment);
        Assert.True(valid.AttemptExtensionEnvironment);
        Assert.True(valid.HasTrustedManagedPayload);
        Assert.True(readyForFreshInstall.AttemptExtensionEnvironment);
        Assert.True(readyForFreshInstall.HasTrustedManagedPayload);
        Assert.False(mutated.AttemptExtensionEnvironment);
        Assert.False(XProductionAdBlockStartupTrustPolicy.Evaluate(true, false, null, null).AttemptExtensionEnvironment);
        Assert.False(XProductionAdBlockStartupTrustPolicy.Evaluate(false, true, binding, null).AttemptExtensionEnvironment);
        Assert.False(XProductionAdBlockStartupTrustPolicy.Evaluate(false, true, null, null).AttemptExtensionEnvironment);
    }

    [Fact]
    public void DiagnosticFormatterRetainsCategoryButRedactsPathsUrisAndBoundsOutput()
    {
        var privatePath = @"C:\Users\owner\Secret Folder\extension";
        var exception = new InvalidOperationException(
            $"Failed at {privatePath} using https://x.com/private?token=secret " + new string('z', 600));

        var formatted = SafeDiagnosticExceptionFormatter.FormatForDebug("Production install", exception, privatePath);

        Assert.Contains("Production install", formatted);
        Assert.Contains(typeof(InvalidOperationException).FullName!, formatted);
        Assert.Contains("0x", formatted);
        Assert.DoesNotContain("owner", formatted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Secret Folder", formatted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token=secret", formatted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" at GeopoliticalMonitor", formatted, StringComparison.Ordinal);
        Assert.True(formatted.Length < 500);
    }

    private static XAdBlockPayloadFileHash File(string path, string contents)
    {
        var bytes = Encoding.UTF8.GetBytes(contents);
        return new(path, bytes.Length, SHA256.HashData(bytes));
    }

    private static XAdBlockPayloadIdentity Payload(string digest) => new(
        XAdBlockPayloadDigest.DigestFormat,
        digest,
        443,
        326_555_718,
        57,
        3,
        63);

    private static XDiagnosticAdBlockSourceIdentity TrustedSource(string path)
    {
        var trust = OfficialAdBlockTrustRecords.ChromeWebStore6470;
        var manifest = new XDiagnosticAdBlockManifestIdentity(
            3,
            trust.ExtensionVersion,
            "__MSG_name__",
            "en_US",
            "AdBlock - block ads across the web",
            "Adblock, Inc.",
            HasKey: false,
            "manifest-hash",
            ChromiumExtensionIdentity.GenerateFromUnpackedPath(path))
        {
            OfficialStoreExtensionId = trust.OfficialStoreExtensionId,
        };
        return new(path, manifest, Payload(trust.PayloadSha256), trust);
    }
}
