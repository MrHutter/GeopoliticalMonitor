namespace GeopoliticalMonitor.Application.Preferences;

public interface IXPaneSidePreferenceStore
{
    Task<string?> GetXPaneSideValueAsync(CancellationToken cancellationToken = default);

    Task SaveXPaneSideValueAsync(XPaneSide side, CancellationToken cancellationToken = default);
}
