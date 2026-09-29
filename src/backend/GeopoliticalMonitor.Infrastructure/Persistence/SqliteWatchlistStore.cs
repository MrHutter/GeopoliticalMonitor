using System.Data;
using GeopoliticalMonitor.Application.Watchlists;
using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Watchlists;
using GeopoliticalMonitor.Infrastructure.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GeopoliticalMonitor.Infrastructure.Persistence;

public sealed class SqliteWatchlistStore(Func<GeopoliticalMonitorDbContext> createDbContext) : IWatchlistStore
{
    public async Task<IReadOnlyList<WatchlistSummaryDto>> GetWatchlistsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        return await dbContext.Watchlists
            .AsNoTracking()
            .Select(item => new WatchlistSummaryDto(item.Id, item.Name, item.EventMemberships.Count))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<CreateWatchlistResult> CreateWatchlistAsync(
        Watchlist watchlist,
        int maximumWatchlists,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = Watchlist.CreateComparisonKey(watchlist.Name);
        await using var dbContext = createDbContext();
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);

        if (await dbContext.Watchlists.CountAsync(cancellationToken).ConfigureAwait(false) >= maximumWatchlists)
        {
            return new CreateWatchlistResult(CreateWatchlistDisposition.WatchlistLimitReached);
        }

        if (await dbContext.Watchlists.AnyAsync(item => item.NormalizedName == normalizedName, cancellationToken).ConfigureAwait(false))
        {
            return new CreateWatchlistResult(CreateWatchlistDisposition.NameConflict);
        }

        dbContext.Watchlists.Add(new WatchlistEntity
        {
            Id = watchlist.Id.Value,
            Name = watchlist.Name,
            NormalizedName = normalizedName,
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (IsConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            if (await NormalizedNameExistsAsync(normalizedName, cancellationToken).ConfigureAwait(false))
            {
                return new CreateWatchlistResult(CreateWatchlistDisposition.NameConflict);
            }

            throw;
        }

        return new CreateWatchlistResult(
            CreateWatchlistDisposition.Created,
            new WatchlistSummaryDto(watchlist.Id.Value, watchlist.Name, 0));
    }

    public async Task<RenameWatchlistResult> RenameWatchlistAsync(Watchlist watchlist, CancellationToken cancellationToken = default)
    {
        var normalizedName = Watchlist.CreateComparisonKey(watchlist.Name);
        await using var dbContext = createDbContext();
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);
        var entity = await dbContext.Watchlists
            .SingleOrDefaultAsync(item => item.Id == watchlist.Id.Value, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return new RenameWatchlistResult(RenameWatchlistDisposition.NotFound);
        }

        var eventCount = await dbContext.WatchlistEvents
            .CountAsync(item => item.WatchlistId == watchlist.Id.Value, cancellationToken)
            .ConfigureAwait(false);
        if (entity.Name == watchlist.Name)
        {
            return new RenameWatchlistResult(
                RenameWatchlistDisposition.Unchanged,
                new WatchlistSummaryDto(entity.Id, entity.Name, eventCount));
        }

        if (await dbContext.Watchlists.AnyAsync(
                item => item.Id != watchlist.Id.Value && item.NormalizedName == normalizedName,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return new RenameWatchlistResult(RenameWatchlistDisposition.NameConflict);
        }

        entity.Name = watchlist.Name;
        entity.NormalizedName = normalizedName;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (IsConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            if (await NormalizedNameExistsAsync(normalizedName, cancellationToken, watchlist.Id.Value).ConfigureAwait(false))
            {
                return new RenameWatchlistResult(RenameWatchlistDisposition.NameConflict);
            }

            throw;
        }

        return new RenameWatchlistResult(
            RenameWatchlistDisposition.Updated,
            new WatchlistSummaryDto(entity.Id, entity.Name, eventCount));
    }

    public async Task<DeleteWatchlistResult> DeleteWatchlistAsync(WatchlistId watchlistId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        var deleted = await dbContext.Watchlists
            .Where(item => item.Id == watchlistId.Value)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        return new DeleteWatchlistResult(
            deleted == 0 ? DeleteWatchlistDisposition.NotFound : DeleteWatchlistDisposition.Deleted,
            watchlistId.Value);
    }

    public async Task<WatchlistEventIdsResult> GetEventIdsAsync(WatchlistId watchlistId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        if (!await dbContext.Watchlists.AnyAsync(item => item.Id == watchlistId.Value, cancellationToken).ConfigureAwait(false))
        {
            return new WatchlistEventIdsResult(WatchlistLookupDisposition.WatchlistNotFound, watchlistId.Value, []);
        }

        var eventIds = await dbContext.WatchlistEvents
            .AsNoTracking()
            .Where(item => item.WatchlistId == watchlistId.Value)
            .OrderBy(item => item.EventId)
            .Select(item => item.EventId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return new WatchlistEventIdsResult(WatchlistLookupDisposition.Found, watchlistId.Value, eventIds);
    }

    public async Task<EventWatchlistIdsResult> GetWatchlistIdsAsync(EventId eventId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        if (!await dbContext.Events.AnyAsync(item => item.Id == eventId.Value, cancellationToken).ConfigureAwait(false))
        {
            return new EventWatchlistIdsResult(WatchlistLookupDisposition.EventNotFound, eventId.Value, []);
        }

        var watchlistIds = await dbContext.WatchlistEvents
            .AsNoTracking()
            .Where(item => item.EventId == eventId.Value)
            .OrderBy(item => item.WatchlistId)
            .Select(item => item.WatchlistId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return new EventWatchlistIdsResult(WatchlistLookupDisposition.Found, eventId.Value, watchlistIds);
    }

    public async Task<SetWatchlistMembershipResult> SetMembershipAsync(
        WatchlistId watchlistId,
        EventId eventId,
        bool isMember,
        int maximumEventsPerWatchlist,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);
        if (!await dbContext.Watchlists.AnyAsync(item => item.Id == watchlistId.Value, cancellationToken).ConfigureAwait(false))
        {
            return Result(SetWatchlistMembershipDisposition.WatchlistNotFound, false, 0);
        }

        var eventCount = await dbContext.WatchlistEvents
            .CountAsync(item => item.WatchlistId == watchlistId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (!await dbContext.Events.AnyAsync(item => item.Id == eventId.Value, cancellationToken).ConfigureAwait(false))
        {
            return Result(SetWatchlistMembershipDisposition.EventNotFound, false, eventCount);
        }

        var membership = await dbContext.WatchlistEvents
            .SingleOrDefaultAsync(
                item => item.WatchlistId == watchlistId.Value && item.EventId == eventId.Value,
                cancellationToken)
            .ConfigureAwait(false);

        if (isMember && membership is not null)
        {
            return Result(SetWatchlistMembershipDisposition.AlreadyMember, true, eventCount);
        }

        if (isMember && eventCount >= maximumEventsPerWatchlist)
        {
            return Result(SetWatchlistMembershipDisposition.MembershipLimitReached, false, eventCount);
        }

        if (isMember)
        {
            dbContext.WatchlistEvents.Add(new WatchlistEventEntity
            {
                WatchlistId = watchlistId.Value,
                EventId = eventId.Value,
            });
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return Result(SetWatchlistMembershipDisposition.Added, true, eventCount + 1);
            }
            catch (DbUpdateException exception) when (IsConstraintViolation(exception))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Result(SetWatchlistMembershipDisposition.AlreadyMember, true, eventCount);
            }
        }

        if (membership is null)
        {
            return Result(SetWatchlistMembershipDisposition.AlreadyAbsent, false, eventCount);
        }

        dbContext.WatchlistEvents.Remove(membership);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Result(SetWatchlistMembershipDisposition.Removed, false, eventCount - 1);

        SetWatchlistMembershipResult Result(SetWatchlistMembershipDisposition disposition, bool actualState, int count)
            => new(disposition, watchlistId.Value, eventId.Value, actualState, count);
    }

    private async Task<bool> NormalizedNameExistsAsync(
        string normalizedName,
        CancellationToken cancellationToken,
        string? excludedWatchlistId = null)
    {
        await using var verificationContext = createDbContext();
        return await verificationContext.Watchlists
            .AnyAsync(
                item => item.NormalizedName == normalizedName && item.Id != excludedWatchlistId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsConstraintViolation(DbUpdateException exception)
        => exception.InnerException is SqliteException { SqliteErrorCode: 19 };
}
