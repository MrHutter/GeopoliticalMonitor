using System.Text.Json;

namespace GeopoliticalMonitor.Infrastructure.Tests;

public sealed class DesktopBuildOutputTests
{
    [Fact]
    public void DependencyManifestIncludesMicrosoftWinUiRuntimeAssembly()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var dependencyManifestPath = Path.Combine(
            repositoryRoot,
            "src",
            "backend",
            "GeopoliticalMonitor.Desktop",
            "bin",
            configuration,
            "net10.0-windows10.0.19041.0",
            "win-x64",
            "GeopoliticalMonitor.Desktop.deps.json");

        Assert.True(
            File.Exists(dependencyManifestPath),
            $"The desktop dependency manifest was not found at '{dependencyManifestPath}'. Build the desktop project before running this packaging test.");

        using var document = JsonDocument.Parse(File.ReadAllBytes(dependencyManifestPath));
        var runtimeTargetName = document.RootElement
            .GetProperty("runtimeTarget")
            .GetProperty("name")
            .GetString();
        Assert.False(string.IsNullOrWhiteSpace(runtimeTargetName));

        var runtimeTarget = document.RootElement
            .GetProperty("targets")
            .GetProperty(runtimeTargetName!);
        var includesMicrosoftWinUi = runtimeTarget
            .EnumerateObject()
            .Where(package => package.Value.TryGetProperty("runtime", out _))
            .SelectMany(package => package.Value.GetProperty("runtime").EnumerateObject())
            .Any(asset => asset.Name.EndsWith("/Microsoft.WinUI.dll", StringComparison.OrdinalIgnoreCase));

        Assert.True(
            includesMicrosoftWinUi,
            "The desktop dependency manifest does not register Microsoft.WinUI.dll. The executable will terminate before App construction even if the DLL was copied to the output directory.");
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
