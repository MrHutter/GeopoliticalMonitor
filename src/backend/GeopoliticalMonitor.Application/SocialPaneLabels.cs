using GeopoliticalMonitor.Application.Preferences;

namespace GeopoliticalMonitor.Application;

/// <summary>Native social pane labels, kept in one place next to <see cref="ProductIdentity"/>.</summary>
public static class SocialPaneLabels
{
    public const string Eyebrow = "SOCIAL FEED";
    public const string XSegment = "X";
    public const string BlueskySegment = "Bluesky";
    public const string ShowX = "Show X";
    public const string ShowBluesky = "Show Bluesky";
    public const string ShowXFeed = "Show X feed";
    public const string ShowBlueskyFeed = "Show Bluesky feed";
    public const string RetryX = "Retry X";
    public const string RetryBluesky = "Retry Bluesky";
    public const string ClearXSessionTitle = "Clear X session?";
    public const string ClearBlueskySessionTitle = "Clear Bluesky session?";

    public static string GetSegmentName(SocialPaneFeed feed) => feed == SocialPaneFeed.Bluesky ? BlueskySegment : XSegment;

    public static string GetShowText(SocialPaneFeed feed) => feed == SocialPaneFeed.Bluesky ? ShowBluesky : ShowX;

    public static string GetSwitcherAccessibleName(SocialPaneFeed feed) => feed == SocialPaneFeed.Bluesky ? ShowBlueskyFeed : ShowXFeed;

    public static string GetRetryText(SocialPaneFeed feed) => feed == SocialPaneFeed.Bluesky ? RetryBluesky : RetryX;

    public static string GetClearSessionTitle(SocialPaneFeed feed) => feed == SocialPaneFeed.Bluesky ? ClearBlueskySessionTitle : ClearXSessionTitle;
}
