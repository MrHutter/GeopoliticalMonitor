namespace GeopoliticalMonitor.Application.Preferences;

public sealed class XPaneSidePreferenceService(IXPaneSidePreferenceStore store)
{
    public const string PreferenceKey = "XPaneSide";

    public async Task<XPaneSide> GetXPaneSideAsync(CancellationToken cancellationToken = default)
    {
        var value = await store.GetXPaneSideValueAsync(cancellationToken).ConfigureAwait(false);
        return TryParseXPaneSide(value, out var side) ? side : XPaneSide.Right;
    }

    public async Task<XPaneSide> SaveXPaneSideAsync(XPaneSide side, CancellationToken cancellationToken = default)
    {
        await store.SaveXPaneSideValueAsync(side, cancellationToken).ConfigureAwait(false);
        return side;
    }

    public static bool TryParseXPaneSide(string? value, out XPaneSide side)
    {
        if (string.Equals(value, nameof(XPaneSide.Left), StringComparison.Ordinal))
        {
            side = XPaneSide.Left;
            return true;
        }

        if (string.Equals(value, nameof(XPaneSide.Right), StringComparison.Ordinal))
        {
            side = XPaneSide.Right;
            return true;
        }

        side = XPaneSide.Right;
        return false;
    }
}
