using System.Text.Json;
using CareerPilot.Application.Jobs;
using CareerPilot.Domain.Jobs;
using CareerPilot.Domain.Profiles;
using static CareerPilot.Infrastructure.Jobs.ConnectorSupport;

namespace CareerPilot.Infrastructure.Jobs;

/// <summary>Ashby public Job Postings API: GET api.ashbyhq.com/posting-api/job-board/{board}?includeCompensation=true (no credentials).</summary>
public sealed class AshbyConnector(HttpClient http, ConnectorOptions options) : IJobSourceConnector
{
    public JobSourceKind Kind => JobSourceKind.Ashby;

    public async Task<ConnectorResult> FetchAsync(WatchedCompany company, CancellationToken ct)
    {
        var url = $"https://api.ashbyhq.com/posting-api/job-board/{Uri.EscapeDataString(company.BoardToken)}?includeCompensation=true";
        using var doc = await GetJsonAsync(http, options, url, ct);

        var jobsElement = Prop(doc.RootElement, "jobs");
        if (jobsElement.ValueKind != JsonValueKind.Array)
            throw new SourceFetchException("Unexpected response: no 'jobs' array.");

        var jobs = new List<SourceJob>();
        foreach (var j in jobsElement.EnumerateArray())
        {
            // Unlisted postings are not public vacancies.
            if (j.TryGetProperty("isListed", out var listed) && listed.ValueKind == JsonValueKind.False) continue;

            var id = Id(j, "id");
            var title = Str(j, "title");
            var link = Str(j, "jobUrl");
            if (id is null || title is null || link is null) continue;

            var locations = new List<string>();
            if (Str(j, "location") is { Length: > 0 } primary) locations.Add(primary);
            var secondary = Prop(j, "secondaryLocations");
            if (secondary.ValueKind == JsonValueKind.Array)
                foreach (var s in secondary.EnumerateArray())
                    if (Str(s, "location") is { Length: > 0 } sl && !locations.Contains(sl)) locations.Add(sl);

            var workMode = Str(j, "workplaceType")?.ToLowerInvariant() switch
            {
                "remote" => WorkMode.Remote,
                "hybrid" => WorkMode.Hybrid,
                "onsite" or "on-site" => WorkMode.OnSite,
                _ => (WorkMode?)null,
            };
            if (workMode is null && j.TryGetProperty("isRemote", out var rem) && rem.ValueKind == JsonValueKind.True)
                workMode = WorkMode.Remote;

            var (min, max, currency, period) = Salary(Prop(j, "compensation"));

            jobs.Add(new SourceJob(
                SourceJobId: id,
                Title: title,
                LocationText: string.Join("; ", locations),
                CountryHint: Str(Prop(Prop(j, "address"), "postalAddress"), "addressCountry"),
                WorkMode: workMode,
                EmploymentType: EmploymentLabel(Str(j, "employmentType")),
                Description: HtmlText.Normalize(Str(j, "descriptionPlain") ?? HtmlText.ToPlainText(Str(j, "descriptionHtml"))),
                PublishedAt: Date(j, "publishedAt"),
                Deadline: null,
                SourceUrl: link,
                ApplyUrl: Str(j, "applyUrl") ?? link,
                SalaryMin: min,
                SalaryMax: max,
                SalaryCurrency: currency,
                SalaryPeriod: period));
        }

        return new ConnectorResult(jobs, IsCompleteSnapshot: true);
    }

    private static string? EmploymentLabel(string? raw) => raw switch
    {
        "FullTime" => "Full time",
        "PartTime" => "Part time",
        "Intern" => "Internship",
        "Contract" => "Contract",
        "Temporary" => "Temporary",
        null or "" => null,
        _ => raw,
    };

    private static (decimal? Min, decimal? Max, string? Currency, string? Period) Salary(JsonElement compensation)
    {
        var components = Prop(compensation, "summaryComponents");
        if (components.ValueKind != JsonValueKind.Array) return (null, null, null, null);
        foreach (var c in components.EnumerateArray())
        {
            if (!string.Equals(Str(c, "compensationType"), "Salary", StringComparison.OrdinalIgnoreCase)) continue;
            return (Decimal(c, "minValue"), Decimal(c, "maxValue"), Str(c, "currencyCode"), Str(c, "interval"));
        }
        return (null, null, null, null);
    }
}
