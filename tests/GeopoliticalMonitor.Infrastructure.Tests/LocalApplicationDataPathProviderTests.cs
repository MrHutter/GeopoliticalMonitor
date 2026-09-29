using GeopoliticalMonitor.Application.Preferences;
using GeopoliticalMonitor.Infrastructure;
using GeopoliticalMonitor.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GeopoliticalMonitor.Infrastructure.Tests;

public class LocalApplicationDataPathProviderTests
{
    [Fact]
    public void InfrastructureAssemblyIsAvailable()
    {
        Assert.Equal("GeopoliticalMonitor.Infrastructure", typeof(InfrastructureAssemblyMarker).Assembly.GetName().Name);
    }

    [Fact]
    public void UsesPerUserLocalApplicationDataDirectory()
    {
        var provider = new LocalApplicationDataPathProvider();

        var path = provider.GetApplicationDataDirectory();

        Assert.True(Path.IsPathRooted(path));
        Assert.EndsWith("GeopoliticalMonitor", path);
        Assert.DoesNotContain("src", path, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("frontend", path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ComposesStableDatabasePathBelowLocalApplicationDataRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var provider = new LocalApplicationDataPathProvider(() => root);

        var applicationDirectory = provider.GetApplicationDataDirectory();
        var dataDirectory = provider.GetDataDirectory();
        var databasePath = provider.GetDatabasePath();
        var diagnosticBindingPath = provider.GetDiagnosticAdBlockBindingPath();
        var productionBindingPath = provider.GetProductionAdBlockBindingPath();
        var productionManagedSource = provider.GetProductionAdBlockManagedSourceDirectory();

        Assert.Equal(Path.Combine(root, "GeopoliticalMonitor"), applicationDirectory);
        Assert.Equal(Path.Combine(root, "GeopoliticalMonitor", "Data"), dataDirectory);
        Assert.Equal(Path.Combine(root, "GeopoliticalMonitor", "Data", "geopolitical-monitor.db"), databasePath);
        Assert.Equal(Path.Combine(root, "GeopoliticalMonitor", "Data", "diagnostic-adblock-binding.json"), diagnosticBindingPath);
        Assert.Equal(Path.Combine(root, "GeopoliticalMonitor", "Data", "x-adblock-binding.json"), productionBindingPath);
        Assert.Equal(Path.Combine(root, "GeopoliticalMonitor", "Extensions", "AdBlock", "Current"), productionManagedSource);
        Assert.False(productionManagedSource.StartsWith(provider.GetXBrowserUserDataFolder(), StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("Dev", databasePath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("src", databasePath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("frontend", databasePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ComposesIsolatedWebView2ProfilePathsAndExtensionStorageBelowLocalApplicationDataRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var provider = new LocalApplicationDataPathProvider(() => root);

        var appProfilePath = provider.GetAppWebViewUserDataFolder();
        var xProfilePath = provider.GetXBrowserUserDataFolder();
        var diagnosticProfilePath = provider.GetXBrowserExtensionDiagnosticUserDataFolder();

        Assert.Equal(Path.Combine(root, "GeopoliticalMonitor", "WebView2", "AppShell"), appProfilePath);
        Assert.Equal(Path.Combine(root, "GeopoliticalMonitor", "WebView2", "XBrowser"), xProfilePath);
        Assert.Equal(Path.Combine(root, "GeopoliticalMonitor", "WebView2", "ExtensionCapabilityDiagnostic"), diagnosticProfilePath);
        Assert.StartsWith(Path.Combine(root, "GeopoliticalMonitor"), xProfilePath);
        Assert.DoesNotContain("OneDrive", xProfilePath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("src", xProfilePath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("frontend", xProfilePath, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(appProfilePath, xProfilePath);
        Assert.NotEqual(appProfilePath, diagnosticProfilePath);
        Assert.NotEqual(xProfilePath, diagnosticProfilePath);
    }
}
