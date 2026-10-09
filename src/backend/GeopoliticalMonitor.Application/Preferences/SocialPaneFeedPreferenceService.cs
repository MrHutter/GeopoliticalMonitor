namespace GeopoliticalMonitor.Application.Preferences;

/// <summary>Native-only social pane feed preference. Not a bridge preference key: the bridge accepts only AccentTheme.</summary>
public sealed class SocialPaneFeedPreferenceService(ISocialPaneFeedPreferenceStore store)
{
    public const string PreferenceKey = "SocialPaneFeed";

    public async Task<SocialPaneFeed> GetSocialPaneFeedAsync(CancellationToken cancellationToken = default)
    {
        var value = await store.GetSocialPaneFeedValueAsync(cancellationToken).ConfigureAwait(false);
        return TryParseSocialPaneFeed(value, out var feed) ? feed : SocialPaneFeed.X;
    }

    public async Task<SocialPaneFeed> SaveSocialPaneFeedAsync(SocialPaneFeed feed, CancellationToken cancellationToken = default)
    {
        await store.SaveSocialPaneFeedValueAsync(feed, cancellationToken).ConfigureAwait(false);
        return feed;
    }

    public static bool TryParseSocialPaneFeed(string? value, out SocialPaneFeed feed)
    {
        if (string.Equals(value, nameof(SocialPaneFeed.Bluesky), StringComparison.Ordinal))
        {
            feed = SocialPaneFeed.Bluesky;
            return true;
        }

        if (string.Equals(value, nameof(SocialPaneFeed.X), StringComparison.Ordinal))
        {
            feed = SocialPaneFeed.X;
            return true;
        }

        feed = SocialPaneFeed.X;
        return false;
    }
}
