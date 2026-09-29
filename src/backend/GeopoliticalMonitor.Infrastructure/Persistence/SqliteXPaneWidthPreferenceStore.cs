using System.Globalization;
using GeopoliticalMonitor.Application.Preferences;
using GeopoliticalMonitor.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace GeopoliticalMonitor.Infrastructure.Persistence;

public sealed class SqliteXPaneWidthPreferenceStore(Func<GeopoliticalMonitorDbContext> createDbContext) : IXPaneWidthPreferenceStore
{
    public async Task<string?> GetXPaneWidthValueAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        var preference = await dbContext.ApplicationPreferences
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Key == XPaneWidthPreferenceService.PreferenceKey, cancellationToken)
            .ConfigureAwait(false);

        return preference?.Value;
    }

    public async Task SaveXPaneWidthValueAsync(double width, CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        var preference = await dbContext.ApplicationPreferences
            .SingleOrDefaultAsync(item => item.Key == XPaneWidthPreferenceService.PreferenceKey, cancellationToken)
            .ConfigureAwait(false);

        var value = width.ToString("F2", CultureInfo.InvariantCulture);
        if (preference is null)
        {
            preference = new ApplicationPreferenceEntity
            {
                Key = XPaneWidthPreferenceService.PreferenceKey,
                Value = value,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            dbContext.ApplicationPreferences.Add(preference);
        }
        else
        {
            preference.Value = value;
            preference.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
