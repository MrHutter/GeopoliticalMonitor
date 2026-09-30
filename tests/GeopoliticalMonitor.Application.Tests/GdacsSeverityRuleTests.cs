using GeopoliticalMonitor.Application.Gdacs;
using GeopoliticalMonitor.Domain.Events;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class GdacsSeverityRuleTests
{
    [Theory]
    [InlineData("Green", null, EventSeverity.Low)]
    [InlineData("Green", 0.0, EventSeverity.Low)]
    [InlineData("Green", 5.99, EventSeverity.Low)]
    [InlineData("Green", 6.0, EventSeverity.Medium)]
    [InlineData("Green", 9.1, EventSeverity.Medium)]
    [InlineData("Orange", null, EventSeverity.High)]
    [InlineData("Orange", 4.2, EventSeverity.High)]
    [InlineData("Orange", 8.9, EventSeverity.High)]
    [InlineData("Red", null, EventSeverity.Critical)]
    [InlineData("Red", 5.0, EventSeverity.Critical)]
    [InlineData("Red", 7.49, EventSeverity.Critical)]
    [InlineData("Red", 7.5, EventSeverity.Catastrophic)]
    [InlineData("Red", 9.5, EventSeverity.Catastrophic)]
    public void MapsAlertLevelAndMagnitudeAtEveryBoundary(string alertLevel, double? magnitude, EventSeverity expected)
    {
        var assessment = GdacsSeverityRule.Assess(alertLevel, magnitude);

        Assert.Equal(expected, assessment.Level);
        Assert.Equal("gdacs-eq-v1", assessment.Rule);
    }

    [Theory]
    [InlineData("Orange", 6.4, "alert=Orange; magnitude=6.4")]
    [InlineData("Green", 6.0, "alert=Green; magnitude=6.0")]
    [InlineData("Red", 7.55, "alert=Red; magnitude=7.55")]
    [InlineData("Red", null, "alert=Red")]
    public void FormatsInputsAndOmitsMissingMagnitude(string alertLevel, double? magnitude, string expected)
    {
        Assert.Equal(expected, GdacsSeverityRule.Assess(alertLevel, magnitude).Inputs);
    }

    [Theory]
    [InlineData("green")]
    [InlineData("Yellow")]
    [InlineData("")]
    public void RejectsUnsupportedAlertLevel(string alertLevel)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GdacsSeverityRule.Assess(alertLevel, 6));
    }

    [Fact]
    public void ParsesAppAuthoredProvenanceText()
    {
        var text = string.Join("\r\n", "Provider: GDACS", "Record: EQ-1, episode 2", "GDACS alert level: Red", "Magnitude: 7.6 M", "Country: Chile");

        Assert.True(GdacsSeverityRule.TryAssessFromProvenanceText(text, out var assessment));
        Assert.Equal(EventSeverity.Catastrophic, assessment!.Level);
        Assert.Equal("alert=Red; magnitude=7.6", assessment.Inputs);
    }

    [Fact]
    public void ParsesProvenanceTextWithoutMagnitude()
    {
        var text = "GDACS alert level: Green\nMagnitude: Not provided";

        Assert.True(GdacsSeverityRule.TryAssessFromProvenanceText(text, out var assessment));
        Assert.Equal(EventSeverity.Low, assessment!.Level);
        Assert.Equal("alert=Green", assessment.Inputs);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Provider record without structured lines")]
    [InlineData("GDACS alert level: Purple\nMagnitude: 6.0 M")]
    [InlineData("gdacs alert level: Red")]
    public void DoesNotAssessUnparseableProvenanceText(string? text)
    {
        Assert.False(GdacsSeverityRule.TryAssessFromProvenanceText(text, out var assessment));
        Assert.Null(assessment);
    }
}
