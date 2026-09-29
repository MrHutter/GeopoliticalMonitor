using GeopoliticalMonitor.Application.Preferences;
using GeopoliticalMonitor.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace GeopoliticalMonitor.Infrastructure.Persistence;

public sealed class SqliteXPaneSidePreferenceStore(Func<GeopoliticalMonitorDbContext> createDbContext) : IXPaneSidePreferenceStore
{
    public async Task<string?> GetXPaneSideValueAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        var preference = await dbContext.ApplicationPreferences
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Key == XPaneSidePreferenceService.PreferenceKey, cancellationToken)
            .ConfigureAwait(false);

        return preference?.Value;
    }

    public async Task SaveXPaneSideValueAsync(XPaneSide side, CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        var preference = await dbContext.ApplicationPreferences
            .SingleOrDefaultAsync(item => item.Key == XPaneSidePreferenceService.PreferenceKey, cancellationToken)
            .ConfigureAwait(false);

        if (preference is null)
        {
            preference = new ApplicationPreferenceEntity
            {
                Key = XPaneSidePreferenceService.PreferenceKey,
                Value = side.ToString(),
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            dbContext.ApplicationPreferences.Add(preference);
        }
        else
        {
            preference.Value = side.ToString();
            preference.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
