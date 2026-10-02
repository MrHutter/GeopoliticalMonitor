using System.Buffers;
using System.Net;
using GeopoliticalMonitor.Application;
using GeopoliticalMonitor.Application.Ucdp;

namespace GeopoliticalMonitor.Infrastructure.Ucdp;

/// <summary>
/// Downloads one monthly UCDP Candidate CSV from the fixed official host over https. Redirects are rejected, there are
/// no retries, the body is streamed under a hard byte cap, and every request target is checked before it is sent.
/// Exception messages are internal and never contain URLs or response content.
/// </summary>
public sealed class UcdpCandidateFeed : IUcdpCandidateFeed
{
    public const string OfficialHost = "ucdp.uu.se";
    public const string ReleasePath = "/downloads/candidateged/";
    public const int MaximumResponseBytes = 32 * 1024 * 1024;
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(60);

    private readonly HttpClient httpClient;
    private readonly UcdpCandidateCsvParser parser;
    private readonly TimeSpan requestTimeout;
    private readonly int maximumResponseBytes;

    public UcdpCandidateFeed(
        HttpClient httpClient,
        UcdpCandidateCsvParser parser,
        TimeSpan? requestTimeout = null,
        int maximumResponseBytes = MaximumResponseBytes)
    {
        this.httpClient = httpClient;
        this.parser = parser;
        this.requestTimeout = requestTimeout ?? RequestTimeout;
        this.maximumResponseBytes = maximumResponseBytes;
    }

    public async Task<UcdpReleaseFetchResult> FetchReleaseAsync(UcdpRelease release, CancellationToken cancellationToken = default)
    {
        var requestUri = BuildReleaseUri(release);
        if (!IsPermittedTarget(requestUri))
        {
            throw new UcdpFeedException(UcdpFeedFailureKind.ProviderUnavailable, "UCDP request target is not permitted.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.UserAgent.ParseAdd(ProductIdentity.UserAgent);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(requestTimeout);

        try
        {
            using var response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return UcdpReleaseFetchResult.NotPublished(release);
            }

            var statusCode = (int)response.StatusCode;
            if (statusCode is >= 300 and < 400)
            {
                throw new UcdpFeedException(UcdpFeedFailureKind.InvalidResponse, "UCDP redirect was rejected.");
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new UcdpFeedException(UcdpFeedFailureKind.ProviderUnavailable, "UCDP request was not successful.");
            }

            if (response.RequestMessage?.RequestUri is { } finalUri && !IsPermittedTarget(finalUri))
            {
                throw new UcdpFeedException(UcdpFeedFailureKind.InvalidResponse, "UCDP response came from an unexpected target.");
            }

            if (response.Content.Headers.ContentLength is { } length && length > maximumResponseBytes)
            {
                throw new UcdpFeedException(UcdpFeedFailureKind.InvalidResponse, "UCDP response exceeded the permitted size.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            var payload = await ReadBoundedAsync(stream, maximumResponseBytes, timeout.Token).ConfigureAwait(false);
            return UcdpReleaseFetchResult.Published(release, parser.Parse(payload));
        }
        catch (UcdpFeedException)
        {
            throw;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UcdpFeedException(UcdpFeedFailureKind.TimedOut, "UCDP request timed out.", exception);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            throw new UcdpFeedException(UcdpFeedFailureKind.ProviderUnavailable, "UCDP is unavailable.", exception);
        }
        catch (IOException exception)
        {
            throw new UcdpFeedException(UcdpFeedFailureKind.ProviderUnavailable, "UCDP response could not be read.", exception);
        }
    }

    public static HttpClient CreateProductionHttpClient()
    {
        return new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    /// <summary>The only URL shape this feed requests; the file name comes from an internal release value.</summary>
    public static Uri BuildReleaseUri(UcdpRelease release)
        => new($"https://{OfficialHost}{ReleasePath}{release.FileName}", UriKind.Absolute);

    public static bool IsPermittedTarget(Uri uri)
        => uri.IsAbsoluteUri
            && uri.Scheme == Uri.UriSchemeHttps
            && string.Equals(uri.Host, OfficialHost, StringComparison.OrdinalIgnoreCase)
            && uri.IsDefaultPort
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.UserInfo)
            && uri.AbsolutePath.StartsWith(ReleasePath, StringComparison.Ordinal);

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, int maximumBytes, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
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

                if (output.Length + read > maximumBytes)
                {
                    throw new UcdpFeedException(UcdpFeedFailureKind.InvalidResponse, "UCDP response exceeded the permitted size.");
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
