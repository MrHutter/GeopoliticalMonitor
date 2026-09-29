using GeopoliticalMonitor.Domain;
using GeopoliticalMonitor.Domain.Sources;

namespace GeopoliticalMonitor.Domain.Tests;

public class SourceItemDomainTests
{
    private static readonly DateTimeOffset Published = new(2026, 9, 17, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Retrieved = Published.AddMinutes(4);
    private static readonly DateTimeOffset Created = Published.AddMinutes(5);

    [Fact]
    public void CreatesValidSourceItem()
    {
        var sourceItem = SourceItem.Create(new SourceItemId("src-demo"), SourceType.Other, "external-1", "Demo source", "Source text", new Uri("https://example.invalid/source"), "Demo Author", "@demo", Published, Retrieved, new Location(10, 20, "Demo place"), Created);

        Assert.Equal("src-demo", sourceItem.Id.Value);
        Assert.Equal(SourceType.Other, sourceItem.SourceType);
        Assert.Equal("external-1", sourceItem.ExternalId);
        Assert.Equal("Demo source", sourceItem.Title);
        Assert.Equal("Source text", sourceItem.Text);
        Assert.Equal("Demo Author", sourceItem.AuthorDisplayName);
        Assert.Equal("@demo", sourceItem.AuthorHandle);
        Assert.Equal(Published, sourceItem.PublishedAtUtc);
        Assert.Equal(Retrieved, sourceItem.RetrievedAtUtc);
        Assert.Equal(Created, sourceItem.CreatedAtUtc);
        Assert.NotNull(sourceItem.Location);
    }

    [Theory]
    [InlineData(SourceType.XPost)]
    [InlineData(SourceType.NewsArticle)]
    [InlineData(SourceType.OfficialStatement)]
    [InlineData(SourceType.DatasetRecord)]
    [InlineData(SourceType.Other)]
    public void PreservesTypedSourceType(SourceType sourceType)
    {
        var sourceItem = SourceItem.Create(new SourceItemId($"src-{sourceType}"), sourceType, null, null, "Source text", null, null, null, Published, Retrieved, null, Created);

        Assert.Equal(sourceType, sourceItem.SourceType);
    }

    [Fact]
    public void OptionalFieldsCanBeOmitted()
    {
        var sourceItem = SourceItem.Create(new SourceItemId("src-minimal"), SourceType.NewsArticle, null, null, "Source text", null, null, null, Published, Retrieved, null, Created);

        Assert.Null(sourceItem.Title);
        Assert.Null(sourceItem.Url);
        Assert.Null(sourceItem.AuthorDisplayName);
        Assert.Null(sourceItem.AuthorHandle);
        Assert.Null(sourceItem.Location);
    }
}