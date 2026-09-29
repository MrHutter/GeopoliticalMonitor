using System.Buffers;
using System.Globalization;
using GeopoliticalMonitor.Application.Firms;

namespace GeopoliticalMonitor.Infrastructure.Firms;

public sealed class FirmsObservationFeed : IFirmsObservationFeed
{
    public const string OfficialHost = "firms.modaps.eosdis.nasa.gov";
    public const int DayRange = 2;
    public const int MaximumResponseBytes = 4 * 1024 * 1024;
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    private readonly HttpClient httpClient;
    private readonly FirmsCsvParser parser;
    private readonly FirmsClientOptions options;
    private readonly TimeSpan requestTimeout;

    public FirmsObservationFeed(
        HttpClient httpClient,
        FirmsCsvParser parser,
        FirmsClientOptions options,
        TimeSpan? requestTimeout = null)
    {
        this.httpClient = httpClient;
        this.parser = parser;
        this.options = options;
        this.requestTimeout = requestTimeout ?? RequestTimeout;
    }

    public async Task<FirmsObservationBatch> FetchAsync(
        FirmsSource source,
        FirmsBoundingBox bounds,
        CancellationToken cancellationToken = default)
    {
        var mapKey = options.NormalizedMapKey;
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
                throw new FirmsFeedException(
                    FirmsFeedFailureKind.RequestFailed,
                    $"NASA FIRMS request failed with HTTP {(int)response.StatusCode}.");
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

    internal static Uri BuildRequestUri(string mapKey, FirmsSource source, FirmsBoundingBox bounds)
    {
        var area = string.Join(",",
            FormatCoordinate(bounds.West),
            FormatCoordinate(bounds.South),
            FormatCoordinate(bounds.East),
            FormatCoordinate(bounds.North));
        return new Uri(
            $"https://{OfficialHost}/api/area/csv/{Uri.EscapeDataString(mapKey)}/{source.ToProviderValue()}/{area}/{DayRange}");
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
