using System.Text.RegularExpressions;

namespace GeopoliticalMonitor.Application.XBrowser;

public static partial class SafeDiagnosticExceptionFormatter
{
    private const int MaximumMessageLength = 300;

    public static string FormatForDebug(string operation, Exception exception, params string[] privateValues) =>
        $"{SanitizeOperation(operation)}: {exception.GetType().FullName} 0x{exception.HResult:X8} {GetSafeMessage(exception, privateValues)}";

    public static string GetSafeMessage(Exception exception, params string[] privateValues)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var message = string.IsNullOrWhiteSpace(exception.Message)
            ? "No additional error message."
            : exception.Message;

        foreach (var privateValue in privateValues.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            message = message.Replace(privateValue, "[private value]", StringComparison.OrdinalIgnoreCase);
        }

        foreach (var environmentPath in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                     Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                 }.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            message = message.Replace(environmentPath, "[private path]", StringComparison.OrdinalIgnoreCase);
        }

        message = SensitiveUriRegex().Replace(message, "[private URI]");
        message = WindowsPathRegex().Replace(message, "[private path]");
        message = WhitespaceRegex().Replace(message, " ").Trim();
        return message.Length <= MaximumMessageLength
            ? message
            : string.Concat(message.AsSpan(0, MaximumMessageLength - 3), "...");
    }

    private static string SanitizeOperation(string operation)
    {
        var value = WhitespaceRegex().Replace(operation ?? string.Empty, " ").Trim();
        return value.Length == 0 ? "Operation" : value[..Math.Min(value.Length, 100)];
    }

    [GeneratedRegex(@"(?i)\b(?:https?|file)://\S+")]
    private static partial Regex SensitiveUriRegex();

    [GeneratedRegex("(?i)(?:[a-z]:\\\\|\\\\\\\\)[^\\r\\n\\t\\\"']+")]
    private static partial Regex WindowsPathRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
