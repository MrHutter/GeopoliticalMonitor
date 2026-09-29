using GeopoliticalMonitor.Domain.Watchlists;

namespace GeopoliticalMonitor.Domain.Tests;

public sealed class WatchlistDomainTests
{
    [Fact]
    public void GeneratedIdUsesRequiredOpaqueLocalFormat()
    {
        var id = WatchlistId.CreateNew();

        Assert.Matches("^wl-[0-9a-f]{32}$", id.Value);
        Assert.True(WatchlistId.TryParse(id.Value, out var parsed));
        Assert.Equal(id, parsed);
    }

    [Theory]
    [InlineData("wl-0123456789abcdef0123456789abcde")]
    [InlineData("wl-0123456789abcdef0123456789abcdef0")]
    [InlineData("wl-0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("xx-0123456789abcdef0123456789abcdef")]
    [InlineData("")]
    public void RejectsInvalidIds(string value)
    {
        Assert.False(WatchlistId.TryParse(value, out _));
        Assert.Throws<ArgumentException>(() => new WatchlistId(value));
    }

    [Fact]
    public void CreatesWatchlistAndNormalizesDisplayName()
    {
        var watchlist = Watchlist.Create(
            new WatchlistId("wl-0123456789abcdef0123456789abcdef"),
            "  Baltic\t  Security  ");

        Assert.Equal("Baltic Security", watchlist.Name);
        Assert.Equal("BALTIC SECURITY", Watchlist.CreateComparisonKey(watchlist.Name));
    }

    [Fact]
    public void NormalizesNameToUnicodeFormC()
    {
        var watchlist = Watchlist.Create(
            new WatchlistId("wl-0123456789abcdef0123456789abcdef"),
            "Cafe\u0301");

        Assert.Equal("Café", watchlist.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \t\r\n")]
    [InlineData("Control\0Name")]
    public void RejectsInvalidNames(string name)
    {
        Assert.Throws<ArgumentException>(() => Watchlist.Create(
            new WatchlistId("wl-0123456789abcdef0123456789abcdef"),
            name));
    }

    [Fact]
    public void RejectsNameOverEightyUtf16CodeUnits()
    {
        Assert.Throws<ArgumentException>(() => Watchlist.Create(
            new WatchlistId("wl-0123456789abcdef0123456789abcdef"),
            new string('a', Watchlist.MaximumNameLength + 1)));
    }

    [Fact]
    public void RenamePreservesIdentity()
    {
        var original = Watchlist.Create(
            new WatchlistId("wl-0123456789abcdef0123456789abcdef"),
            "Original");

        var renamed = original.Rename("Renamed");

        Assert.Equal(original.Id, renamed.Id);
        Assert.Equal("Renamed", renamed.Name);
    }
}
