using GeopoliticalMonitor.Application.Preferences;
using GeopoliticalMonitor.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace GeopoliticalMonitor.Infrastructure.Persistence;

public sealed class SqliteAccentThemePreferenceStore(Func<GeopoliticalMonitorDbContext> createDbContext) : IAccentThemePreferenceStore
{
    public async Task<AccentTheme?> GetAccentThemeAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        var preference = await dbContext.ApplicationPreferences
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Key == AccentThemePreferenceService.PreferenceKey, cancellationToken)
            .ConfigureAwait(false);

        if (preference is null)
        {
            return null;
        }

        if (!AccentThemePreferenceService.TryParseAccentTheme(preference.Value, out var accentTheme))
        {
            throw new InvalidOperationException("Stored AccentTheme preference is invalid.");
        }

        return accentTheme;
    }

    public async Task SaveAccentThemeAsync(AccentTheme accentTheme, CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        var preference = await dbContext.ApplicationPreferences
            .SingleOrDefaultAsync(item => item.Key == AccentThemePreferenceService.PreferenceKey, cancellationToken)
            .ConfigureAwait(false);

        if (preference is null)
        {
            preference = new ApplicationPreferenceEntity
            {
                Key = AccentThemePreferenceService.PreferenceKey,
                Value = accentTheme.ToString(),
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            dbContext.ApplicationPreferences.Add(preference);
        }
        else
        {
            preference.Value = accentTheme.ToString();
            preference.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
