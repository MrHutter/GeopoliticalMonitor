using GeopoliticalMonitor.Application.XBrowser;
using Microsoft.UI.Dispatching;
using Microsoft.Web.WebView2.Core;

namespace GeopoliticalMonitor.Desktop;

public sealed class CoreWebView2ExtensionProfileAdapter(
    CoreWebView2Profile profile,
    DispatcherQueue ownerDispatcher) : IXBrowserExtensionProfile
{
    public Task<IReadOnlyList<XBrowserExtensionInfo>> GetInstalledExtensionsAsync(CancellationToken cancellationToken = default) =>
        RunOnOwnerThreadAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var extensions = await profile.GetBrowserExtensionsAsync();
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<XBrowserExtensionInfo> result = extensions
                .Select(extension => new XBrowserExtensionInfo(extension.Id, extension.Name, extension.IsEnabled))
                .ToArray();
            return result;
        });

    public Task<XBrowserExtensionInfo> AddBrowserExtensionAsync(
        string extensionFolderPath,
        CancellationToken cancellationToken = default) =>
        RunOnOwnerThreadAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var extension = await profile.AddBrowserExtensionAsync(extensionFolderPath);
            cancellationToken.ThrowIfCancellationRequested();
            return new XBrowserExtensionInfo(extension.Id, extension.Name, extension.IsEnabled);
        });

    public Task SetExtensionEnabledAsync(
        string extensionId,
        bool enabled,
        CancellationToken cancellationToken = default) =>
        RunOnOwnerThreadAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var extension = await FindExtensionAsync(extensionId);
            await extension.EnableAsync(enabled);
            cancellationToken.ThrowIfCancellationRequested();
        });

    public Task RemoveExtensionAsync(string extensionId, CancellationToken cancellationToken = default) =>
        RunOnOwnerThreadAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var extension = await FindExtensionAsync(extensionId);
            await extension.RemoveAsync();
            cancellationToken.ThrowIfCancellationRequested();
        });

    private async Task<CoreWebView2BrowserExtension> FindExtensionAsync(string extensionId)
    {
        var extensions = await profile.GetBrowserExtensionsAsync();
        var extension = extensions.SingleOrDefault(item => string.Equals(item.Id, extensionId, StringComparison.Ordinal));
        return extension ?? throw new InvalidOperationException("The managed X extension is not installed.");
    }

    private Task<T> RunOnOwnerThreadAsync<T>(Func<Task<T>> operation)
    {
        if (ownerDispatcher.HasThreadAccess)
        {
            return operation();
        }

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!ownerDispatcher.TryEnqueue(async () =>
            {
                try
                {
                    completion.TrySetResult(await operation());
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            }))
        {
            completion.TrySetException(new InvalidOperationException("The diagnostic WebView dispatcher is unavailable."));
        }

        return completion.Task;
    }

    private Task RunOnOwnerThreadAsync(Func<Task> operation) =>
        RunOnOwnerThreadAsync(async () =>
        {
            await operation();
            return true;
        });
}
