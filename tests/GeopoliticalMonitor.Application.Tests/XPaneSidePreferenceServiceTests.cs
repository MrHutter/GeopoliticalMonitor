using GeopoliticalMonitor.Application.Preferences;
using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class XPaneSidePreferenceServiceTests
{
    [Fact]
    public async Task DefaultsToRightWhenNoPreferenceIsStored()
    {
        var service = new XPaneSidePreferenceService(new InMemoryXPaneSidePreferenceStore(null));

        Assert.Equal(XPaneSide.Right, await service.GetXPaneSideAsync());
    }

    [Theory]
    [InlineData("Left", XPaneSide.Left)]
    [InlineData("Right", XPaneSide.Right)]
    public async Task RestoresValidStoredSide(string storedValue, XPaneSide expected)
    {
        var service = new XPaneSidePreferenceService(new InMemoryXPaneSidePreferenceStore(storedValue));

        Assert.Equal(expected, await service.GetXPaneSideAsync());
    }

    [Theory]
    [InlineData("left")]
    [InlineData("RIGHT")]
    [InlineData("Center")]
    [InlineData("")]
    public async Task InvalidStoredSideFallsBackToRight(string storedValue)
    {
        var service = new XPaneSidePreferenceService(new InMemoryXPaneSidePreferenceStore(storedValue));

        Assert.Equal(XPaneSide.Right, await service.GetXPaneSideAsync());
    }

    [Fact]
    public async Task SavesSideWithoutChangingWidthPreference()
    {
        var store = new InMemoryXPaneSidePreferenceStore(null);
        var service = new XPaneSidePreferenceService(store);

        await service.SaveXPaneSideAsync(XPaneSide.Left);

        Assert.Equal(XPaneSide.Left, store.SavedSide);
        Assert.Equal(1, store.SaveCount);
    }

    [Theory]
    [InlineData(XPaneSide.Right, MainContentOrder.EventsThenMap)]
    [InlineData(XPaneSide.Left, MainContentOrder.MapThenEvents)]
    public void LayoutPolicyKeepsMapBetweenXAndEvents(XPaneSide side, MainContentOrder expectedOrder)
    {
        var arrangement = XPaneLayoutPolicy.Arrange(side);

        Assert.Equal(side, arrangement.XPaneSide);
        Assert.Equal(expectedOrder, arrangement.MainContentOrder);
        Assert.True(arrangement.DetailsSpanMapAndEvents);
    }

    [Fact]
    public void CollapseDoesNotOverwriteSideOrWidth()
    {
        var collapseState = XPaneLayout.Collapse(640, 520, 1920);
        var arrangement = XPaneLayoutPolicy.Arrange(XPaneSide.Left);

        Assert.False(collapseState.ShouldPersist);
        Assert.Equal(640, collapseState.RememberedWidth);
        Assert.Equal(XPaneSide.Left, arrangement.XPaneSide);
    }

    [Fact]
    public void ReopenRestoresSideAndWidthPolicyIndependently()
    {
        var reopenedWidth = XPaneLayout.Reopen(640, 1920);
        var arrangement = XPaneLayoutPolicy.Arrange(XPaneSide.Left);

        Assert.Equal(640, reopenedWidth);
        Assert.Equal(MainContentOrder.MapThenEvents, arrangement.MainContentOrder);
    }

    private sealed class InMemoryXPaneSidePreferenceStore(string? value) : IXPaneSidePreferenceStore
    {
        public int SaveCount { get; private set; }

        public XPaneSide? SavedSide { get; private set; }

        public Task<string?> GetXPaneSideValueAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(value);
        }

        public Task SaveXPaneSideValueAsync(XPaneSide side, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            SavedSide = side;
            value = side.ToString();
            return Task.CompletedTask;
        }
    }
}
