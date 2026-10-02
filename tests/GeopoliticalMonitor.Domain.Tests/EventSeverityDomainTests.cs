using GeopoliticalMonitor.Domain;
using GeopoliticalMonitor.Domain.Events;

namespace GeopoliticalMonitor.Domain.Tests;

public class EventSeverityDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DefinesFiveOrderedSeverityLevels()
    {
        Assert.Equal(
            [EventSeverity.Low, EventSeverity.Medium, EventSeverity.High, EventSeverity.Critical, EventSeverity.Catastrophic],
            Enum.GetValues<EventSeverity>());
    }

    [Fact]
    public void DefinesSupportedProviders()
    {
        Assert.Equal(
            [EventProvider.Gdacs, EventProvider.UcdpCandidate, EventProvider.Gdelt, EventProvider.Demo, EventProvider.Manual],
            Enum.GetValues<EventProvider>());
    }

    [Fact]
    public void EventExposesSeverityAndProvenance()
    {
        var geopoliticalEvent = Event.Create(
            new EventId("evt-gdacs-eq-1"),
            "Title",
            "Summary",
            EventCategory.NaturalDisaster,
            EventStatus.Unverified,
            Now,
            Now,
            new Location(1, 2, "Place"),
            Now,
            Now,
            new EventSeverityAssessment(EventSeverity.Critical, "gdacs-eq-v1", "alert=Red"),
            new EventProvenance(EventProvider.Gdacs, "1"));

        Assert.Equal(EventSeverity.Critical, geopoliticalEvent.Severity);
        Assert.Equal("gdacs-eq-v1", geopoliticalEvent.SeverityRule);
        Assert.Equal("alert=Red", geopoliticalEvent.SeverityInputs);
        Assert.Equal(EventProvider.Gdacs, geopoliticalEvent.Provider);
        Assert.Equal("1", geopoliticalEvent.ProviderKey);
    }

    [Fact]
    public void EventRequiresSeverityAndProvenance()
    {
        var severity = new EventSeverityAssessment(EventSeverity.Low, EventSeverityAssessment.ManualRule, "test");
        var provenance = new EventProvenance(EventProvider.Manual);

        Assert.Throws<ArgumentNullException>(() => Create(null!, provenance));
        Assert.Throws<ArgumentNullException>(() => Create(severity, null!));
    }

    [Fact]
    public void AcceptsSeverityFieldsAtTheirMaximumLengths()
    {
        var assessment = new EventSeverityAssessment(EventSeverity.High, new string('r', 64), new string('i', 256));

        Assert.Equal(64, assessment.Rule.Length);
        Assert.Equal(256, assessment.Inputs.Length);
    }

    [Theory]
    [InlineData("", "inputs")]
    [InlineData("  ", "inputs")]
    [InlineData("rule", "")]
    [InlineData("rule", "   ")]
    public void RejectsBlankSeverityRuleOrInputs(string rule, string inputs)
    {
        Assert.Throws<ArgumentException>(() => new EventSeverityAssessment(EventSeverity.Low, rule, inputs));
    }

    [Fact]
    public void RejectsOverlongSeverityRuleOrInputs()
    {
        Assert.Throws<ArgumentException>(() => new EventSeverityAssessment(EventSeverity.Low, new string('r', 65), "inputs"));
        Assert.Throws<ArgumentException>(() => new EventSeverityAssessment(EventSeverity.Low, "rule", new string('i', 257)));
    }

    [Fact]
    public void RejectsUndefinedSeverityAndProvider()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventSeverityAssessment((EventSeverity)99, "rule", "inputs"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventProvenance((EventProvider)99));
    }

    [Fact]
    public void NormalizesBlankProviderKeyAndRejectsOverlongKey()
    {
        Assert.Null(new EventProvenance(EventProvider.Demo, "  ").ProviderKey);
        Assert.Equal(256, new EventProvenance(EventProvider.Gdacs, new string('7', 256)).ProviderKey!.Length);
        Assert.Throws<ArgumentException>(() => new EventProvenance(EventProvider.Gdacs, new string('7', 257)));
    }

    [Theory]
    [InlineData(200001)]
    [InlineData(202608)]
    [InlineData(202612)]
    [InlineData(999912)]
    public void ProvenanceAcceptsReleaseOrdinalsAsYearTimesHundredPlusMonth(int release)
    {
        var provenance = new EventProvenance(EventProvider.UcdpCandidate, "1", release);

        Assert.Equal(release, provenance.ProviderLastSeenRelease);
        Assert.Equal(release, Create(new EventSeverityAssessment(EventSeverity.Low, "manual", "test"), provenance).ProviderLastSeenRelease);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(202600)]
    [InlineData(202613)]
    [InlineData(199912)]
    [InlineData(1000001)]
    public void ProvenanceRejectsInvalidReleaseOrdinals(int release)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventProvenance(EventProvider.UcdpCandidate, "1", release));
        Assert.Null(new EventProvenance(EventProvider.Gdacs, "1").ProviderLastSeenRelease);
    }

    private static Event Create(EventSeverityAssessment severity, EventProvenance provenance)
        => Event.Create(
            new EventId("evt-one"),
            "Title",
            "Summary",
            EventCategory.Other,
            EventStatus.Active,
            Now,
            Now,
            new Location(0, 0, "Place"),
            Now,
            Now,
            severity,
            provenance);
}
