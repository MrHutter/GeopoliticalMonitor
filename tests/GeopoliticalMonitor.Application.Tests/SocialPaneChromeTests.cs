using GeopoliticalMonitor.Application.Preferences;
using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class SocialPaneChromeTests
{
    private static readonly string Desktop = Path.Combine(SocialPaneSourceGuard.FindRepositoryRoot(), "src", "backend", "GeopoliticalMonitor.Desktop");

    [Theory]
    [InlineData(SocialPaneFeed.X, XPaneSide.Right, "Move X pane to the left side")]
    [InlineData(SocialPaneFeed.X, XPaneSide.Left, "Move X pane to the right side")]
    [InlineData(SocialPaneFeed.Bluesky, XPaneSide.Right, "Move Bluesky pane to the left side")]
    [InlineData(SocialPaneFeed.Bluesky, XPaneSide.Left, "Move Bluesky pane to the right side")]
    public void MoveTooltipNamesTheActiveFeedAndTheTargetSide(SocialPaneFeed feed, XPaneSide side, string expected)
    {
        Assert.Equal(expected, SocialPaneLabels.GetMoveTooltip(feed, side));
    }

    [Theory]
    [InlineData(SocialPaneFeed.X, "More X actions")]
    [InlineData(SocialPaneFeed.Bluesky, "More Bluesky actions")]
    public void OverflowTooltipNamesTheActiveFeed(SocialPaneFeed feed, string expected)
    {
        Assert.Equal(expected, SocialPaneLabels.GetOverflowTooltip(feed));
    }

    [Fact]
    public void MoveAndOverflowTooltipsComeOnlyFromSocialPaneLabels()
    {
        var xaml = File.ReadAllText(Path.Combine(Desktop, "MainWindow.xaml"));
        var mainWindow = File.ReadAllText(Path.Combine(Desktop, "MainWindow.xaml.cs"));
        var socialFeed = File.ReadAllText(Path.Combine(Desktop, "MainWindow.SocialFeed.cs"));

        foreach (var source in new[] { xaml, mainWindow, socialFeed })
        {
            Assert.DoesNotContain("Move X pane", source, StringComparison.Ordinal);
            Assert.DoesNotContain("More X actions", source, StringComparison.Ordinal);
        }

        Assert.Contains("ToolTipService.SetToolTip(XMoveSideButton, SocialPaneLabels.GetMoveTooltip(activeSocialFeed, xPaneSide));", mainWindow, StringComparison.Ordinal);
        Assert.Contains("ToolTipService.SetToolTip(XMoveSideButton, SocialPaneLabels.GetMoveTooltip(activeSocialFeed, xPaneSide));", socialFeed, StringComparison.Ordinal);
        Assert.Contains("ToolTipService.SetToolTip(XOverflowButton, SocialPaneLabels.GetOverflowTooltip(activeSocialFeed));", socialFeed, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SocialPaneFeed.X, SocialPaneFeed.X, false)]
    [InlineData(SocialPaneFeed.X, SocialPaneFeed.Bluesky, true)]
    [InlineData(SocialPaneFeed.Bluesky, SocialPaneFeed.X, true)]
    [InlineData(SocialPaneFeed.Bluesky, SocialPaneFeed.Bluesky, false)]
    public void OnlyTheActiveFeedIsAudible(SocialPaneFeed activeFeed, SocialPaneFeed controlFeed, bool hidden)
    {
        Assert.Equal(hidden, SocialPaneCommandPolicy.IsHiddenFeed(activeFeed, controlFeed));
    }

    [Fact]
    public void HiddenStateIsAppliedWhenEachSocialWebViewCoreBecomesReady()
    {
        var socialFeed = File.ReadAllText(Path.Combine(Desktop, "MainWindow.SocialFeed.cs"));
        var handler = Between(socialFeed, "private void OnSocialWebViewCoreInitialized(", "\n    }\n");

        Assert.Contains("XWebView.CoreWebView2Initialized += OnSocialWebViewCoreInitialized;", socialFeed, StringComparison.Ordinal);
        Assert.Contains("webView.CoreWebView2Initialized += OnSocialWebViewCoreInitialized;", socialFeed, StringComparison.Ordinal);
        Assert.Contains("SetSocialWebViewHidden(core, hidden: SocialPaneCommandPolicy.IsHiddenFeed(activeSocialFeed, controlFeed));", handler, StringComparison.Ordinal);
        // Visibility only: the handler never navigates, reloads or touches extensions.
        foreach (var forbidden in new[] { "Navigate", "Reload", "Extension", "AdBlock" })
        {
            Assert.DoesNotContain(forbidden, handler, StringComparison.Ordinal);
        }
    }

    private static string Between(string source, string start, string end)
    {
        var from = source.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"Missing '{start}'.");
        var to = source.IndexOf(end, from, StringComparison.Ordinal);
        Assert.True(to > from, $"Missing end after '{start}'.");
        return source[from..to];
    }
}
