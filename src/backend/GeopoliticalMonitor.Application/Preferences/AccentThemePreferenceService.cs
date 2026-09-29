namespace GeopoliticalMonitor.Application.Preferences;

public sealed class AccentThemePreferenceService(IAccentThemePreferenceStore store)
{
    public const string PreferenceKey = "AccentTheme";

    public async Task<AccentTheme> GetAccentThemeAsync(CancellationToken cancellationToken = default)
    {
        return await store.GetAccentThemeAsync(cancellationToken).ConfigureAwait(false) ?? AccentTheme.Orange;
    }

    public async Task<AccentTheme> SaveAccentThemeAsync(AccentTheme accentTheme, CancellationToken cancellationToken = default)
    {
        await store.SaveAccentThemeAsync(accentTheme, cancellationToken).ConfigureAwait(false);
        return accentTheme;
    }

    public static bool TryParsePreferenceKey(string? key)
    {
        return string.Equals(key, PreferenceKey, StringComparison.Ordinal);
    }

    public static bool TryParseAccentTheme(string? value, out AccentTheme accentTheme)
    {
        if (Enum.TryParse(value, ignoreCase: false, out accentTheme) && Enum.IsDefined(accentTheme))
        {
            return true;
        }

        accentTheme = AccentTheme.Orange;
        return false;
    }
}
