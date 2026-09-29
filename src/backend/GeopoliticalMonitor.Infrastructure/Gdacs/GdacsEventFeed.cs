using System.Buffers;
using System.Globalization;
using GeopoliticalMonitor.Application.Gdacs;

namespace GeopoliticalMonitor.Infrastructure.Gdacs;

public sealed class GdacsEventFeed : IGdacsEventFeed
{
    public const string OfficialHost = "www.gdacs.org";
    public const int MaximumResponseBytes = 1024 * 1024;
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    private readonly HttpClient httpClient;
    private readonly GdacsGeoJsonParser parser;
    private readonly TimeSpan requestTimeout;

    public GdacsEventFeed(HttpClient httpClient, GdacsGeoJsonParser parser, TimeSpan? requestTimeout = null)
    {
        this.httpClient = httpClient;
        this.parser = parser;
        this.requestTimeout = requestTimeout ?? RequestTimeout;
    }

    public async Task<GdacsEarthquakeFeedBatch> FetchRecentEarthquakesAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        var requestUri = BuildRequestUri(fromUtc, toUtc);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(requestTimeout);

        try
        {
            using var response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                return new GdacsEarthquakeFeedBatch([], 0);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new GdacsFeedException(
                    GdacsFeedFailureKind.ProviderUnavailable,
                    $"GDACS request failed with HTTP {(int)response.StatusCode}.");
            }

            if (response.Content.Headers.ContentLength is > MaximumResponseBytes)
            {
                throw InvalidResponse("GDACS response exceeded the permitted size.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            var payload = await ReadBoundedAsync(stream, timeout.Token).ConfigureAwait(false);
            return parser.Parse(payload);
        }
        catch (GdacsFeedException)
        {
            throw;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GdacsFeedException(GdacsFeedFailureKind.TimedOut, "GDACS request timed out.", exception);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            throw new GdacsFeedException(GdacsFeedFailureKind.ProviderUnavailable, "GDACS is unavailable.", exception);
        }
        catch (IOException exception)
        {
            throw new GdacsFeedException(GdacsFeedFailureKind.ProviderUnavailable, "GDACS response could not be read.", exception);
        }
    }

    public static HttpClient CreateProductionHttpClient()
    {
        return new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }, disposeHandler: true);
    }

    internal static Uri BuildRequestUri(DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        var from = Uri.EscapeDataString(fromUtc.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        var to = Uri.EscapeDataString(toUtc.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        return new Uri(
            $"https://{OfficialHost}/gdacsapi/api/events/geteventlist/SEARCH?eventlist=EQ&fromdate={from}&todate={to}&pagesize={GdacsGeoJsonParser.MaximumRecords}&pagenumber=1");
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
                    throw InvalidResponse("GDACS response exceeded the permitted size.");
                }

                output.Write(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static GdacsFeedException InvalidResponse(string message)
    {
        return new GdacsFeedException(GdacsFeedFailureKind.InvalidResponse, message);
    }
}
