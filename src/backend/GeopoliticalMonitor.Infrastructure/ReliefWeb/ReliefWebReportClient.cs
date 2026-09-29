using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using GeopoliticalMonitor.Application.ReliefWeb;

namespace GeopoliticalMonitor.Infrastructure.ReliefWeb;

public sealed class ReliefWebReportClient : IReliefWebReportClient
{
    public const string OfficialHost = "api.reliefweb.int";
    public const int MaximumResponseBytes = 1024 * 1024;
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    private static readonly string[] IncludedFields =
    [
        "title", "url", "origin", "date.original", "date.created", "date.changed",
        "source.id", "source.name", "source.shortname", "format.id", "format.name",
        "country.id", "country.name", "country.iso3", "disaster.id", "disaster.name",
        "disaster.glide", "disaster.type.id", "disaster.type.name", "language.id", "language.name",
    ];

    private readonly HttpClient httpClient;
    private readonly ReliefWebReportParser parser;
    private readonly ReliefWebClientOptions options;
    private readonly TimeSpan requestTimeout;

    public ReliefWebReportClient(
        HttpClient httpClient,
        ReliefWebReportParser parser,
        ReliefWebClientOptions options,
        TimeSpan? requestTimeout = null)
    {
        this.httpClient = httpClient;
        this.parser = parser;
        this.options = options;
        this.requestTimeout = requestTimeout ?? RequestTimeout;
    }

    public async Task<ReliefWebReportBatch> SearchReportsAsync(
        ReliefWebSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var appName = RequireAppName();
        var requestUri = new Uri($"https://{OfficialHost}/v2/reports?appname={Uri.EscapeDataString(appName)}");
        var payload = new
        {
            profile = "minimal",
            query = new
            {
                value = criteria.QueryText,
                fields = new[] { "title", "disaster.name", "country.name" },
                @operator = "AND",
            },
            filter = BuildFilter(criteria),
            fields = new { include = IncludedFields },
            limit = ReliefWebReportParser.MaximumReports,
            offset = 0,
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        var responsePayload = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        return parser.ParseSearch(responsePayload);
    }

    public async Task<ReliefWebReportRecord> GetReportAsync(long reportId, CancellationToken cancellationToken = default)
    {
        var appName = RequireAppName();
        var fields = string.Join("&", IncludedFields.Select(field => $"fields[include][]={Uri.EscapeDataString(field)}"));
        var requestUri = new Uri(
            $"https://{OfficialHost}/v2/reports/{reportId.ToString(CultureInfo.InvariantCulture)}?appname={Uri.EscapeDataString(appName)}&profile=minimal&{fields}");
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        var responsePayload = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        return parser.ParseReport(responsePayload, reportId);
    }

    public static HttpClient CreateProductionHttpClient()
        => new(new HttpClientHandler { AllowAutoRedirect = false }, disposeHandler: true);

    private string RequireAppName()
    {
        var appName = options.AppName?.Trim();
        if (string.IsNullOrWhiteSpace(appName))
        {
            throw new ReliefWebClientException(
                ReliefWebFailureKind.ConfigurationUnavailable,
                "ReliefWeb integration is not configured.");
        }

        return appName;
    }

    private async Task<byte[]> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(requestTimeout);
        try
        {
            using var response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw MapFailure(response.StatusCode);
            }

            if (response.Content.Headers.ContentLength is > MaximumResponseBytes)
            {
                throw InvalidResponse("ReliefWeb response exceeded the permitted size.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            return await ReadBoundedAsync(stream, timeout.Token).ConfigureAwait(false);
        }
        catch (ReliefWebClientException)
        {
            throw;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ReliefWebClientException(ReliefWebFailureKind.TimedOut, "ReliefWeb request timed out.", exception);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            throw new ReliefWebClientException(ReliefWebFailureKind.ProviderUnavailable, "ReliefWeb is unavailable.", exception);
        }
        catch (IOException exception)
        {
            throw new ReliefWebClientException(ReliefWebFailureKind.ProviderUnavailable, "ReliefWeb response could not be read.", exception);
        }
    }

    private static object BuildFilter(ReliefWebSearchCriteria criteria)
    {
        var dateFilter = new
        {
            field = "date.original",
            value = new
            {
                from = criteria.FromDateUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                to = criteria.ToDateUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            },
        };
        return criteria.CountryIso3 is null
            ? new { conditions = new object[] { dateFilter }, @operator = "AND" }
            : new
            {
                conditions = new object[]
                {
                    dateFilter,
                    new { field = "country.iso3.exact", value = criteria.CountryIso3 },
                },
                @operator = "AND",
            };
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
        try
        {
            using var output = new MemoryStream();
            while (true)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return output.ToArray();
                }

                if (output.Length + read > MaximumResponseBytes)
                {
                    throw InvalidResponse("ReliefWeb response exceeded the permitted size.");
                }

                output.Write(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static ReliefWebClientException MapFailure(HttpStatusCode statusCode)
    {
        var kind = statusCode switch
        {
            HttpStatusCode.NotFound => ReliefWebFailureKind.NotFound,
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ReliefWebFailureKind.InvalidConfiguration,
            (HttpStatusCode)429 => ReliefWebFailureKind.QuotaExceeded,
            _ => ReliefWebFailureKind.ProviderUnavailable,
        };
        return new ReliefWebClientException(kind, $"ReliefWeb request failed with HTTP {(int)statusCode}.");
    }

    private static ReliefWebClientException InvalidResponse(string message)
        => new(ReliefWebFailureKind.InvalidResponse, message);
}
