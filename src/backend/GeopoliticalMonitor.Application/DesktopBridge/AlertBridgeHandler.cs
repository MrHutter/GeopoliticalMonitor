using GeopoliticalMonitor.Application.Alerts;

namespace GeopoliticalMonitor.Application.DesktopBridge;

public sealed class AlertBridgeHandler(AlertService alertService)
{
    public async Task<string> HandleAsync(DesktopBridgeRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return request.Type switch
            {
                DesktopBridgeProtocol.AlertsSummaryMessageType
                    => DesktopBridgeProtocol.CreateAlertSummaryResultResponseJson(
                        await alertService.GetUnreadCountAsync(cancellationToken).ConfigureAwait(false)),
                DesktopBridgeProtocol.AlertRulesListMessageType
                    => DesktopBridgeProtocol.CreateAlertRulesListResultResponseJson(
                        await alertService.GetRulesAsync(cancellationToken).ConfigureAwait(false)),
                DesktopBridgeProtocol.AlertRulesSetMessageType
                    => DesktopBridgeProtocol.CreateAlertRuleSetResultResponseJson(
                        await alertService.SetRuleEnabledAsync(
                            request.WatchlistId!,
                            request.AlertRuleEnabled!.Value,
                            cancellationToken).ConfigureAwait(false)),
                DesktopBridgeProtocol.AlertsListMessageType
                    => DesktopBridgeProtocol.CreateAlertsListResultResponseJson(
                        await alertService.GetAlertsAsync(cancellationToken).ConfigureAwait(false)),
                DesktopBridgeProtocol.AlertsMarkReadMessageType
                    => DesktopBridgeProtocol.CreateAlertMarkReadResultResponseJson(
                        await alertService.MarkReadAsync(request.AlertId!, cancellationToken).ConfigureAwait(false)),
                _ => throw new InvalidOperationException("Unsupported Alert operation."),
            };
        }
        catch (Exception)
        {
            return DesktopBridgeProtocol.CreateAlertErrorResponseJson(
                request.Type,
                "PersistenceUnavailable",
                "Alerts are unavailable. Existing Events and Watchlists were not changed.",
                request.WatchlistId,
                request.AlertId);
        }
    }
}
