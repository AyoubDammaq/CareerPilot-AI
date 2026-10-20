using System.Text.Json;
using CareerPilot.Application.Jobs;
using CareerPilot.Domain.Jobs;
using CareerPilot.Domain.Profiles;
using static CareerPilot.Infrastructure.Jobs.ConnectorSupport;

namespace CareerPilot.Infrastructure.Jobs;

/// <summary>Greenhouse public Job Board API: GET boards-api.greenhouse.io/v1/boards/{token}/jobs?content=true (no credentials).</summary>
public sealed class GreenhouseConnector(HttpClient http, ConnectorOptions options) : IJobSourceConnector
{
    public JobSourceKind Kind => JobSourceKind.Greenhouse;

    public async Task<ConnectorResult> FetchAsync(WatchedCompany company, CancellationToken ct)
    {
        var url = $"https://boards-api.greenhouse.io/v1/boards/{Uri.EscapeDataString(company.BoardToken)}/jobs?content=true";
        using var doc = await GetJsonAsync(http, options, url, ct);

        var jobsElement = Prop(doc.RootElement, "jobs");
        if (jobsElement.ValueKind != JsonValueKind.Array)
            throw new SourceFetchException("Unexpected response: no 'jobs' array.");

        var jobs = new List<SourceJob>();
        foreach (var j in jobsElement.EnumerateArray())
        {
            var id = Id(j, "id");
            var title = Str(j, "title");
            var link = Str(j, "absolute_url");
            if (id is null || title is null || link is null) continue;

            var location = Str(Prop(j, "location"), "name") ?? "";
            var employment = EmploymentFromMetadata(Prop(j, "metadata"));

            jobs.Add(new SourceJob(
                SourceJobId: id,
                Title: title,
                LocationText: location,
                CountryHint: null,
                WorkMode: location.Contains("remote", StringComparison.OrdinalIgnoreCase) ? WorkMode.Remote : null,
                EmploymentType: employment,
                Description: HtmlText.ToPlainText(Str(j, "content")),
                PublishedAt: Date(j, "first_published"),   // never falls back to updated_at: that is not a publication date
                Deadline: Date(j, "application_deadline"),
                SourceUrl: link,
                ApplyUrl: link,
                Language: Str(j, "language")));
        }

        // One request returns the whole board, so a parsed response is a complete snapshot.
        return new ConnectorResult(jobs, IsCompleteSnapshot: true);
    }

    private static string? EmploymentFromMetadata(JsonElement metadata)
    {
        if (metadata.ValueKind != JsonValueKind.Array) return null;
        foreach (var m in metadata.EnumerateArray())
        {
            var name = Str(m, "name") ?? "";
            if (name.Contains("employment", StringComparison.OrdinalIgnoreCase)
                || name.Contains("time type", StringComparison.OrdinalIgnoreCase)
                || name.Contains("job type", StringComparison.OrdinalIgnoreCase))
            {
                var value = Prop(m, "value");
                if (value.ValueKind == JsonValueKind.String) return value.GetString();
            }
        }
        return null;
    }
}
