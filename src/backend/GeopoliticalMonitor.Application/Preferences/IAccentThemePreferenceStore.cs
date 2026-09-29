namespace GeopoliticalMonitor.Application.Preferences;

public interface IAccentThemePreferenceStore
{
    Task<AccentTheme?> GetAccentThemeAsync(CancellationToken cancellationToken = default);

    Task SaveAccentThemeAsync(AccentTheme accentTheme, CancellationToken cancellationToken = default);
}
