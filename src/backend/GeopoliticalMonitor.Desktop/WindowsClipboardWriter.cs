using GeopoliticalMonitor.Application.XBrowser;
using Windows.ApplicationModel.DataTransfer;

namespace GeopoliticalMonitor.Desktop;

public sealed class WindowsClipboardWriter : IXPostLinkClipboard
{
    public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
        return Task.CompletedTask;
    }
}
