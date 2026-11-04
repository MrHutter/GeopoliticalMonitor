using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text;
using GeopoliticalMonitor.Application.Firms;

namespace GeopoliticalMonitor.Infrastructure.Firms;

public sealed class FirmsObservationFeed : IFirmsObservationFeed
{
    public const string OfficialHost = "firms.modaps.eosdis.nasa.gov";
    public const int DayRange = 2;
    public const int MaximumResponseBytes = 4 * 1024 * 1024;
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Only the start of an error body is read, to recognise NASA's plain-text error lines.</summary>
    public const int MaximumErrorBodyBytes = 1024;

    // Observed from firms.modaps.eosdis.nasa.gov on 2026-11-01: an unknown key returns HTTP 400 text/plain
    // "Invalid MAP_KEY."; an unknown product returns HTTP 400 with "Invalid source.". NASA documents neither.
    internal const string InvalidKeyResponseText = "Invalid MAP_KEY";
    internal const string InvalidSourceResponseText = "Invalid source";

    private readonly HttpClient httpClient;
    private readonly FirmsCsvParser parser;
    private readonly IFirmsKeyResolver keyResolver;
    private readonly TimeSpan requestTimeout;

    public FirmsObservationFeed(
        HttpClient httpClient,
        FirmsCsvParser parser,
        IFirmsKeyResolver keyResolver,
        TimeSpan? requestTimeout = null)
    {
        this.httpClient = httpClient;
        this.parser = parser;
        this.keyResolver = keyResolver;
        this.requestTimeout = requestTimeout ?? RequestTimeout;
    }

    public async Task<FirmsObservationBatch> FetchAsync(
        FirmsSource source,
        FirmsBoundingBox bounds,
        CancellationToken cancellationToken = default)
    {
        // Resolved for this request only, so a key set or removed in the app applies to the next refresh.
        var mapKey = keyResolver.Resolve().Key;
        if (mapKey is null)
        {
            throw new FirmsFeedException(
                FirmsFeedFailureKind.ConfigurationUnavailable,
                "NASA FIRMS is not configured.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, BuildRequestUri(mapKey, source, bounds));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(requestTimeout);
        try
        {
            using var response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var kind = await ClassifyFailureAsync(response, timeout.Token).ConfigureAwait(false);
                throw new FirmsFeedException(kind, $"NASA FIRMS request failed with HTTP {(int)response.StatusCode}.");
            }

            if (response.Content.Headers.ContentLength is > MaximumResponseBytes)
            {
                throw new FirmsFeedException(
                    FirmsFeedFailureKind.ResponseTooLarge,
                    "NASA FIRMS response exceeded the permitted size.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            var payload = await ReadBoundedAsync(stream, timeout.Token).ConfigureAwait(false);
            return parser.Parse(payload, source);
        }
        catch (FirmsFeedException)
        {
            throw;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FirmsFeedException(FirmsFeedFailureKind.TimedOut, "NASA FIRMS request timed out.", exception);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            throw new FirmsFeedException(FirmsFeedFailureKind.RequestFailed, "NASA FIRMS is unavailable.", exception);
        }
        catch (IOException exception)
        {
            throw new FirmsFeedException(FirmsFeedFailureKind.RequestFailed, "NASA FIRMS response could not be read.", exception);
        }
    }

    public static HttpClient CreateProductionHttpClient()
        => new(new HttpClientHandler { AllowAutoRedirect = false }, disposeHandler: true);

    /// <summary>
    /// Maps a failed response to a bounded category. HTTP 429 is "Too Many Requests" by the HTTP standard; NASA does
    /// not document its over-limit response. The error body is used only for this decision and is never returned.
    /// </summary>
    private static async Task<FirmsFeedFailureKind> ClassifyFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return FirmsFeedFailureKind.RateLimited;
        }

        if (response.StatusCode != HttpStatusCode.BadRequest)
        {
            return FirmsFeedFailureKind.RequestFailed;
        }

        var text = await ReadErrorTextAsync(response, cancellationToken).ConfigureAwait(false);
        if (text.Contains(InvalidKeyResponseText, StringComparison.OrdinalIgnoreCase))
        {
            return FirmsFeedFailureKind.KeyRejected;
        }

        return text.Contains(InvalidSourceResponseText, StringComparison.OrdinalIgnoreCase)
            ? FirmsFeedFailureKind.ProductUnavailable
            : FirmsFeedFailureKind.RequestFailed;
    }

    private static async Task<string> ReadErrorTextAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var buffer = new byte[MaximumErrorBodyBytes];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return Encoding.UTF8.GetString(buffer, 0, total);
    }

    internal static Uri BuildRequestUri(FirmsMapKey mapKey, FirmsSource source, FirmsBoundingBox bounds)
    {
        var area = string.Join(",",
            FormatCoordinate(bounds.West),
            FormatCoordinate(bounds.South),
            FormatCoordinate(bounds.East),
            FormatCoordinate(bounds.North));
        return new Uri(
            $"https://{OfficialHost}/api/area/csv/{Uri.EscapeDataString(mapKey.RevealForProviderRequest())}/{source.ToProviderValue()}/{area}/{DayRange}");
    }

    private static string FormatCoordinate(double value)
        => value.ToString("0.######", CultureInfo.InvariantCulture);

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
                    throw new FirmsFeedException(
                        FirmsFeedFailureKind.ResponseTooLarge,
                        "NASA FIRMS response exceeded the permitted size.");
                }

                output.Write(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
