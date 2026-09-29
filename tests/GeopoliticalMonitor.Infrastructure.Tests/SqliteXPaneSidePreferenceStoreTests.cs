using GeopoliticalMonitor.Application.Preferences;
using GeopoliticalMonitor.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GeopoliticalMonitor.Infrastructure.Tests;

public sealed class SqliteXPaneSidePreferenceStoreTests : IDisposable
{
    private readonly string temporaryDirectory = Path.Combine(Path.GetTempPath(), "GeopoliticalMonitorTests", Guid.NewGuid().ToString("N"));
    private readonly string databasePath;

    public SqliteXPaneSidePreferenceStoreTests()
    {
        databasePath = Path.Combine(temporaryDirectory, "geopolitical-monitor-test.db");
    }

    [Fact]
    public async Task ReturnsRightByDefaultWhenNoPreferenceExists()
    {
        var service = await CreateMigratedServiceAsync();

        Assert.Equal(XPaneSide.Right, await service.GetXPaneSideAsync());
    }

    [Theory]
    [InlineData(XPaneSide.Left)]
    [InlineData(XPaneSide.Right)]
    public async Task PersistsSideInApplicationPreferencesTable(XPaneSide side)
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await initializer.InitializeAsync();
        var service = CreateService(initializer);

        await service.SaveXPaneSideAsync(side);

        await using var dbContext = initializer.CreateDbContext();
        var storedValue = await dbContext.ApplicationPreferences
            .Where(item => item.Key == XPaneSidePreferenceService.PreferenceKey)
            .Select(item => item.Value)
            .SingleAsync();
        Assert.Equal(side.ToString(), storedValue);
    }

    [Fact]
    public async Task RestoresPersistedSideAcrossDbContextInstances()
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await initializer.InitializeAsync();
        var firstService = CreateService(initializer);
        await firstService.SaveXPaneSideAsync(XPaneSide.Left);

        var secondInitializer = new LocalDatabaseInitializer(databasePath);
        var secondService = CreateService(secondInitializer);

        Assert.Equal(XPaneSide.Left, await secondService.GetXPaneSideAsync());
    }

    [Fact]
    public async Task CorruptStoredSideFallsBackToRight()
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await initializer.InitializeAsync();
        await using (var dbContext = initializer.CreateDbContext())
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "INSERT INTO ApplicationPreferences (Key, Value, UpdatedAt) VALUES ({0}, {1}, {2})",
                XPaneSidePreferenceService.PreferenceKey,
                "Center",
                DateTimeOffset.UtcNow);
        }

        var service = CreateService(initializer);

        Assert.Equal(XPaneSide.Right, await service.GetXPaneSideAsync());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private async Task<XPaneSidePreferenceService> CreateMigratedServiceAsync()
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await initializer.InitializeAsync();
        return CreateService(initializer);
    }

    private static XPaneSidePreferenceService CreateService(LocalDatabaseInitializer initializer)
    {
        var store = new SqliteXPaneSidePreferenceStore(initializer.CreateDbContext);
        return new XPaneSidePreferenceService(store);
    }
}
