namespace GeopoliticalMonitor.Application.XBrowser;

/// <summary>The one muted line the X pane header shows about the production ad blocker.</summary>
public enum XAdBlockIndication
{
    None,
    Inactive,
    Off,
    NotSetUp,
}

/// <summary>Result of the read-only live check that compares the installed extensions with the bound instance.</summary>
public enum XAdBlockLiveCheck
{
    /// <summary>No check ran, it failed, it timed out, or there is no bound instance to compare with.</summary>
    Unknown,
    BoundInstanceEnabled,
    BoundInstanceDisabled,
    BoundInstanceMissing,
}

/// <summary>
/// Maps observed ad blocker state to the X pane indication. Observation only: this policy never enables, disables,
/// installs, removes or reconciles anything and never touches the binding or the payload. It can only report whether
/// the trusted instance is installed and enabled; it cannot tell whether an enabled extension is actually filtering.
/// </summary>
public static class XAdBlockIndicationPolicy
{
    public const string InactiveText = "Ad blocker inactive: open Ad Blocker... for details";
    public const string OffText = "Ad blocker off: open Ad Blocker... to turn it on";
    public const string NotSetUpText = "Ad blocker not set up";

    /// <param name="hostState">The production extension host state decided at startup.</param>
    /// <param name="snapshotState">The state of the newest trusted snapshot (startup or Ad Blocker dialog), or null if none exists yet.</param>
    /// <param name="managedPayloadExists">Whether the application-managed payload folder exists (an existence check only).</param>
    /// <param name="liveCheck">The newest live check taken after that snapshot; Unknown when none applies.</param>
    public static XAdBlockIndication Evaluate(
        XProductionExtensionHostState hostState,
        XDiagnosticAdBlockState? snapshotState,
        bool managedPayloadExists,
        XAdBlockLiveCheck liveCheck)
    {
        switch (hostState)
        {
            case XProductionExtensionHostState.XUnavailable:
                // The pane's own error banner covers a failed X pane.
                return XAdBlockIndication.None;
            case XProductionExtensionHostState.FallbackUnavailable:
                return XAdBlockIndication.Inactive;
        }

        if (snapshotState is null or XDiagnosticAdBlockState.NotTested)
        {
            // Nothing observed yet: an unknown state never creates a warning.
            return XAdBlockIndication.None;
        }

        // A live check is newer than the snapshot it was compared with, so it wins; it only applies while the snapshot
        // names a bound managed instance. An unknown or failed check keeps the snapshot's state.
        if (snapshotState is XDiagnosticAdBlockState.InstalledManagedEnabled or XDiagnosticAdBlockState.InstalledManagedDisabled)
        {
            switch (liveCheck)
            {
                case XAdBlockLiveCheck.BoundInstanceEnabled:
                    return XAdBlockIndication.None;
                case XAdBlockLiveCheck.BoundInstanceDisabled:
                    return XAdBlockIndication.Off;
                case XAdBlockLiveCheck.BoundInstanceMissing:
                    return XAdBlockIndication.Inactive;
            }
        }

        return snapshotState switch
        {
            XDiagnosticAdBlockState.InstalledManagedEnabled => XAdBlockIndication.None,
            XDiagnosticAdBlockState.InstalledManagedDisabled => XAdBlockIndication.Off,
            // No binding exists. Without a managed payload either, AdBlock was never set up (or was fully removed through
            // the dialog); with a payload present, enrolment is incomplete. A removed instance whose binding remains is
            // BindingStale, not NoManagedInstance.
            XDiagnosticAdBlockState.NoManagedInstance => managedPayloadExists ? XAdBlockIndication.Inactive : XAdBlockIndication.NotSetUp,
            _ => XAdBlockIndication.Inactive,
        };
    }

    public static string? GetText(XAdBlockIndication indication) => indication switch
    {
        XAdBlockIndication.Inactive => InactiveText,
        XAdBlockIndication.Off => OffText,
        XAdBlockIndication.NotSetUp => NotSetUpText,
        _ => null,
    };

    /// <summary>The runtime id of the bound managed instance named by a trusted snapshot, or null when it names none.</summary>
    public static string? GetBoundRuntimeId(XDiagnosticAdBlockSnapshot? snapshot) =>
        snapshot?.AdBlockStatus is { IsManagedInstance: true, Extension.Id: { Length: > 0 } id } ? id : null;

    /// <summary>Compares an installed-extension list with the bound runtime id; pure and side-effect free.</summary>
    public static XAdBlockLiveCheck EvaluateLiveCheck(string? boundRuntimeId, IReadOnlyList<XBrowserExtensionInfo>? installedExtensions)
    {
        if (string.IsNullOrEmpty(boundRuntimeId) || installedExtensions is null)
        {
            return XAdBlockLiveCheck.Unknown;
        }

        var matches = installedExtensions.Where(item => string.Equals(item.Id, boundRuntimeId, StringComparison.Ordinal)).ToArray();
        return matches.Length switch
        {
            0 => XAdBlockLiveCheck.BoundInstanceMissing,
            1 => matches[0].IsEnabled ? XAdBlockLiveCheck.BoundInstanceEnabled : XAdBlockLiveCheck.BoundInstanceDisabled,
            _ => XAdBlockLiveCheck.Unknown,
        };
    }
}
