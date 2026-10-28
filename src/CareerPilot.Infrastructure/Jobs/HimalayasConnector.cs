using System.Text.Json;
using CareerPilot.Application.Jobs;
using CareerPilot.Domain.Jobs;
using CareerPilot.Domain.Profiles;
using static CareerPilot.Infrastructure.Jobs.ConnectorSupport;

namespace CareerPilot.Infrastructure.Jobs;

/// <summary>
/// Himalayas public jobs API (https://himalayas.app/api, no key): GET /jobs/api/search?q=&amp;page=.
/// Unlike the board connectors this is a multi-employer feed, so the watched entry's BoardToken is a search
/// keyword ("dotnet", "backend-engineer"; hyphens and underscores become spaces).
///
/// Rules taken from the published API terms: the unchanged applicationLink is kept as the apply URL, the
/// SourceUrl points at Himalayas so the source stays visible, expired jobs are not stored, and the API is
/// only called from this backend. A keyword search is a window, never a full snapshot, so it can never
/// close jobs that are missing from it.
///
/// Eligibility is taken only from the fields the source documents: locationRestrictions lists the countries
/// where candidates must live. An empty list is reported as "no country restriction listed", and the
/// classifier still marks it for review; nothing is labelled "worldwide" by this connector.
/// </summary>
public sealed class HimalayasConnector(HttpClient http, ConnectorOptions options, TimeProvider clock) : IJobSourceConnector
{
    public const int MaxPages = 5;   // 5 x 20 = at most 100 jobs per keyword and sync (the API caps a page at 20)
    private const string SearchUrl = "https://himalayas.app/jobs/api/search";

    public JobSourceKind Kind => JobSourceKind.Himalayas;

    public async Task<ConnectorResult> FetchAsync(WatchedCompany company, CancellationToken ct)
    {
        var keyword = company.BoardToken.Replace('-', ' ').Replace('_', ' ').Trim();
        var now = clock.GetUtcNow();
        var jobs = new List<SourceJob>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var page = 1; page <= MaxPages; page++)
        {
            ct.ThrowIfCancellationRequested();
            if (page > 1 && options.BaseDelay > TimeSpan.Zero)
                await Task.Delay(options.BaseDelay, ct);   // stay well under the documented rate limit

            using var doc = await GetJsonAsync(http, options,
                $"{SearchUrl}?q={Uri.EscapeDataString(keyword)}&sort=recent&page={page}", ct);

            var items = Prop(doc.RootElement, "jobs");
            if (items.ValueKind != JsonValueKind.Array)
                throw new SourceFetchException("Unexpected response: no 'jobs' array.");
            if (items.GetArrayLength() == 0) break;   // documented stop condition: a page with no jobs

            foreach (var j in items.EnumerateArray())
            {
                var job = Map(j, now);
                if (job is not null && seen.Add(job.SourceJobId)) jobs.Add(job);
            }
        }

        return new ConnectorResult(jobs, IsCompleteSnapshot: false);
    }

    internal static SourceJob? Map(JsonElement j, DateTimeOffset now)
    {
        var id = Id(j, "guid");
        var title = Str(j, "title");
        var apply = Str(j, "applicationLink");
        if (id is null || title is null || apply is null) return null;

        var expiry = Epoch(j, "expiryDate");
        if (expiry is { } e && e <= now) return null;   // terms: remove jobs once they expire

        var restrictions = new List<string>();
        var r = Prop(j, "locationRestrictions");
        if (r.ValueKind == JsonValueKind.Array)
            foreach (var item in r.EnumerateArray())
            {
                var name = item.ValueKind == JsonValueKind.String ? item.GetString() : Str(item, "name");
                if (!string.IsNullOrWhiteSpace(name)) restrictions.Add(name.Trim());
            }

        var location = restrictions.Count > 0
            ? $"Remote - candidates must be located in: {string.Join("; ", restrictions)}"
            : "Remote - no country restriction listed by Himalayas";

        var companySlug = Str(j, "companySlug");
        var sourceUrl = companySlug is null
            ? "https://himalayas.app/jobs"
            : $"https://himalayas.app/companies/{Uri.EscapeDataString(companySlug)}/jobs";

        return new SourceJob(
            SourceJobId: id,
            Title: title,
            LocationText: location,
            CountryHint: null,            // restrictions are already in LocationText; a single hint would hide the others
            WorkMode: WorkMode.Remote,    // Himalayas lists remote jobs only
            EmploymentType: Str(j, "employmentType"),
            Description: HtmlText.Normalize(HtmlText.ToPlainText(Str(j, "description") ?? Str(j, "excerpt"))),
            PublishedAt: null,            // pubDate is the last update, not the first publication
            Deadline: expiry,
            SourceUrl: sourceUrl,
            ApplyUrl: apply,
            SalaryMin: Decimal(j, "minSalary"),
            SalaryMax: Decimal(j, "maxSalary"),
            SalaryCurrency: Str(j, "currency"),
            SalaryPeriod: Str(j, "salaryPeriod") ?? (Decimal(j, "minSalary") is null && Decimal(j, "maxSalary") is null ? null : "annual"),
            CompanyName: Str(j, "companyName"));
    }

    private static DateTimeOffset? Epoch(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var s)
            ? DateTimeOffset.FromUnixTimeSeconds(s)
            : null;
}
