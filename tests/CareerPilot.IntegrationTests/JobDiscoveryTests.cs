using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerPilot.Application.Jobs;
using CareerPilot.Application.Matching;

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
    public async Task Jobs_are_scored_explained_sortable_and_rescorable()
    {
        using var client = await api.CreateFakeConnectorClientAsync();
        await RunSearchAsync(client);

        var list = (await client.GetFromJsonAsync<PagedResult<JobSummaryDto>>("/api/jobs?sort=score&relevantOnly=false&pageSize=100", Json))!;
        Assert.NotEmpty(list.Items);
        Assert.All(list.Items, j => Assert.NotNull(j.MatchScore));
        var scores = list.Items.Select(j => j.MatchScore!.Value).ToList();
        Assert.Equal(scores.OrderByDescending(x => x), scores);

        var best = list.Items[0];
        var detail = (await client.GetFromJsonAsync<JobDetailDto>($"/api/jobs/{best.Id}", Json))!;
        Assert.NotNull(detail.Match);
        Assert.Equal(best.MatchScore, detail.Match!.Score);
        Assert.Equal(8, detail.Match.Components.Count);
        Assert.All(detail.Match.Components, c => Assert.False(string.IsNullOrWhiteSpace(c.Note)));

        var min = scores.Max();
        var filtered = (await client.GetFromJsonAsync<PagedResult<JobSummaryDto>>($"/api/jobs?relevantOnly=false&minScore={min}&pageSize=100", Json))!;
        Assert.NotEmpty(filtered.Items);
        Assert.All(filtered.Items, j => Assert.True(j.MatchScore >= min));

        var rescore = await client.PostAsync("/api/jobs/rescore", null);
        Assert.Equal(HttpStatusCode.OK, rescore.StatusCode);
        Assert.Contains("scored", await rescore.Content.ReadAsStringAsync());

        using var anon = api.CreateAnonymousClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsync("/api/jobs/rescore", null)).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task AI_analysis_is_off_by_default_and_says_so_without_leaking_anything()
    {
        using var client = await api.CreateFakeConnectorClientAsync();
        await RunSearchAsync(client);
        var status = await client.GetStringAsync("/api/ai/status");
        Assert.Contains("\"enabled\":false", status);

        var list = (await client.GetFromJsonAsync<PagedResult<JobSummaryDto>>("/api/jobs?relevantOnly=false&pageSize=1", Json))!;
        var detail = (await client.GetFromJsonAsync<JobDetailDto>($"/api/jobs/{list.Items[0].Id}", Json))!;
        Assert.False(detail.AiAvailable);
        Assert.Null(detail.Analysis);

        var response = await client.PostAsync($"/api/jobs/{list.Items[0].Id}/analysis", null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("not configured", await response.Content.ReadAsStringAsync());

        using var anon = api.CreateAnonymousClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/ai/status")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsync($"/api/jobs/{list.Items[0].Id}/analysis", null)).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task A_cv_can_be_prepared_previewed_and_downloaded_and_never_invents_unverified_facts()
    {
        using var client = await api.CreateFakeConnectorClientAsync();
        await RunSearchAsync(client);
        var list = (await client.GetFromJsonAsync<PagedResult<JobSummaryDto>>("/api/jobs?relevantOnly=false&pageSize=1", Json))!;
        var jobId = list.Items[0].Id;

        // This test needs a profile with nothing verified (other tests may have verified facts in the shared database).
        var current = (await client.GetFromJsonAsync<CareerPilot.Application.Profiles.ProfileDto>("/api/profile", Json))!;
        var unverified = current with
        {
            WorkExperiences = [.. current.WorkExperiences.Select(w => w with { IsVerified = false })],
            Skills = [.. current.Skills.Select(x => x with { IsVerified = false })],
            Projects = [.. current.Projects.Select(x => x with { IsVerified = false })],
        };
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/profile", unverified, Json)).StatusCode);

        var created = await client.PostAsync($"/api/jobs/{jobId}/documents/cv", null);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var doc = (await created.Content.ReadFromJsonAsync<CareerPilot.Application.Documents.DocumentDto>(Json))!;
        Assert.Equal(64, doc.ContentHash.Length);
        Assert.Equal(jobId, doc.JobId);
        Assert.NotNull(doc.Report);
        // The starter profile has nothing verified yet, so the CV must not contain experience or skills.
        Assert.Empty(doc.Content.Experience);
        Assert.Empty(doc.Content.Skills);
        Assert.NotEmpty(doc.Report!.Warnings);

        var again = (await client.GetFromJsonAsync<CareerPilot.Application.Documents.DocumentDto>($"/api/documents/{doc.Id}", Json))!;
        Assert.Equal(doc.ContentHash, again.ContentHash);

        var file = await client.GetAsync($"/api/documents/{doc.Id}/download");
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", file.Content.Headers.ContentType!.MediaType);
        var bytes = await file.Content.ReadAsByteArrayAsync();
        Assert.Equal((byte)'P', bytes[0]);

        var pdf = await client.GetAsync($"/api/documents/{doc.Id}/download?format=pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
        Assert.EndsWith(".pdf", pdf.Content.Headers.ContentDisposition!.FileNameStar ?? pdf.Content.Headers.ContentDisposition.FileName!.Trim('"'));
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(await pdf.Content.ReadAsByteArrayAsync(), 0, 4));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync($"/api/documents/{doc.Id}/download?format=exe")).StatusCode);

        using var anon = api.CreateAnonymousClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync($"/api/documents/{doc.Id}/download?format=pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync($"/api/documents/{doc.Id}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/documents/{Guid.NewGuid()}")).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Cover_letters_need_a_configured_AI_and_are_protected()
    {
        using var client = await api.CreateFakeConnectorClientAsync();
        await RunSearchAsync(client);
        var list = (await client.GetFromJsonAsync<PagedResult<JobSummaryDto>>("/api/jobs?relevantOnly=false&pageSize=1", Json))!;
        var jobId = list.Items[0].Id;

        var draft = await client.PostAsync($"/api/jobs/{jobId}/documents/cover-letter", null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, draft.StatusCode);
        Assert.Contains("not configured", await draft.Content.ReadAsStringAsync());

        // A CV is not a letter: letter endpoints refuse it.
        var cv = await (await client.PostAsync($"/api/jobs/{jobId}/documents/cv", null)).Content.ReadFromJsonAsync<CareerPilot.Application.Documents.DocumentDto>(Json);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/documents/{cv!.Id}/cover-letter")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/documents/{cv.Id}/revise", new { paragraphs = new[] { "x" } })).StatusCode);

        using var anon = api.CreateAnonymousClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsync($"/api/jobs/{jobId}/documents/cover-letter", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync($"/api/documents/{cv.Id}/revise", new { paragraphs = new[] { "x" } })).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task The_approval_flow_is_enforced_end_to_end_and_nothing_is_submitted_by_the_system()
    {
        using var client = await api.CreateFakeConnectorClientAsync();
        await RunSearchAsync(client);
        var list = (await client.GetFromJsonAsync<PagedResult<JobSummaryDto>>("/api/jobs?relevantOnly=false&pageSize=1", Json))!;
        var jobId = list.Items[0].Id;

        // Make the profile able to produce a usable CV: verify the owner's facts.
        var profile = (await client.GetFromJsonAsync<CareerPilot.Application.Profiles.ProfileDto>("/api/profile", Json))!;
        var verified = profile with
        {
            FullName = "Test Owner",
            Email = "owner@example.test",
            WorkExperiences = [.. profile.WorkExperiences.Select(w => w with { IsVerified = true })],
            Skills = [.. profile.Skills.Select(s => s with { IsVerified = true })],
        };
        var put = await client.PutAsJsonAsync("/api/profile", verified, Json);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var started = (await (await client.PostAsync($"/api/jobs/{jobId}/application", null)).Content
            .ReadFromJsonAsync<CareerPilot.Application.Applications.ApplicationDto>(Json))!;
        Assert.Equal("Shortlisted", started.Status);
        var again = (await (await client.PostAsync($"/api/jobs/{jobId}/application", null)).Content
            .ReadFromJsonAsync<CareerPilot.Application.Applications.ApplicationDto>(Json))!;
        Assert.Equal(started.Id, again.Id);

        // Not approved yet: every later step is refused.
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/applications/{started.Id}/ready", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/applications/{started.Id}/submission", new { confirmed = true }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/applications/{started.Id}/status", new { status = "Submitted" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/applications/{started.Id}/approve", new { bundleHash = "x", acknowledgeFlags = true }, Json)).StatusCode);

        var cv = (await (await client.PostAsync($"/api/jobs/{jobId}/documents/cv", null)).Content
            .ReadFromJsonAsync<CareerPilot.Application.Documents.DocumentDto>(Json))!;
        var docs = (await client.GetFromJsonAsync<List<CareerPilot.Application.Applications.DocumentSummaryDto>>($"/api/jobs/{jobId}/documents", Json))!;
        Assert.Contains(docs, d => d.Id == cv.Id && d.Kind == "Cv");

        var attached = (await (await client.PutAsJsonAsync($"/api/applications/{started.Id}/documents", new { cvDocumentId = cv.Id }, Json)).Content
            .ReadFromJsonAsync<CareerPilot.Application.Applications.ApplicationDto>(Json))!;
        Assert.NotNull(attached.Review!.BundleHash);

        var review = (await (await client.PostAsync($"/api/applications/{started.Id}/request-review", null)).Content
            .ReadFromJsonAsync<CareerPilot.Application.Applications.ApplicationDto>(Json))!;
        Assert.Equal("AwaitingApproval", review.Status);

        var wrong = await client.PostAsJsonAsync($"/api/applications/{started.Id}/approve", new { bundleHash = "not-the-hash", acknowledgeFlags = false }, Json);
        Assert.Equal(HttpStatusCode.Conflict, wrong.StatusCode);

        var approved = (await (await client.PostAsJsonAsync($"/api/applications/{started.Id}/approve",
            new { bundleHash = review.Review!.BundleHash, acknowledgeFlags = false }, Json)).Content
            .ReadFromJsonAsync<CareerPilot.Application.Applications.ApplicationDto>(Json))!;
        Assert.Equal("Approved", approved.Status);
        Assert.Equal(review.Review.BundleHash, approved.ApprovedBundleHash);

        var ready = (await (await client.PostAsync($"/api/applications/{started.Id}/ready", null)).Content
            .ReadFromJsonAsync<CareerPilot.Application.Applications.ApplicationDto>(Json))!;
        Assert.Equal("ReadyToSubmit", ready.Status);
        Assert.False(string.IsNullOrEmpty(ready.ApplyUrl));

        var done = (await (await client.PostAsJsonAsync($"/api/applications/{started.Id}/submission", new { confirmed = true, note = "I saw the confirmation" }, Json)).Content
            .ReadFromJsonAsync<CareerPilot.Application.Applications.ApplicationDto>(Json))!;
        Assert.Equal("Submitted", done.Status);
        Assert.Equal("I saw the confirmation", done.SubmissionNote);

        var all = (await client.GetFromJsonAsync<List<CareerPilot.Application.Applications.ApplicationDto>>("/api/applications", Json))!;
        Assert.Contains(all, a => a.Id == started.Id && a.Status == "Submitted");

        using var anon = api.CreateAnonymousClient();
        var summary = (await client.GetFromJsonAsync<CareerPilot.Application.Tracking.TrackingSummary>("/api/tracking/summary", Json))!;
        Assert.True(summary.Submitted >= 1);
        Assert.Equal(8, summary.Weekly.Count);
        Assert.NotNull(await client.GetFromJsonAsync<List<CareerPilot.Application.Tracking.Reminder>>("/api/tracking/reminders", Json));
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/tracking/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/tracking/reminders")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/applications")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync($"/api/applications/{started.Id}/approve", new { bundleHash = "x" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/applications/{Guid.NewGuid()}")).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task A_cv_file_can_be_previewed_then_applied_by_choice_and_is_never_stored()
    {
        using var client = await api.CreateFakeConnectorClientAsync();
        const string cv = "Import Tester\nSoftware engineer. import.tester@example.test\nBuilt services with Elasticsearch, Terraform and Kubernetes for several years at a company.\n";
        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(cv)), "file", "cv.txt" },
            { new StringContent("false"), "useAi" },
        };
        var response = await client.PostAsync("/api/profile/import/preview", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = (await response.Content.ReadFromJsonAsync<CareerPilot.Application.Import.ImportProposal>(Json))!;
        Assert.False(preview.AiUsed);
        Assert.Contains("Terraform", preview.Skills);
        Assert.Contains("Elasticsearch", preview.Skills);

        // Previewing changed nothing.
        var before = (await client.GetFromJsonAsync<CareerPilot.Application.Profiles.ProfileDto>("/api/profile", Json))!;
        Assert.DoesNotContain(before.Skills, x => x.Name == "Terraform" && x.Category == "" && !x.IsVerified && false);
        Assert.Equal(preview.ProfileVersion, before.Version);

        var applied = await client.PostAsJsonAsync("/api/profile/import/apply",
            new { version = preview.ProfileVersion, markVerified = false, skills = new[] { "Terraform" } }, Json);
        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
        var result = (await applied.Content.ReadFromJsonAsync<CareerPilot.Application.Import.ImportApplyResult>(Json))!;
        Assert.Contains(result.Profile.Skills, x => x.Name == "Terraform" && !x.IsVerified);
        Assert.DoesNotContain(result.Profile.Skills, x => x.Name == "Elasticsearch");

        // A second apply with the old version is refused.
        var stale = await client.PostAsJsonAsync("/api/profile/import/apply",
            new { version = preview.ProfileVersion, markVerified = false, skills = new[] { "Elasticsearch" } }, Json);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        // Bad uploads.
        using var bad = new MultipartFormDataContent { { new ByteArrayContent([1, 2, 3, 4, 5, 6]), "file", "cv.exe" } };
        var refused = await client.PostAsync("/api/profile/import/preview", bad);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("Unsupported", await refused.Content.ReadAsStringAsync());
        using var noFile = new MultipartFormDataContent { { new StringContent("x"), "other" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/profile/import/preview", noFile)).StatusCode);

        // AI requested but not configured.
        using var ai = new MultipartFormDataContent
        {
            { new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(cv)), "file", "cv.txt" },
            { new StringContent("true"), "useAi" },
        };
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsync("/api/profile/import/preview", ai)).StatusCode);

        using var anon = api.CreateAnonymousClient();
        using var anonForm = new MultipartFormDataContent { { new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(cv)), "file", "cv.txt" } };
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsync("/api/profile/import/preview", anonForm)).StatusCode);
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
