using System.Net;
using System.Text;
using System.Text.Json;
using GeopoliticalMonitor.Application.ReliefWeb;
using GeopoliticalMonitor.Infrastructure.ReliefWeb;

namespace GeopoliticalMonitor.Infrastructure.Tests;

public sealed class ReliefWebReportClientTests
{
    [Fact]
    public async Task SendsFixedBoundedPostSearchWithNativeAppName()
    {
        HttpRequestMessage? captured = null;
        string? body = null;
        var payload = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "reliefweb-reports.json"));
        using var httpClient = new HttpClient(new StubHandler(async (request, _) =>
        {
            captured = request;
            body = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) };
        }));
        var client = CreateClient(httpClient);

        var result = await client.SearchReportsAsync(new(
            "flood response", DateTimeOffset.Parse("2026-09-01T00:00:00Z"), DateTimeOffset.Parse("2026-09-30T00:00:00Z"), "EXA"));

        Assert.Equal(2, result.Reports.Count);
        Assert.Equal(HttpMethod.Post, captured?.Method);
        Assert.Equal(Uri.UriSchemeHttps, captured?.RequestUri?.Scheme);
        Assert.Equal(ReliefWebReportClient.OfficialHost, captured?.RequestUri?.Host);
        Assert.Equal("/v2/reports", captured?.RequestUri?.AbsolutePath);
        Assert.Contains("appname=unit-test-app", captured?.RequestUri?.Query);
        using var json = JsonDocument.Parse(body!);
        Assert.Equal(20, json.RootElement.GetProperty("limit").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("offset").GetInt32());
        Assert.Equal("AND", json.RootElement.GetProperty("query").GetProperty("operator").GetString());
        Assert.Contains("date.original", body);
        Assert.Contains("country.iso3.exact", body);
        Assert.Contains("date.changed", body);
        Assert.DoesNotContain("body", body);
    }

    [Fact]
    public async Task SendsFixedItemGetAndParsesAuthoritativeReport()
    {
        HttpRequestMessage? captured = null;
        var payload = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "reliefweb-reports.json"));
        using var httpClient = new HttpClient(new StubHandler((request, _) =>
        {
            captured = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) });
        }));

        var result = await CreateClient(httpClient).GetReportAsync(5187658);

        Assert.Equal(5187658, result.ReportId);
        Assert.Equal(HttpMethod.Get, captured?.Method);
        Assert.Equal("/v2/reports/5187658", captured?.RequestUri?.AbsolutePath);
        Assert.Contains("appname=unit-test-app", captured?.RequestUri?.Query);
        Assert.Contains("fields[include][]=title", Uri.UnescapeDataString(captured?.RequestUri?.Query ?? string.Empty));
    }

    [Fact]
    public async Task MissingAppNameNeverSendsRequest()
    {
        var requestCount = 0;
        using var httpClient = new HttpClient(new StubHandler((_, _) =>
        {
            requestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }));
        var client = new ReliefWebReportClient(httpClient, new ReliefWebReportParser(), new ReliefWebClientOptions(null));

        var exception = await Assert.ThrowsAsync<ReliefWebClientException>(() => client.SearchReportsAsync(Criteria()));
        Assert.Equal(ReliefWebFailureKind.ConfigurationUnavailable, exception.Kind);
        var attachException = await Assert.ThrowsAsync<ReliefWebClientException>(() => client.GetReportAsync(5187658));
        Assert.Equal(ReliefWebFailureKind.ConfigurationUnavailable, attachException.Kind);
        Assert.Equal(0, requestCount);
    }

    [Fact]
    public async Task RejectsMalformedSuccessPayload()
    {
        using var httpClient = new HttpClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        })));
        var exception = await Assert.ThrowsAsync<ReliefWebClientException>(() => CreateClient(httpClient).SearchReportsAsync(Criteria()));
        Assert.Equal(ReliefWebFailureKind.InvalidResponse, exception.Kind);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, ReliefWebFailureKind.QuotaExceeded)]
    [InlineData(HttpStatusCode.Unauthorized, ReliefWebFailureKind.InvalidConfiguration)]
    [InlineData(HttpStatusCode.Forbidden, ReliefWebFailureKind.InvalidConfiguration)]
    [InlineData(HttpStatusCode.NotFound, ReliefWebFailureKind.NotFound)]
    [InlineData(HttpStatusCode.BadGateway, ReliefWebFailureKind.ProviderUnavailable)]
    public async Task MapsProviderStatusCodes(HttpStatusCode statusCode, ReliefWebFailureKind expected)
    {
        using var httpClient = new HttpClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(statusCode))));
        var exception = await Assert.ThrowsAsync<ReliefWebClientException>(() => CreateClient(httpClient).GetReportAsync(1));
        Assert.Equal(expected, exception.Kind);
    }

    [Fact]
    public async Task EnforcesTimeoutResponseBoundAndCallerCancellation()
    {
        using var timeoutClient = new HttpClient(new StubHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        var timeout = await Assert.ThrowsAsync<ReliefWebClientException>(() =>
            new ReliefWebReportClient(timeoutClient, new ReliefWebReportParser(), new("unit-test-app"), TimeSpan.FromMilliseconds(20))
                .SearchReportsAsync(Criteria()));
        Assert.Equal(ReliefWebFailureKind.TimedOut, timeout.Kind);

        using var oversizedClient = new HttpClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[ReliefWebReportClient.MaximumResponseBytes + 1]),
        })));
        var oversized = await Assert.ThrowsAsync<ReliefWebClientException>(() => CreateClient(oversizedClient).SearchReportsAsync(Criteria()));
        Assert.Equal(ReliefWebFailureKind.InvalidResponse, oversized.Kind);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateClient(timeoutClient).SearchReportsAsync(Criteria(), cancellation.Token));
    }

    private static ReliefWebReportClient CreateClient(HttpClient httpClient)
        => new(httpClient, new ReliefWebReportParser(), new ReliefWebClientOptions("unit-test-app"));

    private static ReliefWebSearchCriteria Criteria() => new(
        "flood", DateTimeOffset.Parse("2026-09-01T00:00:00Z"), DateTimeOffset.Parse("2026-09-30T00:00:00Z"), null);

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => response(request, cancellationToken);
    }
}
