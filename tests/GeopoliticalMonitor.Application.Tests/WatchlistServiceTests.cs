using GeopoliticalMonitor.Application.Watchlists;
using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Watchlists;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class WatchlistServiceTests
{
    private const string FirstId = "wl-00000000000000000000000000000001";
    private const string SecondId = "wl-00000000000000000000000000000002";

    [Fact]
    public async Task CreatesNormalizedWatchlistWithGeneratedId()
    {
        var store = new InMemoryWatchlistStore();
        var service = new WatchlistService(store);

        var result = await service.CreateWatchlistAsync("  Baltic   Security  ");

        Assert.Equal(CreateWatchlistDisposition.Created, result.Disposition);
        Assert.Equal("Baltic Security", result.Watchlist?.Name);
        Assert.Matches("^wl-[0-9a-f]{32}$", result.Watchlist?.Id);
    }

    [Fact]
    public async Task DuplicateNamesAreCaseInsensitive()
    {
        var store = new InMemoryWatchlistStore();
        var service = new WatchlistService(store);
        await service.CreateWatchlistAsync("Baltic Security");

        var duplicate = await service.CreateWatchlistAsync("baltic security");

        Assert.Equal(CreateWatchlistDisposition.NameConflict, duplicate.Disposition);
    }

    [Fact]
    public async Task RenameSupportsCasingChangesAndReportsUnchangedOrConflict()
    {
        var store = new InMemoryWatchlistStore(
            CreateWatchlist(FirstId, "Baltic"),
            CreateWatchlist(SecondId, "Humanitarian"));
        var service = new WatchlistService(store);

        var casing = await service.RenameWatchlistAsync(FirstId, "BALTIC");
        var unchanged = await service.RenameWatchlistAsync(FirstId, "BALTIC");
        var conflict = await service.RenameWatchlistAsync(FirstId, "humanitarian");

        Assert.Equal(RenameWatchlistDisposition.Updated, casing.Disposition);
        Assert.Equal("BALTIC", casing.Watchlist?.Name);
        Assert.Equal(RenameWatchlistDisposition.Unchanged, unchanged.Disposition);
        Assert.Equal(RenameWatchlistDisposition.NameConflict, conflict.Disposition);
    }

    [Fact]
    public async Task CreatePassesLockedWatchlistLimitToStore()
    {
        var store = new InMemoryWatchlistStore { SimulatedWatchlistCount = WatchlistService.MaximumWatchlists };
        var service = new WatchlistService(store);

        var result = await service.CreateWatchlistAsync("Over limit");

        Assert.Equal(CreateWatchlistDisposition.WatchlistLimitReached, result.Disposition);
        Assert.Equal(WatchlistService.MaximumWatchlists, store.LastMaximumWatchlists);
    }

    [Fact]
    public async Task MembershipIsIdempotentAndEventMayBelongToMultipleWatchlists()
    {
        var store = new InMemoryWatchlistStore(
            CreateWatchlist(FirstId, "Baltic"),
            CreateWatchlist(SecondId, "Security"));
        store.Events.Add(new EventId("evt-one"));
        var service = new WatchlistService(store);

        var first = await service.SetMembershipAsync(FirstId, "evt-one", true);
        var duplicate = await service.SetMembershipAsync(FirstId, "evt-one", true);
        var second = await service.SetMembershipAsync(SecondId, "evt-one", true);
        var removed = await service.SetMembershipAsync(FirstId, "evt-one", false);
        var absent = await service.SetMembershipAsync(FirstId, "evt-one", false);

        Assert.Equal(SetWatchlistMembershipDisposition.Added, first.Disposition);
        Assert.Equal(SetWatchlistMembershipDisposition.AlreadyMember, duplicate.Disposition);
        Assert.Equal(SetWatchlistMembershipDisposition.Added, second.Disposition);
        Assert.Equal(SetWatchlistMembershipDisposition.Removed, removed.Disposition);
        Assert.Equal(SetWatchlistMembershipDisposition.AlreadyAbsent, absent.Disposition);
        Assert.Single((await service.GetWatchlistIdsForEventAsync("evt-one")).WatchlistIds);
    }

    [Fact]
    public async Task MembershipReportsUnknownIdsAndLockedLimit()
    {
        var store = new InMemoryWatchlistStore(CreateWatchlist(FirstId, "Baltic"));
        store.Events.Add(new EventId("evt-one"));
        store.SimulatedMembershipCount = WatchlistService.MaximumEventsPerWatchlist;
        var service = new WatchlistService(store);

        var missingWatchlist = await service.SetMembershipAsync(SecondId, "evt-one", true);
        var missingEvent = await service.SetMembershipAsync(FirstId, "evt-missing", true);
        var limited = await service.SetMembershipAsync(FirstId, "evt-one", true);

        Assert.Equal(SetWatchlistMembershipDisposition.WatchlistNotFound, missingWatchlist.Disposition);
        Assert.Equal(SetWatchlistMembershipDisposition.EventNotFound, missingEvent.Disposition);
        Assert.Equal(SetWatchlistMembershipDisposition.MembershipLimitReached, limited.Disposition);
        Assert.Equal(WatchlistService.MaximumEventsPerWatchlist, store.LastMaximumEventsPerWatchlist);
    }

    [Fact]
    public async Task DeleteRemovesWatchlistAndListOrderingIsDeterministic()
    {
        var store = new InMemoryWatchlistStore(
            CreateWatchlist(FirstId, "zulu"),
            CreateWatchlist(SecondId, "Alpha"));
        var service = new WatchlistService(store);

        var ordered = await service.GetWatchlistsAsync();
        var deleted = await service.DeleteWatchlistAsync(SecondId);
        var afterDelete = await service.GetWatchlistsAsync();

        Assert.Equal(["Alpha", "zulu"], ordered.Select(item => item.Name));
        Assert.Equal(DeleteWatchlistDisposition.Deleted, deleted.Disposition);
        Assert.DoesNotContain(afterDelete, item => item.Id == SecondId);
    }

    private static Watchlist CreateWatchlist(string id, string name) => Watchlist.Create(new WatchlistId(id), name);

    private sealed class InMemoryWatchlistStore(params Watchlist[] initialWatchlists) : IWatchlistStore
    {
        private readonly Dictionary<WatchlistId, Watchlist> watchlists = initialWatchlists.ToDictionary(item => item.Id);
        private readonly HashSet<(WatchlistId WatchlistId, EventId EventId)> memberships = [];

        public HashSet<EventId> Events { get; } = [];
        public int? SimulatedWatchlistCount { get; set; }
        public int? SimulatedMembershipCount { get; set; }
        public int LastMaximumWatchlists { get; private set; }
        public int LastMaximumEventsPerWatchlist { get; private set; }

        public Task<IReadOnlyList<WatchlistSummaryDto>> GetWatchlistsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WatchlistSummaryDto>>(watchlists.Values
                .Select(item => new WatchlistSummaryDto(item.Id.Value, item.Name, memberships.Count(link => link.WatchlistId == item.Id)))
                .ToArray());
        }

        public Task<CreateWatchlistResult> CreateWatchlistAsync(Watchlist watchlist, int maximumWatchlists, CancellationToken cancellationToken = default)
        {
            LastMaximumWatchlists = maximumWatchlists;
            if ((SimulatedWatchlistCount ?? watchlists.Count) >= maximumWatchlists)
            {
                return Task.FromResult(new CreateWatchlistResult(CreateWatchlistDisposition.WatchlistLimitReached));
            }

            if (watchlists.Values.Any(item => Watchlist.CreateComparisonKey(item.Name) == Watchlist.CreateComparisonKey(watchlist.Name)))
            {
                return Task.FromResult(new CreateWatchlistResult(CreateWatchlistDisposition.NameConflict));
            }

            watchlists.Add(watchlist.Id, watchlist);
            return Task.FromResult(new CreateWatchlistResult(
                CreateWatchlistDisposition.Created,
                new WatchlistSummaryDto(watchlist.Id.Value, watchlist.Name, 0)));
        }

        public Task<RenameWatchlistResult> RenameWatchlistAsync(Watchlist watchlist, CancellationToken cancellationToken = default)
        {
            if (!watchlists.TryGetValue(watchlist.Id, out var current))
            {
                return Task.FromResult(new RenameWatchlistResult(RenameWatchlistDisposition.NotFound));
            }

            if (current.Name == watchlist.Name)
            {
                return Task.FromResult(new RenameWatchlistResult(
                    RenameWatchlistDisposition.Unchanged,
                    Summary(current)));
            }

            if (watchlists.Values.Any(item => item.Id != watchlist.Id
                && Watchlist.CreateComparisonKey(item.Name) == Watchlist.CreateComparisonKey(watchlist.Name)))
            {
                return Task.FromResult(new RenameWatchlistResult(RenameWatchlistDisposition.NameConflict));
            }

            watchlists[watchlist.Id] = watchlist;
            return Task.FromResult(new RenameWatchlistResult(RenameWatchlistDisposition.Updated, Summary(watchlist)));
        }

        public Task<DeleteWatchlistResult> DeleteWatchlistAsync(WatchlistId watchlistId, CancellationToken cancellationToken = default)
        {
            var disposition = watchlists.Remove(watchlistId)
                ? DeleteWatchlistDisposition.Deleted
                : DeleteWatchlistDisposition.NotFound;
            memberships.RemoveWhere(item => item.WatchlistId == watchlistId);
            return Task.FromResult(new DeleteWatchlistResult(disposition, watchlistId.Value));
        }

        public Task<WatchlistEventIdsResult> GetEventIdsAsync(WatchlistId watchlistId, CancellationToken cancellationToken = default)
        {
            var found = watchlists.ContainsKey(watchlistId);
            return Task.FromResult(new WatchlistEventIdsResult(
                found ? WatchlistLookupDisposition.Found : WatchlistLookupDisposition.WatchlistNotFound,
                watchlistId.Value,
                found ? memberships.Where(item => item.WatchlistId == watchlistId).Select(item => item.EventId.Value).Order().ToArray() : []));
        }

        public Task<EventWatchlistIdsResult> GetWatchlistIdsAsync(EventId eventId, CancellationToken cancellationToken = default)
        {
            var found = Events.Contains(eventId);
            return Task.FromResult(new EventWatchlistIdsResult(
                found ? WatchlistLookupDisposition.Found : WatchlistLookupDisposition.EventNotFound,
                eventId.Value,
                found ? memberships.Where(item => item.EventId == eventId).Select(item => item.WatchlistId.Value).Order().ToArray() : []));
        }

        public Task<SetWatchlistMembershipResult> SetMembershipAsync(
            WatchlistId watchlistId,
            EventId eventId,
            bool isMember,
            int maximumEventsPerWatchlist,
            CancellationToken cancellationToken = default)
        {
            LastMaximumEventsPerWatchlist = maximumEventsPerWatchlist;
            if (!watchlists.ContainsKey(watchlistId))
            {
                return Result(SetWatchlistMembershipDisposition.WatchlistNotFound, false, 0);
            }

            if (!Events.Contains(eventId))
            {
                return Result(SetWatchlistMembershipDisposition.EventNotFound, false, Count());
            }

            var link = (watchlistId, eventId);
            if (isMember && memberships.Contains(link))
            {
                return Result(SetWatchlistMembershipDisposition.AlreadyMember, true, Count());
            }

            if (isMember && (SimulatedMembershipCount ?? Count()) >= maximumEventsPerWatchlist)
            {
                return Result(SetWatchlistMembershipDisposition.MembershipLimitReached, false, Count());
            }

            if (isMember)
            {
                memberships.Add(link);
                return Result(SetWatchlistMembershipDisposition.Added, true, Count());
            }

            var removed = memberships.Remove(link);
            return Result(
                removed ? SetWatchlistMembershipDisposition.Removed : SetWatchlistMembershipDisposition.AlreadyAbsent,
                false,
                Count());

            int Count() => memberships.Count(item => item.WatchlistId == watchlistId);
            Task<SetWatchlistMembershipResult> Result(SetWatchlistMembershipDisposition disposition, bool actual, int count)
                => Task.FromResult(new SetWatchlistMembershipResult(disposition, watchlistId.Value, eventId.Value, actual, count));
        }

        private WatchlistSummaryDto Summary(Watchlist watchlist)
            => new(watchlist.Id.Value, watchlist.Name, memberships.Count(item => item.WatchlistId == watchlist.Id));
    }
}
