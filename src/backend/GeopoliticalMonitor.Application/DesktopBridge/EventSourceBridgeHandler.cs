using GeopoliticalMonitor.Application.Events;

namespace GeopoliticalMonitor.Application.DesktopBridge;

public sealed class EventSourceBridgeHandler(EventCatalogService eventCatalogService)
{
    private const string InvalidUrlMessage = "Enter a public X status URL.";
    private const string InvalidTimestampMessage = "The X status URL does not contain a valid post timestamp.";
    private const string MissingEventMessage = "The selected event is no longer available.";
    private const string PersistenceFailureMessage = "Unable to attach the X post source.";

    public async Task<string> AttachXPostAsync(string eventId, string xPostUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await eventCatalogService
                .AttachXPostSourceToEventAsync(eventId, xPostUrl, cancellationToken)
                .ConfigureAwait(false);

            return result.Disposition switch
            {
                AttachXPostSourceDisposition.CreatedAndAttached
                    or AttachXPostSourceDisposition.ExistingSourceAttached
                    or AttachXPostSourceDisposition.AlreadyAttached
                    => DesktopBridgeProtocol.CreateEventSourcesAttachXPostResultResponseJson(eventId, result),
                AttachXPostSourceDisposition.InvalidXPostUrl
                    => DesktopBridgeProtocol.CreateEventSourcesAttachXPostErrorResponseJson(eventId, InvalidUrlMessage),
                AttachXPostSourceDisposition.InvalidStatusTimestamp
                    => DesktopBridgeProtocol.CreateEventSourcesAttachXPostErrorResponseJson(eventId, InvalidTimestampMessage),
                _ => DesktopBridgeProtocol.CreateEventSourcesAttachXPostErrorResponseJson(eventId, MissingEventMessage),
            };
        }
        catch (Exception)
        {
            return DesktopBridgeProtocol.CreateEventSourcesAttachXPostErrorResponseJson(eventId, PersistenceFailureMessage);
        }
    }
}
