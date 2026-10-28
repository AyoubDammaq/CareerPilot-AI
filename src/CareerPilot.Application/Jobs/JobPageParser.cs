using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CareerPilot.Domain.Profiles;

namespace CareerPilot.Application.Jobs;

/// <summary>
/// Reads the schema.org JobPosting data (JSON-LD) that many employers publish for search engines. Only what the page
/// states is used: missing fields stay null, and pages without a JobPosting yield nothing (the owner then pastes the text).
/// The page content is untrusted: it is parsed as data and never executed or followed.
/// </summary>
public static partial class JobPageParser
{
    [GeneratedRegex("<script[^>]*type\\s*=\\s*[\"']?application/ld\\+json[\"']?[^>]*>(.*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex LdJson();

    public static SourceJob? Parse(string html, Uri pageUrl)
    {
        foreach (Match m in LdJson().Matches(html))
        {
            JsonDocument doc;
            try { doc = JsonDocument.Parse(m.Groups[1].Value.Trim(), new JsonDocumentOptions { MaxDepth = 32 }); }
            catch (JsonException) { continue; }
            using (doc)
                foreach (var node in Nodes(doc.RootElement))
                    if (IsType(node, "JobPosting") && Map(node, pageUrl) is { } job)
                        return job;
        }
        return null;
    }

    private static IEnumerable<JsonElement> Nodes(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in e.EnumerateArray())
                foreach (var n in Nodes(item)) yield return n;
        }
        else if (e.ValueKind == JsonValueKind.Object)
        {
            yield return e;
            if (e.TryGetProperty("@graph", out var g))
                foreach (var n in Nodes(g)) yield return n;
        }
    }

    private static bool IsType(JsonElement e, string type)
    {
        if (!e.TryGetProperty("@type", out var t)) return false;
        return t.ValueKind switch
        {
            JsonValueKind.String => string.Equals(t.GetString(), type, StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Array => t.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String
                                                              && string.Equals(x.GetString(), type, StringComparison.OrdinalIgnoreCase)),
            _ => false,
        };
    }

    private static SourceJob? Map(JsonElement j, Uri pageUrl)
    {
        var title = Text(j, "title");
        var description = HtmlText.Normalize(HtmlText.ToPlainText(Text(j, "description")));
        if (string.IsNullOrWhiteSpace(title)) return null;

        var company = j.TryGetProperty("hiringOrganization", out var org)
            ? org.ValueKind == JsonValueKind.String ? org.GetString() : Text(org, "name")
            : null;

        var (location, country) = Location(j);
        var telecommute = string.Equals(Text(j, "jobLocationType"), "TELECOMMUTE", StringComparison.OrdinalIgnoreCase);
        if (telecommute && !location.Contains("remote", StringComparison.OrdinalIgnoreCase))
            location = location.Length == 0 ? "Remote" : $"Remote - {location}";

        var (min, max, currency, period) = Salary(j);

        return new SourceJob(
            SourceJobId: ImportIds.ForUrl(pageUrl),
            Title: title.Trim(),
            LocationText: location,
            CountryHint: country,
            WorkMode: telecommute ? WorkMode.Remote : null,
            EmploymentType: EmploymentType(j),
            Description: description,
            PublishedAt: Date(j, "datePosted"),
            Deadline: Date(j, "validThrough"),
            SourceUrl: pageUrl.ToString(),
            ApplyUrl: pageUrl.ToString(),
            SalaryMin: min, SalaryMax: max, SalaryCurrency: currency, SalaryPeriod: period,
            CompanyName: string.IsNullOrWhiteSpace(company) ? null : company.Trim());
    }

    private static (string Text, string? Country) Location(JsonElement j)
    {
        var parts = new List<string>();
        string? country = null;
        if (j.TryGetProperty("jobLocation", out var loc))
        {
            var items = loc.ValueKind == JsonValueKind.Array ? loc.EnumerateArray().ToList() : [loc];
            foreach (var item in items)
            {
                if (item.ValueKind == JsonValueKind.String) { parts.Add(item.GetString()!); continue; }
                if (!item.TryGetProperty("address", out var a)) continue;
                if (a.ValueKind == JsonValueKind.String) { parts.Add(a.GetString()!); continue; }
                var c = a.TryGetProperty("addressCountry", out var cv)
                    ? cv.ValueKind == JsonValueKind.String ? cv.GetString() : Text(cv, "name")
                    : null;
                country ??= c;
                var piece = string.Join(", ", new[] { Text(a, "addressLocality"), Text(a, "addressRegion"), c }
                    .Where(x => !string.IsNullOrWhiteSpace(x)));
                if (piece.Length > 0) parts.Add(piece);
            }
        }
        return (string.Join("; ", parts.Distinct()), country);
    }

    private static string? EmploymentType(JsonElement j)
    {
        if (!j.TryGetProperty("employmentType", out var e)) return null;
        var raw = e.ValueKind == JsonValueKind.Array ? e.EnumerateArray().FirstOrDefault().ToString() : e.ValueKind == JsonValueKind.String ? e.GetString() : null;
        return raw?.ToUpperInvariant() switch
        {
            "FULL_TIME" => "Full time",
            "PART_TIME" => "Part time",
            "CONTRACTOR" => "Contract",
            "TEMPORARY" => "Temporary",
            "INTERN" => "Internship",
            null or "" => null,
            _ => raw,
        };
    }

    private static (decimal?, decimal?, string?, string?) Salary(JsonElement j)
    {
        if (!j.TryGetProperty("baseSalary", out var s) || s.ValueKind != JsonValueKind.Object) return (null, null, null, null);
        var currency = Text(s, "currency");
        if (!s.TryGetProperty("value", out var v) || v.ValueKind != JsonValueKind.Object) return (null, null, currency, null);
        var min = Number(v, "minValue");
        var max = Number(v, "maxValue");
        var single = Number(v, "value");
        var unit = Text(v, "unitText")?.ToLowerInvariant() switch
        {
            "year" => "annual", "month" => "monthly", "week" => "weekly", "day" => "daily", "hour" => "hourly", var u => u,
        };
        return (min ?? single, max ?? single, currency, unit);
    }

    private static decimal? Number(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
        return v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var p) ? p : null;
    }

    private static string? Text(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static DateTimeOffset? Date(JsonElement e, string name) =>
        Text(e, name) is { } s && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d) ? d : null;
}
