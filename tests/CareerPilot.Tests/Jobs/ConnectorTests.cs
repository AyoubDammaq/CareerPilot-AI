using System.Net;
using System.Text;
using CareerPilot.Application.Jobs;
using CareerPilot.Domain.Jobs;
using CareerPilot.Domain.Profiles;
using CareerPilot.Infrastructure.Jobs;

namespace CareerPilot.Tests.Jobs;

internal sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<string> Urls { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Urls.Add(request.RequestUri!.ToString());
        return Task.FromResult(respond(request));
    }

    public static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK, string media = "application/json") =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, media) };
}

/// <summary>
/// Fixtures keep the exact field names and value shapes of the real public feeds, observed on 2026-10-20
/// (Greenhouse: Datadog board, Lever: Spotify site, Ashby: Ashby board), with texts shortened.
/// </summary>
public class ConnectorTests
{
    private static readonly ConnectorOptions NoDelay = new() { MaxAttempts = 3, BaseDelay = TimeSpan.Zero };

    private static (T Connector, StubHandler Handler) Create<T>(Func<HttpClient, ConnectorOptions, T> make, Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        return (make(new HttpClient(handler), NoDelay), handler);
    }

    // ---------------------------------------------------------------- Greenhouse

    private const string GreenhouseJson = """
    {"jobs":[
      {"id":7194969,"internal_job_id":3228359,"title":"AI Research Engineer - Datadog AI Research (DAIR)",
       "updated_at":"2026-09-02T10:22:15-04:00","first_published":"2025-08-27T10:34:20-04:00","requisition_id":"R15376",
       "location":{"name":"Paris, France"},"absolute_url":"https://careers.datadoghq.com/detail/7194969/?gh_jid=7194969",
       "language":"en","company_name":"Datadog",
       "metadata":[{"id":33310,"name":"Time Type","value":"Full time","value_type":"single_select"}],
       "content":"&lt;p&gt;As a Research Engineer you will work with C# and Python.&lt;/p&gt;",
       "departments":[{"id":72253,"name":"Dev Eng","child_ids":[],"parent_id":9022}],"application_deadline":null},
      {"id":6652564,"title":"Remote Backend Engineer","first_published":null,"updated_at":"2026-09-02T10:22:15-04:00",
       "location":{"name":"Remote, Poland"},"absolute_url":"https://example.test/job/2","content":""},
      {"title":"Record without id","absolute_url":"https://example.test/x"}
    ]}
    """;

    [Fact]
    public async Task Greenhouse_jobs_are_normalized_to_the_common_schema()
    {
        var (connector, handler) = Create((h, o) => new GreenhouseConnector(h, o), _ => StubHandler.Json(GreenhouseJson));

        var result = await connector.FetchAsync(Helpers.Company("Datadog", JobSourceKind.Greenhouse, "datadog"), CancellationToken.None);

        Assert.Equal("https://boards-api.greenhouse.io/v1/boards/datadog/jobs?content=true", Assert.Single(handler.Urls));
        Assert.True(result.IsCompleteSnapshot);
        Assert.Equal(2, result.Jobs.Count);

        var first = result.Jobs[0];
        Assert.Equal("7194969", first.SourceJobId);
        Assert.Equal("Paris, France", first.LocationText);
        Assert.Equal("Full time", first.EmploymentType);
        Assert.Equal("en", first.Language);
        Assert.Contains("C# and Python", first.Description);
        Assert.DoesNotContain("<", first.Description);
        Assert.Equal(new DateTimeOffset(2025, 8, 27, 10, 34, 20, TimeSpan.FromHours(-4)), first.PublishedAt);
        Assert.Equal("https://careers.datadoghq.com/detail/7194969/?gh_jid=7194969", first.ApplyUrl);

        var second = result.Jobs[1];
        Assert.Null(second.PublishedAt); // updated_at is not a publication date and must not be used as one
        Assert.Equal(WorkMode.Remote, second.WorkMode);
    }

    [Fact]
    public async Task A_missing_board_is_reported_as_not_found()
    {
        var (connector, _) = Create((h, o) => new GreenhouseConnector(h, o), _ => StubHandler.Json("{}", HttpStatusCode.NotFound));
        var ex = await Assert.ThrowsAsync<SourceFetchException>(() => connector.FetchAsync(Helpers.Company(), CancellationToken.None));
        Assert.True(ex.NotFound);
        Assert.Contains("404", ex.Message);
    }

    [Fact]
    public async Task Server_errors_are_retried_then_reported()
    {
        var (connector, handler) = Create((h, o) => new GreenhouseConnector(h, o), _ => StubHandler.Json("", HttpStatusCode.BadGateway));
        var ex = await Assert.ThrowsAsync<SourceFetchException>(() => connector.FetchAsync(Helpers.Company(), CancellationToken.None));
        Assert.Contains("temporarily unavailable", ex.Message);
        Assert.Equal(3, handler.Urls.Count);
    }

    [Fact]
    public async Task A_transient_failure_followed_by_success_is_recovered()
    {
        var calls = 0;
        var (connector, handler) = Create((h, o) => new GreenhouseConnector(h, o),
            _ => ++calls == 1 ? StubHandler.Json("", HttpStatusCode.ServiceUnavailable) : StubHandler.Json(GreenhouseJson));
        var result = await connector.FetchAsync(Helpers.Company(), CancellationToken.None);
        Assert.Equal(2, result.Jobs.Count);
        Assert.Equal(2, handler.Urls.Count);
    }

    [Fact]
    public async Task An_html_answer_is_not_treated_as_a_job_feed()
    {
        var (connector, _) = Create((h, o) => new GreenhouseConnector(h, o),
            _ => StubHandler.Json("<html>maintenance</html>", HttpStatusCode.OK, "text/html"));
        var ex = await Assert.ThrowsAsync<SourceFetchException>(() => connector.FetchAsync(Helpers.Company(), CancellationToken.None));
        Assert.Contains("instead of JSON", ex.Message);
    }

    [Fact]
    public async Task Malformed_or_unexpected_json_is_rejected()
    {
        var (bad, _) = Create((h, o) => new GreenhouseConnector(h, o), _ => StubHandler.Json("{\"jobs\":[{\"id\":"));
        await Assert.ThrowsAsync<SourceFetchException>(() => bad.FetchAsync(Helpers.Company(), CancellationToken.None));

        var (shape, _) = Create((h, o) => new GreenhouseConnector(h, o), _ => StubHandler.Json("{\"other\":1}"));
        var ex = await Assert.ThrowsAsync<SourceFetchException>(() => shape.FetchAsync(Helpers.Company(), CancellationToken.None));
        Assert.Contains("jobs", ex.Message);
    }

    [Fact]
    public async Task The_board_token_is_url_encoded_and_cannot_change_the_host()
    {
        var (connector, handler) = Create((h, o) => new GreenhouseConnector(h, o), _ => StubHandler.Json("{\"jobs\":[]}"));
        await connector.FetchAsync(Helpers.Company(token: "evil/../x?y=1"), CancellationToken.None);
        var url = Assert.Single(handler.Urls);
        Assert.StartsWith("https://boards-api.greenhouse.io/v1/boards/", url);
        Assert.DoesNotContain("/../", url);
    }

    // ---------------------------------------------------------------- Lever

    private const string LeverJson = """
    [
      {"id":"223f9722-3579-4ff2-b123-e4cc7b0c1a64","text":"Art Director - Mexico",
       "categories":{"commitment":"Permanent","department":"Markets","location":"Mexico City","team":"Marketing","allLocations":["Mexico City"]},
       "country":"MX","workplaceType":"hybrid","createdAt":1791566108174,
       "descriptionPlain":"The Marketing team defines how we show up.",
       "lists":[{"text":"What You'll Do","content":"<li>Bring ideas to life</li>"}],
       "additionalPlain":"We are an equal opportunity employer.",
       "hostedUrl":"https://jobs.lever.co/spotify/223f9722-3579-4ff2-b123-e4cc7b0c1a64",
       "applyUrl":"https://jobs.lever.co/spotify/223f9722-3579-4ff2-b123-e4cc7b0c1a64/apply"},
      {"id":"d87833d0-fb78-4794-b45d-3fe5c8274bc8","text":"Backend Engineer, Paris",
       "categories":{"commitment":"Full-time","location":"Paris"},"country":"FR","workplaceType":"on-site","createdAt":1787802187805,
       "descriptionPlain":"Build APIs.","salaryRange":{"currency":"EUR","interval":"per-year-salary","min":60000,"max":80000},
       "hostedUrl":"https://jobs.lever.co/spotify/d87833d0-fb78-4794-b45d-3fe5c8274bc8"}
    ]
    """;

    private static string LeverPage(int from, int count) =>
        "[" + string.Join(",", Enumerable.Range(from, count).Select(i =>
            $$"""{"id":"id-{{i}}","text":"Engineer {{i}}","categories":{"location":"Paris"},"hostedUrl":"https://jobs.lever.co/x/id-{{i}}"}""")) + "]";

    [Fact]
    public async Task Lever_postings_are_normalized()
    {
        var (connector, handler) = Create((h, o) => new LeverConnector(h, o), _ => StubHandler.Json(LeverJson));

        var result = await connector.FetchAsync(Helpers.Company("Spotify", JobSourceKind.Lever, "spotify"), CancellationToken.None);

        Assert.StartsWith("https://api.lever.co/v0/postings/spotify?mode=json&skip=0&limit=100", Assert.Single(handler.Urls));
        Assert.True(result.IsCompleteSnapshot);
        var a = result.Jobs[0];
        Assert.Equal("Mexico City", a.LocationText);
        Assert.Equal("MX", a.CountryHint);
        Assert.Equal(WorkMode.Hybrid, a.WorkMode);
        Assert.Equal("Permanent", a.EmploymentType);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1791566108174), a.PublishedAt);
        Assert.Contains("What You'll Do", a.Description);
        Assert.Contains("- Bring ideas to life", a.Description);
        Assert.Contains("equal opportunity", a.Description);
        Assert.Null(a.SalaryMin);

        var b = result.Jobs[1];
        Assert.Equal(WorkMode.OnSite, b.WorkMode);
        Assert.Equal(60000m, b.SalaryMin);
        Assert.Equal(80000m, b.SalaryMax);
        Assert.Equal("EUR", b.SalaryCurrency);
        Assert.Equal(b.SourceUrl, b.ApplyUrl); // no applyUrl given: fall back to the hosted page
    }

    [Fact]
    public async Task Lever_uses_the_eu_host_when_configured()
    {
        var (connector, handler) = Create((h, o) => new LeverConnector(h, o), _ => StubHandler.Json("[]"));
        var company = Helpers.Company("X", JobSourceKind.Lever, "x");
        company.UseEuEndpoint = true;
        await connector.FetchAsync(company, CancellationToken.None);
        Assert.StartsWith("https://api.eu.lever.co/", handler.Urls[0]);
    }

    [Fact]
    public async Task Lever_pages_until_a_short_page_and_is_then_complete()
    {
        var (connector, handler) = Create((h, o) => new LeverConnector(h, o), req =>
            StubHandler.Json(req.RequestUri!.Query.Contains("skip=0") ? LeverPage(0, 100) : LeverPage(100, 5)));

        var result = await connector.FetchAsync(Helpers.Company(kind: JobSourceKind.Lever), CancellationToken.None);

        Assert.Equal(105, result.Jobs.Count);
        Assert.True(result.IsCompleteSnapshot);
        Assert.Equal(2, handler.Urls.Count);
        Assert.Contains("skip=100", handler.Urls[1]);
    }

    [Fact]
    public async Task Lever_that_ignores_paging_is_not_reported_as_complete()
    {
        var (connector, _) = Create((h, o) => new LeverConnector(h, o), _ => StubHandler.Json(LeverPage(0, 100)));
        var result = await connector.FetchAsync(Helpers.Company(kind: JobSourceKind.Lever), CancellationToken.None);
        Assert.Equal(100, result.Jobs.Count);
        Assert.False(result.IsCompleteSnapshot); // cannot prove nothing is missing, so nothing may be closed
    }

    [Fact]
    public async Task Lever_rejects_a_non_array_answer()
    {
        var (connector, _) = Create((h, o) => new LeverConnector(h, o), _ => StubHandler.Json("{\"ok\":false}"));
        await Assert.ThrowsAsync<SourceFetchException>(() => connector.FetchAsync(Helpers.Company(kind: JobSourceKind.Lever), CancellationToken.None));
    }

    // ---------------------------------------------------------------- Ashby

    private const string AshbyJson = """
    {"jobs":[
      {"id":"7458d4e9-da2e-47bd-98cb-adfda43d42b2","title":"Engineering Manager - EU","department":"Engineering","team":"EMEA Engineering",
       "employmentType":"FullTime","location":"Remote - European Union",
       "secondaryLocations":[{"location":"Remote - France","address":{"postalAddress":{"addressCountry":"France"}}}],
       "publishedAt":"2024-03-04T14:29:08.532+00:00","isListed":true,"isRemote":true,"workplaceType":"Remote",
       "address":{"postalAddress":{"postalCode":"","addressRegion":"","addressCountry":"European Union","addressLocality":""}},
       "jobUrl":"https://jobs.ashbyhq.com/ashby/7458d4e9-da2e-47bd-98cb-adfda43d42b2",
       "applyUrl":"https://jobs.ashbyhq.com/ashby/7458d4e9-da2e-47bd-98cb-adfda43d42b2/application",
       "descriptionPlain":"Hi, I am the Director of Engineering.",
       "compensation":{"compensationTierSummary":"€110K – €185K","summaryComponents":[
          {"compensationType":"Salary","interval":"1 YEAR","currencyCode":"EUR","minValue":110000,"maxValue":185000},
          {"compensationType":"EquityPercentage","interval":"NONE","currencyCode":null,"minValue":0.1,"maxValue":0.5}]}},
      {"id":"hidden-1","title":"Unlisted Role","isListed":false,"jobUrl":"https://jobs.ashbyhq.com/ashby/hidden-1","location":"Remote"},
      {"id":"onsite-1","title":"Software Engineer","employmentType":"Intern","location":"Paris","workplaceType":"OnSite","isListed":true,
       "jobUrl":"https://jobs.ashbyhq.com/ashby/onsite-1","descriptionHtml":"<p>Build things.</p>"}
    ]}
    """;

    [Fact]
    public async Task Ashby_jobs_are_normalized_and_unlisted_ones_are_skipped()
    {
        var (connector, handler) = Create((h, o) => new AshbyConnector(h, o), _ => StubHandler.Json(AshbyJson));

        var result = await connector.FetchAsync(Helpers.Company("Ashby", JobSourceKind.Ashby, "ashby"), CancellationToken.None);

        Assert.Equal("https://api.ashbyhq.com/posting-api/job-board/ashby?includeCompensation=true", Assert.Single(handler.Urls));
        Assert.True(result.IsCompleteSnapshot);
        Assert.Equal(2, result.Jobs.Count);
        Assert.DoesNotContain(result.Jobs, j => j.SourceJobId == "hidden-1");

        var a = result.Jobs[0];
        Assert.Equal("Remote - European Union; Remote - France", a.LocationText);
        Assert.Equal("European Union", a.CountryHint);
        Assert.Equal(WorkMode.Remote, a.WorkMode);
        Assert.Equal("Full time", a.EmploymentType);
        Assert.Equal(110000m, a.SalaryMin);
        Assert.Equal(185000m, a.SalaryMax);
        Assert.Equal("EUR", a.SalaryCurrency);
        Assert.Equal("1 YEAR", a.SalaryPeriod);
        Assert.Equal(new DateTimeOffset(2024, 3, 4, 14, 29, 8, 532, TimeSpan.Zero), a.PublishedAt);
        Assert.EndsWith("/application", a.ApplyUrl);

        var b = result.Jobs[1];
        Assert.Equal(WorkMode.OnSite, b.WorkMode);
        Assert.Equal("Internship", b.EmploymentType);
        Assert.Equal("Build things.", b.Description);
        Assert.Null(b.SalaryMin);
    }
}
