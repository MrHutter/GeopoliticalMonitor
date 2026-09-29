using System.Text.Json;
using GeopoliticalMonitor.Application.DesktopBridge;
using GeopoliticalMonitor.Application.Watchlists;
using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Watchlists;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class WatchlistBridgeTests
{
    private const string WatchlistId = "wl-0123456789abcdef0123456789abcdef";

    [Theory]
    [InlineData("""{"type":"watchlists.list","payload":{}}""", "watchlists.list")]
    [InlineData("""{"type":"watchlists.create","payload":{"name":"Baltic"}}""", "watchlists.create")]
    [InlineData("""{"type":"watchlists.rename","payload":{"watchlistId":"wl-0123456789abcdef0123456789abcdef","name":"Baltic"}}""", "watchlists.rename")]
    [InlineData("""{"type":"watchlists.delete","payload":{"watchlistId":"wl-0123456789abcdef0123456789abcdef"}}""", "watchlists.delete")]
    [InlineData("""{"type":"watchlists.members.get","payload":{"watchlistId":"wl-0123456789abcdef0123456789abcdef"}}""", "watchlists.members.get")]
    [InlineData("""{"type":"watchlists.memberships.getForEvent","payload":{"eventId":"evt-one"}}""", "watchlists.memberships.getForEvent")]
    [InlineData("""{"type":"watchlists.members.set","payload":{"watchlistId":"wl-0123456789abcdef0123456789abcdef","eventId":"evt-one","isMember":true}}""", "watchlists.members.set")]
    public void ParsesEveryTypedWatchlistRequest(string json, string expectedType)
    {
        var accepted = DesktopBridgeProtocol.TryParseRequest(json, out var request, out var error);

        Assert.True(accepted, error);
        Assert.Equal(expectedType, request?.Type);
    }

    [Theory]
    [InlineData("""{"type":"watchlists.list","payload":{"all":true}}""")]
    [InlineData("""{"type":"watchlists.create","payload":{}}""")]
    [InlineData("""{"type":"watchlists.create","payload":{"name":42}}""")]
    [InlineData("""{"type":"watchlists.create","payload":{"name":"Baltic","description":"extra"}}""")]
    [InlineData("""{"type":"watchlists.rename","payload":{"watchlistId":"bad","name":"Baltic"}}""")]
    [InlineData("""{"type":"watchlists.delete","payload":{"watchlistId":42}}""")]
    [InlineData("""{"type":"watchlists.memberships.getForEvent","payload":{"eventId":"bad id"}}""")]
    [InlineData("""{"type":"watchlists.members.set","payload":{"watchlistId":"wl-0123456789abcdef0123456789abcdef","eventId":"evt-one"}}""")]
    [InlineData("""{"type":"watchlists.members.set","payload":{"watchlistId":"wl-0123456789abcdef0123456789abcdef","eventId":"evt-one","isMember":"yes"}}""")]
    public void RejectsMalformedOrBroadenedWatchlistRequests(string json)
    {
        Assert.False(DesktopBridgeProtocol.TryParseRequest(json, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void RejectsRawWatchlistNameOverEightyCharacters()
    {
        var json = JsonSerializer.Serialize(new
        {
            type = DesktopBridgeProtocol.WatchlistsCreateMessageType,
            payload = new { name = new string('a', 81) },
        });

        Assert.False(DesktopBridgeProtocol.TryParseRequest(json, out _, out _));
    }

    [Fact]
    public void SerializesBoundedWatchlistResponsesAndDispositions()
    {
        var summary = new WatchlistSummaryDto(WatchlistId, "Baltic", 1);
        using var list = JsonDocument.Parse(DesktopBridgeProtocol.CreateWatchlistsListResultResponseJson([summary]));
        using var members = JsonDocument.Parse(DesktopBridgeProtocol.CreateWatchlistMembersResultResponseJson(
            new WatchlistEventIdsResult(WatchlistLookupDisposition.Found, WatchlistId, ["evt-one"])));
        using var membership = JsonDocument.Parse(DesktopBridgeProtocol.CreateWatchlistMembershipSetResultResponseJson(
            new SetWatchlistMembershipResult(SetWatchlistMembershipDisposition.AlreadyMember, WatchlistId, "evt-one", true, 1)));

        Assert.Equal("watchlists.list.result", list.RootElement.GetProperty("type").GetString());
        Assert.Equal("Baltic", list.RootElement.GetProperty("payload").GetProperty("watchlists")[0].GetProperty("name").GetString());
        Assert.Equal("evt-one", members.RootElement.GetProperty("payload").GetProperty("eventIds")[0].GetString());
        Assert.Equal("AlreadyMember", membership.RootElement.GetProperty("payload").GetProperty("disposition").GetString());
    }

    [Fact]
    public async Task HandlerMapsStableDomainErrorsWithoutPersistenceDetails()
    {
        var handler = new WatchlistBridgeHandler(new WatchlistService(new StubStore
        {
            CreateResult = new CreateWatchlistResult(CreateWatchlistDisposition.NameConflict),
        }));
        var request = new DesktopBridgeRequest(
            DesktopBridgeProtocol.WatchlistsCreateMessageType,
            WatchlistName: "Baltic");

        using var response = JsonDocument.Parse(await handler.HandleAsync(request));

        Assert.Equal("watchlists.error", response.RootElement.GetProperty("type").GetString());
        Assert.Equal("NameConflict", response.RootElement.GetProperty("payload").GetProperty("code").GetString());
        Assert.DoesNotContain("SQL", response.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandlerMapsPersistenceFailureWithoutLeakingException()
    {
        var handler = new WatchlistBridgeHandler(new WatchlistService(new ThrowingStore()));
        var request = new DesktopBridgeRequest(DesktopBridgeProtocol.WatchlistsListMessageType);

        var json = await handler.HandleAsync(request);
        using var response = JsonDocument.Parse(json);

        Assert.Equal("PersistenceUnavailable", response.RootElement.GetProperty("payload").GetProperty("code").GetString());
        Assert.DoesNotContain("sensitive-database-path", json, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubStore : IWatchlistStore
    {
        public CreateWatchlistResult CreateResult { get; init; } = new(CreateWatchlistDisposition.Created);

        public Task<CreateWatchlistResult> CreateWatchlistAsync(Watchlist watchlist, int maximumWatchlists, CancellationToken cancellationToken = default)
            => Task.FromResult(CreateResult);
        public Task<IReadOnlyList<WatchlistSummaryDto>> GetWatchlistsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WatchlistSummaryDto>>([]);
        public Task<RenameWatchlistResult> RenameWatchlistAsync(Watchlist watchlist, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<DeleteWatchlistResult> DeleteWatchlistAsync(GeopoliticalMonitor.Domain.Watchlists.WatchlistId watchlistId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<WatchlistEventIdsResult> GetEventIdsAsync(GeopoliticalMonitor.Domain.Watchlists.WatchlistId watchlistId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<EventWatchlistIdsResult> GetWatchlistIdsAsync(EventId eventId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<SetWatchlistMembershipResult> SetMembershipAsync(GeopoliticalMonitor.Domain.Watchlists.WatchlistId watchlistId, EventId eventId, bool isMember, int maximumEventsPerWatchlist, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class ThrowingStore : IWatchlistStore
    {
        private static InvalidOperationException Failure() => new("sensitive-database-path");
        public Task<IReadOnlyList<WatchlistSummaryDto>> GetWatchlistsAsync(CancellationToken cancellationToken = default) => throw Failure();
        public Task<CreateWatchlistResult> CreateWatchlistAsync(Watchlist watchlist, int maximumWatchlists, CancellationToken cancellationToken = default) => throw Failure();
        public Task<RenameWatchlistResult> RenameWatchlistAsync(Watchlist watchlist, CancellationToken cancellationToken = default) => throw Failure();
        public Task<DeleteWatchlistResult> DeleteWatchlistAsync(GeopoliticalMonitor.Domain.Watchlists.WatchlistId watchlistId, CancellationToken cancellationToken = default) => throw Failure();
        public Task<WatchlistEventIdsResult> GetEventIdsAsync(GeopoliticalMonitor.Domain.Watchlists.WatchlistId watchlistId, CancellationToken cancellationToken = default) => throw Failure();
        public Task<EventWatchlistIdsResult> GetWatchlistIdsAsync(EventId eventId, CancellationToken cancellationToken = default) => throw Failure();
        public Task<SetWatchlistMembershipResult> SetMembershipAsync(GeopoliticalMonitor.Domain.Watchlists.WatchlistId watchlistId, EventId eventId, bool isMember, int maximumEventsPerWatchlist, CancellationToken cancellationToken = default) => throw Failure();
    }
}
