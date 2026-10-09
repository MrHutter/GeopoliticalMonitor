using GeopoliticalMonitor.Application.Preferences;
using GeopoliticalMonitor.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GeopoliticalMonitor.Infrastructure.Tests;

public sealed class SocialPanePersistenceTests : IDisposable
{
    private readonly string temporaryDirectory = Path.Combine(Path.GetTempPath(), "GeopoliticalMonitorTests", Guid.NewGuid().ToString("N"));
    private readonly string databasePath;

    public SocialPanePersistenceTests()
    {
        databasePath = Path.Combine(temporaryDirectory, "geopolitical-monitor-test.db");
    }

    [Fact]
    public void BlueskyProfileHasItsOwnUserDataFolderBesideX()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var provider = new LocalApplicationDataPathProvider(() => root);

        var blueskyFolder = provider.GetBlueskyBrowserUserDataFolder();

        Assert.Equal(Path.Combine(root, "GeopoliticalMonitor", "WebView2", "BlueskyBrowser"), blueskyFolder);
        Assert.Equal(Path.Combine(root, "GeopoliticalMonitor", "WebView2", "XBrowser"), provider.GetXBrowserUserDataFolder());
        Assert.NotEqual(provider.GetXBrowserUserDataFolder(), blueskyFolder, StringComparer.OrdinalIgnoreCase);
        Assert.False(blueskyFolder.StartsWith(provider.GetXBrowserUserDataFolder() + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        Assert.False(provider.GetXBrowserUserDataFolder().StartsWith(blueskyFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        Assert.NotEqual(provider.GetAppWebViewUserDataFolder(), blueskyFolder, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FeedDefaultsToXWhenNoPreferenceExists()
    {
        var service = await CreateMigratedServiceAsync();

        Assert.Equal(SocialPaneFeed.X, await service.GetSocialPaneFeedAsync());
    }

    [Theory]
    [InlineData(SocialPaneFeed.X)]
    [InlineData(SocialPaneFeed.Bluesky)]
    public async Task PersistsFeedInApplicationPreferencesTableAcrossContexts(SocialPaneFeed feed)
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await initializer.InitializeAsync();
        await CreateService(initializer).SaveSocialPaneFeedAsync(feed);

        await using (var dbContext = initializer.CreateDbContext())
        {
            var storedValue = await dbContext.ApplicationPreferences
                .Where(item => item.Key == SocialPaneFeedPreferenceService.PreferenceKey)
                .Select(item => item.Value)
                .SingleAsync();
            Assert.Equal(feed.ToString(), storedValue);
        }

        Assert.Equal(feed, await CreateService(new LocalDatabaseInitializer(databasePath)).GetSocialPaneFeedAsync());
    }

    [Fact]
    public async Task SwitchingFeedDoesNotWriteSideOrWidthPreferences()
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await initializer.InitializeAsync();
        var service = CreateService(initializer);

        await service.SaveSocialPaneFeedAsync(SocialPaneFeed.Bluesky);
        await service.SaveSocialPaneFeedAsync(SocialPaneFeed.X);

        await using var dbContext = initializer.CreateDbContext();
        var keys = await dbContext.ApplicationPreferences.Select(item => item.Key).ToListAsync();
        Assert.Equal([SocialPaneFeedPreferenceService.PreferenceKey], keys);
    }

    [Fact]
    public async Task CorruptStoredFeedFallsBackToX()
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await initializer.InitializeAsync();
        await using (var dbContext = initializer.CreateDbContext())
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "INSERT INTO ApplicationPreferences (Key, Value, UpdatedAt) VALUES ({0}, {1}, {2})",
                SocialPaneFeedPreferenceService.PreferenceKey,
                "Mastodon",
                DateTimeOffset.UtcNow);
        }

        Assert.Equal(SocialPaneFeed.X, await CreateService(initializer).GetSocialPaneFeedAsync());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private async Task<SocialPaneFeedPreferenceService> CreateMigratedServiceAsync()
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await initializer.InitializeAsync();
        return CreateService(initializer);
    }

    private static SocialPaneFeedPreferenceService CreateService(LocalDatabaseInitializer initializer)
    {
        return new SocialPaneFeedPreferenceService(new SqliteSocialPaneFeedPreferenceStore(initializer.CreateDbContext));
    }
}
