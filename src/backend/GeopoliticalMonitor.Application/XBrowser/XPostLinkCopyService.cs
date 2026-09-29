namespace GeopoliticalMonitor.Application.XBrowser;

public sealed class XPostLinkCopyService(IXPostLinkClipboard clipboard)
{
    public async Task<XPostLinkCopyResult> CopyIfValidAsync(string? url, CancellationToken cancellationToken = default)
    {
        if (!XPostUrl.TryNormalize(url, out var normalizedUrl))
        {
            return XPostLinkCopyResult.Rejected;
        }

        await clipboard.SetTextAsync(normalizedUrl, cancellationToken).ConfigureAwait(false);
        return new XPostLinkCopyResult(true, normalizedUrl);
    }
}

public sealed record XPostLinkCopyResult(bool Copied, string? NormalizedUrl)
{
    public static XPostLinkCopyResult Rejected { get; } = new(false, null);
}
