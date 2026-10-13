using System.Globalization;
using System.Text;
using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Infrastructure.Diagnostics;

// TEMPORARY DIAGNOSTIC: records what happens to the X profile's ad blocker instance at app close and at the next
// startup, to find out whether the browser drops it at shutdown or at launch. Remove this class, its call sites in
// MainWindow.AdBlockLifecycle.cs and MainWindow.xaml.cs, and the Diagnostics log once the instance-loss cause is known.
//
// Facts only: event names, state names, counts, sizes, timestamps, versions, exception type and HRESULT. It never
// opens, reads or parses a profile file, and every value passes the existing path- and URI-redacting formatter.
public sealed class AdBlockLifecycleLog
{
    public const long MaximumBytes = 128 * 1024;
    public const int MaximumLineLength = 2048;
    public const string LogFileName = "adblock-lifecycle.log";
    public const string PreviousLogFileName = "adblock-lifecycle.log.old";
    public const string CleanCloseMarkerFileName = "adblock-lifecycle.clean-close";

    /// <summary>
    /// Extension-related entries of the X profile, relative to its WebView2 user-data folder. The layout is Chromium's
    /// default profile inside WebView2's EBWebView folder; an entry that does not exist is logged as such.
    /// </summary>
    public static IReadOnlyList<(string Label, string RelativePath)> ProfileEntries { get; } =
    [
        ("profile", Path.Combine("EBWebView", "Default")),
        ("preferences", Path.Combine("EBWebView", "Default", "Preferences")),
        ("securePreferences", Path.Combine("EBWebView", "Default", "Secure Preferences")),
        ("extensions", Path.Combine("EBWebView", "Default", "Extensions")),
        ("extensionState", Path.Combine("EBWebView", "Default", "Extension State")),
        ("localExtensionSettings", Path.Combine("EBWebView", "Default", "Local Extension Settings")),
        ("extensionRules", Path.Combine("EBWebView", "Default", "Extension Rules")),
        ("extensionScripts", Path.Combine("EBWebView", "Default", "Extension Scripts")),
    ];

    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
    private readonly object gate = new();
    private readonly string directory;
    private readonly Func<DateTimeOffset> clock;

    public AdBlockLifecycleLog(string diagnosticsDirectory, Func<DateTimeOffset>? clock = null)
    {
        directory = diagnosticsDirectory;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public string LogPath => Path.Combine(directory, LogFileName);

    public string PreviousLogPath => Path.Combine(directory, PreviousLogFileName);

    public string CleanCloseMarkerPath => Path.Combine(directory, CleanCloseMarkerFileName);

    /// <summary>Appends one line. Keeps the file under <see cref="MaximumBytes"/> by moving it to the single .old file. Never throws.</summary>
    public void Write(string eventName, params (string Key, string? Value)[] fields)
    {
        try
        {
            lock (gate)
            {
                Directory.CreateDirectory(directory);
                var line = FormatLine(clock(), eventName, fields) + "\n";
                var log = new FileInfo(LogPath);
                if (log.Exists && log.Length + Utf8WithoutBom.GetByteCount(line) > MaximumBytes)
                {
                    File.Move(LogPath, PreviousLogPath, overwrite: true);
                }

                File.AppendAllText(LogPath, line, Utf8WithoutBom);
            }
        }
        catch (Exception)
        {
            // A diagnostic must never affect the app.
        }
    }

    /// <summary>True when the previous session wrote the clean-close marker; the marker is removed for this session. Never throws.</summary>
    public bool ConsumeCleanCloseMarker()
    {
        try
        {
            lock (gate)
            {
                if (!File.Exists(CleanCloseMarkerPath))
                {
                    return false;
                }

                File.Delete(CleanCloseMarkerPath);
                return true;
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Writes the clean-close marker at the end of a normal close. Returns false instead of throwing.</summary>
    public bool WriteCleanCloseMarker()
    {
        try
        {
            lock (gate)
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(CleanCloseMarkerPath, FormatTimestamp(clock().UtcDateTime), Utf8WithoutBom);
                return true;
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static string FormatLine(DateTimeOffset at, string eventName, IEnumerable<(string Key, string? Value)> fields)
    {
        var builder = new StringBuilder(FormatTimestamp(at.UtcDateTime));
        builder.Append(' ').Append(SanitizeToken(eventName));
        foreach (var (key, value) in fields)
        {
            builder.Append(' ').Append(SanitizeToken(key)).Append('=').Append(SanitizeValue(value));
        }

        var line = builder.ToString();
        return line.Length <= MaximumLineLength ? line : string.Concat(line.AsSpan(0, MaximumLineLength - 3), "...");
    }

    /// <summary>Uses the existing redacting formatter: private paths and URIs are replaced and whitespace is collapsed.</summary>
    public static string SanitizeValue(string? value)
    {
        if (value is null)
        {
            return "unknown";
        }

        var redacted = SafeDiagnosticExceptionFormatter.RedactText(value).Replace('"', '\'');
        return redacted.Length == 0 || redacted.Contains(' ', StringComparison.Ordinal) ? $"\"{redacted}\"" : redacted;
    }

    public static string DescribeException(Exception exception) =>
        $"{exception.GetType().FullName} 0x{exception.HResult:X8}";

    public static string FormatTimestamp(DateTime utc) =>
        utc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    public static string FormatBool(bool? value) => value switch
    {
        true => "true",
        false => "false",
        _ => "unknown",
    };

    /// <summary>Metadata of the X profile's extension-related entries. Never opens a file.</summary>
    public static (string Key, string? Value)[] DescribeProfile(string userDataFolder) =>
        ProfileEntries.SelectMany(entry => DescribeEntry(entry.Label, Path.Combine(userDataFolder, entry.RelativePath))).ToArray();

    /// <summary>Existence, then size (file) or top-level entry count (directory), and last-write time UTC. Never opens a file.</summary>
    public static (string Key, string? Value)[] DescribeEntry(string label, string fullPath)
    {
        try
        {
            var file = new FileInfo(fullPath);
            if (file.Exists)
            {
                return
                [
                    ($"{label}.exists", "file"),
                    ($"{label}.size", file.Length.ToString(CultureInfo.InvariantCulture)),
                    ($"{label}.lastWriteUtc", FormatTimestamp(file.LastWriteTimeUtc)),
                ];
            }

            var folder = new DirectoryInfo(fullPath);
            if (folder.Exists)
            {
                return
                [
                    ($"{label}.exists", "directory"),
                    ($"{label}.entries", folder.EnumerateFileSystemInfos().Count().ToString(CultureInfo.InvariantCulture)),
                    ($"{label}.lastWriteUtc", FormatTimestamp(folder.LastWriteTimeUtc)),
                ];
            }

            return [($"{label}.exists", "no")];
        }
        catch (Exception exception)
        {
            return [($"{label}.error", DescribeException(exception))];
        }
    }

    /// <summary>
    /// Whether the managed payload folder and the browser-written _metadata folder inside it exist, and how many files
    /// _metadata holds. Lists only _metadata; it never walks or hashes the payload.
    /// </summary>
    public static (string Key, string? Value)[] DescribeManagedPayload(string managedSourcePath)
    {
        try
        {
            var metadata = new DirectoryInfo(Path.Combine(managedSourcePath, "_metadata"));
            return
            [
                ("currentExists", FormatBool(Directory.Exists(managedSourcePath))),
                ("currentMetadataExists", FormatBool(metadata.Exists)),
                ("currentMetadataFiles", metadata.Exists
                    ? metadata.EnumerateFiles("*", SearchOption.AllDirectories).Count().ToString(CultureInfo.InvariantCulture)
                    : "0"),
            ];
        }
        catch (Exception exception)
        {
            return [("currentError", DescribeException(exception))];
        }
    }

    private static string SanitizeToken(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_' ? character : '_');
        }

        return builder.Length == 0 ? "_" : builder.ToString();
    }
}
