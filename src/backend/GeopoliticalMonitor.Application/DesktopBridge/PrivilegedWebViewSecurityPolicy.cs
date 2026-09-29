namespace GeopoliticalMonitor.Application.DesktopBridge;

public static class PrivilegedWebViewSecurityPolicy
{
    public const string TrustedOrigin = "https://app.geopoliticalmonitor.local";

    private static readonly Uri TrustedOriginUri = new(TrustedOrigin, UriKind.Absolute);

    public static string TrustedHost => TrustedOriginUri.IdnHost;

    public static Uri StartPage { get; } = new(TrustedOriginUri, "/index.html");

    public static bool IsTrustedOrigin(string? uriText)
    {
        if (!Uri.TryCreate(uriText, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Scheme, TrustedOriginUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.IdnHost, TrustedOriginUri.IdnHost, StringComparison.OrdinalIgnoreCase)
            && uri.Port == TrustedOriginUri.Port;
    }

    public static bool TryParseTrustedBridgeRequest(
        string? sourceUri,
        string json,
        out DesktopBridgeRequest? request,
        out string? error)
    {
        if (!IsTrustedOrigin(sourceUri))
        {
            request = null;
            error = "Bridge message source is not trusted.";
            return false;
        }

        return DesktopBridgeProtocol.TryParseRequest(json, out request, out error);
    }
}
