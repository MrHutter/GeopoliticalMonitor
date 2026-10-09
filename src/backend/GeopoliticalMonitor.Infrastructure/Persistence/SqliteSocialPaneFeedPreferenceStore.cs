using GeopoliticalMonitor.Application.Preferences;
using GeopoliticalMonitor.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace GeopoliticalMonitor.Infrastructure.Persistence;

public sealed class SqliteSocialPaneFeedPreferenceStore(Func<GeopoliticalMonitorDbContext> createDbContext) : ISocialPaneFeedPreferenceStore
{
    public async Task<string?> GetSocialPaneFeedValueAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        var preference = await dbContext.ApplicationPreferences
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Key == SocialPaneFeedPreferenceService.PreferenceKey, cancellationToken)
            .ConfigureAwait(false);

        return preference?.Value;
    }

    public async Task SaveSocialPaneFeedValueAsync(SocialPaneFeed feed, CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        var preference = await dbContext.ApplicationPreferences
            .SingleOrDefaultAsync(item => item.Key == SocialPaneFeedPreferenceService.PreferenceKey, cancellationToken)
            .ConfigureAwait(false);

        if (preference is null)
        {
            preference = new ApplicationPreferenceEntity
            {
                Key = SocialPaneFeedPreferenceService.PreferenceKey,
                Value = feed.ToString(),
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            dbContext.ApplicationPreferences.Add(preference);
        }
        else
        {
            preference.Value = feed.ToString();
            preference.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
