using GeopoliticalMonitor.Application.ReliefWeb;

namespace GeopoliticalMonitor.Application.DesktopBridge;

public sealed class ReliefWebBridgeHandler(ReliefWebReportDiscoveryService discoveryService)
{
    public async Task<string> SearchAsync(DesktopBridgeRequest request, CancellationToken cancellationToken = default)
    {
        var result = await discoveryService.SearchAsync(
            new ReliefWebSearchCommand(
                request.EventId,
                request.ReliefWebQueryText,
                request.ReliefWebFromDateUtc,
                request.ReliefWebToDateUtc,
                request.ReliefWebCountryIso3),
            cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? DesktopBridgeProtocol.CreateReliefWebSearchResultResponseJson(request.EventId!, result)
            : DesktopBridgeProtocol.CreateReliefWebSearchErrorResponseJson(
                request.EventId!, result.Status, SearchMessage(result.Status));
    }

    public async Task<string> AttachAsync(DesktopBridgeRequest request, CancellationToken cancellationToken = default)
    {
        var result = await discoveryService
            .AttachAsync(request.EventId, request.ReliefWebReportId, cancellationToken)
            .ConfigureAwait(false);
        return result.IsSuccess
            ? DesktopBridgeProtocol.CreateReliefWebAttachResultResponseJson(
                request.EventId!, request.ReliefWebReportId!, result)
            : DesktopBridgeProtocol.CreateReliefWebAttachErrorResponseJson(
                request.EventId!, request.ReliefWebReportId!, result.Status, AttachMessage(result.Status));
    }

    private static string SearchMessage(ReliefWebSearchStatus status) => status switch
    {
        ReliefWebSearchStatus.ConfigurationUnavailable => "ReliefWeb integration is not configured.",
        ReliefWebSearchStatus.EventNotFound => "The selected event is no longer available.",
        ReliefWebSearchStatus.InvalidQuery => "Enter a ReliefWeb search query of 200 characters or fewer.",
        ReliefWebSearchStatus.InvalidDateRange => "Enter a valid ReliefWeb date range of no more than 366 days.",
        ReliefWebSearchStatus.InvalidCountry => "Enter a valid three-letter country code.",
        ReliefWebSearchStatus.TimedOut => "ReliefWeb search timed out.",
        ReliefWebSearchStatus.QuotaExceeded => "ReliefWeb request quota is currently exhausted.",
        ReliefWebSearchStatus.InvalidConfiguration => "ReliefWeb rejected the configured application identifier.",
        ReliefWebSearchStatus.Cancelled => "ReliefWeb search was cancelled.",
        ReliefWebSearchStatus.InvalidResponse => "ReliefWeb returned an unsupported response.",
        _ => "ReliefWeb search is unavailable.",
    };

    private static string AttachMessage(ReliefWebAttachStatus status) => status switch
    {
        ReliefWebAttachStatus.ConfigurationUnavailable => "ReliefWeb integration is not configured.",
        ReliefWebAttachStatus.EventNotFound => "The selected event is no longer available.",
        ReliefWebAttachStatus.ReportNotFound => "The ReliefWeb report is no longer available.",
        ReliefWebAttachStatus.Conflict => "A conflicting local source already uses this ReliefWeb identity.",
        ReliefWebAttachStatus.TimedOut => "ReliefWeb report retrieval timed out.",
        ReliefWebAttachStatus.QuotaExceeded => "ReliefWeb request quota is currently exhausted.",
        ReliefWebAttachStatus.InvalidConfiguration => "ReliefWeb rejected the configured application identifier.",
        ReliefWebAttachStatus.Cancelled => "ReliefWeb report attachment was cancelled.",
        ReliefWebAttachStatus.InvalidResponse => "ReliefWeb returned an unsupported report response.",
        _ => "Unable to attach the ReliefWeb report.",
    };
}
