using System.Globalization;
using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Application.Preferences;

public sealed class XPaneWidthPreferenceService(IXPaneWidthPreferenceStore store)
{
    public const string PreferenceKey = "XPaneWidth";

    public async Task<double> GetXPaneWidthAsync(double availableWidth, CancellationToken cancellationToken = default)
    {
        var width = await GetPreferredXPaneWidthAsync(availableWidth, cancellationToken).ConfigureAwait(false);
        return XPaneLayout.ClampPaneWidth(width, availableWidth);
    }

    public async Task<double> GetPreferredXPaneWidthAsync(double availableWidth, CancellationToken cancellationToken = default)
    {
        var value = await store.GetXPaneWidthValueAsync(cancellationToken).ConfigureAwait(false);
        return TryParseStoredWidth(value, out var width)
            ? width
            : XPaneLayout.GetDefaultPaneWidth(availableWidth);
    }

    public async Task<double> SaveXPaneWidthAsync(double width, double availableWidth, CancellationToken cancellationToken = default)
    {
        var clampedWidth = XPaneLayout.ClampPaneWidth(width, availableWidth);
        await store.SaveXPaneWidthValueAsync(clampedWidth, cancellationToken).ConfigureAwait(false);
        return clampedWidth;
    }

    public static bool TryParseStoredWidth(string? value, out double width)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out width))
        {
            width = 0;
            return false;
        }

        if (!double.IsFinite(width) || width <= 0)
        {
            width = 0;
            return false;
        }

        return true;
    }
}
