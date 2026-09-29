namespace GeopoliticalMonitor.Application.Preferences;

public interface IXPaneWidthPreferenceStore
{
    Task<string?> GetXPaneWidthValueAsync(CancellationToken cancellationToken = default);

    Task SaveXPaneWidthValueAsync(double width, CancellationToken cancellationToken = default);
}
