namespace GeopoliticalMonitor.Application.XBrowser;

public sealed record XBrowserExtensionInfo(string Id, string Name, bool IsEnabled);

public enum XBrowserExtensionStatusKind
{
    NotInstalled,
    Installed,
    Unavailable,
}

public sealed record XBrowserExtensionStatus(
    XBrowserExtensionStatusKind Kind,
    string? ExtensionId,
    string? Name,
    bool IsEnabled,
    string? Message)
{
    public static XBrowserExtensionStatus NotInstalled { get; } = new(
        XBrowserExtensionStatusKind.NotInstalled,
        ExtensionId: null,
        Name: null,
        IsEnabled: false,
        Message: "No extension installed");

    public static XBrowserExtensionStatus Unavailable(string message) => new(
        XBrowserExtensionStatusKind.Unavailable,
        ExtensionId: null,
        Name: null,
        IsEnabled: false,
        Message: message);

    public static XBrowserExtensionStatus Installed(XBrowserExtensionInfo extension) => new(
        XBrowserExtensionStatusKind.Installed,
        extension.Id,
        extension.Name,
        extension.IsEnabled,
        null);
}

public interface IXBrowserExtensionProfile
{
    Task<IReadOnlyList<XBrowserExtensionInfo>> GetInstalledExtensionsAsync(CancellationToken cancellationToken = default);

    Task<XBrowserExtensionInfo> AddBrowserExtensionAsync(string extensionFolderPath, CancellationToken cancellationToken = default);

    Task SetExtensionEnabledAsync(string extensionId, bool enabled, CancellationToken cancellationToken = default);

    Task RemoveExtensionAsync(string extensionId, CancellationToken cancellationToken = default);
}

public interface IXManagedExtensionIdStore
{
    Task<string?> GetManagedExtensionIdAsync(CancellationToken cancellationToken = default);

    Task SaveManagedExtensionIdAsync(string extensionId, CancellationToken cancellationToken = default);

    Task ClearManagedExtensionIdAsync(CancellationToken cancellationToken = default);
}