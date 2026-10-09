using GeopoliticalMonitor.Application.XBrowser;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace GeopoliticalMonitor.Desktop;

// Ad blocker indication for the X pane header (docs/design/x-pane-design.md, section 7). Observation only: it reads the
// snapshots the startup sequence and the Ad Blocker dialog already produced, plus a read-only list of installed
// extensions, and never changes extension, binding or payload state.
public sealed partial class MainWindow
{
    private static readonly TimeSpan XAdBlockLiveCheckTimeout = TimeSpan.FromSeconds(3);

    private XDiagnosticAdBlockState? xAdBlockIndicationSnapshotState;
    private string? xAdBlockIndicationBoundRuntimeId;
    private bool xAdBlockIndicationManagedPayloadExists;
    private XAdBlockLiveCheck xAdBlockIndicationLiveCheck = XAdBlockLiveCheck.Unknown;
    private bool xAdBlockLiveCheckInFlight;
    private CoreWebView2ExtensionProfileAdapter? xAdBlockIndicationProfile;
    private XAdBlockIndication? xAdBlockIndicationShown;

    /// <summary>Records a newer trusted observation (startup or Ad Blocker dialog) and refreshes the line.</summary>
    private void ObserveXAdBlockSnapshot(XDiagnosticAdBlockSnapshot? snapshot, bool managedPayloadExists)
    {
        if (snapshot is null)
        {
            // No new observation (for example the dialog closed before it listed anything): keep the known state.
            UpdateXAdBlockIndication();
            return;
        }

        xAdBlockIndicationSnapshotState = snapshot.AdBlockStatus.State;
        xAdBlockIndicationBoundRuntimeId = XAdBlockIndicationPolicy.GetBoundRuntimeId(snapshot);
        xAdBlockIndicationManagedPayloadExists = managedPayloadExists;
        // The snapshot is newer than any earlier live check.
        xAdBlockIndicationLiveCheck = XAdBlockLiveCheck.Unknown;
        UpdateXAdBlockIndication();
    }

    /// <summary>Read-only live check after a completed X navigation: lists installed extensions, at most one in flight.</summary>
    private async Task RunXAdBlockLiveCheckAsync()
    {
        if (xAdBlockLiveCheckInFlight
            || xProductionExtensionHostState != XProductionExtensionHostState.Available
            || string.IsNullOrEmpty(xAdBlockIndicationBoundRuntimeId)
            || XWebView.CoreWebView2 is null)
        {
            return;
        }

        var boundRuntimeId = xAdBlockIndicationBoundRuntimeId;
        xAdBlockLiveCheckInFlight = true;
        try
        {
            xAdBlockIndicationProfile ??= new CoreWebView2ExtensionProfileAdapter(XWebView.CoreWebView2.Profile, XWebView.DispatcherQueue);
            var listing = xAdBlockIndicationProfile.GetInstalledExtensionsAsync();
            if (await Task.WhenAny(listing, Task.Delay(XAdBlockLiveCheckTimeout)).ConfigureAwait(true) != listing)
            {
                return;
            }

            var result = XAdBlockIndicationPolicy.EvaluateLiveCheck(boundRuntimeId, await listing.ConfigureAwait(true));
            // A newer snapshot may have arrived meanwhile; an unknown result never replaces a known state.
            if (result == XAdBlockLiveCheck.Unknown || !string.Equals(boundRuntimeId, xAdBlockIndicationBoundRuntimeId, StringComparison.Ordinal))
            {
                return;
            }

            xAdBlockIndicationLiveCheck = result;
            UpdateXAdBlockIndication();
        }
        catch (Exception)
        {
            // A failed live check leaves the current indication unchanged.
        }
        finally
        {
            xAdBlockLiveCheckInFlight = false;
        }
    }

    private void UpdateXAdBlockIndication()
    {
        var indication = XAdBlockIndicationPolicy.Evaluate(
            xProductionExtensionHostState,
            xAdBlockIndicationSnapshotState,
            xAdBlockIndicationManagedPayloadExists,
            xAdBlockIndicationLiveCheck);
        if (xAdBlockIndicationShown == indication)
        {
            return;
        }

        xAdBlockIndicationShown = indication;
        var text = XAdBlockIndicationPolicy.GetText(indication);
        XAdBlockIndicationText.Text = text ?? string.Empty;
        ToolTipService.SetToolTip(XAdBlockIndicationText, text);
        AutomationProperties.SetName(XAdBlockIndicationText, text ?? string.Empty);
        ApplyXAdBlockIndicationVisibility();
    }
}
