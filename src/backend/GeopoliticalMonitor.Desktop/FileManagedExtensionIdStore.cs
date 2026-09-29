using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Desktop;

public sealed class FileManagedExtensionIdStore(string extensionsRoot) : IXManagedExtensionIdStore
{
    private const string ManagedExtensionIdFileName = "managed-extension-id.txt";

    private string ManagedExtensionIdPath => Path.Combine(extensionsRoot, ManagedExtensionIdFileName);

    public async Task<string?> GetManagedExtensionIdAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ManagedExtensionIdPath))
        {
            return null;
        }

        var value = await File.ReadAllTextAsync(ManagedExtensionIdPath, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public async Task SaveManagedExtensionIdAsync(string extensionId, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(extensionsRoot);
        await File.WriteAllTextAsync(ManagedExtensionIdPath, extensionId, cancellationToken).ConfigureAwait(false);
    }

    public Task ClearManagedExtensionIdAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(ManagedExtensionIdPath))
        {
            File.Delete(ManagedExtensionIdPath);
        }

        return Task.CompletedTask;
    }
}