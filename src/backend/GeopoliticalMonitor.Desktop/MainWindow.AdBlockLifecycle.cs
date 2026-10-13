using System.Globalization;
using GeopoliticalMonitor.Application;
using GeopoliticalMonitor.Application.XBrowser;
using GeopoliticalMonitor.Infrastructure.Diagnostics;
using GeopoliticalMonitor.Infrastructure.Persistence;
using Microsoft.UI.Windowing;

namespace GeopoliticalMonitor.Desktop;

// TEMPORARY DIAGNOSTIC (see AdBlockLifecycleLog): records the X profile's ad blocker lifecycle at session start, after
// X startup, after the X Ad Blocker dialog and at close. Remove this file, its call sites in MainWindow.xaml.cs
// (LogAdBlockSessionStart, LogXAdBlockStartupState, LogXAdBlockStartupFailure, LogXAdBlockDialogClosed,
// LogAdBlockCloseBegin, LogAdBlockCloseEnd) and the Diagnostics log once the instance-loss cause is known.
// X profile only; nothing here changes extension, binding or payload state, and no value is a path or URL.
public sealed partial class MainWindow
{
    private AdBlockLifecycleLog? adBlockLifecycleLog;
    private bool adBlockCloseRequestedByUser;
    private int? adBlockLastObservedExtensionCount;
    private bool? adBlockLastObservedBoundInstancePresent;
    private string adBlockLastObservedSource = "none";

    private void LogAdBlockSessionStart()
    {
        try
        {
            adBlockLifecycleLog = new AdBlockLifecycleLog(new LocalApplicationDataPathProvider().GetDiagnosticsDirectory());
            AppWindow.Closing += OnAppWindowClosingForAdBlockLog;
            var previousSessionCleanClose = adBlockLifecycleLog.ConsumeCleanCloseMarker();
            adBlockLifecycleLog.Write(
                "session-start",
                ("runtimeAvailable", GetAvailableWebView2RuntimeVersion()),
                ("appVersion", ProductIdentity.Version),
                ("previousSessionCleanClose", AdBlockLifecycleLog.FormatBool(previousSessionCleanClose)));
            WriteXProfileMetadata("startup");
        }
        catch (Exception)
        {
            // A diagnostic must never affect the app.
        }
    }

    private void LogXAdBlockStartupState(
        bool extensionEnvironmentAttempted,
        bool bindingFilePresent,
        bool bindingValid,
        string managedSourcePath,
        XDiagnosticAdBlockSnapshot? startupSnapshot)
    {
        try
        {
            RememberAdBlockObservation(startupSnapshot, "startup");
            adBlockLifecycleLog?.Write(
                "startup-extension-state",
                [
                    ("result", "ready"),
                    ("runtimeInUse", XWebView.CoreWebView2?.Environment.BrowserVersionString),
                    ("extensionEnvironmentAttempted", AdBlockLifecycleLog.FormatBool(extensionEnvironmentAttempted)),
                    ("hostState", xProductionExtensionHostState.ToString()),
                    ("snapshotState", startupSnapshot?.AdBlockStatus.State.ToString()),
                    ("extensionCount", startupSnapshot?.Extensions.Count.ToString(CultureInfo.InvariantCulture)),
                    ("boundInstancePresent", AdBlockLifecycleLog.FormatBool(adBlockLastObservedBoundInstancePresent)),
                    ("restorable", AdBlockLifecycleLog.FormatBool(XProductionAdBlockPolicy.IsRestorableState(startupSnapshot))),
                    ("bindingFilePresent", AdBlockLifecycleLog.FormatBool(bindingFilePresent)),
                    ("bindingValid", AdBlockLifecycleLog.FormatBool(bindingValid)),
                    .. AdBlockLifecycleLog.DescribeManagedPayload(managedSourcePath),
                ]);
        }
        catch (Exception)
        {
            // A diagnostic must never affect the app.
        }
    }

    private void LogXAdBlockStartupFailure(Exception exception)
    {
        adBlockLifecycleLog?.Write(
            "startup-extension-state",
            ("result", "failed"),
            ("exception", AdBlockLifecycleLog.DescribeException(exception)));
    }

    private void LogXAdBlockDialogClosed(XDiagnosticAdBlockSnapshot? snapshot)
    {
        try
        {
            if (snapshot is null)
            {
                adBlockLifecycleLog?.Write("dialog-closed", ("snapshotState", null));
                return;
            }

            RememberAdBlockObservation(snapshot, "dialog");
            adBlockLifecycleLog?.Write(
                "dialog-closed",
                ("snapshotState", snapshot.AdBlockStatus.State.ToString()),
                ("extensionCount", snapshot.Extensions.Count.ToString(CultureInfo.InvariantCulture)),
                ("boundInstancePresent", AdBlockLifecycleLog.FormatBool(snapshot.AdBlockStatus.IsManagedInstance)),
                ("restorable", AdBlockLifecycleLog.FormatBool(XProductionAdBlockPolicy.IsRestorableState(snapshot))));
        }
        catch (Exception)
        {
            // A diagnostic must never affect the app.
        }
    }

    /// <summary>
    /// OnClosed is synchronous, so the browser is not queried at close: the values are the last observed ones (startup
    /// snapshot, dialog snapshot or the read-only live check after the last completed X navigation).
    /// </summary>
    private void LogAdBlockCloseBegin()
    {
        try
        {
            var (boundInstancePresent, source) = xAdBlockIndicationLiveCheck switch
            {
                XAdBlockLiveCheck.BoundInstanceEnabled or XAdBlockLiveCheck.BoundInstanceDisabled => ((bool?)true, "live-check"),
                XAdBlockLiveCheck.BoundInstanceMissing => ((bool?)false, "live-check"),
                _ => (adBlockLastObservedBoundInstancePresent, adBlockLastObservedSource),
            };
            adBlockLifecycleLog?.Write(
                "close-begin",
                ("closePath", adBlockCloseRequestedByUser ? "window-close-request" : "other"),
                ("lastObservedExtensionCount", adBlockLastObservedExtensionCount?.ToString(CultureInfo.InvariantCulture)),
                ("lastObservedBoundInstancePresent", AdBlockLifecycleLog.FormatBool(boundInstancePresent)),
                ("lastObservedFrom", source),
                ("liveCheck", xAdBlockIndicationLiveCheck.ToString()));
            WriteXProfileMetadata("close");
        }
        catch (Exception)
        {
            // A diagnostic must never affect the app.
        }
    }

    /// <summary>Runs after the WebViews were asked to close; the browser process may still be shutting down.</summary>
    private void LogAdBlockCloseEnd()
    {
        try
        {
            if (adBlockLifecycleLog is not { } log)
            {
                return;
            }

            var markerWritten = log.WriteCleanCloseMarker();
            log.Write("close-end", ("cleanCloseMarker", markerWritten ? "written" : "failed"));
        }
        catch (Exception)
        {
            // A diagnostic must never affect the app.
        }
    }

    private void OnAppWindowClosingForAdBlockLog(AppWindow sender, AppWindowClosingEventArgs args)
    {
        adBlockCloseRequestedByUser = true;
    }

    private void RememberAdBlockObservation(XDiagnosticAdBlockSnapshot? snapshot, string source)
    {
        adBlockLastObservedExtensionCount = snapshot?.Extensions.Count;
        adBlockLastObservedBoundInstancePresent = snapshot?.AdBlockStatus.IsManagedInstance;
        adBlockLastObservedSource = source;
    }

    private void WriteXProfileMetadata(string phase)
    {
        adBlockLifecycleLog?.Write(
            "profile-metadata",
            [
                ("phase", phase),
                .. AdBlockLifecycleLog.DescribeProfile(new LocalApplicationDataPathProvider().GetXBrowserUserDataFolder()),
            ]);
    }
}
