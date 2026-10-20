using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerPilot.Application.Jobs;

namespace CareerPilot.IntegrationTests;

/// <summary>End-to-end discovery through HTTP, EF Core and PostgreSQL, with fake job boards.</summary>
public class JobDiscoveryTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static async Task<SearchRunDto> WaitForRunAsync(HttpClient client, Guid id)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            var run = (await client.GetFromJsonAsync<SearchRunDto>($"/api/search-runs/{id}", Json))!;
            if (run.Status is not ("Queued" or "Running")) return run;
            await Task.Delay(250);
        }
        throw new TimeoutException("The search run did not finish in time.");
    }

    private static async Task<SearchRunDto> RunSearchAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/search-runs", null);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var queued = (await response.Content.ReadFromJsonAsync<SearchRunDto>(Json))!;
        return await WaitForRunAsync(client, queued.Id);
    }

    [RequiresDatabaseFact]
    public async Task Job_endpoints_require_authentication()
    {
        using var client = api.CreateAnonymousClient();
        foreach (var url in new[] { "/api/jobs", "/api/search-runs", "/api/sources/companies" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/search-runs", null)).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task The_starter_watchlist_is_present_on_a_new_database()
    {
        using var client = await api.CreateAuthenticatedClientAsync();
        var companies = (await client.GetFromJsonAsync<List<CompanyDto>>("/api/sources/companies", Json))!;
        Assert.Contains(companies, c => c.Name == "Datadog" && c.Kind == "Greenhouse" && c.BoardToken == "datadog");
    }

    [RequiresDatabaseFact]
    public async Task Company_validation_duplicates_and_deletion_work()
    {
        using var client = await api.CreateAuthenticatedClientAsync();

        var bad = await client.PostAsJsonAsync("/api/sources/companies", new { name = "X", kind = "Greenhouse", boardToken = "bad token!", isEnabled = true });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Contains("boardToken", await bad.Content.ReadAsStringAsync());

        var token = "it-" + Guid.NewGuid().ToString("N")[..8];
        var input = new { name = "Integration Co", kind = "Greenhouse", boardToken = token, useEuEndpoint = false, isEnabled = false };
        var created = await client.PostAsJsonAsync("/api/sources/companies", input);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var company = (await created.Content.ReadFromJsonAsync<CompanyDto>(Json))!;

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/sources/companies", input)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/sources/companies/{company.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/sources/companies/{company.Id}")).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task A_search_discovers_classifies_and_exposes_jobs_with_source_urls()
    {
        using var client = await api.CreateFakeConnectorClientAsync();

        var run = await RunSearchAsync(client);

        Assert.Equal("Succeeded", run.Status);
        Assert.True(run.JobsFetched > 0);
        Assert.Empty(run.Errors);

        // Default view: relevant, open jobs only.
        var relevant = (await client.GetFromJsonAsync<PagedResult<JobSummaryDto>>("/api/jobs?q=Backend%20Engineer&pageSize=100", Json))!;
        Assert.NotEmpty(relevant.Items);
        Assert.All(relevant.Items, j =>
        {
            Assert.True(j.IsRelevant);
            Assert.StartsWith("https://jobs.example.test/", j.SourceUrl);
            Assert.Equal("Open", j.Status);
            Assert.Contains("C#", j.Skills);
        });

        // The sales role and the New York role exist but are hidden by default, with reasons.
        Assert.DoesNotContain((await client.GetFromJsonAsync<PagedResult<JobSummaryDto>>("/api/jobs?pageSize=100", Json))!.Items,
            j => j.Title == "Sales Manager");
        var all = (await client.GetFromJsonAsync<PagedResult<JobSummaryDto>>("/api/jobs?relevantOnly=false&pageSize=100", Json))!;
        var sales = all.Items.First(j => j.Title == "Sales Manager");
        Assert.False(sales.IsRelevant);
        Assert.NotEmpty(sales.FilterReasons);
        var ny = all.Items.First(j => j.Country == "United States");
        Assert.Equal("NoMatch", ny.GeoFit);
    }

    [RequiresDatabaseFact]
    public async Task Filters_sorting_paging_and_detail_work()
    {
        using var client = await api.CreateFakeConnectorClientAsync();
        await RunSearchAsync(client);

        var france = (await client.GetFromJsonAsync<PagedResult<JobSummaryDto>>("/api/jobs?country=France&sort=company&pageSize=2&page=1", Json))!;
        Assert.True(france.Total >= 2);
        Assert.Equal(2, france.Items.Count);
        Assert.All(france.Items, j => Assert.Equal("France", j.Country));

        var page2 = (await client.GetFromJsonAsync<PagedResult<JobSummaryDto>>("/api/jobs?country=France&sort=company&pageSize=2&page=2", Json))!;
        Assert.DoesNotContain(page2.Items, j => france.Items.Any(f => f.Id == j.Id));

        var detail = (await client.GetFromJsonAsync<JobDetailDto>($"/api/jobs/{france.Items[0].Id}", Json))!;
        Assert.Contains("C# and PostgreSQL", detail.Description);
        Assert.NotEmpty(detail.Postings);
        Assert.True(detail.Postings[0].IsPresent);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/jobs/{Guid.NewGuid()}")).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task An_excluded_job_disappears_from_the_list_and_stays_excluded_after_another_search()
    {
        using var client = await api.CreateFakeConnectorClientAsync();
        await RunSearchAsync(client);
        var job = (await client.GetFromJsonAsync<PagedResult<JobSummaryDto>>("/api/jobs?q=Backend%20Engineer&pageSize=1", Json))!.Items[0];

        var invalid = await client.PutAsJsonAsync($"/api/jobs/{job.Id}/decision", new { decision = "Maybe" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var excluded = await client.PutAsJsonAsync($"/api/jobs/{job.Id}/decision", new { decision = "Excluded" });
        Assert.Equal(HttpStatusCode.OK, excluded.StatusCode);

        await RunSearchAsync(client);

        var visible = (await client.GetFromJsonAsync<PagedResult<JobSummaryDto>>("/api/jobs?q=Backend%20Engineer&pageSize=100", Json))!;
        Assert.DoesNotContain(visible.Items, j => j.Id == job.Id);
        var onlyExcluded = (await client.GetFromJsonAsync<PagedResult<JobSummaryDto>>("/api/jobs?decision=Excluded&relevantOnly=false&pageSize=100", Json))!;
        Assert.Contains(onlyExcluded.Items, j => j.Id == job.Id);

        await client.PutAsJsonAsync($"/api/jobs/{job.Id}/decision", new { decision = "None" });
    }

    [RequiresDatabaseFact]
    public async Task Search_runs_are_listed_newest_first()
    {
        using var client = await api.CreateFakeConnectorClientAsync();
        var run = await RunSearchAsync(client);
        var runs = (await client.GetFromJsonAsync<List<SearchRunDto>>("/api/search-runs", Json))!;
        Assert.Equal(run.Id, runs[0].Id);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/search-runs/{Guid.NewGuid()}")).StatusCode);
    }
}
