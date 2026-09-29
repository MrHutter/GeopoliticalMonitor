using GeopoliticalMonitor.Domain.Alerts;
using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Watchlists;

namespace GeopoliticalMonitor.Domain.Tests;

public sealed class AlertDomainTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly AlertRuleId RuleId = new("ar-0123456789abcdef0123456789abcdef");
    private static readonly AlertId OccurrenceId = new("al-0123456789abcdef0123456789abcdef");
    private static readonly WatchlistId WatchlistId = new("wl-0123456789abcdef0123456789abcdef");

    [Fact]
    public void GeneratedIdsUseRequiredOpaqueLocalFormats()
    {
        var ruleId = AlertRuleId.CreateNew();
        var alertId = AlertId.CreateNew();

        Assert.Matches("^ar-[0-9a-f]{32}$", ruleId.Value);
        Assert.Matches("^al-[0-9a-f]{32}$", alertId.Value);
        Assert.True(AlertRuleId.TryParse(ruleId.Value, out var parsedRule));
        Assert.True(AlertId.TryParse(alertId.Value, out var parsedAlert));
        Assert.Equal(ruleId, parsedRule);
        Assert.Equal(alertId, parsedAlert);
    }

    [Theory]
    [InlineData("ar-0123456789abcdef0123456789abcde")]
    [InlineData("ar-0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("xx-0123456789abcdef0123456789abcdef")]
    [InlineData("ar-0123456789abcdef0123456789abcdeg")]
    public void RejectsMalformedRuleIds(string value)
    {
        Assert.False(AlertRuleId.TryParse(value, out _));
        Assert.Throws<ArgumentException>(() => new AlertRuleId(value));
    }

    [Theory]
    [InlineData("al-0123456789abcdef0123456789abcde")]
    [InlineData("al-0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("xx-0123456789abcdef0123456789abcdef")]
    [InlineData("al-0123456789abcdef0123456789abcdeg")]
    public void RejectsMalformedAlertIds(string value)
    {
        Assert.False(AlertId.TryParse(value, out _));
        Assert.Throws<ArgumentException>(() => new AlertId(value));
    }

    [Fact]
    public void RuleLifecyclePreservesCreatedTimeAndUpdatesLastEnabledOnlyOnReenable()
    {
        var rule = AlertRule.CreateEnabled(RuleId, WatchlistId, AlertTriggerType.GdacsEventMateriallyUpdated, CreatedAt);
        var unchanged = rule.SetEnabled(true, CreatedAt.AddMinutes(1));
        var disabled = rule.SetEnabled(false, CreatedAt.AddMinutes(2));
        var reenabled = disabled.SetEnabled(true, CreatedAt.AddMinutes(3));

        Assert.True(rule.IsEnabled);
        Assert.Same(rule, unchanged);
        Assert.False(disabled.IsEnabled);
        Assert.Equal(CreatedAt, disabled.LastEnabledAtUtc);
        Assert.True(reenabled.IsEnabled);
        Assert.Equal(CreatedAt, reenabled.CreatedAtUtc);
        Assert.Equal(CreatedAt.AddMinutes(3), reenabled.LastEnabledAtUtc);
    }

    [Fact]
    public void RuleAndOccurrenceRejectNonUtcTimestamps()
    {
        var local = CreatedAt.ToOffset(TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(() => AlertRule.CreateEnabled(
            RuleId,
            WatchlistId,
            AlertTriggerType.GdacsEventMateriallyUpdated,
            local));
        Assert.Throws<ArgumentException>(() => AlertOccurrence.Create(
            OccurrenceId,
            RuleId,
            new EventId("evt-one"),
            local,
            CreatedAt));
    }

    [Fact]
    public void OccurrenceBeginsUnreadAndFirstMarkReadWins()
    {
        var occurrence = AlertOccurrence.Create(
            OccurrenceId,
            RuleId,
            new EventId("evt-one"),
            CreatedAt.AddMinutes(-1),
            CreatedAt);

        var read = occurrence.MarkRead(CreatedAt.AddMinutes(1));
        var repeated = read.MarkRead(CreatedAt.AddMinutes(2));

        Assert.False(occurrence.IsRead);
        Assert.True(read.IsRead);
        Assert.Equal(CreatedAt.AddMinutes(1), read.ReadAtUtc);
        Assert.Same(read, repeated);
    }
}
