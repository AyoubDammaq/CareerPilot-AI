using System.Net;
using System.Net.Sockets;
using CareerPilot.Application.Jobs;

namespace CareerPilot.Infrastructure.Jobs;

/// <summary>
/// Fetches one public job page for the "import from URL" feature, with SSRF protection:
/// https only; the resolved IP address of every connection is checked (so a public name cannot point at a private address,
/// and DNS changes between check and connect do not matter); redirects are followed by hand, at most 3, each one re-validated;
/// no proxy, no credentials, no cookies; the page size and the time are capped. It never signs in and never solves a challenge:
/// a login wall, CAPTCHA or error page simply fails with a clear message.
/// </summary>
public sealed class SafeJobPageFetcher(HttpClient http, DiscoveryOptions options) : IJobPageFetcher
{
    private const int MaxRedirects = 3;

    public static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        UseCookies = false,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        ConnectCallback = ConnectToPublicAddressAsync,
    };

    public async Task<FetchedPage> FetchAsync(Uri url, CancellationToken ct)
    {
        var current = url;
        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml");
            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (HttpRequestException ex)
            {
                throw new SourceFetchException("The page could not be reached.", inner: ex);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                throw new SourceFetchException("The page took too long to answer.", inner: ex);
            }

            using (response)
            {
                if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
                {
                    var next = location.IsAbsoluteUri ? location : new Uri(current, location);
                    try { current = UrlGuard.ParsePublicHttpsUrl(next.ToString()); }
                    catch (Application.Common.ValidationFailedException)
                    {
                        throw new SourceFetchException("The page redirects to an address that is not a public https website; it was not followed.");
                    }
                    if (UrlGuard.IsInHostList(current, options.ManualOnlyHosts))
                        throw new SourceFetchException($"The page redirects to {current.Host}, which CareerPilot does not fetch automatically.");
                    continue;
                }

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                    throw new SourceFetchException($"The site refused automated access (HTTP {(int)response.StatusCode}). Open the job yourself and paste its text.");
                if (!response.IsSuccessStatusCode)
                    throw new SourceFetchException($"The page answered HTTP {(int)response.StatusCode}.");

                var media = response.Content.Headers.ContentType?.MediaType;
                if (media is null || !(media.Contains("html", StringComparison.OrdinalIgnoreCase) || media.Contains("xml", StringComparison.OrdinalIgnoreCase)))
                    throw new SourceFetchException("The address is not a web page.");

                return new FetchedPage(current, await ReadLimitedAsync(response, options.ImportMaxPageBytes, ct));
            }
        }
        throw new SourceFetchException("The page redirects too many times.");
    }

    private static async Task<string> ReadLimitedAsync(HttpResponseMessage response, int maxBytes, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength is { } declared && declared > maxBytes)
            throw new SourceFetchException("The page is too large to import.");

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > maxBytes) throw new SourceFetchException("The page is too large to import.");
            buffer.Write(chunk, 0, read);
        }
        return System.Text.Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static async ValueTask<Stream> ConnectToPublicAddressAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, ct);

        if (addresses.Length == 0 || addresses.Any(a => !UrlGuard.IsPublicAddress(a)))
            throw new HttpRequestException("The address does not resolve to a public website.");

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
