using GeopoliticalMonitor.Application.Preferences;
using GeopoliticalMonitor.Application.XBrowser;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace GeopoliticalMonitor.Desktop;

// Ad blocker indication for the Bluesky feed (docs/design/social-pane-adblock-design.md, slice 5). Observation only, like
// the X line: it reads the snapshots the Bluesky gate and the Bluesky dialog already produced, plus a read-only list of
// installed Bluesky extensions, and never changes extension, binding or payload state. It shows nothing until the owner
// sets up the Bluesky ad blocker.
public sealed partial class MainWindow
{
    private XDiagnosticAdBlockState? blueskyAdBlockIndicationSnapshotState;
    private string? blueskyAdBlockIndicationBoundRuntimeId;
    private bool blueskyAdBlockIndicationManagedPayloadExists;
    private XAdBlockLiveCheck blueskyAdBlockIndicationLiveCheck = XAdBlockLiveCheck.Unknown;
    private bool blueskyAdBlockLiveCheckInFlight;
    private CoreWebView2ExtensionProfileAdapter? blueskyAdBlockIndicationProfile;

    /// <summary>Records a newer trusted Bluesky observation (control creation or Bluesky dialog) and refreshes the line.</summary>
    private void ObserveBlueskyAdBlockSnapshot(XDiagnosticAdBlockSnapshot? snapshot, bool managedPayloadExists)
    {
        blueskyAdBlockIndicationManagedPayloadExists = managedPayloadExists;
        if (snapshot is not null)
        {
            blueskyAdBlockIndicationSnapshotState = snapshot.AdBlockStatus.State;
            blueskyAdBlockIndicationBoundRuntimeId = XAdBlockIndicationPolicy.GetBoundRuntimeId(snapshot);
            blueskyAdBlockIndicationLiveCheck = XAdBlockLiveCheck.Unknown;
        }

        UpdateBlueskyAdBlockIndication();
    }

    /// <summary>Forgets the observations of a closed Bluesky control; a new control starts from its own gate.</summary>
    private void ForgetBlueskyAdBlockIndicationObservations()
    {
        blueskyAdBlockIndicationSnapshotState = null;
        blueskyAdBlockIndicationBoundRuntimeId = null;
        blueskyAdBlockIndicationLiveCheck = XAdBlockLiveCheck.Unknown;
        blueskyAdBlockIndicationProfile = null;
        UpdateBlueskyAdBlockIndication();
    }

    /// <summary>Read-only live check after a completed Bluesky navigation: lists installed extensions, at most one in flight.</summary>
    private async Task RunBlueskyAdBlockLiveCheckAsync()
    {
        if (blueskyAdBlockLiveCheckInFlight
            || blueskyExtensionHostState != XProductionExtensionHostState.Available
            || string.IsNullOrEmpty(blueskyAdBlockIndicationBoundRuntimeId)
            || blueskyWebView?.CoreWebView2 is not { } core)
        {
            return;
        }

        var boundRuntimeId = blueskyAdBlockIndicationBoundRuntimeId;
        blueskyAdBlockLiveCheckInFlight = true;
        try
        {
            blueskyAdBlockIndicationProfile ??= new CoreWebView2ExtensionProfileAdapter(core.Profile, blueskyWebView.DispatcherQueue);
            var listing = blueskyAdBlockIndicationProfile.GetInstalledExtensionsAsync();
            if (await Task.WhenAny(listing, Task.Delay(XAdBlockLiveCheckTimeout)).ConfigureAwait(true) != listing)
            {
                return;
            }

            var result = XAdBlockIndicationPolicy.EvaluateLiveCheck(boundRuntimeId, await listing.ConfigureAwait(true));
            if (result == XAdBlockLiveCheck.Unknown || !string.Equals(boundRuntimeId, blueskyAdBlockIndicationBoundRuntimeId, StringComparison.Ordinal))
            {
                return;
            }

            blueskyAdBlockIndicationLiveCheck = result;
            UpdateBlueskyAdBlockIndication();
        }
        catch (Exception)
        {
            // A failed live check leaves the current indication unchanged.
        }
        finally
        {
            blueskyAdBlockLiveCheckInFlight = false;
        }
    }

    private void UpdateBlueskyAdBlockIndication()
    {
        var indication = BlueskyAdBlockPolicy.GetIndication(
            blueskyAdBlockSetUp,
            blueskyExtensionHostState,
            blueskyAdBlockIndicationSnapshotState,
            blueskyAdBlockIndicationManagedPayloadExists,
            blueskyAdBlockIndicationLiveCheck);
        var text = XAdBlockIndicationPolicy.GetText(indication);
        BlueskyAdBlockIndicationText.Text = text ?? string.Empty;
        ToolTipService.SetToolTip(BlueskyAdBlockIndicationText, text);
        AutomationProperties.SetName(BlueskyAdBlockIndicationText, text ?? string.Empty);
        ApplyBlueskyAdBlockIndicationVisibility();
    }

    private void ApplyBlueskyAdBlockIndicationVisibility()
    {
        BlueskyAdBlockIndicationText.Visibility = ToVisibility(
            !string.IsNullOrEmpty(BlueskyAdBlockIndicationText.Text)
            && SocialPaneCommandPolicy.ShowsAdBlockIndication(activeSocialFeed, SocialPaneFeed.Bluesky));
    }
}
