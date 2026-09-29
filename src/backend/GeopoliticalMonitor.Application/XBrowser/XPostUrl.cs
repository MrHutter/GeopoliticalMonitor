using System.Text.RegularExpressions;

namespace GeopoliticalMonitor.Application.XBrowser;

public static partial class XPostUrl
{
    [GeneratedRegex("^[A-Za-z0-9_]{1,15}$", RegexOptions.CultureInvariant)]
    private static partial Regex AccountRegex();

    [GeneratedRegex("^[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex NumericStatusIdRegex();

    public static bool TryNormalize(string? url, out string normalizedUrl)
    {
        var accepted = TryNormalizeWithStatusId(url, out var result);
        normalizedUrl = accepted ? result.NormalizedUrl : string.Empty;
        return accepted;
    }

    public static bool TryNormalizeWithStatusId(string? url, out XPostUrlInfo result)
    {
        result = XPostUrlInfo.Empty;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(uri.Host, XBrowserDefaults.PrimaryHost, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Host, XBrowserDefaults.WwwHost, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 3)
        {
            return false;
        }

        var account = segments[0];
        var statusLiteral = segments[1];
        var statusId = segments[2];
        if (!AccountRegex().IsMatch(account)
            || !string.Equals(statusLiteral, "status", StringComparison.Ordinal)
            || !NumericStatusIdRegex().IsMatch(statusId))
        {
            return false;
        }

        result = new XPostUrlInfo(
            $"https://{XBrowserDefaults.PrimaryHost}/{account}/status/{statusId}",
            statusId);
        return true;
    }
}

public sealed record XPostUrlInfo(string NormalizedUrl, string StatusId)
{
    public static XPostUrlInfo Empty { get; } = new(string.Empty, string.Empty);
}
