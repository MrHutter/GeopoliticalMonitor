namespace GeopoliticalMonitor.Application.XBrowser;

public interface IXPostLinkClipboard
{
    Task SetTextAsync(string text, CancellationToken cancellationToken = default);
}
