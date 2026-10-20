using System.Globalization;
using System.Net;
using System.Text.Json;
using CareerPilot.Application.Jobs;

namespace CareerPilot.Infrastructure.Jobs;

public sealed class ConnectorOptions
{
    public int MaxAttempts { get; set; } = 3;
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(2);
}

/// <summary>Shared HTTP + JSON helpers for the board connectors (retry, content-type check, tolerant parsing).</summary>
internal static class ConnectorSupport
{
    public const string UserAgent = "CareerPilotPersonal/1.0 (personal job search tool)";

    public static async Task<JsonDocument> GetJsonAsync(HttpClient http, ConnectorOptions options, string url, CancellationToken ct)
    {
        Exception? last = null;
        for (var attempt = 1; attempt <= Math.Max(1, options.MaxAttempts); attempt++)
        {
            if (attempt > 1 && options.BaseDelay > TimeSpan.Zero)
                await Task.Delay(options.BaseDelay * (attempt - 1), ct);

            try
            {
                using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

                if (response.StatusCode == HttpStatusCode.NotFound)
                    throw new SourceFetchException("HTTP 404: this board was not found (check the board token).", notFound: true);

                if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    last = new SourceFetchException($"HTTP {(int)response.StatusCode}: the source is temporarily unavailable.");
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                    throw new SourceFetchException($"HTTP {(int)response.StatusCode}: the source refused the request.");

                // Some sources occasionally answer 200 with an HTML page; that is not a job feed.
                var mediaType = response.Content.Headers.ContentType?.MediaType;
                if (mediaType is not null && !mediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
                {
                    last = new SourceFetchException($"The source returned '{mediaType}' instead of JSON.");
                    continue;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                try
                {
                    return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                }
                catch (JsonException ex)
                {
                    last = new SourceFetchException("The source returned malformed JSON.", inner: ex);
                }
            }
            catch (HttpRequestException ex)
            {
                last = new SourceFetchException("The source could not be reached.", inner: ex);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                last = new SourceFetchException("The source timed out.", inner: ex);
            }
        }
        throw last as SourceFetchException ?? new SourceFetchException("The source could not be read.");
    }

    public static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    /// <summary>Ids may be numbers (Greenhouse) or strings (Lever, Ashby).</summary>
    public static string? Id(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            _ => null,
        };
    }

    public static DateTimeOffset? Date(JsonElement e, string name) =>
        Str(e, name) is { } s && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d)
            ? d
            : null;

    public static decimal? Decimal(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)
            ? d
            : null;

    public static JsonElement Prop(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) ? v : default;
}
