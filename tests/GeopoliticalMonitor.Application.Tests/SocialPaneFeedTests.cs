using GeopoliticalMonitor.Application.DesktopBridge;
using GeopoliticalMonitor.Application.Preferences;
using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class SocialPaneFeedPreferenceServiceTests
{
    [Fact]
    public async Task DefaultsToXWhenNoPreferenceIsStored()
    {
        var service = new SocialPaneFeedPreferenceService(new InMemorySocialPaneFeedPreferenceStore(null));

        Assert.Equal(SocialPaneFeed.X, await service.GetSocialPaneFeedAsync());
    }

    [Theory]
    [InlineData("X", SocialPaneFeed.X)]
    [InlineData("Bluesky", SocialPaneFeed.Bluesky)]
    public async Task RestoresValidStoredFeed(string storedValue, SocialPaneFeed expected)
    {
        var service = new SocialPaneFeedPreferenceService(new InMemorySocialPaneFeedPreferenceStore(storedValue));

        Assert.Equal(expected, await service.GetSocialPaneFeedAsync());
    }

    [Theory]
    [InlineData("bluesky")]
    [InlineData("BLUESKY")]
    [InlineData("x")]
    [InlineData("Mastodon")]
    [InlineData("1")]
    [InlineData("")]
    [InlineData(" ")]
    public async Task UnknownStoredFeedFallsBackToX(string storedValue)
    {
        var service = new SocialPaneFeedPreferenceService(new InMemorySocialPaneFeedPreferenceStore(storedValue));

        Assert.Equal(SocialPaneFeed.X, await service.GetSocialPaneFeedAsync());
    }

    [Theory]
    [InlineData(SocialPaneFeed.X)]
    [InlineData(SocialPaneFeed.Bluesky)]
    public async Task SavesTheChosenFeed(SocialPaneFeed feed)
    {
        var store = new InMemorySocialPaneFeedPreferenceStore(null);
        var service = new SocialPaneFeedPreferenceService(store);

        Assert.Equal(feed, await service.SaveSocialPaneFeedAsync(feed));
        Assert.Equal(feed, store.SavedFeed);
        Assert.Equal(feed, await service.GetSocialPaneFeedAsync());
    }

    [Fact]
    public void PreferenceKeyIsNativeOnlyAndNotABridgePreferenceKey()
    {
        Assert.Equal("SocialPaneFeed", SocialPaneFeedPreferenceService.PreferenceKey);
        Assert.False(AccentThemePreferenceService.TryParsePreferenceKey(SocialPaneFeedPreferenceService.PreferenceKey));
        Assert.True(AccentThemePreferenceService.TryParsePreferenceKey(AccentThemePreferenceService.PreferenceKey));
    }

    [Theory]
    [InlineData("""{"type":"preferences.set","payload":{"key":"SocialPaneFeed","value":"Bluesky"}}""")]
    [InlineData("""{"type":"preferences.set","payload":{"key":"SocialPaneFeed","value":"Blue"}}""")]
    [InlineData("""{"type":"preferences.get","payload":{"key":"SocialPaneFeed"}}""")]
    [InlineData("""{"type":"preferences.set","payload":{"key":"XPaneSide","value":"Left"}}""")]
    public void BridgeStillAcceptsOnlyTheAccentThemePreferenceKey(string json)
    {
        Assert.False(DesktopBridgeProtocol.TryParseRequest(json, out _, out _));
    }

    private sealed class InMemorySocialPaneFeedPreferenceStore(string? value) : ISocialPaneFeedPreferenceStore
    {
        private string? storedValue = value;

        public SocialPaneFeed? SavedFeed { get; private set; }

        public Task<string?> GetSocialPaneFeedValueAsync(CancellationToken cancellationToken = default) => Task.FromResult(storedValue);

        public Task SaveSocialPaneFeedValueAsync(SocialPaneFeed feed, CancellationToken cancellationToken = default)
        {
            SavedFeed = feed;
            storedValue = feed.ToString();
            return Task.CompletedTask;
        }
    }
}

public sealed class BlueskyBrowserNavigationPolicyTests
{
    [Theory]
    [InlineData("https://bsky.app/")]
    [InlineData("https://bsky.app")]
    [InlineData("https://BSKY.APP/profile/example.bsky.social")]
    [InlineData("https://bsky.app/profile/did:plc:abc123/post/3k2a4b5c6d7e2")]
    [InlineData("https://bsky.app:443/settings")]
    public void AllowsHttpsPagesOnTheExactHostInsidePane(string uri)
    {
        Assert.Equal(XBrowserNavigationDecision.AllowInPane, BlueskyBrowserNavigationPolicy.ClassifyTopLevelNavigation(uri));
        Assert.True(BlueskyBrowserNavigationPolicy.IsAllowedInPane(uri));
    }

    [Theory]
    [InlineData("https://bsky.app.evil.test/")]
    [InlineData("https://notbsky.app/")]
    [InlineData("https://www.bsky.app/")]
    [InlineData("https://staging.bsky.app/")]
    [InlineData("https://bsky.social/oauth/authorize")]
    [InlineData("https://x.com/home")]
    [InlineData("https://example.com/")]
    [InlineData("https://bsky.app@evil.test/")]
    public void OpensOtherHttpsPagesInTheSystemBrowser(string uri)
    {
        Assert.Equal(XBrowserNavigationDecision.OpenExternally, BlueskyBrowserNavigationPolicy.ClassifyTopLevelNavigation(uri));
        Assert.False(BlueskyBrowserNavigationPolicy.IsAllowedInPane(uri));
    }

    [Theory]
    [InlineData("http://bsky.app/")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/temp/bsky.html")]
    [InlineData("data:text/html,hello")]
    [InlineData("bluesky://profile")]
    [InlineData("about:blank")]
    [InlineData("/profile/example")]
    [InlineData("bsky.app")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void BlocksEverythingElse(string? uri)
    {
        Assert.Equal(XBrowserNavigationDecision.Block, BlueskyBrowserNavigationPolicy.ClassifyTopLevelNavigation(uri));
        Assert.False(BlueskyBrowserNavigationPolicy.IsAllowedInPane(uri));
    }

    [Fact]
    public void XAndBlueskyPoliciesDoNotAllowEachOthersHosts()
    {
        Assert.False(XBrowserNavigationPolicy.IsAllowedInPane(BlueskyBrowserDefaults.HomeUrl));
        Assert.False(BlueskyBrowserNavigationPolicy.IsAllowedInPane(XBrowserDefaults.HomeUrl));
    }

    [Fact]
    public void HomeUrlIsAllowedInPane()
    {
        Assert.Equal("https://bsky.app/", BlueskyBrowserDefaults.HomeUrl);
        Assert.True(BlueskyBrowserNavigationPolicy.IsAllowedInPane(BlueskyBrowserDefaults.HomeUrl));
    }
}

public sealed class SocialPaneCommandPolicyTests
{
    private static readonly XToolbarCommand[] AllCommands = Enum.GetValues<XToolbarCommand>();

    [Fact]
    public void HomeUrlPerFeed()
    {
        Assert.Equal("https://x.com/home", SocialPaneCommandPolicy.GetHomeUrl(SocialPaneFeed.X));
        Assert.Equal("https://bsky.app/", SocialPaneCommandPolicy.GetHomeUrl(SocialPaneFeed.Bluesky));
    }

    [Theory]
    [InlineData(SocialPaneFeed.X, "https://x.com/user/status/1", "https://x.com/user/status/1")]
    [InlineData(SocialPaneFeed.X, "https://bsky.app/", "https://x.com/home")]
    [InlineData(SocialPaneFeed.X, null, "https://x.com/home")]
    [InlineData(SocialPaneFeed.Bluesky, "https://bsky.app/profile/a.bsky.social", "https://bsky.app/profile/a.bsky.social")]
    [InlineData(SocialPaneFeed.Bluesky, "https://x.com/home", "https://bsky.app/")]
    [InlineData(SocialPaneFeed.Bluesky, "https://bsky.app.evil.test/", "https://bsky.app/")]
    [InlineData(SocialPaneFeed.Bluesky, null, "https://bsky.app/")]
    public void OpenExternalTargetPerFeed(SocialPaneFeed feed, string? currentUrl, string expected)
    {
        Assert.Equal(expected, SocialPaneCommandPolicy.GetOpenExternalTarget(feed, currentUrl));
    }

    [Fact]
    public void CopyPostLinkIsVisibleForXOnly()
    {
        Assert.True(SocialPaneCommandPolicy.IsVisible(SocialPaneFeed.X, XToolbarCommand.CopyPostLink));
        Assert.False(SocialPaneCommandPolicy.IsVisible(SocialPaneFeed.Bluesky, XToolbarCommand.CopyPostLink));
    }

    [Fact]
    public void EveryXCommandStaysVisibleForX()
    {
        Assert.All(AllCommands, command => Assert.True(SocialPaneCommandPolicy.IsVisible(SocialPaneFeed.X, command)));
    }

    [Fact]
    public void EachFeedHasItsOwnAdBlockerEntryAndStatusLine()
    {
        Assert.True(SocialPaneCommandPolicy.IsVisible(SocialPaneFeed.Bluesky, XToolbarCommand.AdBlocker));
        Assert.True(SocialPaneCommandPolicy.ShowsAdBlockIndication(SocialPaneFeed.X, SocialPaneFeed.X));
        Assert.False(SocialPaneCommandPolicy.ShowsAdBlockIndication(SocialPaneFeed.X, SocialPaneFeed.Bluesky));
        Assert.True(SocialPaneCommandPolicy.ShowsAdBlockIndication(SocialPaneFeed.Bluesky, SocialPaneFeed.Bluesky));
        Assert.False(SocialPaneCommandPolicy.ShowsAdBlockIndication(SocialPaneFeed.Bluesky, SocialPaneFeed.X));

        var ready = new XToolbarCommandState(XBrowserRuntimeState.Ready, false, false, "https://bsky.app/");
        Assert.True(SocialPaneCommandPolicy.IsEnabled(SocialPaneFeed.Bluesky, XToolbarCommand.AdBlocker, ready));
        Assert.False(SocialPaneCommandPolicy.IsEnabled(SocialPaneFeed.Bluesky, XToolbarCommand.AdBlocker, ready with { RuntimeState = XBrowserRuntimeState.Failed }));
    }

    [Theory]
    [InlineData(XBrowserRuntimeState.NotStarted)]
    [InlineData(XBrowserRuntimeState.Initializing)]
    [InlineData(XBrowserRuntimeState.Ready)]
    [InlineData(XBrowserRuntimeState.Failed)]
    public void XFeedKeepsTheExistingToolbarRules(XBrowserRuntimeState runtimeState)
    {
        foreach (var url in new[] { null, "https://x.com/home", "https://x.com/user/status/123", "https://bsky.app/" })
        {
            foreach (var canNavigate in new[] { false, true })
            {
                var state = new XToolbarCommandState(runtimeState, canNavigate, canNavigate, url);
                Assert.All(AllCommands, command => Assert.Equal(
                    XToolbarCommandStatePolicy.IsEnabled(command, state),
                    SocialPaneCommandPolicy.IsEnabled(SocialPaneFeed.X, command, state)));
            }
        }
    }

    [Fact]
    public void BlueskyCommandsActOnTheBlueskyControl()
    {
        var ready = new XToolbarCommandState(XBrowserRuntimeState.Ready, CanGoBack: true, CanGoForward: false, "https://bsky.app/profile/a.bsky.social/post/3k2a4b5c6d7e2");

        Assert.True(SocialPaneCommandPolicy.IsEnabled(SocialPaneFeed.Bluesky, XToolbarCommand.Back, ready));
        Assert.False(SocialPaneCommandPolicy.IsEnabled(SocialPaneFeed.Bluesky, XToolbarCommand.Forward, ready));
        Assert.True(SocialPaneCommandPolicy.IsEnabled(SocialPaneFeed.Bluesky, XToolbarCommand.Home, ready));
        Assert.True(SocialPaneCommandPolicy.IsEnabled(SocialPaneFeed.Bluesky, XToolbarCommand.Refresh, ready));
        Assert.True(SocialPaneCommandPolicy.IsEnabled(SocialPaneFeed.Bluesky, XToolbarCommand.OpenExternal, ready));
        Assert.True(SocialPaneCommandPolicy.IsEnabled(SocialPaneFeed.Bluesky, XToolbarCommand.ClearSession, ready));
        Assert.False(SocialPaneCommandPolicy.IsEnabled(SocialPaneFeed.Bluesky, XToolbarCommand.CopyPostLink, ready));
    }

    [Fact]
    public void BlueskyCommandsNeedAReadyControl()
    {
        var initializing = new XToolbarCommandState(XBrowserRuntimeState.Initializing, CanGoBack: true, CanGoForward: true, CurrentUrl: null);
        var failed = initializing with { RuntimeState = XBrowserRuntimeState.Failed };

        foreach (var command in new[] { XToolbarCommand.Back, XToolbarCommand.Forward, XToolbarCommand.Home, XToolbarCommand.Refresh, XToolbarCommand.ClearSession, XToolbarCommand.OpenExternal })
        {
            Assert.False(SocialPaneCommandPolicy.IsEnabled(SocialPaneFeed.Bluesky, command, initializing));
        }

        Assert.True(SocialPaneCommandPolicy.IsEnabled(SocialPaneFeed.Bluesky, XToolbarCommand.OpenExternal, failed));
    }

    [Fact]
    public void BlueskyOpenExternalIsDisabledForAnUnexpectedCurrentUrl()
    {
        var state = new XToolbarCommandState(XBrowserRuntimeState.Ready, false, false, "https://x.com/home");

        Assert.False(SocialPaneCommandPolicy.IsEnabled(SocialPaneFeed.Bluesky, XToolbarCommand.OpenExternal, state));
    }
}

public sealed class SocialPaneLabelsTests
{
    [Fact]
    public void LabelsAreNeutralAndPerFeed()
    {
        Assert.Equal("SOCIAL FEED", SocialPaneLabels.Eyebrow);
        Assert.Equal("X", SocialPaneLabels.GetSegmentName(SocialPaneFeed.X));
        Assert.Equal("Bluesky", SocialPaneLabels.GetSegmentName(SocialPaneFeed.Bluesky));
        Assert.Equal("Show X", SocialPaneLabels.GetShowText(SocialPaneFeed.X));
        Assert.Equal("Show Bluesky", SocialPaneLabels.GetShowText(SocialPaneFeed.Bluesky));
        Assert.Equal("Show X feed", SocialPaneLabels.GetSwitcherAccessibleName(SocialPaneFeed.X));
        Assert.Equal("Show Bluesky feed", SocialPaneLabels.GetSwitcherAccessibleName(SocialPaneFeed.Bluesky));
        Assert.Equal("Retry X", SocialPaneLabels.GetRetryText(SocialPaneFeed.X));
        Assert.Equal("Retry Bluesky", SocialPaneLabels.GetRetryText(SocialPaneFeed.Bluesky));
        Assert.Equal("Clear X session?", SocialPaneLabels.GetClearSessionTitle(SocialPaneFeed.X));
        Assert.Equal("Clear Bluesky session?", SocialPaneLabels.GetClearSessionTitle(SocialPaneFeed.Bluesky));
    }

    [Fact]
    public void PaneHeaderNoLongerHardCodesTheOldXLabels()
    {
        var desktop = Path.Combine(SocialPaneSourceGuard.FindRepositoryRoot(), "src", "backend", "GeopoliticalMonitor.Desktop");
        var xaml = File.ReadAllText(Path.Combine(desktop, "MainWindow.xaml"));

        Assert.DoesNotContain("MY X FEED", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("X Website", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Show X", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Retry X", xaml, StringComparison.Ordinal);
    }
}

public sealed class SocialPaneSourceGuardTests
{
    [Fact]
    public void BlueskyWiringUsesNoScriptBridgeCookieReadingOrApiHost()
    {
        foreach (var file in SocialPaneSourceGuard.BlueskyWiringFiles())
        {
            var source = File.ReadAllText(file);
            foreach (var forbidden in SocialPaneSourceGuard.ForbiddenInBlueskyWiring)
            {
                Assert.False(source.Contains(forbidden, StringComparison.OrdinalIgnoreCase), $"{Path.GetFileName(file)} references {forbidden}.");
            }
        }
    }

    [Fact]
    public void BlueskyPaneDeniesEveryPermissionRequest()
    {
        var source = File.ReadAllText(SocialPaneSourceGuard.BlueskyWiringFiles()[0]);

        Assert.Contains("core.PermissionRequested += OnBlueskyPermissionRequested;", source, StringComparison.Ordinal);
        Assert.Contains("args.State = CoreWebView2PermissionState.Deny;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CoreWebView2PermissionState.Allow", source, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyTheAppWebViewIsWiredToThePrivilegedMessageHandler()
    {
        var desktop = Path.Combine(SocialPaneSourceGuard.FindRepositoryRoot(), "src", "backend", "GeopoliticalMonitor.Desktop");
        var mainWindow = File.ReadAllText(Path.Combine(desktop, "MainWindow.xaml.cs"));

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(mainWindow, @"\.WebMessageReceived \+="));
        Assert.Contains("AppWebView.WebMessageReceived += OnWebMessageReceived;", mainWindow, StringComparison.Ordinal);
    }
}

internal static class SocialPaneSourceGuard
{
    public static readonly string[] ForbiddenInBlueskyWiring =
    [
        "AddScriptToExecuteOnDocumentCreatedAsync",
        "ExecuteScriptAsync",
        "WebMessageReceived",
        "AddHostObjectToScript",
        "PostWebMessage",
        "GetCookiesAsync",
        "OnWebMessageReceived",
        "PrivilegedBridgeMessageProcessor",
        "DispatchPrivilegedBridgeRequestAsync",
        "DesktopBridgeProtocol",
        "CallDevToolsProtocolMethod",
        "api.bsky.app",
        "public.api.bsky.app",
        "bsky.social",
        "xrpc",
        "hybrid",
    ];

    public static string[] BlueskyWiringFiles()
    {
        var desktop = Path.Combine(FindRepositoryRoot(), "src", "backend", "GeopoliticalMonitor.Desktop");
        return
        [
            Path.Combine(desktop, "MainWindow.SocialFeed.cs"),
            Path.Combine(desktop, "MainWindow.BlueskyAdBlock.cs"),
            Path.Combine(desktop, "MainWindow.BlueskyAdBlockIndication.cs"),
        ];
    }

    public static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "backend", "GeopoliticalMonitor.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the GeopoliticalMonitor repository root from the test output directory.");
    }
}
