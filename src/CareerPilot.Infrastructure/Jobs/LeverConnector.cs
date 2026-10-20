using System.Text;
using System.Text.Json;
using CareerPilot.Application.Jobs;
using CareerPilot.Domain.Jobs;
using CareerPilot.Domain.Profiles;
using static CareerPilot.Infrastructure.Jobs.ConnectorSupport;

namespace CareerPilot.Infrastructure.Jobs;

/// <summary>Lever public Postings API: GET api.lever.co/v0/postings/{site}?mode=json (EU: api.eu.lever.co), no credentials.</summary>
public sealed class LeverConnector(HttpClient http, ConnectorOptions options) : IJobSourceConnector
{
    private const int PageSize = 100;
    private const int MaxPages = 50;

    public JobSourceKind Kind => JobSourceKind.Lever;

    public async Task<ConnectorResult> FetchAsync(WatchedCompany company, CancellationToken ct)
    {
        var host = company.UseEuEndpoint ? "api.eu.lever.co" : "api.lever.co";
        var site = Uri.EscapeDataString(company.BoardToken);

        var jobs = new List<SourceJob>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var complete = false;

        for (var page = 0; page < MaxPages; page++)
        {
            var url = $"https://{host}/v0/postings/{site}?mode=json&skip={page * PageSize}&limit={PageSize}";
            using var doc = await GetJsonAsync(http, options, url, ct);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                throw new SourceFetchException("Unexpected response: expected an array of postings.");

            var count = 0;
            var added = 0;
            foreach (var p in doc.RootElement.EnumerateArray())
            {
                count++;
                var job = Parse(p);
                if (job is null || !seen.Add(job.SourceJobId)) continue;
                jobs.Add(job);
                added++;
            }

            if (count < PageSize) { complete = true; break; }   // last (short or empty) page reached
            if (added == 0) break;                              // paging not honoured: cannot prove completeness
        }

        return new ConnectorResult(jobs, complete);
    }

    internal static SourceJob? Parse(JsonElement p)
    {
        var id = Id(p, "id");
        var title = Str(p, "text");
        var link = Str(p, "hostedUrl");
        if (id is null || title is null || link is null) return null;

        var categories = Prop(p, "categories");
        var locations = new List<string>();
        var all = Prop(categories, "allLocations");
        if (all.ValueKind == JsonValueKind.Array)
            locations.AddRange(all.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!));
        if (locations.Count == 0 && Str(categories, "location") is { } single) locations.Add(single);

        var workMode = Str(p, "workplaceType")?.ToLowerInvariant() switch
        {
            "remote" => WorkMode.Remote,
            "hybrid" => WorkMode.Hybrid,
            "on-site" or "onsite" => WorkMode.OnSite,
            _ => (WorkMode?)null,
        };

        DateTimeOffset? published = null;
        if (p.TryGetProperty("createdAt", out var created) && created.ValueKind == JsonValueKind.Number && created.TryGetInt64(out var ms) && ms > 0)
            published = DateTimeOffset.FromUnixTimeMilliseconds(ms);

        var salary = Prop(p, "salaryRange");

        return new SourceJob(
            SourceJobId: id,
            Title: title,
            LocationText: string.Join("; ", locations),
            CountryHint: Str(p, "country"),
            WorkMode: workMode,
            EmploymentType: Str(categories, "commitment"),
            Description: BuildDescription(p),
            PublishedAt: published,
            Deadline: null,
            SourceUrl: link,
            ApplyUrl: Str(p, "applyUrl") ?? link,
            SalaryMin: Decimal(salary, "min"),
            SalaryMax: Decimal(salary, "max"),
            SalaryCurrency: Str(salary, "currency"),
            SalaryPeriod: Str(salary, "interval"));
    }

    private static string BuildDescription(JsonElement p)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Str(p, "descriptionPlain") ?? HtmlText.ToPlainText(Str(p, "description")));

        var lists = Prop(p, "lists");
        if (lists.ValueKind == JsonValueKind.Array)
        {
            foreach (var l in lists.EnumerateArray())
            {
                sb.AppendLine().AppendLine(Str(l, "text"));
                sb.AppendLine(HtmlText.ToPlainText(Str(l, "content")));
            }
        }

        var additional = Str(p, "additionalPlain");
        if (!string.IsNullOrWhiteSpace(additional)) sb.AppendLine().AppendLine(additional);
        return HtmlText.Normalize(sb.ToString());
    }
}
