namespace GeopoliticalMonitor.Application.XBrowser;

public enum XBrowserExtensionCapabilityTestTrigger
{
    ApplicationStartup,
    DialogOpened,
    UserRequested,
}

public enum XBrowserExtensionCapabilityTestState
{
    NotTested,
    Testing,
    EnvironmentCreationFailed,
    WinUiInitializationFailed,
    ExtensionApiFailed,
    Confirmed,
}

public sealed record XBrowserExtensionCapabilityTestResult(
    XBrowserExtensionCapabilityTestState State,
    string SdkVersion,
    string RuntimeVersion,
    string Stage,
    string? ExceptionType,
    string? HResult,
    string? ExceptionMessage,
    IReadOnlyList<XBrowserExtensionInfo>? Extensions,
    XDiagnosticAdBlockStatus AdBlockStatus)
{
    private const int MaximumExtensionNameLength = 200;
    private const int MaximumExtensionIdLength = 128;

    public bool IsConfirmed => State == XBrowserExtensionCapabilityTestState.Confirmed;

    public int? ExtensionCount => Extensions?.Count;

    public static XBrowserExtensionCapabilityTestResult NotTested(string sdkVersion, string runtimeVersion) => new(
        XBrowserExtensionCapabilityTestState.NotTested,
        sdkVersion,
        runtimeVersion,
        "Explicit user action required",
        ExceptionType: null,
        HResult: null,
        ExceptionMessage: null,
        Extensions: null,
        XDiagnosticAdBlockStatus.NotTested);

    public static XBrowserExtensionCapabilityTestResult Testing(string sdkVersion, string runtimeVersion) => new(
        XBrowserExtensionCapabilityTestState.Testing,
        sdkVersion,
        runtimeVersion,
        "CoreWebView2Environment.CreateWithOptionsAsync",
        ExceptionType: null,
        HResult: null,
        ExceptionMessage: null,
        Extensions: null,
        XDiagnosticAdBlockStatus.NotTested);

    public static XBrowserExtensionCapabilityTestResult EnvironmentCreationFailed(
        string sdkVersion,
        string runtimeVersion,
        Exception exception,
        string safeMessage) => Failed(
            XBrowserExtensionCapabilityTestState.EnvironmentCreationFailed,
            sdkVersion,
            runtimeVersion,
            "CoreWebView2Environment.CreateWithOptionsAsync",
            exception,
            safeMessage);

    public static XBrowserExtensionCapabilityTestResult WinUiInitializationFailed(
        string sdkVersion,
        string runtimeVersion,
        Exception exception,
        string safeMessage) => Failed(
            XBrowserExtensionCapabilityTestState.WinUiInitializationFailed,
            sdkVersion,
            runtimeVersion,
            "WebView2.EnsureCoreWebView2Async",
            exception,
            safeMessage);

    public static XBrowserExtensionCapabilityTestResult ExtensionApiFailed(
        string sdkVersion,
        string runtimeVersion,
        Exception exception,
        string safeMessage) => Failed(
            XBrowserExtensionCapabilityTestState.ExtensionApiFailed,
            sdkVersion,
            runtimeVersion,
            "Profile.GetBrowserExtensionsAsync",
            exception,
            safeMessage);

    public static XBrowserExtensionCapabilityTestResult Confirmed(
        string sdkVersion,
        string runtimeVersion,
        IEnumerable<XBrowserExtensionInfo> extensions,
        XDiagnosticAdBlockStatus? adBlockStatus = null)
    {
        ArgumentNullException.ThrowIfNull(extensions);

        var boundedExtensions = extensions.Select(CreateBoundedSnapshot).ToArray();
        var boundedAdBlockStatus = adBlockStatus is null
            ? XDiagnosticAdBlockStatus.NoManagedInstance()
            : CreateBoundedAdBlockStatus(adBlockStatus);

        return new(
            XBrowserExtensionCapabilityTestState.Confirmed,
            sdkVersion,
            runtimeVersion,
            "Profile.GetBrowserExtensionsAsync",
            ExceptionType: null,
            HResult: null,
            ExceptionMessage: null,
            boundedExtensions,
            boundedAdBlockStatus);
    }

    private static XBrowserExtensionCapabilityTestResult Failed(
        XBrowserExtensionCapabilityTestState state,
        string sdkVersion,
        string runtimeVersion,
        string stage,
        Exception exception,
        string safeMessage) => new(
            state,
            sdkVersion,
            runtimeVersion,
            stage,
            exception.GetType().FullName,
            $"0x{exception.HResult:X8}",
            safeMessage,
            Extensions: null,
            XDiagnosticAdBlockStatus.NotTested);

    private static XDiagnosticAdBlockStatus CreateBoundedAdBlockStatus(XDiagnosticAdBlockStatus status) =>
        status.Extension is null
            ? status
            : status with { Extension = CreateBoundedSnapshot(status.Extension) };

    private static XBrowserExtensionInfo CreateBoundedSnapshot(XBrowserExtensionInfo extension) => new(
        Bound(extension.Id, MaximumExtensionIdLength, "[ID unavailable]"),
        Bound(extension.Name, MaximumExtensionNameLength, "[name unavailable]"),
        extension.IsEnabled);

    private static string Bound(string? value, int maximumLength, string unavailableValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return unavailableValue;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maximumLength ? trimmed : $"{trimmed[..maximumLength]}...";
    }
}

public sealed record XBrowserExtensionCapabilityTestActions(
    bool CanRunTest,
    bool CanInstall,
    bool CanBind,
    bool CanEnable,
    bool CanDisable,
    bool CanRemove);

public static class XBrowserExtensionCapabilityTestPolicy
{
    public static bool ShouldRun(XBrowserExtensionCapabilityTestTrigger trigger) =>
        trigger == XBrowserExtensionCapabilityTestTrigger.UserRequested;

    public static XBrowserExtensionCapabilityTestActions GetActions(
        XBrowserExtensionCapabilityTestResult result,
        bool hasLiveDiagnosticSession = false) => new(
        CanRunTest: result.State != XBrowserExtensionCapabilityTestState.Testing,
        CanInstall: hasLiveDiagnosticSession && result.IsConfirmed && result.AdBlockStatus.State is
            XDiagnosticAdBlockState.NoManagedInstance or
            XDiagnosticAdBlockState.BindingStale or
            XDiagnosticAdBlockState.InvalidFolder or
            XDiagnosticAdBlockState.InstallationFailed,
        CanBind: hasLiveDiagnosticSession && result.IsConfirmed && result.AdBlockStatus.State is
            XDiagnosticAdBlockState.NoManagedInstance or
            XDiagnosticAdBlockState.InstalledUnmanaged or
            XDiagnosticAdBlockState.BindingStale or
            XDiagnosticAdBlockState.InvalidFolder or
            XDiagnosticAdBlockState.OperationFailed,
        CanEnable: hasLiveDiagnosticSession && result.IsConfirmed && result.AdBlockStatus.State == XDiagnosticAdBlockState.InstalledManagedDisabled,
        CanDisable: hasLiveDiagnosticSession && result.IsConfirmed && result.AdBlockStatus.State == XDiagnosticAdBlockState.InstalledManagedEnabled,
        CanRemove: hasLiveDiagnosticSession && result.IsConfirmed && result.AdBlockStatus.IsManagedInstance);

    public static string Format(XBrowserExtensionCapabilityTestResult result)
    {
        var heading = result.State switch
        {
            XBrowserExtensionCapabilityTestState.NotTested => "Host capability: Not tested",
            XBrowserExtensionCapabilityTestState.Testing => "Host capability: Testing",
            XBrowserExtensionCapabilityTestState.EnvironmentCreationFailed => "Host capability: Environment creation failed",
            XBrowserExtensionCapabilityTestState.WinUiInitializationFailed => "Host capability: WinUI initialization failed",
            XBrowserExtensionCapabilityTestState.ExtensionApiFailed => "Host capability: Extension API failed",
            XBrowserExtensionCapabilityTestState.Confirmed => "Host capability: Confirmed",
            _ => "Host capability: Unknown",
        };

        var details = new List<string>
        {
            heading,
            $"WebView2 SDK: {result.SdkVersion}",
            $"WebView2 Runtime: {result.RuntimeVersion}",
            $"Stage: {result.Stage}",
        };

        if (result.Extensions is not null)
        {
            details.Add($"Diagnostic profile extensions: {result.Extensions.Count}");
            for (var index = 0; index < result.Extensions.Count; index++)
            {
                var extension = result.Extensions[index];
                var state = extension.IsEnabled ? "Enabled" : "Disabled";
                var storeIdentity = XAdBlockExtensionPolicy.HasKnownStoreIdentity(extension) ? "Known" : "Different";
                var managed = result.AdBlockStatus.IsManagedInstance
                    && string.Equals(result.AdBlockStatus.Extension?.Id, extension.Id, StringComparison.Ordinal)
                    ? "Yes"
                    : "No";
                details.Add($"{index + 1}. {extension.Name} - {extension.Id} - {state} - Store identity: {storeIdentity} - Diagnostic managed instance: {managed}");
            }
        }

        details.Add($"Diagnostic AdBlock: {FormatAdBlockState(result.AdBlockStatus.State)}");
        details.Add($"Diagnostic managed instance: {(result.AdBlockStatus.IsManagedInstance ? "Yes" : "No")}");

        if (result.AdBlockStatus.Extension is not null)
        {
            var extension = result.AdBlockStatus.Extension;
            var storeIdentity = XAdBlockExtensionPolicy.HasKnownStoreIdentity(extension) ? "Known" : "Different";
            details.Add($"Installed: Yes - Enabled: {(extension.IsEnabled ? "Yes" : "No")} - Store identity: {storeIdentity}");
            details.Add($"Managed result: {extension.Name} - {extension.Id}");
        }
        else
        {
            details.Add("Installed: Not established");
        }

        if (result.AdBlockStatus.Manifest is not null)
        {
            var manifest = result.AdBlockStatus.Manifest;
            details.Add($"Source manifest: MV{manifest.ManifestVersion} - Version {manifest.Version} - Identity key: {(manifest.HasKey ? "Present" : "Absent")}");
            if (!manifest.HasKey)
            {
                details.Add("Identity note: this unpacked instance uses a source-path-derived ID, not its Chrome Store ID.");
            }
        }

        if (!string.IsNullOrWhiteSpace(result.AdBlockStatus.Stage))
        {
            details.Add($"AdBlock stage: {result.AdBlockStatus.Stage}");
        }

        if (!string.IsNullOrWhiteSpace(result.AdBlockStatus.ExceptionType))
        {
            details.Add($"AdBlock error: {result.AdBlockStatus.ExceptionType} ({result.AdBlockStatus.HResult ?? "HRESULT unavailable"})");
        }

        if (!string.IsNullOrWhiteSpace(result.AdBlockStatus.Message))
        {
            details.Add($"AdBlock message: {result.AdBlockStatus.Message}");
        }

        if (!string.IsNullOrWhiteSpace(result.ExceptionType))
        {
            details.Add($"Error: {result.ExceptionType} ({result.HResult ?? "HRESULT unavailable"})");
        }

        if (!string.IsNullOrWhiteSpace(result.ExceptionMessage))
        {
            details.Add($"Message: {result.ExceptionMessage}");
        }

        details.Add("Normal X startup remains unchanged.");
        return string.Join(Environment.NewLine, details);
    }

    private static string FormatAdBlockState(XDiagnosticAdBlockState state) => state switch
    {
        XDiagnosticAdBlockState.NotTested => "Not tested",
        XDiagnosticAdBlockState.NoManagedInstance => "No managed instance",
        XDiagnosticAdBlockState.InstalledManagedEnabled => "Installed, managed, and enabled",
        XDiagnosticAdBlockState.InstalledManagedDisabled => "Installed, managed, and disabled",
        XDiagnosticAdBlockState.InstalledUnmanaged => "Installed but not managed",
        XDiagnosticAdBlockState.BindingStale => "Saved binding is stale or unverified",
        XDiagnosticAdBlockState.InvalidFolder => "Invalid selected folder",
        XDiagnosticAdBlockState.InstallationFailed => "Installation failed",
        XDiagnosticAdBlockState.OperationFailed => "Operation failed",
        _ => "Unknown",
    };
}
