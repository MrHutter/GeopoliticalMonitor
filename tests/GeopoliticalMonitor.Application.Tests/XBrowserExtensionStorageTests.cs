using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class XBrowserExtensionStorageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public void RejectsMissingFolder()
    {
        var storage = new XBrowserExtensionStorage();

        Assert.Throws<InvalidOperationException>(() => storage.PrepareForInstall(Path.Combine(root, "missing"), Path.Combine(root, "stable")));
    }

    [Fact]
    public void RejectsFolderWithoutManifest()
    {
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        var storage = new XBrowserExtensionStorage();

        Assert.Throws<InvalidOperationException>(() => storage.PrepareForInstall(source, Path.Combine(root, "stable")));
    }

    [Fact]
    public void CopiesValidExtensionIntoStableManagedLocation()
    {
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(Path.Combine(source, "assets"));
        File.WriteAllText(Path.Combine(source, XBrowserExtensionStorage.ManifestFileName), "{\"manifest_version\":3,\"name\":\"Test\",\"version\":\"1.0\"}");
        File.WriteAllText(Path.Combine(source, "assets", "rules.json"), "[]");
        var stableRoot = Path.Combine(root, "stable");
        var storage = new XBrowserExtensionStorage();

        var prepared = storage.PrepareForInstall(source, stableRoot);

        Assert.Equal(Path.Combine(stableRoot, XBrowserExtensionStorage.ManagedExtensionDirectoryName), prepared);
        Assert.True(File.Exists(Path.Combine(prepared, XBrowserExtensionStorage.ManifestFileName)));
        Assert.True(File.Exists(Path.Combine(prepared, "assets", "rules.json")));
        Assert.StartsWith(stableRoot, prepared, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsReparsePointSourceFolderWhenPlatformCanCreateOne()
    {
        var source = Path.Combine(root, "source");
        var target = Path.Combine(root, "target");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, XBrowserExtensionStorage.ManifestFileName), "{}");

        try
        {
            Directory.CreateSymbolicLink(source, target);
        }
        catch (Exception) when (!OperatingSystem.IsWindows())
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
        catch (IOException)
        {
            return;
        }

        var storage = new XBrowserExtensionStorage();
        Assert.Throws<InvalidOperationException>(() => storage.PrepareForInstall(source, Path.Combine(root, "stable")));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}