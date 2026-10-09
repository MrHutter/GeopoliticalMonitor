namespace GeopoliticalMonitor.Application.XBrowser;

public sealed record BlueskyAdBlockActions(
    bool CanRefresh,
    bool CanInstall,
    bool CanUseVerifiedXCopy,
    bool CanBind,
    bool CanEnable,
    bool CanDisable,
    bool CanRemove);

/// <summary>
/// Rules for the Bluesky feed's separate verified AdBlock copy (docs/design/social-pane-adblock-design.md, option (c)).
/// The ad blocker is opt-in for Bluesky: nothing is shown until the owner sets it up, there is no diagnostic route, and
/// every decision reuses the pinned trust record and the same pure X policies.
/// </summary>
public static class BlueskyAdBlockPolicy
{
    public const string DialogTitle = "Bluesky Ad Blocker";

    public const string NotSetUpMessage =
        "AdBlock is not set up for Bluesky. The Bluesky pane stays extension-disabled until you install the reviewed payload here with Install AdBlock... or Use verified X copy. Then restart the app and finish with Install or Reconcile.";

    public const string Explanation =
        "Install accepts only the reviewed Chrome Web Store AdBlock 6.47.0 payload. The app authenticates the complete runtime tree, stages and re-verifies its own Bluesky copy in private local storage, and manages only that trusted Bluesky-profile instance. Use verified X copy authenticates the X copy again and copies it; the X copy, its binding and the X ad blocker are not changed.";

    /// <summary>Set up means the owner has started using the Bluesky ad blocker: a binding (or an interrupted activation) or a managed payload exists.</summary>
    public static bool IsSetUp(bool persistedBindingExists, bool managedPayloadExists) =>
        persistedBindingExists || managedPayloadExists;

    /// <summary>The Bluesky status line: the X mapping, except that a never set up Bluesky ad blocker shows nothing.</summary>
    public static XAdBlockIndication GetIndication(
        bool isSetUp,
        XProductionExtensionHostState hostState,
        XDiagnosticAdBlockState? snapshotState,
        bool managedPayloadExists,
        XAdBlockLiveCheck liveCheck)
    {
        if (!isSetUp)
        {
            return XAdBlockIndication.None;
        }

        var indication = XAdBlockIndicationPolicy.Evaluate(hostState, snapshotState, managedPayloadExists, liveCheck);
        return indication == XAdBlockIndication.NotSetUp ? XAdBlockIndication.None : indication;
    }

    /// <param name="enrollmentAllowed">
    /// True when the Bluesky environment started extension-disabled by its own gate (never set up, or untrusted saved
    /// state): the payload may then be enrolled and takes effect after a restart, exactly like X's re-enrollment path.
    /// </param>
    /// <param name="verifiedXCopyExists">Whether X's managed copy folder exists (existence only; it is authenticated again before use).</param>
    public static BlueskyAdBlockActions GetActions(
        XProductionExtensionHostState hostState,
        XDiagnosticAdBlockSnapshot? snapshot,
        bool hasManagedSource,
        bool isBusy,
        bool enrollmentAllowed,
        bool verifiedXCopyExists)
    {
        var actions = XProductionAdBlockPolicy.GetActions(hostState, snapshot, hasManagedSource, isBusy);
        var canInstall = actions.CanInstall || (enrollmentAllowed && !isBusy && hostState != XProductionExtensionHostState.XUnavailable);
        return new(
            actions.CanRefresh,
            canInstall,
            CanUseVerifiedXCopy: canInstall && verifiedXCopyExists,
            actions.CanBind,
            actions.CanEnable,
            actions.CanDisable,
            actions.CanRemove);
    }
}
