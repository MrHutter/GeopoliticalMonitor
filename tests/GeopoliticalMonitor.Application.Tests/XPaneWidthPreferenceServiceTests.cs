using GeopoliticalMonitor.Application.Preferences;
using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class XPaneWidthPreferenceServiceTests
{
    private const double DesktopWidth = 1920;

    [Fact]
    public async Task ReturnsDefaultWidthWhenNoPreferenceIsStored()
    {
        var service = new XPaneWidthPreferenceService(new InMemoryXPaneWidthPreferenceStore(null));

        var width = await service.GetXPaneWidthAsync(DesktopWidth);

        Assert.Equal(518.4, width, precision: 1);
    }

    [Fact]
    public async Task RestoresValidStoredWidth()
    {
        var service = new XPaneWidthPreferenceService(new InMemoryXPaneWidthPreferenceStore("640"));

        var width = await service.GetXPaneWidthAsync(DesktopWidth);

        Assert.Equal(640, width);
    }

    [Fact]
    public async Task ClampsTooSmallStoredWidth()
    {
        var service = new XPaneWidthPreferenceService(new InMemoryXPaneWidthPreferenceStore("120"));

        var width = await service.GetXPaneWidthAsync(DesktopWidth);

        Assert.Equal(XBrowserDefaults.MinimumPaneWidth, width);
    }

    [Fact]
    public async Task ClampsTooLargeStoredWidth()
    {
        var service = new XPaneWidthPreferenceService(new InMemoryXPaneWidthPreferenceStore("5000"));

        var width = await service.GetXPaneWidthAsync(DesktopWidth);

        Assert.Equal(XPaneLayout.GetMaximumPaneWidth(DesktopWidth), width);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("0")]
    [InlineData("-20")]
    public async Task InvalidStoredWidthFallsBackToDefault(string storedValue)
    {
        var service = new XPaneWidthPreferenceService(new InMemoryXPaneWidthPreferenceStore(storedValue));

        var width = await service.GetXPaneWidthAsync(DesktopWidth);

        Assert.Equal(XPaneLayout.GetDefaultPaneWidth(DesktopWidth), width);
    }

    [Fact]
    public async Task SavePersistsOnlyTheClampedFinalWidth()
    {
        var store = new InMemoryXPaneWidthPreferenceStore(null);
        var service = new XPaneWidthPreferenceService(store);

        var savedWidth = await service.SaveXPaneWidthAsync(5000, DesktopWidth);

        Assert.Equal(XPaneLayout.GetMaximumPaneWidth(DesktopWidth), savedWidth);
        Assert.Equal(1, store.SaveCount);
        Assert.Equal(savedWidth, store.SavedWidth);
    }

    [Fact]
    public void CollapseDoesNotRequestPreferencePersistence()
    {
        var collapseState = XPaneLayout.Collapse(700, 640, DesktopWidth);

        Assert.False(collapseState.ShouldPersist);
        Assert.Equal(700, collapseState.RememberedWidth);
    }

    [Fact]
    public void ReopenRestoresRememberedWidthWithinCurrentWindowLimits()
    {
        var restored = XPaneLayout.Reopen(700, DesktopWidth);

        Assert.Equal(700, restored);
    }

    [Theory]
    [InlineData(XPaneSide.Right)]
    [InlineData(XPaneSide.Left)]
    public void MaximumPaneWidthPreservesMainApplicationMinimumOnEitherSide(XPaneSide side)
    {
        var arrangement = XPaneLayoutPolicy.Arrange(side);
        var maximumXWidth = XPaneLayout.GetMaximumPaneWidth(DesktopWidth);
        var mainWidth = XPaneLayout.GetMainPaneWidth(maximumXWidth, DesktopWidth);

        Assert.Equal(side, arrangement.XPaneSide);
        Assert.Equal(XBrowserDefaults.MinimumMainPaneWidth, mainWidth);
        Assert.Equal(Math.Min(DesktopWidth * XBrowserDefaults.MaximumPaneWidthRatio, DesktopWidth - XBrowserDefaults.MinimumMainPaneWidth - XBrowserDefaults.SplitterWidth), maximumXWidth);
    }

    [Fact]
    public async Task OversizedStoredWidthRemainsPreferredButRenderedWidthIsClamped()
    {
        var service = new XPaneWidthPreferenceService(new InMemoryXPaneWidthPreferenceStore("1200"));
        const double smallerWindow = 1300;

        var preferredWidth = await service.GetPreferredXPaneWidthAsync(smallerWindow);
        var renderedWidth = await service.GetXPaneWidthAsync(smallerWindow);

        Assert.Equal(1200, preferredWidth);
        Assert.Equal(XPaneLayout.GetMaximumPaneWidth(smallerWindow), renderedWidth);
        Assert.Equal(XBrowserDefaults.MinimumMainPaneWidth, XPaneLayout.GetMainPaneWidth(renderedWidth, smallerWindow));
    }

    private sealed class InMemoryXPaneWidthPreferenceStore(string? value) : IXPaneWidthPreferenceStore
    {
        public int SaveCount { get; private set; }

        public double SavedWidth { get; private set; }

        public Task<string?> GetXPaneWidthValueAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(value);
        }

        public Task SaveXPaneWidthValueAsync(double width, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            SavedWidth = width;
            value = width.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Task.CompletedTask;
        }
    }
}
