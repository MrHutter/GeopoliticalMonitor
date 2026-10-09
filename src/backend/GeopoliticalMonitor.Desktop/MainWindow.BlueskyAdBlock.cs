using System.Diagnostics;
using GeopoliticalMonitor.Application.XBrowser;
using GeopoliticalMonitor.Infrastructure.Persistence;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using Windows.Storage.Pickers;
using Windows.UI;
using WinRT.Interop;

namespace GeopoliticalMonitor.Desktop;

// The Bluesky feed's own verified AdBlock copy (docs/design/social-pane-adblock-design.md, option (c)). It has its own
// payload folder, binding file, scope, service, environment decision, dialog and status line, and shares only the
// pinned trust record with X. Every Bluesky step catches its own failures and sets only Bluesky state, so it never
// affects X. The only X item it touches is the X managed copy, read and authenticated again by "Use verified X copy".
public sealed partial class MainWindow
{
    private XDiagnosticAdBlockService? blueskyAdBlockService;
    private XProductionExtensionHostState blueskyExtensionHostState = XProductionExtensionHostState.XUnavailable;
    private string? blueskyExtensionStartupFailure;
    private bool blueskyAdBlockEnrollmentAllowed;
    private bool blueskyAdBlockSetUp;

    private sealed record BlueskyAdBlockContext(
        string ManagedSourcePath,
        FileXDiagnosticAdBlockSourceInspector SourceInspector,
        FileXAdBlockManagedPayloadStore PayloadStore,
        FileXDiagnosticAdBlockBindingStore BindingStore);

    private static BlueskyAdBlockContext CreateBlueskyAdBlockContext()
    {
        var pathProvider = new LocalApplicationDataPathProvider();
        var managedSourcePath = pathProvider.GetBlueskyAdBlockManagedSourceDirectory();
        var sourceInspector = new FileXDiagnosticAdBlockSourceInspector();
        return new(
            managedSourcePath,
            sourceInspector,
            new FileXAdBlockManagedPayloadStore(managedSourcePath, sourceInspector),
            new FileXDiagnosticAdBlockBindingStore(pathProvider.GetBlueskyAdBlockBindingPath()));
    }

    /// <summary>
    /// The Bluesky trust gate, run each time a Bluesky control is created: recover, read the Bluesky binding, authenticate
    /// the Bluesky payload in full and decide with the Bluesky scope. A clean state stays extension-disabled.
    /// </summary>
    private async Task<CoreWebView2Environment> CreateBlueskyEnvironmentAsync(string userDataFolder)
    {
        blueskyAdBlockService = null;
        blueskyExtensionHostState = XProductionExtensionHostState.FallbackUnavailable;
        blueskyExtensionStartupFailure = null;
        blueskyAdBlockEnrollmentAllowed = false;
        var attemptExtensionEnvironment = false;
        try
        {
            var context = CreateBlueskyAdBlockContext();
            var activationRecovered = await context.PayloadStore.RecoverInterruptedActivationAsync().ConfigureAwait(true);
            var binding = await context.BindingStore.GetAsync().ConfigureAwait(true);
            var managedPayloadExists = Directory.Exists(context.ManagedSourcePath);
            var persistedBindingExists = context.BindingStore.HasPersistedState || !activationRecovered;
            blueskyAdBlockSetUp = BlueskyAdBlockPolicy.IsSetUp(persistedBindingExists, managedPayloadExists);
            XDiagnosticAdBlockSourceValidation? managedValidation = null;
            if (managedPayloadExists)
            {
                managedValidation = await context.SourceInspector.InspectAsync(context.ManagedSourcePath).ConfigureAwait(true);
            }

            var trustDecision = XProductionAdBlockStartupTrustPolicy.Evaluate(
                managedPayloadExists,
                persistedBindingExists,
                binding,
                managedValidation,
                XDiagnosticAdBlockBinding.ProductionBlueskyProfileScope);
            attemptExtensionEnvironment = trustDecision.AttemptExtensionEnvironment;
            blueskyAdBlockEnrollmentAllowed = !trustDecision.AttemptExtensionEnvironment;
            blueskyExtensionStartupFailure = trustDecision.Message ?? (blueskyAdBlockSetUp ? null : BlueskyAdBlockPolicy.NotSetUpMessage);
        }
        catch (Exception ex)
        {
            blueskyAdBlockSetUp = true;
            blueskyExtensionStartupFailure = GetSafeDiagnosticExceptionMessage(ex, userDataFolder);
            Debug.WriteLine($"Bluesky ad blocker trust gate failed: {ex.GetType().FullName} 0x{ex.HResult:X8}");
        }

        if (attemptExtensionEnvironment)
        {
            try
            {
                var environment = await CreateExtensionEnabledXEnvironmentAsync(userDataFolder).ConfigureAwait(true);
                blueskyExtensionHostState = XProductionExtensionHostState.Available;
                return environment;
            }
            catch (Exception ex)
            {
                blueskyExtensionStartupFailure = GetSafeDiagnosticExceptionMessage(ex, userDataFolder);
                Debug.WriteLine(SafeDiagnosticExceptionFormatter.FormatForDebug(
                    "Bluesky extension environment creation failed; using default environment",
                    ex,
                    userDataFolder));
            }
        }

        return await CreateDefaultWebViewEnvironmentAsync(userDataFolder).ConfigureAwait(true);
    }

    /// <summary>
    /// Before the first Bluesky navigation: builds the Bluesky service and secures the extension state, keeping its
    /// snapshot. Returns false when securing failed, so the control must not navigate.
    /// </summary>
    private async Task<bool> SecureBlueskyAdBlockAsync(WebView2 webView)
    {
        var context = CreateBlueskyAdBlockContext();
        if (blueskyExtensionHostState != XProductionExtensionHostState.Available)
        {
            ObserveBlueskyAdBlockSnapshot(null, Directory.Exists(context.ManagedSourcePath));
            return true;
        }

        try
        {
            var service = new XDiagnosticAdBlockService(
                new CoreWebView2ExtensionProfileAdapter(webView.CoreWebView2.Profile, webView.DispatcherQueue),
                context.BindingStore,
                context.SourceInspector,
                XDiagnosticAdBlockBinding.ProductionBlueskyProfileScope,
                context.ManagedSourcePath);
            var snapshot = await service.SecureForAuthenticatedNavigationAsync().ConfigureAwait(true);
            blueskyAdBlockService = service;
            ObserveBlueskyAdBlockSnapshot(snapshot, Directory.Exists(context.ManagedSourcePath));
            return true;
        }
        catch (Exception ex)
        {
            blueskyAdBlockService = null;
            blueskyExtensionHostState = XProductionExtensionHostState.FallbackUnavailable;
            blueskyExtensionStartupFailure = GetSafeDiagnosticExceptionMessage(ex, context.ManagedSourcePath);
            Debug.WriteLine($"Bluesky ad blocker securing failed: {ex.GetType().FullName} 0x{ex.HResult:X8}");
            return false;
        }
    }

    /// <summary>Called when a Bluesky control is closed: its service and profile adapter belong to that control only.</summary>
    private void ResetBlueskyAdBlockRuntime()
    {
        blueskyAdBlockService = null;
        blueskyExtensionHostState = XProductionExtensionHostState.XUnavailable;
        ForgetBlueskyAdBlockIndicationObservations();
    }

    private async Task ShowBlueskyAdBlockDialogAsync()
    {
        var context = CreateBlueskyAdBlockContext();
        var managedSourcePath = context.ManagedSourcePath;
        var sourceInspector = context.SourceInspector;
        var bindingStore = context.BindingStore;
        var payloadStore = context.PayloadStore;
        var verifiedXCopy = new VerifiedXAdBlockCopySource(
            new LocalApplicationDataPathProvider().GetProductionAdBlockManagedSourceDirectory(),
            sourceInspector);
        var statusText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 212, 214, 218)),
        };
        var refreshButton = CreateXExtensionDialogButton("Refresh");
        var installButton = CreateXExtensionDialogButton("Install AdBlock...");
        var xCopyButton = CreateXExtensionDialogButton("Use verified X copy");
        var bindButton = CreateXExtensionDialogButton("Reconcile");
        var enableButton = CreateXExtensionDialogButton("Enable");
        var disableButton = CreateXExtensionDialogButton("Disable");
        var removeButton = CreateXExtensionDialogButton("Remove");
        XDiagnosticAdBlockSnapshot? snapshot = null;
        var actionInProgress = false;
        string? operationMessage = null;

        var managementActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        managementActions.Children.Add(enableButton);
        managementActions.Children.Add(disableButton);
        managementActions.Children.Add(removeButton);
        var installActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        installActions.Children.Add(installButton);
        installActions.Children.Add(xCopyButton);
        var setupActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        setupActions.Children.Add(bindButton);
        setupActions.Children.Add(refreshButton);
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new ScrollViewer
        {
            MaxHeight = 360,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = statusText,
        });
        content.Children.Add(new TextBlock
        {
            Text = BlueskyAdBlockPolicy.Explanation,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 180, 182, 187)),
        });
        content.Children.Add(installActions);
        content.Children.Add(setupActions);
        content.Children.Add(managementActions);

        var dialog = new ContentDialog
        {
            Title = BlueskyAdBlockPolicy.DialogTitle,
            Content = content,
            CloseButtonText = "Close",
            XamlRoot = RootGrid.XamlRoot,
        };

        void RefreshPresentation()
        {
            var hasManagedSource = Directory.Exists(managedSourcePath);
            statusText.Text = XProductionAdBlockPolicy.Format(
                blueskyExtensionHostState,
                snapshot,
                hasManagedSource,
                blueskyExtensionStartupFailure,
                operationMessage);
            var actions = BlueskyAdBlockPolicy.GetActions(
                blueskyExtensionHostState,
                snapshot,
                hasManagedSource,
                actionInProgress,
                blueskyAdBlockEnrollmentAllowed,
                verifiedXCopy.Exists);
            refreshButton.IsEnabled = actions.CanRefresh;
            installButton.IsEnabled = actions.CanInstall;
            xCopyButton.IsEnabled = actions.CanUseVerifiedXCopy;
            bindButton.IsEnabled = actions.CanBind;
            enableButton.IsEnabled = actions.CanEnable;
            disableButton.IsEnabled = actions.CanDisable;
            removeButton.IsEnabled = actions.CanRemove;
        }

        async Task ExecuteAsync(
            string stage,
            Func<XDiagnosticAdBlockService, Task<XDiagnosticAdBlockSnapshot>> operation,
            params string[] privatePaths)
        {
            var service = blueskyAdBlockService;
            if (service is null)
            {
                return;
            }

            actionInProgress = true;
            operationMessage = null;
            RefreshPresentation();
            try
            {
                snapshot = await operation(service);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(SafeDiagnosticExceptionFormatter.FormatForDebug(
                    $"Bluesky AdBlock operation failed at {stage}",
                    ex,
                    privatePaths));
                snapshot = new(
                    snapshot?.Extensions ?? [],
                    XDiagnosticAdBlockStatus.Failed(
                        XDiagnosticAdBlockState.OperationFailed,
                        stage,
                        ex,
                        GetSafeDiagnosticExceptionMessage(ex, privatePaths)));
            }
            finally
            {
                actionInProgress = false;
                RefreshPresentation();
            }
        }

        // Stages an authenticated source into the Bluesky folder only, then installs it in the Bluesky profile (or, when
        // the Bluesky environment is extension-disabled, enrolls it for the next start).
        async Task EnrollAsync(Func<Task<XDiagnosticAdBlockSourceValidation>> authenticate, string stage, string sourcePath)
        {
            actionInProgress = true;
            operationMessage = null;
            RefreshPresentation();
            try
            {
                var validation = await authenticate();
                if (!validation.IsValid || validation.Identity is null)
                {
                    snapshot = new(
                        snapshot?.Extensions ?? [],
                        XDiagnosticAdBlockStatus.InvalidFolder(validation.ErrorMessage ?? "The selected folder is invalid."));
                    return;
                }

                var prepared = await payloadStore.PrepareAsync(validation.Identity);
                if (!prepared.Success || prepared.Identity is null)
                {
                    snapshot = new(
                        snapshot?.Extensions ?? [],
                        XDiagnosticAdBlockStatus.InvalidFolder(prepared.ErrorMessage ?? "The managed payload could not be prepared."));
                    return;
                }

                blueskyAdBlockSetUp = true;
                if (blueskyAdBlockService is null)
                {
                    await bindingStore.ClearAsync();
                    operationMessage = "The trusted AdBlock payload was enrolled for Bluesky without binding a live extension. Restart, then use Reconcile (or Install if no instance remains) in the Bluesky Ad Blocker to perform a fresh verified WebView2 install transaction.";
                    return;
                }

                snapshot = await blueskyAdBlockService.InstallAsync(prepared.Identity.SourceFolderPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(SafeDiagnosticExceptionFormatter.FormatForDebug(
                    $"Bluesky AdBlock installation failed at {stage}",
                    ex,
                    sourcePath,
                    managedSourcePath));
                snapshot = new(
                    snapshot?.Extensions ?? [],
                    XDiagnosticAdBlockStatus.Failed(
                        XDiagnosticAdBlockState.InstallationFailed,
                        stage,
                        ex,
                        GetSafeDiagnosticExceptionMessage(ex, sourcePath, managedSourcePath)));
            }
            finally
            {
                actionInProgress = false;
                RefreshPresentation();
            }
        }

        refreshButton.Click += async (_, _) =>
            await ExecuteAsync("Profile.GetBrowserExtensionsAsync", service => service.GetSnapshotAsync());

        installButton.Click += async (_, _) =>
        {
            if (blueskyAdBlockService is null && !blueskyAdBlockEnrollmentAllowed)
            {
                return;
            }

            var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.Downloads };
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            var folder = await picker.PickSingleFolderAsync();
            if (folder is null)
            {
                return;
            }

            await EnrollAsync(() => sourceInspector.InspectAsync(folder.Path), "Bluesky install", folder.Path);
        };

        xCopyButton.Click += async (_, _) =>
        {
            if (blueskyAdBlockService is null && !blueskyAdBlockEnrollmentAllowed)
            {
                return;
            }

            // Read-only: the X copy is authenticated again in full before it is staged into the Bluesky folder.
            await EnrollAsync(() => verifiedXCopy.AuthenticateAsync(), "Bluesky install from verified X copy", managedSourcePath);
        };

        bindButton.Click += async (_, _) =>
            await ExecuteAsync(
                "Bluesky binding validation",
                service => service.BindInstalledAsync(managedSourcePath),
                managedSourcePath);
        enableButton.Click += async (_, _) =>
            await ExecuteAsync("CoreWebView2BrowserExtension.EnableAsync(true)", service => service.SetEnabledAsync(true));
        disableButton.Click += async (_, _) =>
            await ExecuteAsync("CoreWebView2BrowserExtension.EnableAsync(false)", service => service.SetEnabledAsync(false));
        removeButton.Click += async (_, _) =>
        {
            var wasManaged = snapshot?.AdBlockStatus.IsManagedInstance == true;
            await ExecuteAsync("CoreWebView2BrowserExtension.RemoveAsync", service => service.RemoveAsync());
            if (wasManaged && snapshot?.AdBlockStatus.State == XDiagnosticAdBlockState.NoManagedInstance)
            {
                // Deletes only the Bluesky managed copy; the service already cleared only the Bluesky binding.
                var cleanup = await payloadStore.RemoveAsync();
                operationMessage = cleanup.Success
                    ? "AdBlock for Bluesky and its application-managed Bluesky payload were removed. X is not affected."
                    : cleanup.ErrorMessage;
                RefreshPresentation();
            }
        };
        dialog.Opened += async (_, _) =>
        {
            RefreshPresentation();
            if (blueskyAdBlockService is not null)
            {
                await ExecuteAsync("Profile.GetBrowserExtensionsAsync", service => service.GetSnapshotAsync());
            }
        };
        dialog.Closing += (_, args) => args.Cancel = actionInProgress;

        RefreshPresentation();
        await dialog.ShowAsync();
        // Read-only: reuse the dialog's last snapshot for the Bluesky ad blocker indication.
        var payloadExists = Directory.Exists(managedSourcePath);
        blueskyAdBlockSetUp = blueskyAdBlockSetUp || payloadExists || bindingStore.HasPersistedState;
        ObserveBlueskyAdBlockSnapshot(snapshot, payloadExists);
    }
}
