namespace GeopoliticalMonitor.Infrastructure.Persistence;

public sealed class LocalApplicationDataPathProvider
{
    private const string ApplicationDirectoryName = "GeopoliticalMonitor";
    private const string DataDirectoryName = "Data";
    private const string WebView2DirectoryName = "WebView2";
    private const string AppWebViewDirectoryName = "AppShell";
    private const string XBrowserDirectoryName = "XBrowser";
    private const string XBrowserExtensionDiagnosticDirectoryName = "ExtensionCapabilityDiagnostic";
    private const string ExtensionsDirectoryName = "Extensions";
    private const string AdBlockDirectoryName = "AdBlock";
    private const string CurrentExtensionDirectoryName = "Current";
    private const string DatabaseFileName = "geopolitical-monitor.db";
    private const string DiagnosticAdBlockBindingFileName = "diagnostic-adblock-binding.json";
    private const string ProductionAdBlockBindingFileName = "x-adblock-binding.json";

    private readonly Func<string> getLocalApplicationDataRoot;

    public LocalApplicationDataPathProvider()
        : this(() => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
    {
    }

    public LocalApplicationDataPathProvider(Func<string> getLocalApplicationDataRoot)
    {
        this.getLocalApplicationDataRoot = getLocalApplicationDataRoot;
    }

    public string GetApplicationDataDirectory()
    {
        return Path.Combine(GetLocalApplicationDataRoot(), ApplicationDirectoryName);
    }

    public string GetDataDirectory()
    {
        return Path.Combine(GetApplicationDataDirectory(), DataDirectoryName);
    }

    public string GetDatabasePath()
    {
        return Path.Combine(GetDataDirectory(), DatabaseFileName);
    }

    public string GetDiagnosticAdBlockBindingPath()
    {
        return Path.Combine(GetDataDirectory(), DiagnosticAdBlockBindingFileName);
    }

    public string GetProductionAdBlockBindingPath()
    {
        return Path.Combine(GetDataDirectory(), ProductionAdBlockBindingFileName);
    }

    public string GetProductionAdBlockManagedSourceDirectory()
    {
        return Path.Combine(
            GetApplicationDataDirectory(),
            ExtensionsDirectoryName,
            AdBlockDirectoryName,
            CurrentExtensionDirectoryName);
    }

    public string GetAppWebViewUserDataFolder()
    {
        return Path.Combine(GetApplicationDataDirectory(), WebView2DirectoryName, AppWebViewDirectoryName);
    }

    public string GetXBrowserUserDataFolder()
    {
        return Path.Combine(GetApplicationDataDirectory(), WebView2DirectoryName, XBrowserDirectoryName);
    }

    public string GetXBrowserExtensionDiagnosticUserDataFolder()
    {
        return Path.Combine(GetApplicationDataDirectory(), WebView2DirectoryName, XBrowserExtensionDiagnosticDirectoryName);
    }

    private string GetLocalApplicationDataRoot()
    {
        var localAppData = getLocalApplicationDataRoot();

        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new InvalidOperationException("The local application data directory is unavailable.");
        }

        return localAppData;
    }
}
