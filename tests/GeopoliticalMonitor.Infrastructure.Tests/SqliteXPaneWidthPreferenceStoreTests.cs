using GeopoliticalMonitor.Application.Preferences;
using GeopoliticalMonitor.Application.XBrowser;
using GeopoliticalMonitor.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GeopoliticalMonitor.Infrastructure.Tests;

public sealed class SqliteXPaneWidthPreferenceStoreTests : IDisposable
{
    private readonly string temporaryDirectory = Path.Combine(Path.GetTempPath(), "GeopoliticalMonitorTests", Guid.NewGuid().ToString("N"));
    private readonly string databasePath;

    public SqliteXPaneWidthPreferenceStoreTests()
    {
        databasePath = Path.Combine(temporaryDirectory, "geopolitical-monitor-test.db");
    }

    [Fact]
    public async Task ReturnsDefaultWidthWhenNoPreferenceExists()
    {
        var service = await CreateMigratedServiceAsync();

        var width = await service.GetXPaneWidthAsync(1920);

        Assert.Equal(XPaneLayout.GetDefaultPaneWidth(1920), width);
    }

    [Fact]
    public async Task PersistsWidthInApplicationPreferencesTable()
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await initializer.InitializeAsync();
        var service = CreateService(initializer);

        await service.SaveXPaneWidthAsync(640, 1920);

        await using var dbContext = initializer.CreateDbContext();
        var storedValue = await dbContext.ApplicationPreferences
            .Where(item => item.Key == XPaneWidthPreferenceService.PreferenceKey)
            .Select(item => item.Value)
            .SingleAsync();
        Assert.Equal("640.00", storedValue);
    }

    [Fact]
    public async Task RestoresPersistedWidthAcrossDbContextInstances()
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await initializer.InitializeAsync();
        var firstService = CreateService(initializer);
        await firstService.SaveXPaneWidthAsync(640, 1920);

        var secondInitializer = new LocalDatabaseInitializer(databasePath);
        var secondService = CreateService(secondInitializer);

        Assert.Equal(640, await secondService.GetXPaneWidthAsync(1920));
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-1")]
    public async Task CorruptStoredWidthFallsBackToDefault(string storedValue)
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await initializer.InitializeAsync();
        await using (var dbContext = initializer.CreateDbContext())
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "INSERT INTO ApplicationPreferences (Key, Value, UpdatedAt) VALUES ({0}, {1}, {2})",
                XPaneWidthPreferenceService.PreferenceKey,
                storedValue,
                DateTimeOffset.UtcNow);
        }

        var service = CreateService(initializer);

        Assert.Equal(XPaneLayout.GetDefaultPaneWidth(1920), await service.GetXPaneWidthAsync(1920));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private async Task<XPaneWidthPreferenceService> CreateMigratedServiceAsync()
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await initializer.InitializeAsync();
        return CreateService(initializer);
    }

    private static XPaneWidthPreferenceService CreateService(LocalDatabaseInitializer initializer)
    {
        var store = new SqliteXPaneWidthPreferenceStore(initializer.CreateDbContext);
        return new XPaneWidthPreferenceService(store);
    }
}
