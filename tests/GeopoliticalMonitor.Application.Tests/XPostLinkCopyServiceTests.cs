using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class XPostLinkCopyServiceTests
{
    [Theory]
    [InlineData("https://x.com/example/status/123456789", "https://x.com/example/status/123456789")]
    [InlineData("https://www.x.com/example/status/123456789", "https://x.com/example/status/123456789")]
    [InlineData("https://x.com/example/status/123456789?ref=share#fragment", "https://x.com/example/status/123456789")]
    public void NormalizesValidPublicStatusUrls(string url, string expected)
    {
        Assert.True(XPostUrl.TryNormalize(url, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void NormalizationAlsoExposesTheDecimalStatusId()
    {
        Assert.True(XPostUrl.TryNormalizeWithStatusId(
            "https://www.x.com/example/status/1759288472993702104?ref=share",
            out var result));

        Assert.Equal("https://x.com/example/status/1759288472993702104", result.NormalizedUrl);
        Assert.Equal("1759288472993702104", result.StatusId);
    }

    [Theory]
    [InlineData("https://x.com/home")]
    [InlineData("https://x.com/example")]
    [InlineData("https://example.com/example/status/123")]
    [InlineData("http://x.com/example/status/123")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,test")]
    [InlineData("file:///C:/test")]
    [InlineData("https://x.com/example/status/not-a-number")]
    public void RejectsNonPublicStatusUrls(string url)
    {
        Assert.False(XPostUrl.TryNormalize(url, out var normalized));
        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    public async Task CopiesCanonicalUrlForValidCurrentPost()
    {
        var clipboard = new InMemoryPostLinkClipboard();
        var service = new XPostLinkCopyService(clipboard);

        var result = await service.CopyIfValidAsync("https://www.x.com/example/status/123456789?utm=ignored#fragment");

        Assert.True(result.Copied);
        Assert.Equal("https://x.com/example/status/123456789", result.NormalizedUrl);
        Assert.Equal("https://x.com/example/status/123456789", clipboard.Text);
    }

    [Theory]
    [InlineData("https://x.com/home")]
    [InlineData("https://example.com/example/status/123")]
    public async Task InvalidUrlDoesNotWriteClipboard(string url)
    {
        var clipboard = new InMemoryPostLinkClipboard();
        var service = new XPostLinkCopyService(clipboard);

        var result = await service.CopyIfValidAsync(url);

        Assert.False(result.Copied);
        Assert.Null(result.NormalizedUrl);
        Assert.Null(clipboard.Text);
    }

    [Theory]
    [InlineData("https://x.com/example/status/123456789", true)]
    [InlineData("https://www.x.com/example/status/123456789", true)]
    [InlineData("https://x.com/home", false)]
    [InlineData("https://x.com/example", false)]
    [InlineData("https://example.com/example/status/123", false)]
    [InlineData("http://x.com/example/status/123", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html,test", false)]
    public void ContextMenuCopyCommandRequiresWebViewLinkMetadataWithValidStatusUrl(string linkUri, bool shouldOfferCommand)
    {
        var canOfferCommand = XContextMenuPostLinkPolicy.TryCreateCopyPostLinkCommand(linkUri, out var normalizedUrl);

        Assert.Equal(shouldOfferCommand, canOfferCommand);
        if (shouldOfferCommand)
        {
            Assert.Equal("https://x.com/example/status/123456789", normalizedUrl);
        }
        else
        {
            Assert.Equal(string.Empty, normalizedUrl);
        }
    }

    private sealed class InMemoryPostLinkClipboard : IXPostLinkClipboard
    {
        public string? Text { get; private set; }

        public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
        {
            Text = text;
            return Task.CompletedTask;
        }
    }
}
