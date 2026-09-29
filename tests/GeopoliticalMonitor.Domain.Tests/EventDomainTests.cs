using GeopoliticalMonitor.Domain;
using GeopoliticalMonitor.Domain.Events;

namespace GeopoliticalMonitor.Domain.Tests;

public class EventDomainTests
{
    private static readonly DateTimeOffset Started = new(2026, 9, 17, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Updated = Started.AddMinutes(12);

    [Fact]
    public void CreatesValidEvent()
    {
        var location = new Location(57.4, 24.8, "Northern corridor", "DE", "Baltic demo region");

        var geopoliticalEvent = Event.Create(new EventId("evt-demo"), "Title", "Summary", EventCategory.Security, EventStatus.Developing, Started, Updated, location, Started, Updated);

        Assert.Equal("evt-demo", geopoliticalEvent.Id.Value);
        Assert.Equal("Title", geopoliticalEvent.Title);
        Assert.Equal(EventCategory.Security, geopoliticalEvent.Category);
        Assert.Equal(EventStatus.Developing, geopoliticalEvent.Status);
        Assert.Equal(57.4, geopoliticalEvent.Location.Latitude);
        Assert.Equal(24.8, geopoliticalEvent.Location.Longitude);
        Assert.Equal(Started, geopoliticalEvent.StartedAtUtc);
        Assert.Equal(Updated, geopoliticalEvent.UpdatedAtUtc);
    }

    [Fact]
    public void RejectsBlankTitle()
    {
        Assert.Throws<ArgumentException>(() => Event.Create(new EventId("evt-demo"), " ", "Summary", EventCategory.Security, EventStatus.Active, Started, Updated, new Location(0, 0, "Demo"), Started, Updated));
    }

    [Theory]
    [InlineData(EventCategory.MilitaryConflict)]
    [InlineData(EventCategory.Security)]
    [InlineData(EventCategory.PoliticalDiplomatic)]
    [InlineData(EventCategory.CivilUnrest)]
    [InlineData(EventCategory.Infrastructure)]
    [InlineData(EventCategory.Humanitarian)]
    [InlineData(EventCategory.NaturalDisaster)]
    [InlineData(EventCategory.Economic)]
    [InlineData(EventCategory.Other)]
    public void AcceptsSupportedCategory(EventCategory category)
    {
        var geopoliticalEvent = Event.Create(new EventId($"evt-{category}"), "Title", "Summary", category, EventStatus.Active, Started, Updated, new Location(0, 0, "Demo"), Started, Updated);

        Assert.Equal(category, geopoliticalEvent.Category);
    }

    [Theory]
    [InlineData(EventStatus.Developing)]
    [InlineData(EventStatus.Active)]
    [InlineData(EventStatus.Resolved)]
    [InlineData(EventStatus.Unverified)]
    public void AcceptsSupportedStatus(EventStatus status)
    {
        var geopoliticalEvent = Event.Create(new EventId($"evt-{status}"), "Title", "Summary", EventCategory.Other, status, Started, Updated, new Location(0, 0, "Demo"), Started, Updated);

        Assert.Equal(status, geopoliticalEvent.Status);
    }

    [Theory]
    [InlineData(-90, 0)]
    [InlineData(90, 0)]
    [InlineData(0, -180)]
    [InlineData(0, 180)]
    public void AcceptsValidCoordinates(double latitude, double longitude)
    {
        var location = new Location(latitude, longitude, "Valid point");

        Assert.Equal(latitude, location.Latitude);
        Assert.Equal(longitude, location.Longitude);
    }

    [Theory]
    [InlineData(-90.1, 0)]
    [InlineData(90.1, 0)]
    public void RejectsInvalidLatitude(double latitude, double longitude)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Location(latitude, longitude, "Invalid point"));
    }

    [Theory]
    [InlineData(0, -180.1)]
    [InlineData(0, 180.1)]
    public void RejectsInvalidLongitude(double latitude, double longitude)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Location(latitude, longitude, "Invalid point"));
    }
}
