using GeopoliticalMonitor.Domain;

namespace GeopoliticalMonitor.Domain.Sources;

public sealed class SourceItem
{
    private SourceItem(
        SourceItemId id,
        SourceType sourceType,
        string? externalId,
        string? title,
        string text,
        Uri? url,
        string? authorDisplayName,
        string? authorHandle,
        DateTimeOffset publishedAtUtc,
        DateTimeOffset retrievedAtUtc,
        Location? location,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        SourceType = sourceType;
        ExternalId = externalId;
        Title = title;
        Text = text;
        Url = url;
        AuthorDisplayName = authorDisplayName;
        AuthorHandle = authorHandle;
        PublishedAtUtc = publishedAtUtc;
        RetrievedAtUtc = retrievedAtUtc;
        Location = location;
        CreatedAtUtc = createdAtUtc;
    }

    public SourceItemId Id { get; }
    public SourceType SourceType { get; }
    public string? ExternalId { get; }
    public string? Title { get; }
    public string Text { get; }
    public Uri? Url { get; }
    public string? AuthorDisplayName { get; }
    public string? AuthorHandle { get; }
    public DateTimeOffset PublishedAtUtc { get; }
    public DateTimeOffset RetrievedAtUtc { get; }
    public Location? Location { get; }
    public DateTimeOffset CreatedAtUtc { get; }

    public static SourceItem Create(
        SourceItemId id,
        SourceType sourceType,
        string? externalId,
        string? title,
        string text,
        Uri? url,
        string? authorDisplayName,
        string? authorHandle,
        DateTimeOffset publishedAtUtc,
        DateTimeOffset retrievedAtUtc,
        Location? location,
        DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Source item text is required.", nameof(text));
        }

        return new SourceItem(
            id,
            sourceType,
            string.IsNullOrWhiteSpace(externalId) ? null : externalId,
            string.IsNullOrWhiteSpace(title) ? null : title,
            text,
            url,
            string.IsNullOrWhiteSpace(authorDisplayName) ? null : authorDisplayName,
            string.IsNullOrWhiteSpace(authorHandle) ? null : authorHandle,
            publishedAtUtc,
            retrievedAtUtc,
            location,
            createdAtUtc);
    }
}