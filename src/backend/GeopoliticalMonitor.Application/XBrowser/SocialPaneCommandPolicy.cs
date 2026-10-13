using GeopoliticalMonitor.Application.Preferences;

namespace GeopoliticalMonitor.Application.XBrowser;

/// <summary>Per-feed rules for the social pane header. The X feed keeps exactly the rules of <see cref="XToolbarCommandStatePolicy"/>.</summary>
public static class SocialPaneCommandPolicy
{
    public static string GetHomeUrl(SocialPaneFeed feed)
    {
        return feed == SocialPaneFeed.Bluesky ? BlueskyBrowserDefaults.HomeUrl : XBrowserDefaults.HomeUrl;
    }

    public static string GetOpenExternalTarget(SocialPaneFeed feed, string? currentUrl)
    {
        return feed == SocialPaneFeed.Bluesky
            ? BlueskyBrowserNavigationPolicy.IsAllowedInPane(currentUrl) ? currentUrl! : BlueskyBrowserDefaults.HomeUrl
            : XBrowserNavigationPolicy.IsAllowedInPane(currentUrl) ? currentUrl! : XBrowserDefaults.HomeUrl;
    }

    /// <summary>Copy Post Link is X only. The Ad Blocker entry opens the active feed's own dialog.</summary>
    public static bool IsVisible(SocialPaneFeed feed, XToolbarCommand command)
    {
        return command != XToolbarCommand.CopyPostLink || feed == SocialPaneFeed.X;
    }

    /// <summary>
    /// Whether a feed's WebView is hidden (muted, low memory). Applied on every switch and again as soon as a WebView's
    /// browser core becomes ready, so a switch made while a control was still starting cannot leave it audible.
    /// </summary>
    public static bool IsHiddenFeed(SocialPaneFeed activeFeed, SocialPaneFeed controlFeed)
    {
        return activeFeed != controlFeed;
    }

    /// <summary>Each feed has its own ad blocker status line, shown only while that feed is active.</summary>
    public static bool ShowsAdBlockIndication(SocialPaneFeed activeFeed, SocialPaneFeed lineFeed)
    {
        return activeFeed == lineFeed;
    }

    public static bool IsEnabled(SocialPaneFeed feed, XToolbarCommand command, XToolbarCommandState state)
    {
        if (feed == SocialPaneFeed.X)
        {
            return XToolbarCommandStatePolicy.IsEnabled(command, state);
        }

        return command switch
        {
            XToolbarCommand.Back => state.RuntimeState == XBrowserRuntimeState.Ready && state.CanGoBack,
            XToolbarCommand.Forward => state.RuntimeState == XBrowserRuntimeState.Ready && state.CanGoForward,
            XToolbarCommand.Home => state.RuntimeState == XBrowserRuntimeState.Ready,
            XToolbarCommand.Refresh => state.RuntimeState == XBrowserRuntimeState.Ready,
            XToolbarCommand.OpenExternal => state.RuntimeState == XBrowserRuntimeState.Failed || BlueskyBrowserNavigationPolicy.IsAllowedInPane(state.CurrentUrl),
            XToolbarCommand.ClearSession => state.RuntimeState == XBrowserRuntimeState.Ready,
            XToolbarCommand.AdBlocker => state.RuntimeState == XBrowserRuntimeState.Ready,
            _ => false,
        };
    }
}
