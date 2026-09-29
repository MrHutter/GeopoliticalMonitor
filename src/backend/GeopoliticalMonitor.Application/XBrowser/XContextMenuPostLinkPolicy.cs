namespace GeopoliticalMonitor.Application.XBrowser;

public static class XContextMenuPostLinkPolicy
{
    public static bool TryCreateCopyPostLinkCommand(string? contextLinkUri, out string normalizedUrl)
    {
        return XPostUrl.TryNormalize(contextLinkUri, out normalizedUrl);
    }
}