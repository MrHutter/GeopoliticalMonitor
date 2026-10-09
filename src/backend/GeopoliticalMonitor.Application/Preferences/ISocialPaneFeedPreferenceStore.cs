namespace GeopoliticalMonitor.Application.Preferences;

public interface ISocialPaneFeedPreferenceStore
{
    Task<string?> GetSocialPaneFeedValueAsync(CancellationToken cancellationToken = default);

    Task SaveSocialPaneFeedValueAsync(SocialPaneFeed feed, CancellationToken cancellationToken = default);
}
