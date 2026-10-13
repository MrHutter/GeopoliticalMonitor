using GeopoliticalMonitor.Application.DesktopBridge;

namespace GeopoliticalMonitor.Application.Tests;

/// <summary>The display name changed to "Northern Monitor"; the internal identifiers keep "GeopoliticalMonitor" on purpose.</summary>
public sealed class ProductIdentifierGuardTests
{
    [Fact]
    public void DisplayNameAndUserAgentUseTheDecidedName()
    {
        Assert.Equal("Northern Monitor", ProductIdentity.DisplayName);
        Assert.Equal("NorthernMonitor/" + ProductIdentity.Version, ProductIdentity.UserAgent);
    }

    [Fact]
    public void TrustedOriginKeepsTheInternalIdentifier()
    {
        Assert.Equal("https://app.geopoliticalmonitor.local", PrivilegedWebViewSecurityPolicy.TrustedOrigin);
    }

    [Fact]
    public void DesktopExecutableAndEnvironmentVariableKeepTheInternalIdentifier()
    {
        var desktop = Path.Combine(FindRepositoryRoot(), "src", "backend", "GeopoliticalMonitor.Desktop");
        var project = File.ReadAllText(Path.Combine(desktop, "GeopoliticalMonitor.Desktop.csproj"));

        // Without an override the executable is named after the project file: GeopoliticalMonitor.Desktop.exe.
        Assert.DoesNotContain("<AssemblyName>", project, StringComparison.Ordinal);
        Assert.DoesNotContain("<TargetName>", project, StringComparison.Ordinal);
        Assert.Contains("<RootNamespace>GeopoliticalMonitor.Desktop</RootNamespace>", project, StringComparison.Ordinal);
        Assert.Contains("\"GEOPOLITICALMONITOR_FIRMS_MAP_KEY\"", File.ReadAllText(Path.Combine(desktop, "MainWindow.xaml.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void DesktopFilePropertiesUseTheDisplayName()
    {
        var project = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "backend", "GeopoliticalMonitor.Desktop", "GeopoliticalMonitor.Desktop.csproj"));

        Assert.Equal(ProductIdentity.DisplayName, System.Text.RegularExpressions.Regex.Match(project, "<Product>([^<]*)</Product>").Groups[1].Value);
        Assert.Equal(ProductIdentity.DisplayName, System.Text.RegularExpressions.Regex.Match(project, "<AssemblyTitle>([^<]*)</AssemblyTitle>").Groups[1].Value);
        Assert.Matches("<Description>[^<]+</Description>", project);
        Assert.DoesNotContain("<Company>", project, StringComparison.Ordinal);
        Assert.DoesNotContain("<Copyright>", project, StringComparison.Ordinal);
        Assert.DoesNotContain("\u2122", project, StringComparison.Ordinal);
        Assert.DoesNotContain("\u00AE", project, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "backend", "GeopoliticalMonitor.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the GeopoliticalMonitor repository root from the test output directory.");
    }
}
