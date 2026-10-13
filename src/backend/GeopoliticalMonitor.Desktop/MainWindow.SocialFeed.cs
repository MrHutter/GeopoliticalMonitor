using System.Diagnostics;
using GeopoliticalMonitor.Application;
using GeopoliticalMonitor.Application.Preferences;
using GeopoliticalMonitor.Application.XBrowser;
using GeopoliticalMonitor.Infrastructure.Persistence;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace GeopoliticalMonitor.Desktop;

// Social pane feed switcher and the isolated Bluesky website pane (docs/design/x-pane-design.md, slice 3). The Bluesky
// WebView is a plain, user-operated website pane: it is never wired to the privileged bridge, gets no host objects or
// web messages, no injected script, no DOM inspection and no cookie reading (Clear Session only deletes).
public sealed partial class MainWindow
{
    private SocialPaneFeedPreferenceService? socialPaneFeedPreferenceService;
    private SocialPaneFeed activeSocialFeed = SocialPaneFeed.X;
    private bool socialFeedChosenByUser;
    private string? xErrorMessage;
    private string? blueskyErrorMessage;
    private WebView2? blueskyWebView;
    private string? blueskyBrowserUserDataFolder;
    private Task? blueskyInitializationTask;
    private XBrowserRuntimeState blueskyRuntimeState = XBrowserRuntimeState.NotStarted;

    private void InitializeSocialFeedChrome()
    {
        XHeaderEyebrow.Text = SocialPaneLabels.Eyebrow;
        ShowXFeedButton.Content = SocialPaneLabels.XSegment;
        ShowBlueskyFeedButton.Content = SocialPaneLabels.BlueskySegment;
        AutomationProperties.SetName(ShowXFeedButton, SocialPaneLabels.ShowXFeed);
        AutomationProperties.SetName(ShowBlueskyFeedButton, SocialPaneLabels.ShowBlueskyFeed);
        ToolTipService.SetToolTip(ShowXFeedButton, SocialPaneLabels.ShowXFeed);
        ToolTipService.SetToolTip(ShowBlueskyFeedButton, SocialPaneLabels.ShowBlueskyFeed);
        XWebView.CoreWebView2Initialized += OnSocialWebViewCoreInitialized;
        ApplySocialFeedChrome();
    }

    /// <summary>
    /// A switch can happen before a feed's browser core exists, when the mute could not be applied yet; apply the
    /// hidden or visible state as soon as the core is ready.
    /// </summary>
    private void OnSocialWebViewCoreInitialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
    {
        if (args.Exception is not null || sender.CoreWebView2 is not { } core)
        {
            return;
        }

        var controlFeed = ReferenceEquals(sender, XWebView) ? SocialPaneFeed.X : SocialPaneFeed.Bluesky;
        SetSocialWebViewHidden(core, hidden: SocialPaneCommandPolicy.IsHiddenFeed(activeSocialFeed, controlFeed));
    }

    private void ApplySocialFeedChrome()
    {
        ShowXFeedButton.IsChecked = activeSocialFeed == SocialPaneFeed.X;
        ShowBlueskyFeedButton.IsChecked = activeSocialFeed == SocialPaneFeed.Bluesky;
        ShowXPaneButton.Content = SocialPaneLabels.GetShowText(activeSocialFeed);
        ToolTipService.SetToolTip(XMoveSideButton, SocialPaneLabels.GetMoveTooltip(activeSocialFeed, xPaneSide));
        ToolTipService.SetToolTip(XOverflowButton, SocialPaneLabels.GetOverflowTooltip(activeSocialFeed));
        XRetryButton.Content = SocialPaneLabels.GetRetryText(activeSocialFeed);
        XCopyPostLinkMenuItem.Visibility = ToVisibility(SocialPaneCommandPolicy.IsVisible(activeSocialFeed, XToolbarCommand.CopyPostLink));
        var adBlockerVisibility = ToVisibility(SocialPaneCommandPolicy.IsVisible(activeSocialFeed, XToolbarCommand.AdBlocker));
        XExtensionsMenuItem.Visibility = adBlockerVisibility;
        XOverflowAdBlockSeparator.Visibility = adBlockerVisibility;
    }

    private async void OnSocialFeedSegmentClicked(object sender, RoutedEventArgs args)
    {
        var feed = ReferenceEquals(sender, ShowBlueskyFeedButton) ? SocialPaneFeed.Bluesky : SocialPaneFeed.X;
        socialFeedChosenByUser = true;
        await SwitchSocialFeedAsync(feed, persist: true);
        (sender as Control)?.Focus(FocusState.Programmatic);
    }

    /// <summary>Shows the chosen feed and hides the other without closing, reloading or navigating either.</summary>
    private async Task SwitchSocialFeedAsync(SocialPaneFeed feed, bool persist)
    {
        if (feed == activeSocialFeed)
        {
            ApplySocialFeedChrome();
            return;
        }

        HideXOverflowPanel();
        XCopyStatusText.Text = string.Empty;
        activeSocialFeed = feed;
        ApplySocialFeedChrome();
        ApplySocialFeedVisibility();
        UpdateActiveSocialFeedButtons();
        ApplySocialErrorBanner();
        ApplyXAdBlockIndicationVisibility();
        ApplyBlueskyAdBlockIndicationVisibility();
        if (persist)
        {
            PersistSocialPaneFeedAsync(feed);
        }

        if (feed == SocialPaneFeed.Bluesky && blueskyWebView is null && blueskyRuntimeState == XBrowserRuntimeState.NotStarted)
        {
            blueskyInitializationTask = InitializeBlueskyWebViewAsync();
            await blueskyInitializationTask;
        }
    }

    /// <summary>Runs after the X control has initialized; restores the saved feed without writing it back.</summary>
    private async Task RestoreSocialPaneFeedAsync()
    {
        if (socialPaneFeedPreferenceService is null || socialFeedChosenByUser)
        {
            return;
        }

        SocialPaneFeed savedFeed;
        try
        {
            savedFeed = await socialPaneFeedPreferenceService.GetSocialPaneFeedAsync().ConfigureAwait(true);
        }
        catch (Exception)
        {
            return;
        }

        if (savedFeed == SocialPaneFeed.Bluesky && !socialFeedChosenByUser)
        {
            await SwitchSocialFeedAsync(SocialPaneFeed.Bluesky, persist: false);
        }
    }

    private async void PersistSocialPaneFeedAsync(SocialPaneFeed feed)
    {
        if (socialPaneFeedPreferenceService is null)
        {
            return;
        }

        try
        {
            await socialPaneFeedPreferenceService.SaveSocialPaneFeedAsync(feed).ConfigureAwait(true);
        }
        catch (Exception)
        {
            // The switch is usable for the current session even if preference persistence is unavailable.
        }
    }

    private void ApplySocialFeedVisibility()
    {
        var blueskyActive = activeSocialFeed == SocialPaneFeed.Bluesky;
        XWebView.Visibility = ToVisibility(!blueskyActive);
        SetSocialWebViewHidden(XWebView.CoreWebView2, hidden: SocialPaneCommandPolicy.IsHiddenFeed(activeSocialFeed, SocialPaneFeed.X));
        if (blueskyWebView is not null)
        {
            blueskyWebView.Visibility = ToVisibility(blueskyActive);
            SetSocialWebViewHidden(blueskyWebView.CoreWebView2, hidden: SocialPaneCommandPolicy.IsHiddenFeed(activeSocialFeed, SocialPaneFeed.Bluesky));
        }
    }

    /// <summary>Runtime properties only: a hidden feed is muted with a low memory target; the visible one is restored.</summary>
    private static void SetSocialWebViewHidden(CoreWebView2? core, bool hidden)
    {
        if (core is null)
        {
            return;
        }

        try
        {
            core.IsMuted = hidden;
            core.MemoryUsageTargetLevel = hidden ? CoreWebView2MemoryUsageTargetLevel.Low : CoreWebView2MemoryUsageTargetLevel.Normal;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Social feed visibility update failed: {ex.GetType().FullName} 0x{ex.HResult:X8}");
        }
    }

    private void UpdateActiveSocialFeedButtons()
    {
        if (activeSocialFeed == SocialPaneFeed.X)
        {
            UpdateXNavigationButtons();
            UpdateXPostLinkButton();
            return;
        }

        UpdateBlueskyNavigationButtons();
    }

    private void ApplySocialErrorBanner()
    {
        var message = activeSocialFeed == SocialPaneFeed.Bluesky ? blueskyErrorMessage : xErrorMessage;
        XStatusText.Text = message ?? string.Empty;
        XErrorBanner.Visibility = ToVisibility(message is not null);
    }

    private void ApplyXAdBlockIndicationVisibility()
    {
        XAdBlockIndicationText.Visibility = ToVisibility(
            !string.IsNullOrEmpty(XAdBlockIndicationText.Text) && SocialPaneCommandPolicy.ShowsAdBlockIndication(activeSocialFeed, SocialPaneFeed.X));
    }

    private static Visibility ToVisibility(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private async Task InitializeBlueskyWebViewAsync()
    {
        if (blueskyRuntimeState == XBrowserRuntimeState.Initializing)
        {
            return;
        }

        blueskyRuntimeState = XBrowserRuntimeState.Initializing;
        HideBlueskyError();
        UpdateBlueskyNavigationButtons();
        try
        {
            blueskyBrowserUserDataFolder ??= new LocalApplicationDataPathProvider().GetBlueskyBrowserUserDataFolder();
            // The Bluesky ad blocker gate decides this control's environment; a clean state is extension-disabled.
            var environment = await CreateBlueskyEnvironmentAsync(blueskyBrowserUserDataFolder).ConfigureAwait(true);
            blueskyWebView ??= CreateBlueskyWebViewElement();
            await blueskyWebView.EnsureCoreWebView2Async(environment);
            ConfigureBlueskyCoreWebView(blueskyWebView.CoreWebView2);
            if (!await SecureBlueskyAdBlockAsync(blueskyWebView).ConfigureAwait(true))
            {
                throw new InvalidOperationException("The Bluesky extension state could not be secured before navigation.");
            }

            ApplySocialFeedVisibility();
            NavigateBlueskyHome();
            blueskyRuntimeState = XBrowserRuntimeState.Ready;
        }
        catch (Exception ex)
        {
            blueskyRuntimeState = XBrowserRuntimeState.Failed;
            Debug.WriteLine($"Bluesky WebView initialization failed: {ex.GetType().FullName} 0x{ex.HResult:X8}");
            DiscardBlueskyWebView();
            ShowBlueskyError("Bluesky website pane could not initialize.");
        }

        UpdateBlueskyNavigationButtons();
    }

    private WebView2 CreateBlueskyWebViewElement()
    {
        var webView = new WebView2 { Visibility = ToVisibility(activeSocialFeed == SocialPaneFeed.Bluesky) };
        webView.CoreWebView2Initialized += OnSocialWebViewCoreInitialized;
        Grid.SetRow(webView, 2);
        XPane.Children.Add(webView);
        return webView;
    }

    private void ConfigureBlueskyCoreWebView(CoreWebView2 core)
    {
        ApplyXSecuritySettings(core.Settings);
        core.NavigationStarting += OnBlueskyNavigationStarting;
        core.NavigationCompleted += OnBlueskyNavigationCompleted;
        core.SourceChanged += OnBlueskySourceChanged;
        core.HistoryChanged += OnBlueskyHistoryChanged;
        core.NewWindowRequested += OnBlueskyNewWindowRequested;
        core.DownloadStarting += OnBlueskyDownloadStarting;
        core.PermissionRequested += OnBlueskyPermissionRequested;
        core.ProcessFailed += OnBlueskyProcessFailed;
    }

    private void OnBlueskyNavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        switch (BlueskyBrowserNavigationPolicy.ClassifyTopLevelNavigation(args.Uri))
        {
            case XBrowserNavigationDecision.AllowInPane:
                HideBlueskyError();
                return;
            case XBrowserNavigationDecision.OpenExternally:
                args.Cancel = true;
                OpenInSystemBrowser(args.Uri);
                return;
            default:
                args.Cancel = true;
                ShowBlueskyError("Bluesky navigation was blocked because it was not a permitted HTTPS Bluesky website page.");
                return;
        }
    }

    private void OnBlueskyNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (args.IsSuccess)
        {
            blueskyRuntimeState = XBrowserRuntimeState.Ready;
            HideBlueskyError();
            _ = RunBlueskyAdBlockLiveCheckAsync();
        }
        else
        {
            blueskyRuntimeState = XBrowserRuntimeState.Failed;
            ShowBlueskyError($"Bluesky website could not load ({args.WebErrorStatus}). Use Retry or Open External.");
        }

        UpdateBlueskyNavigationButtons();
    }

    private void OnBlueskySourceChanged(CoreWebView2 sender, CoreWebView2SourceChangedEventArgs args)
    {
        UpdateBlueskyNavigationButtons();
    }

    private void OnBlueskyHistoryChanged(object? sender, object args)
    {
        UpdateBlueskyNavigationButtons();
    }

    private void OnBlueskyNewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
    {
        args.Handled = true;
        switch (BlueskyBrowserNavigationPolicy.ClassifyTopLevelNavigation(args.Uri))
        {
            case XBrowserNavigationDecision.AllowInPane:
                sender.Navigate(args.Uri);
                break;
            case XBrowserNavigationDecision.OpenExternally:
                OpenInSystemBrowser(args.Uri);
                break;
        }
    }

    private static void OnBlueskyDownloadStarting(CoreWebView2 sender, CoreWebView2DownloadStartingEventArgs args)
    {
        args.Cancel = true;
    }

    /// <summary>The Bluesky pane grants no permission: camera, microphone, location, notifications, clipboard and all others are denied.</summary>
    private static void OnBlueskyPermissionRequested(CoreWebView2 sender, CoreWebView2PermissionRequestedEventArgs args)
    {
        args.State = CoreWebView2PermissionState.Deny;
    }

    private void OnBlueskyProcessFailed(CoreWebView2 sender, CoreWebView2ProcessFailedEventArgs args)
    {
        switch (args.ProcessFailedKind)
        {
            case CoreWebView2ProcessFailedKind.BrowserProcessExited:
                // The control cannot be reused; Retry creates a new one.
                DispatcherQueue.TryEnqueue(DiscardBlueskyWebView);
                break;
            case CoreWebView2ProcessFailedKind.RenderProcessExited:
            case CoreWebView2ProcessFailedKind.RenderProcessUnresponsive:
                break;
            default:
                return;
        }

        blueskyRuntimeState = XBrowserRuntimeState.Failed;
        ShowBlueskyError("Bluesky website stopped working. Use Retry or Open External.");
        UpdateBlueskyNavigationButtons();
    }

    private void DiscardBlueskyWebView()
    {
        if (blueskyWebView is not { } webView)
        {
            return;
        }

        blueskyWebView = null;
        ResetBlueskyAdBlockRuntime();
        CloseBlueskyWebView(webView);
        XPane.Children.Remove(webView);
        UpdateBlueskyNavigationButtons();
    }

    private static void CloseBlueskyWebView(WebView2 webView)
    {
        try
        {
            webView.Close();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Bluesky WebView close failed: {ex.GetType().FullName} 0x{ex.HResult:X8}");
        }
    }

    /// <summary>Called from OnClosed before the X and app WebViews close; does nothing if Bluesky was never created.</summary>
    private void CloseBlueskyWebViewOnExit()
    {
        if (blueskyWebView is { } webView)
        {
            CloseBlueskyWebView(webView);
        }
    }

    /// <summary>The Ad Blocker entry opens the active feed's own dialog; the X dialog is reached only from the X feed.</summary>
    private async Task ShowActiveSocialFeedAdBlockDialogAsync()
    {
        if (activeSocialFeed == SocialPaneFeed.Bluesky && blueskyWebView?.CoreWebView2 is not null)
        {
            await ShowBlueskyAdBlockDialogAsync();
        }
    }

    private void NavigateBlueskyHome()
    {
        HideBlueskyError();
        if (blueskyWebView?.CoreWebView2 is { } core)
        {
            core.Navigate(BlueskyBrowserDefaults.HomeUrl);
        }
    }

    private void OnBlueskyBackClicked()
    {
        if (blueskyWebView?.CoreWebView2?.CanGoBack == true)
        {
            blueskyWebView.CoreWebView2.GoBack();
        }
    }

    private void OnBlueskyForwardClicked()
    {
        if (blueskyWebView?.CoreWebView2?.CanGoForward == true)
        {
            blueskyWebView.CoreWebView2.GoForward();
        }
    }

    private void OnBlueskyRefreshClicked()
    {
        if (blueskyWebView?.CoreWebView2 is not { } core)
        {
            return;
        }

        HideBlueskyError();
        core.Reload();
    }

    private async Task RetryBlueskyAsync()
    {
        if (blueskyInitializationTask is { IsCompleted: false })
        {
            return;
        }

        if (blueskyWebView?.CoreWebView2 is not null)
        {
            blueskyRuntimeState = XBrowserRuntimeState.Initializing;
            NavigateBlueskyHome();
            UpdateBlueskyNavigationButtons();
            return;
        }

        blueskyRuntimeState = XBrowserRuntimeState.NotStarted;
        blueskyInitializationTask = InitializeBlueskyWebViewAsync();
        await blueskyInitializationTask;
    }

    private void OpenBlueskyExternal()
    {
        OpenInSystemBrowser(SocialPaneCommandPolicy.GetOpenExternalTarget(SocialPaneFeed.Bluesky, blueskyWebView?.Source?.ToString()));
    }

    /// <summary>Clears only the Bluesky profile (its own user-data folder): deletes cookies and browsing data, never reads them.</summary>
    private async Task ClearBlueskySessionAsync()
    {
        if (blueskyWebView?.CoreWebView2 is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = SocialPaneLabels.ClearBlueskySessionTitle,
            Content = $"This clears the isolated Bluesky website cookies and browsing data for {ProductIdentity.DisplayName}. The X session, local events and preferences are not affected.",
            PrimaryButtonText = "Clear Session",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = RootGrid.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary || blueskyWebView?.CoreWebView2 is not { } core)
        {
            return;
        }

        core.CookieManager.DeleteAllCookies();
        await core.Profile.ClearBrowsingDataAsync();
        NavigateBlueskyHome();
    }

    private void UpdateBlueskyNavigationButtons()
    {
        if (activeSocialFeed != SocialPaneFeed.Bluesky)
        {
            return;
        }

        var core = blueskyWebView?.CoreWebView2;
        var state = new XToolbarCommandState(
            blueskyRuntimeState,
            CanGoBack: core?.CanGoBack == true,
            CanGoForward: core?.CanGoForward == true,
            CurrentUrl: blueskyWebView?.Source?.ToString());
        XBackButton.IsEnabled = SocialPaneCommandPolicy.IsEnabled(SocialPaneFeed.Bluesky, XToolbarCommand.Back, state);
        XForwardButton.IsEnabled = SocialPaneCommandPolicy.IsEnabled(SocialPaneFeed.Bluesky, XToolbarCommand.Forward, state);
        XHomeButton.IsEnabled = SocialPaneCommandPolicy.IsEnabled(SocialPaneFeed.Bluesky, XToolbarCommand.Home, state);
        XRefreshButton.IsEnabled = SocialPaneCommandPolicy.IsEnabled(SocialPaneFeed.Bluesky, XToolbarCommand.Refresh, state);
        XOpenExternalMenuItem.IsEnabled = SocialPaneCommandPolicy.IsEnabled(SocialPaneFeed.Bluesky, XToolbarCommand.OpenExternal, state);
        XClearSessionMenuItem.IsEnabled = SocialPaneCommandPolicy.IsEnabled(SocialPaneFeed.Bluesky, XToolbarCommand.ClearSession, state);
        XCopyPostLinkMenuItem.IsEnabled = SocialPaneCommandPolicy.IsEnabled(SocialPaneFeed.Bluesky, XToolbarCommand.CopyPostLink, state);
        XExtensionsMenuItem.IsEnabled = SocialPaneCommandPolicy.IsEnabled(SocialPaneFeed.Bluesky, XToolbarCommand.AdBlocker, state);
        XRetryButton.IsEnabled = blueskyRuntimeState == XBrowserRuntimeState.Failed;
    }

    private void ShowBlueskyError(string message)
    {
        blueskyErrorMessage = $"{message} Open Bluesky in your browser if the embedded website is unavailable.";
        ApplySocialErrorBanner();
    }

    private void HideBlueskyError()
    {
        blueskyErrorMessage = null;
        ApplySocialErrorBanner();
    }
}
