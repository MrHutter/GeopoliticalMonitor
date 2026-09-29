namespace GeopoliticalMonitor.Application.XBrowser;

public sealed class XManagedExtensionService(IXBrowserExtensionProfile profile, IXManagedExtensionIdStore managedExtensionIdStore)
{
    public async Task<XBrowserExtensionStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var managedId = await managedExtensionIdStore.GetManagedExtensionIdAsync(cancellationToken).ConfigureAwait(false);
        var installedExtensions = await profile.GetInstalledExtensionsAsync(cancellationToken).ConfigureAwait(false);
        var extension = string.IsNullOrWhiteSpace(managedId)
            ? null
            : installedExtensions.SingleOrDefault(item => string.Equals(item.Id, managedId, StringComparison.Ordinal));

        if (extension is not null && XAdBlockExtensionPolicy.IsApproved(extension))
        {
            return XBrowserExtensionStatus.Installed(extension);
        }

        if (!string.IsNullOrWhiteSpace(managedId))
        {
            await managedExtensionIdStore.ClearManagedExtensionIdAsync(cancellationToken).ConfigureAwait(false);
        }

        extension = installedExtensions.FirstOrDefault(XAdBlockExtensionPolicy.IsApproved);
        if (extension is null)
        {
            return XBrowserExtensionStatus.NotInstalled;
        }

        await managedExtensionIdStore.SaveManagedExtensionIdAsync(extension.Id, cancellationToken).ConfigureAwait(false);
        return XBrowserExtensionStatus.Installed(extension);
    }

    public async Task<XBrowserExtensionStatus> InstallAsync(string preparedExtensionFolderPath, CancellationToken cancellationToken = default)
    {
        var currentStatus = await GetStatusAsync(cancellationToken).ConfigureAwait(false);
        if (currentStatus.Kind == XBrowserExtensionStatusKind.Installed)
        {
            return currentStatus;
        }

        var extension = await profile.AddBrowserExtensionAsync(preparedExtensionFolderPath, cancellationToken).ConfigureAwait(false);
        if (!XAdBlockExtensionPolicy.IsApproved(extension))
        {
            await profile.RemoveExtensionAsync(extension.Id, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("The selected unpacked extension is not the approved AdBlock by eyeo release.");
        }

        await managedExtensionIdStore.SaveManagedExtensionIdAsync(extension.Id, cancellationToken).ConfigureAwait(false);
        return XBrowserExtensionStatus.Installed(extension);
    }

    public async Task<XBrowserExtensionStatus> SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(cancellationToken).ConfigureAwait(false);
        if (status.Kind != XBrowserExtensionStatusKind.Installed || string.IsNullOrWhiteSpace(status.ExtensionId))
        {
            return status;
        }

        await profile.SetExtensionEnabledAsync(status.ExtensionId, enabled, cancellationToken).ConfigureAwait(false);
        var refreshed = await profile.GetInstalledExtensionsAsync(cancellationToken).ConfigureAwait(false);
        var extension = refreshed.SingleOrDefault(item => string.Equals(item.Id, status.ExtensionId, StringComparison.Ordinal));
        return extension is null ? XBrowserExtensionStatus.NotInstalled : XBrowserExtensionStatus.Installed(extension);
    }

    public async Task<XBrowserExtensionStatus> RemoveAsync(CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(cancellationToken).ConfigureAwait(false);
        if (status.Kind == XBrowserExtensionStatusKind.Installed && !string.IsNullOrWhiteSpace(status.ExtensionId))
        {
            await profile.RemoveExtensionAsync(status.ExtensionId, cancellationToken).ConfigureAwait(false);
        }

        await managedExtensionIdStore.ClearManagedExtensionIdAsync(cancellationToken).ConfigureAwait(false);
        return XBrowserExtensionStatus.NotInstalled;
    }
}
