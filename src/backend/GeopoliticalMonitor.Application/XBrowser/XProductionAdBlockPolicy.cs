namespace GeopoliticalMonitor.Application.XBrowser;

public enum XProductionExtensionHostState
{
    Available,
    FallbackUnavailable,
    XUnavailable,
}

public sealed record XProductionAdBlockActions(
    bool CanRefresh,
    bool CanInstall,
    bool CanBind,
    bool CanEnable,
    bool CanDisable,
    bool CanRemove,
    bool CanRunDiagnostic);

public static class XProductionAdBlockPolicy
{
    public const string RestoreActionText = "Restore trusted instance";

    public const string RestoreHint =
        "The trusted AdBlock instance is missing from the X profile, but its binding and verified managed copy are intact. Restore trusted instance adds it again from that copy after authenticating it; no folder is chosen.";

    public const string RestoreNotPerformedMessage =
        "Restore was not performed and nothing was changed: it needs a valid X binding, a verified managed copy and a missing bound instance.";

    /// <summary>The lost-but-restorable state: a valid X binding and verified managed payload whose bound instance is gone.</summary>
    public static bool IsRestorableState(XDiagnosticAdBlockSnapshot? snapshot) =>
        snapshot?.AdBlockStatus is { State: XDiagnosticAdBlockState.BindingStale, BoundInstanceMissing: true };

    /// <summary>Restore is offered only in the restorable state and only where the existing rules already allow an install.</summary>
    public static bool CanRestoreTrustedInstance(
        XProductionExtensionHostState hostState,
        XDiagnosticAdBlockSnapshot? snapshot,
        bool hasManagedSource,
        bool isBusy) =>
        !isBusy
        && hostState == XProductionExtensionHostState.Available
        && hasManagedSource
        && IsRestorableState(snapshot)
        && GetActions(hostState, snapshot, hasManagedSource, isBusy).CanInstall;

    public static XProductionAdBlockActions GetActions(
        XProductionExtensionHostState hostState,
        XDiagnosticAdBlockSnapshot? snapshot,
        bool hasManagedSource,
        bool isBusy)
    {
        if (isBusy || hostState != XProductionExtensionHostState.Available)
        {
            return new(false, false, false, false, false, false, !isBusy);
        }

        var state = snapshot?.AdBlockStatus.State ?? XDiagnosticAdBlockState.NotTested;
        var hasReviewedTrustRecord = OfficialAdBlockTrustRecords.All.Count > 0;
        var hasTrustedPayload = OfficialAdBlockTrustRecords.IsApproved(snapshot?.AdBlockStatus.TrustRecord);
        return new(
            CanRefresh: true,
            CanInstall: hasReviewedTrustRecord && (state is XDiagnosticAdBlockState.NoManagedInstance
                or XDiagnosticAdBlockState.BindingStale
                or XDiagnosticAdBlockState.InvalidFolder
                or XDiagnosticAdBlockState.InstallationFailed),
            CanBind: hasManagedSource && hasTrustedPayload && state is not XDiagnosticAdBlockState.InstalledManagedEnabled
                and not XDiagnosticAdBlockState.InstalledManagedDisabled,
            CanEnable: hasTrustedPayload && state == XDiagnosticAdBlockState.InstalledManagedDisabled,
            CanDisable: hasTrustedPayload && state == XDiagnosticAdBlockState.InstalledManagedEnabled,
            CanRemove: hasTrustedPayload && (state is XDiagnosticAdBlockState.InstalledManagedEnabled
                or XDiagnosticAdBlockState.InstalledManagedDisabled),
            CanRunDiagnostic: true);
    }

    public static string Format(
        XProductionExtensionHostState hostState,
        XDiagnosticAdBlockSnapshot? snapshot,
        bool hasManagedSource,
        string? startupFailure = null,
        string? operationMessage = null)
    {
        var status = snapshot?.AdBlockStatus;
        var extension = status?.Extension;
        var host = hostState == XProductionExtensionHostState.Available ? "Available" : "Unavailable";
        var installed = snapshot is null ? "Not established" : extension is null ? "No" : "Yes";
        var managed = status?.IsManagedInstance == true ? "Yes" : "No";
        var enabled = extension is null ? "Not established" : extension.IsEnabled ? "Yes" : "No";
        var version = status?.Manifest?.Version ?? "Not established";
        var runtimeId = extension?.Id ?? "Not established";
        var sourceManaged = hasManagedSource ? "Yes" : "No";
        var trusted = OfficialAdBlockTrustRecords.IsApproved(status?.TrustRecord) ? "Yes" : "No";
        var detail = operationMessage ?? status?.Message ?? startupFailure;

        return $"Extension host: {host}\nInstalled: {installed}\nManaged: {managed}\nEnabled: {enabled}\nVersion: {version}\nRuntime ID: {runtimeId}\nSource managed by application: {sourceManaged}\nPayload trusted: {trusted}"
            + (string.IsNullOrWhiteSpace(detail) ? string.Empty : $"\n\n{detail}");
    }
}
