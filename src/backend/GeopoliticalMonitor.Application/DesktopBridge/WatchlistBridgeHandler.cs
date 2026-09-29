using GeopoliticalMonitor.Application.Watchlists;

namespace GeopoliticalMonitor.Application.DesktopBridge;

public sealed class WatchlistBridgeHandler(WatchlistService watchlistService)
{
    public async Task<string> HandleAsync(DesktopBridgeRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return request.Type switch
            {
                DesktopBridgeProtocol.WatchlistsListMessageType
                    => DesktopBridgeProtocol.CreateWatchlistsListResultResponseJson(
                        await watchlistService.GetWatchlistsAsync(cancellationToken).ConfigureAwait(false)),
                DesktopBridgeProtocol.WatchlistsCreateMessageType
                    => CreateResponse(request, await watchlistService.CreateWatchlistAsync(request.WatchlistName!, cancellationToken).ConfigureAwait(false)),
                DesktopBridgeProtocol.WatchlistsRenameMessageType
                    => CreateResponse(request, await watchlistService.RenameWatchlistAsync(request.WatchlistId!, request.WatchlistName!, cancellationToken).ConfigureAwait(false)),
                DesktopBridgeProtocol.WatchlistsDeleteMessageType
                    => CreateResponse(request, await watchlistService.DeleteWatchlistAsync(request.WatchlistId!, cancellationToken).ConfigureAwait(false)),
                DesktopBridgeProtocol.WatchlistMembersGetMessageType
                    => CreateResponse(request, await watchlistService.GetEventIdsAsync(request.WatchlistId!, cancellationToken).ConfigureAwait(false)),
                DesktopBridgeProtocol.WatchlistMembershipsGetForEventMessageType
                    => CreateResponse(request, await watchlistService.GetWatchlistIdsForEventAsync(request.EventId!, cancellationToken).ConfigureAwait(false)),
                DesktopBridgeProtocol.WatchlistMembersSetMessageType
                    => CreateResponse(request, await watchlistService.SetMembershipAsync(
                        request.WatchlistId!,
                        request.EventId!,
                        request.WatchlistIsMember!.Value,
                        cancellationToken).ConfigureAwait(false)),
                _ => throw new InvalidOperationException("Unsupported Watchlist operation."),
            };
        }
        catch (Exception)
        {
            return Error(request, "PersistenceUnavailable", "Unable to update local watchlists.");
        }
    }

    private static string CreateResponse(DesktopBridgeRequest request, CreateWatchlistResult result)
    {
        return result.Disposition switch
        {
            CreateWatchlistDisposition.Created => DesktopBridgeProtocol.CreateWatchlistCreateResultResponseJson(result.Watchlist!),
            CreateWatchlistDisposition.NameConflict => Error(request, "NameConflict", "A watchlist with that name already exists."),
            _ => Error(request, "WatchlistLimitReached", $"A maximum of {WatchlistService.MaximumWatchlists} watchlists is supported."),
        };
    }

    private static string CreateResponse(DesktopBridgeRequest request, RenameWatchlistResult result)
    {
        return result.Disposition switch
        {
            RenameWatchlistDisposition.Updated or RenameWatchlistDisposition.Unchanged
                => DesktopBridgeProtocol.CreateWatchlistRenameResultResponseJson(result),
            RenameWatchlistDisposition.NameConflict
                => Error(request, "NameConflict", "A watchlist with that name already exists."),
            _ => Error(request, "WatchlistNotFound", "The watchlist is no longer available."),
        };
    }

    private static string CreateResponse(DesktopBridgeRequest request, DeleteWatchlistResult result)
    {
        return result.Disposition == DeleteWatchlistDisposition.Deleted
            ? DesktopBridgeProtocol.CreateWatchlistDeleteResultResponseJson(result)
            : Error(request, "WatchlistNotFound", "The watchlist is no longer available.");
    }

    private static string CreateResponse(DesktopBridgeRequest request, WatchlistEventIdsResult result)
    {
        return result.Disposition == WatchlistLookupDisposition.Found
            ? DesktopBridgeProtocol.CreateWatchlistMembersResultResponseJson(result)
            : Error(request, "WatchlistNotFound", "The watchlist is no longer available.");
    }

    private static string CreateResponse(DesktopBridgeRequest request, EventWatchlistIdsResult result)
    {
        return result.Disposition == WatchlistLookupDisposition.Found
            ? DesktopBridgeProtocol.CreateEventWatchlistMembershipsResultResponseJson(result)
            : Error(request, "EventNotFound", "The selected event is no longer available.");
    }

    private static string CreateResponse(DesktopBridgeRequest request, SetWatchlistMembershipResult result)
    {
        return result.Disposition switch
        {
            SetWatchlistMembershipDisposition.Added
                or SetWatchlistMembershipDisposition.AlreadyMember
                or SetWatchlistMembershipDisposition.Removed
                or SetWatchlistMembershipDisposition.AlreadyAbsent
                => DesktopBridgeProtocol.CreateWatchlistMembershipSetResultResponseJson(result),
            SetWatchlistMembershipDisposition.WatchlistNotFound
                => Error(request, "WatchlistNotFound", "The watchlist is no longer available."),
            SetWatchlistMembershipDisposition.EventNotFound
                => Error(request, "EventNotFound", "The selected event is no longer available."),
            _ => Error(request, "MembershipLimitReached", $"A watchlist can contain at most {WatchlistService.MaximumEventsPerWatchlist} events."),
        };
    }

    private static string Error(DesktopBridgeRequest request, string code, string message)
    {
        return DesktopBridgeProtocol.CreateWatchlistErrorResponseJson(
            request.Type,
            code,
            message,
            request.WatchlistId,
            request.EventId);
    }
}
