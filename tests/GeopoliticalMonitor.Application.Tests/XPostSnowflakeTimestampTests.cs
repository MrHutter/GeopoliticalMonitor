using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class XPostSnowflakeTimestampTests
{
    private static readonly DateTimeOffset ReferenceNow = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DecodesKnownStatusIdToDeterministicUtcTimestamp()
    {
        var accepted = XPostSnowflakeTimestamp.TryDecode("1759288472993702104", ReferenceNow, out var timestamp);

        Assert.True(accepted);
        Assert.Equal(new DateTimeOffset(2024, 2, 18, 18, 47, 12, 617, TimeSpan.Zero), timestamp);
        Assert.Equal(TimeSpan.Zero, timestamp.Offset);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123abc")]
    [InlineData("+123")]
    [InlineData("18446744073709551616")]
    public void RejectsMalformedOrOverflowingStatusIds(string? statusId)
    {
        Assert.False(XPostSnowflakeTimestamp.TryDecode(statusId, ReferenceNow, out _));
    }

    [Fact]
    public void AcceptsTheSnowflakeEpochBoundary()
    {
        Assert.True(XPostSnowflakeTimestamp.TryDecode("0", ReferenceNow, out var timestamp));
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(XPostSnowflakeTimestamp.EpochUnixMilliseconds), timestamp);
    }

    [Fact]
    public void RejectsImplausibleFutureTimestamp()
    {
        const string futureStatusId = "2104070444356661248";

        Assert.False(XPostSnowflakeTimestamp.TryDecode(futureStatusId, ReferenceNow, out _));
    }
}
