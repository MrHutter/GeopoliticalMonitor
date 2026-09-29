namespace GeopoliticalMonitor.Application.XBrowser;

public enum XBrowserExtensionFailureKind
{
    None,
    NotInitialized,
    NotSupported,
    InvalidState,
    ApiFailure,
}

public sealed record XBrowserExtensionCapability(bool IsAvailable, XBrowserExtensionCapabilityDiagnostic Diagnostic)
{
    public static XBrowserExtensionCapability Available(string sdkVersion, string runtimeVersion) => new(
        true,
        XBrowserExtensionCapabilityDiagnostic.Available(sdkVersion, runtimeVersion));

    public static XBrowserExtensionCapability Unavailable(XBrowserExtensionCapabilityDiagnostic diagnostic) => new(false, diagnostic);
}

public sealed record XBrowserExtensionCapabilityDiagnostic(
    string SdkVersion,
    string RuntimeVersion,
    string Stage,
    XBrowserExtensionFailureKind FailureKind,
    string? ExceptionType,
    string? HResult,
    string ExtensionApi)
{
    private const int HResultErrorNotSupported = unchecked((int)0x80070032);
    private const int HResultErrorInvalidState = unchecked((int)0x8007139F);

    public static XBrowserExtensionCapabilityDiagnostic Available(string sdkVersion, string runtimeVersion) => new(
        sdkVersion,
        runtimeVersion,
        "Profile.GetBrowserExtensionsAsync",
        XBrowserExtensionFailureKind.None,
        ExceptionType: null,
        HResult: null,
        ExtensionApi: "available");

    public static XBrowserExtensionCapabilityDiagnostic NotInitialized(string sdkVersion, string runtimeVersion) => new(
        sdkVersion,
        runtimeVersion,
        "X WebView2 initialization",
        XBrowserExtensionFailureKind.NotInitialized,
        ExceptionType: null,
        HResult: null,
        ExtensionApi: "not initialized");

    public static XBrowserExtensionCapabilityDiagnostic UnsupportedHost(string sdkVersion, string runtimeVersion) => new(
        sdkVersion,
        runtimeVersion,
        "WinUI 3 WebView2 host policy",
        XBrowserExtensionFailureKind.NotSupported,
        ExceptionType: null,
        HResult: null,
        ExtensionApi: "unavailable");

    public static XBrowserExtensionCapabilityDiagnostic FromException(string sdkVersion, string runtimeVersion, string stage, Exception exception) => new(
        sdkVersion,
        runtimeVersion,
        stage,
        Classify(exception),
        exception.GetType().FullName,
        FormatHResult(exception.HResult),
        "unavailable");

    public string ToUnavailableMessage()
    {
        var error = string.IsNullOrWhiteSpace(ExceptionType)
            ? FailureKind.ToString()
            : $"{ExceptionType} ({HResult ?? "HRESULT unavailable"})";

        return string.Join(Environment.NewLine,
            "X browser extension support is unavailable in the current WinUI 3 WebView2 host. X browsing remains available.",
            $"WebView2 SDK: {SdkVersion}",
            $"WebView2 Runtime: {RuntimeVersion}",
            $"Extension API: {ExtensionApi}",
            $"Stage: {Stage}",
            $"Error: {error}");
    }

    private static XBrowserExtensionFailureKind Classify(Exception exception)
    {
        return exception.HResult switch
        {
            HResultErrorNotSupported => XBrowserExtensionFailureKind.NotSupported,
            HResultErrorInvalidState => XBrowserExtensionFailureKind.InvalidState,
            _ => XBrowserExtensionFailureKind.ApiFailure,
        };
    }

    private static string FormatHResult(int hresult)
    {
        return $"0x{hresult:X8}";
    }
}
