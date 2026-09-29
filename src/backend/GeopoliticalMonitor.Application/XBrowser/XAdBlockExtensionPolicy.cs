namespace GeopoliticalMonitor.Application.XBrowser;

public static class XAdBlockExtensionPolicy
{
    public const string ChromeExtensionId = "gighmmpiobklfepjocnamgkkbiglidom";
    public const string EdgeExtensionId = "ndcileolkflehcjpmjnfbnaibdcgglog";
    public const string OfficialInstallationInstructionsUrl = "https://helpcenter.getadblock.com/adblock-help-center/how-to-install-adblock-in-chrome-when-you-can-t-do-it-the-normal-way";

    public static bool HasKnownStoreIdentity(XBrowserExtensionInfo extension)
    {
        return IsKnownStoreId(extension.Id);
    }

    public static bool IsKnownStoreId(string? extensionId)
    {
        return string.Equals(extensionId, ChromeExtensionId, StringComparison.Ordinal)
            || string.Equals(extensionId, EdgeExtensionId, StringComparison.Ordinal);
    }

    public static bool IsApproved(XBrowserExtensionInfo extension) => HasKnownStoreIdentity(extension);

    public static bool IsApprovedId(string? extensionId) => IsKnownStoreId(extensionId);
}
