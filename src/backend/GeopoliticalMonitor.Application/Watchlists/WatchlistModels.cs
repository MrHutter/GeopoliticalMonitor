namespace GeopoliticalMonitor.Application.Watchlists;

public sealed record WatchlistSummaryDto(string Id, string Name, int EventCount);

public enum CreateWatchlistDisposition
{
    Created,
    NameConflict,
    WatchlistLimitReached,
}

public sealed record CreateWatchlistResult(
    CreateWatchlistDisposition Disposition,
    WatchlistSummaryDto? Watchlist = null);

public enum RenameWatchlistDisposition
{
    Updated,
    Unchanged,
    NotFound,
    NameConflict,
}

public sealed record RenameWatchlistResult(
    RenameWatchlistDisposition Disposition,
    WatchlistSummaryDto? Watchlist = null);

public enum DeleteWatchlistDisposition
{
    Deleted,
    NotFound,
}

public sealed record DeleteWatchlistResult(DeleteWatchlistDisposition Disposition, string WatchlistId);

public enum WatchlistLookupDisposition
{
    Found,
    WatchlistNotFound,
    EventNotFound,
}

public sealed record WatchlistEventIdsResult(
    WatchlistLookupDisposition Disposition,
    string WatchlistId,
    IReadOnlyList<string> EventIds);

public sealed record EventWatchlistIdsResult(
    WatchlistLookupDisposition Disposition,
    string EventId,
    IReadOnlyList<string> WatchlistIds);

public enum SetWatchlistMembershipDisposition
{
    Added,
    AlreadyMember,
    Removed,
    AlreadyAbsent,
    WatchlistNotFound,
    EventNotFound,
    MembershipLimitReached,
}

public sealed record SetWatchlistMembershipResult(
    SetWatchlistMembershipDisposition Disposition,
    string WatchlistId,
    string EventId,
    bool IsMember,
    int EventCount);
