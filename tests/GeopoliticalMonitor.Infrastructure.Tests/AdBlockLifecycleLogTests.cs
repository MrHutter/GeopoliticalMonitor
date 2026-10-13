using System.Text;
using GeopoliticalMonitor.Infrastructure.Diagnostics;
using GeopoliticalMonitor.Infrastructure.Persistence;

namespace GeopoliticalMonitor.Infrastructure.Tests;

public sealed class AdBlockLifecycleLogTests : IDisposable
{
    private static readonly DateTimeOffset FixedTime = new(2026, 10, 13, 8, 30, 15, 123, TimeSpan.Zero);
    private readonly string root = Path.Combine(Path.GetTempPath(), $"gpm-lifecycle-{Guid.NewGuid():N}");

    [Fact]
    public void DiagnosticsFolderComesFromThePathProvider()
    {
        var provider = new LocalApplicationDataPathProvider(() => root);

        Assert.Equal(Path.Combine(root, "GeopoliticalMonitor", "Diagnostics"), provider.GetDiagnosticsDirectory());
    }

    [Fact]
    public void WritesOneLinePerEventWithTheUtcTimestampFirst()
    {
        var log = new AdBlockLifecycleLog(root, () => FixedTime);

        log.Write("session-start", ("runtimeAvailable", "154.0.4258.62"), ("previousSessionCleanClose", "false"));
        log.Write("close-end", ("cleanCloseMarker", "written"));

        var lines = File.ReadAllLines(log.LogPath);
        Assert.Equal(
            [
                "2026-10-13T08:30:15.123Z session-start runtimeAvailable=154.0.4258.62 previousSessionCleanClose=false",
                "2026-10-13T08:30:15.123Z close-end cleanCloseMarker=written",
            ],
            lines);
    }

    [Fact]
    public void LogStaysBoundedAndKeepsOnePreviousFile()
    {
        var log = new AdBlockLifecycleLog(root, () => FixedTime);
        var filler = new string('a', 1000);

        for (var index = 0; index < 600; index++)
        {
            log.Write("filler", ("value", filler));
            Assert.True(new FileInfo(log.LogPath).Length <= AdBlockLifecycleLog.MaximumBytes);
        }

        Assert.True(File.Exists(log.PreviousLogPath));
        Assert.True(new FileInfo(log.PreviousLogPath).Length <= AdBlockLifecycleLog.MaximumBytes);
        Assert.Equal(
            [AdBlockLifecycleLog.LogFileName, AdBlockLifecycleLog.PreviousLogFileName],
            Directory.GetFiles(root).Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void LongLinesAreTruncated()
    {
        var line = AdBlockLifecycleLog.FormatLine(FixedTime, "event", Enumerable.Range(0, 200).Select(index => ($"key{index}", (string?)"value value")));

        Assert.Equal(AdBlockLifecycleLog.MaximumLineLength, line.Length);
        Assert.EndsWith("...", line, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteFailuresAreSwallowed()
    {
        Directory.CreateDirectory(root);
        var blockedDirectory = Path.Combine(root, "not-a-directory");
        File.WriteAllText(blockedDirectory, "file in the way");
        var log = new AdBlockLifecycleLog(blockedDirectory, () => FixedTime);

        var exception = Record.Exception(() =>
        {
            log.Write("session-start", ("appVersion", "0.1.0"));
            Assert.False(log.WriteCleanCloseMarker());
            Assert.False(log.ConsumeCleanCloseMarker());
        });

        Assert.Null(exception);
        Assert.Equal("file in the way", File.ReadAllText(blockedDirectory));
    }

    [Fact]
    public void MissingCleanCloseMarkerMeansTheLastSessionDidNotEndCleanly()
    {
        var log = new AdBlockLifecycleLog(root, () => FixedTime);

        Assert.False(log.ConsumeCleanCloseMarker());
        Assert.True(log.WriteCleanCloseMarker());
        Assert.True(log.ConsumeCleanCloseMarker());
        Assert.False(log.ConsumeCleanCloseMarker());
        Assert.False(File.Exists(log.CleanCloseMarkerPath));
    }

    [Fact]
    public void ValuesNeverCarryPathsUserNamesOrUrls()
    {
        var log = new AdBlockLifecycleLog(root, () => FixedTime);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        log.Write(
            "startup-extension-state",
            ("runtimeInUse", @"C:\Users\jane.doe\AppData\Local\GeopoliticalMonitor\WebView2\XBrowser\EBWebView"),
            ("profile", Path.Combine(localAppData, "GeopoliticalMonitor", "WebView2", "XBrowser")),
            ("page", "https://x.com/jane_doe/status/1234567890?token=secret"),
            ("exception", AdBlockLifecycleLog.DescribeException(new IOException(@"Access to C:\Users\jane.doe\secret.txt was denied"))));

        var text = File.ReadAllText(log.LogPath);
        Assert.DoesNotContain("jane", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"Users\", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(localAppData, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exception=\"System.IO.IOException 0x", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProfileMetadataReportsFactsWithoutContentsOrPaths()
    {
        var userData = Path.Combine(root, "XBrowser");
        var profile = Path.Combine(userData, "EBWebView", "Default");
        Directory.CreateDirectory(Path.Combine(profile, "Extensions", "one"));
        Directory.CreateDirectory(Path.Combine(profile, "Extensions", "two"));
        File.WriteAllText(Path.Combine(profile, "Preferences"), "SECRET-PREFERENCES-CONTENT", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var log = new AdBlockLifecycleLog(Path.Combine(root, "Diagnostics"), () => FixedTime);

        log.Write("profile-metadata", [("phase", "startup"), .. AdBlockLifecycleLog.DescribeProfile(userData)]);

        var line = Assert.Single(File.ReadAllLines(log.LogPath));
        Assert.Contains("preferences.exists=file preferences.size=26 preferences.lastWriteUtc=", line, StringComparison.Ordinal);
        Assert.Contains("extensions.exists=directory extensions.entries=2 ", line, StringComparison.Ordinal);
        Assert.Contains("securePreferences.exists=no", line, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET", line, StringComparison.Ordinal);
        Assert.DoesNotContain(root, line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EBWebView", line, StringComparison.Ordinal);
    }

    [Fact]
    public void ManagedPayloadCheckCountsOnlyTheBrowserMetadataFolder()
    {
        var current = Path.Combine(root, "Extensions", "AdBlock", "Current");
        Directory.CreateDirectory(Path.Combine(current, "_metadata", "generated_indexed_rulesets"));
        File.WriteAllText(Path.Combine(current, "manifest.json"), "{}");
        File.WriteAllText(Path.Combine(current, "worker.js"), "payload");
        File.WriteAllText(Path.Combine(current, "_metadata", "generated_indexed_rulesets", "_ruleset1"), "x");
        File.WriteAllText(Path.Combine(current, "_metadata", "generated_indexed_rulesets", "_ruleset2"), "x");

        var fields = AdBlockLifecycleLog.DescribeManagedPayload(current).ToDictionary(field => field.Key, field => field.Value);

        Assert.Equal("true", fields["currentExists"]);
        Assert.Equal("true", fields["currentMetadataExists"]);
        Assert.Equal("2", fields["currentMetadataFiles"]);
        Assert.Equal("false", AdBlockLifecycleLog.DescribeManagedPayload(Path.Combine(root, "missing")).ToDictionary(f => f.Key, f => f.Value)["currentExists"]);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
