using System.Text;
using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class XDiagnosticAdBlockIdentityTests
{
    private const string ExpectedRuntimeId = "hpgebfmcjdefclbkpgkdlbkbpkjjeofb";
    private const string SelectedSourcePath = "C:\\Users\\owner\\Desktop\\adblock";

    [Fact]
    public void SelectedUnpackedPathProducesExpectedRuntimeId()
    {
        Assert.Equal(ExpectedRuntimeId, ChromiumExtensionIdentity.GenerateFromUnpackedPath(SelectedSourcePath));
    }

    [Fact]
    public void MovingKeylessUnpackedSourceChangesRuntimeId()
    {
        var original = ChromiumExtensionIdentity.GenerateFromUnpackedPath(SelectedSourcePath);
        var moved = ChromiumExtensionIdentity.GenerateFromUnpackedPath("C:\\AdBlock\\Moved");

        Assert.NotEqual(original, moved);
    }

    [Fact]
    public void KeylessOfficialManifestUsesPathIdentityAndBoundedMetadata()
    {
        var validation = Inspect(SelectedSourcePath, CreateManifest());

        Assert.True(validation.IsValid);
        var manifest = Assert.IsType<XDiagnosticAdBlockSourceIdentity>(validation.Identity).Manifest;
        Assert.Equal(3, manifest.ManifestVersion);
        Assert.Equal("6.46.0", manifest.Version);
        Assert.Equal("__MSG_name__", manifest.RawName);
        Assert.Equal("en_US", manifest.DefaultLocale);
        Assert.Equal("AdBlock - block ads across the web", manifest.DefaultLocaleName);
        Assert.Equal("Adblock, Inc.", manifest.Author);
        Assert.False(manifest.HasKey);
        Assert.Equal(ExpectedRuntimeId, manifest.ExpectedRuntimeId);
        Assert.Equal(64, manifest.ManifestSha256.Length);
    }

    [Fact]
    public void ManifestKeyProducesStableIdentityAcrossSourcePaths()
    {
        const string key = "AQIDBAUGBwgJCgsMDQ4PEA==";
        var first = Inspect("C:\\First", CreateManifest(key));
        var second = Inspect("D:\\Moved", CreateManifest(key));

        Assert.True(first.IsValid);
        Assert.True(second.IsValid);
        Assert.True(first.Identity?.Manifest.HasKey);
        Assert.Equal(first.Identity?.Manifest.ExpectedRuntimeId, second.Identity?.Manifest.ExpectedRuntimeId);
    }

    [Fact]
    public void ManifestWithoutOfficialIdentityMetadataIsRejected()
    {
        var manifest = CreateManifest().Replace(
            XAdBlockExtensionPolicy.ChromeExtensionId,
            "unrelated-extension-id",
            StringComparison.Ordinal);

        var validation = Inspect(SelectedSourcePath, manifest);

        Assert.False(validation.IsValid);
        Assert.Null(validation.Identity);
    }

    private static XDiagnosticAdBlockSourceValidation Inspect(string path, string manifest) =>
        XDiagnosticAdBlockManifestPolicy.Inspect(
            path,
            Encoding.UTF8.GetBytes(manifest),
            Encoding.UTF8.GetBytes("""
                {
                  "name": { "message": "AdBlock - block ads across the web" }
                }
                """));

    private static string CreateManifest(string? key = null)
    {
        var keyProperty = key is null ? string.Empty : $", \"key\": \"{key}\"";
        return $$"""
            {
              "manifest_version": 3,
              "version": "6.46.0",
              "name": "__MSG_name__",
              "default_locale": "en_US",
              "author": "Adblock, Inc.",
              "externally_connectable": {
                "ids": [
                  "{{XAdBlockExtensionPolicy.ChromeExtensionId}}",
                  "{{XAdBlockExtensionPolicy.EdgeExtensionId}}"
                ]
              }{{keyProperty}}
            }
            """;
    }
}
