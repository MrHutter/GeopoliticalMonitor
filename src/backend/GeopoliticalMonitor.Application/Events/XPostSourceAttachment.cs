using GeopoliticalMonitor.Domain.Sources;

namespace GeopoliticalMonitor.Application.Events;

public enum AttachXPostSourceDisposition
{
    CreatedAndAttached,
    ExistingSourceAttached,
    AlreadyAttached,
    InvalidEventId,
    InvalidXPostUrl,
    InvalidStatusTimestamp,
    EventNotFound,
}

public sealed record AttachXPostSourceResult(
    AttachXPostSourceDisposition Disposition,
    SourceItemDto? Source = null);

public enum SourceAttachmentStoreDisposition
{
    CreatedAndAttached,
    ExistingSourceAttached,
    AlreadyAttached,
    EventNotFound,
}

public sealed record SourceAttachmentStoreResult(
    SourceAttachmentStoreDisposition Disposition,
    SourceItem? Source = null);
