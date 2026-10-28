using System.Net;
using CareerPilot.Application.Jobs;
using CareerPilot.Domain.Jobs;
using CareerPilot.Domain.Profiles;
using CareerPilot.Infrastructure.Jobs;

namespace CareerPilot.Tests.Jobs;

/// <summary>
/// MOCK-BASED tests. The fixtures use the field names documented at himalayas.app/api (guid, title,
/// applicationLink, locationRestrictions, minSalary, pubDate, expiryDate ...). They are NOT a capture of a live
/// response, so the 'jobs' envelope key is an assumption until a real call is made from the owner's machine.
/// </summary>
public class HimalayasConnectorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly long Future = Now.AddDays(20).ToUnixTimeSeconds();
    private static readonly long Past = Now.AddDays(-2).ToUnixTimeSeconds();

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static (HimalayasConnector, StubHandler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        var options = new ConnectorOptions { MaxAttempts = 2, BaseDelay = TimeSpan.Zero };
        return (new HimalayasConnector(new HttpClient(handler), options, new FixedClock(Now)), handler);
    }

    private static WatchedCompany Search(string keyword) => Helpers.Company("Himalayas dotnet", JobSourceKind.Himalayas, keyword);

    private static string Page(params string[] jobs) => "{\"jobs\":[" + string.Join(",", jobs) + "]}";

    private static string Job(string guid, string title, string restrictions, long? expiry = null, string extra = "") =>
        $$"""
        {"guid":"{{guid}}","title":"{{title}}","companyName":"Acme Cloud","companySlug":"acme-cloud",
         "applicationLink":"https://himalayas.app/apply/{{guid}}","locationRestrictions":{{restrictions}},
         "employmentType":"Full Time","description":"<p>Build APIs with C# and .NET.</p>","pubDate":{{Past}},
         "expiryDate":{{(expiry ?? Future)}}{{extra}}}
        """;

    [Fact]
    public async Task Fields_are_mapped_and_the_apply_link_and_source_are_kept()
    {
        // page 1 returns jobs, page 2 returns an empty list so the loop stops
        var calls = 0;
        var (c, handler) = Create(_ => StubHandler.Json(calls++ == 0
            ? Page(Job("g1", "Backend Engineer (.NET)", "[\"France\",\"Tunisia\"]",
                extra: ",\"minSalary\":50000,\"maxSalary\":70000,\"currency\":\"EUR\",\"salaryPeriod\":\"annual\""))
            : Page()));

        var result = await c.FetchAsync(Search("dotnet"), CancellationToken.None);

        Assert.StartsWith("https://himalayas.app/jobs/api/search?q=dotnet", handler.Urls[0]);
        Assert.Equal(2, handler.Urls.Count);
        Assert.False(result.IsCompleteSnapshot);   // a keyword window must never close other jobs
        var job = Assert.Single(result.Jobs);
        Assert.Equal("g1", job.SourceJobId);
        Assert.Equal("Acme Cloud", job.CompanyName);
        Assert.Equal("https://himalayas.app/apply/g1", job.ApplyUrl);
        Assert.Equal("https://himalayas.app/companies/acme-cloud/jobs", job.SourceUrl);
        Assert.Contains("France; Tunisia", job.LocationText);
        Assert.Equal(WorkMode.Remote, job.WorkMode);
        Assert.Null(job.PublishedAt);              // pubDate is a last-update time
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(Future), job.Deadline);
        Assert.Equal((50000m, 70000m, "EUR", "annual"), (job.SalaryMin, job.SalaryMax, job.SalaryCurrency, job.SalaryPeriod));
        Assert.DoesNotContain("<", job.Description);
    }

    [Fact]
    public async Task An_empty_restriction_list_is_not_labelled_worldwide()
    {
        var (c, _) = Create(_ => StubHandler.Json(Page(Job("g1", "Backend Engineer", "[]"))));
        var job = Assert.Single((await c.FetchAsync(Search("backend"), CancellationToken.None)).Jobs);
        Assert.Contains("no country restriction listed", job.LocationText);
        Assert.DoesNotContain("worldwide", job.LocationText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Expired_jobs_and_incomplete_records_are_skipped()
    {
        var calls = 0;
        var (c, _) = Create(_ => StubHandler.Json(calls++ == 0
            ? Page(Job("old", "Expired role", "[]", expiry: Past), "{\"guid\":\"x\",\"title\":\"No apply link\"}", Job("ok", "Open role", "[]"))
            : Page()));
        var result = await c.FetchAsync(Search("backend"), CancellationToken.None);
        Assert.Equal(["ok"], result.Jobs.Select(j => j.SourceJobId));
    }

    [Fact]
    public async Task Pagination_stops_at_the_page_cap_and_removes_duplicates()
    {
        var (c, handler) = Create(_ => StubHandler.Json(Page(Job("same", "Backend Engineer", "[]"))));
        var result = await c.FetchAsync(Search("backend"), CancellationToken.None);
        Assert.Equal(HimalayasConnector.MaxPages, handler.Urls.Count);
        Assert.Single(result.Jobs);
    }

    [Fact]
    public async Task Keyword_is_url_encoded_and_hyphens_become_spaces()
    {
        var (c, handler) = Create(_ => StubHandler.Json(Page()));
        var result = await c.FetchAsync(Search("backend-engineer&x=1"), CancellationToken.None);
        Assert.Empty(result.Jobs);
        Assert.Contains("q=backend", handler.Urls[0]);
        Assert.DoesNotContain("&x=1", handler.Urls[0]);   // the keyword cannot inject extra query parameters
    }

    [Fact]
    public async Task Failures_become_source_errors()
    {
        var (c, _) = Create(_ => StubHandler.Json("busy", HttpStatusCode.TooManyRequests));
        await Assert.ThrowsAsync<SourceFetchException>(() => c.FetchAsync(Search("backend"), CancellationToken.None));

        (c, _) = Create(_ => StubHandler.Json("{\"unexpected\":true}"));
        await Assert.ThrowsAsync<SourceFetchException>(() => c.FetchAsync(Search("backend"), CancellationToken.None));

        (c, _) = Create(_ => StubHandler.Json("<html>", media: "text/html"));
        await Assert.ThrowsAsync<SourceFetchException>(() => c.FetchAsync(Search("backend"), CancellationToken.None));
    }

    [Fact]
    public async Task Cancellation_is_honoured()
    {
        var (c, _) = Create(_ => StubHandler.Json(Page(Job("g1", "Backend Engineer", "[]"))));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => c.FetchAsync(Search("backend"), cts.Token));
    }
}
