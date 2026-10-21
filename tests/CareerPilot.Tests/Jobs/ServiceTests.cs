using CareerPilot.Application.Common;
using CareerPilot.Application.Jobs;
using CareerPilot.Application.Profiles;
using CareerPilot.Domain.Jobs;
using CareerPilot.Domain.Profiles;
using Microsoft.Extensions.Logging.Abstractions;

namespace CareerPilot.Tests.Jobs;

public class JobSearchServiceTests
{
    private sealed class Harness
    {
        public InMemoryJobStore Store { get; } = new();
        public UserProfile Profile { get; } = DefaultProfileFactory.Create();
        public NoopAudit Audit { get; } = new();
        public Dictionary<string, Func<ConnectorResult>> Feeds { get; } = [];
        public DiscoveryOptions Options { get; } = Helpers.Options();

        public Harness AddCompany(string name, Func<ConnectorResult> feed, bool enabled = true)
        {
            var company = Helpers.Company(name, JobSourceKind.Greenhouse, name.ToLowerInvariant());
            company.IsEnabled = enabled;
            Store.Companies.Add(company);
            Feeds[name] = feed;
            return this;
        }

        public JobSearchService Service()
        {
            var connector = new FakeConnector(JobSourceKind.Greenhouse, c => Feeds[c.Name]());
            return new JobSearchService(Store, new StaticProfileStore(Profile), [connector], Options, new CareerPilot.Application.Matching.ScoringOptions(), TimeProvider.System,
                Audit, NullLogger<JobSearchService>.Instance);
        }

        public async Task<SearchRun> RunAsync()
        {
            var service = Service();
            var dto = await service.QueueAsync(SearchTrigger.Manual, CancellationToken.None);
            await service.ExecuteAsync(dto.Id, CancellationToken.None);
            return Store.Runs.Single(r => r.Id == dto.Id);
        }
    }

    [Fact]
    public async Task A_successful_run_stores_and_classifies_jobs()
    {
        var h = new Harness()
            .AddCompany("Alpha", () => Helpers.Result(true, Helpers.Job("1"), Helpers.Job("2", "Sales Manager")))
            .AddCompany("Beta", () => Helpers.Result(true, Helpers.Job("9", "Backend Engineer", "Dubai, United Arab Emirates")));

        var run = await h.RunAsync();

        Assert.Equal(SearchRunStatus.Succeeded, run.Status);
        Assert.Equal(2, run.CompaniesSucceeded);
        Assert.Equal(3, run.JobsFetched);
        Assert.Equal(3, run.JobsNew);
        Assert.NotNull(run.StartedAt);
        Assert.NotNull(run.FinishedAt);
        Assert.Equal(3, h.Store.Listings.Count);
        Assert.True(h.Store.Listings.Single(l => l.Title == "Backend Engineer" && l.CompanyName == "Alpha").IsRelevant);
        Assert.False(h.Store.Listings.Single(l => l.Title == "Sales Manager").IsRelevant);
        Assert.All(h.Store.Companies, c => Assert.NotNull(c.LastSuccessAt));
        Assert.Contains("search.queued", h.Audit.Actions);
        Assert.Contains("search.finished", h.Audit.Actions);
    }

    [Fact]
    public async Task One_failing_company_does_not_stop_the_others_or_close_its_existing_jobs()
    {
        var h = new Harness()
            .AddCompany("Alpha", () => Helpers.Result(true, Helpers.Job("1")))
            .AddCompany("Beta", () => Helpers.Result(true, Helpers.Job("9", "Data Engineer")));
        await h.RunAsync();

        h.Feeds["Beta"] = () => throw new SourceFetchException("HTTP 502: the source is temporarily unavailable.");
        var run = await h.RunAsync();

        Assert.Equal(SearchRunStatus.PartiallyFailed, run.Status);
        Assert.Equal(1, run.CompaniesSucceeded);
        Assert.Single(run.Errors);
        Assert.StartsWith("Beta:", run.Errors[0]);
        Assert.Contains("502", h.Store.Companies.Single(c => c.Name == "Beta").LastError);
        Assert.Equal(JobStatus.Open, h.Store.Listings.Single(l => l.Title == "Data Engineer").Status);
    }

    [Fact]
    public async Task When_every_company_fails_the_run_is_failed_with_errors()
    {
        var h = new Harness().AddCompany("Alpha", () => throw new SourceFetchException("HTTP 404: this board was not found.", notFound: true));
        var run = await h.RunAsync();
        Assert.Equal(SearchRunStatus.Failed, run.Status);
        Assert.Contains("Alpha", run.Errors[0]);
    }

    [Fact]
    public async Task No_enabled_companies_is_a_successful_empty_run()
    {
        var h = new Harness().AddCompany("Alpha", () => throw new InvalidOperationException("must not be called"), enabled: false);
        var run = await h.RunAsync();
        Assert.Equal(SearchRunStatus.Succeeded, run.Status);
        Assert.Equal(0, run.CompaniesTotal);
    }

    [Fact]
    public async Task A_vanished_job_is_closed_only_by_a_complete_snapshot()
    {
        var h = new Harness().AddCompany("Alpha", () => Helpers.Result(true, Helpers.Job("1"), Helpers.Job("2", "Data Engineer")));
        await h.RunAsync();

        h.Feeds["Alpha"] = () => Helpers.Result(false, Helpers.Job("1"));
        var partial = await h.RunAsync();
        Assert.Equal(0, partial.JobsClosed);

        h.Feeds["Alpha"] = () => Helpers.Result(true, Helpers.Job("1"));
        var complete = await h.RunAsync();
        Assert.Equal(1, complete.JobsClosed);
        Assert.Equal(JobStatus.Closed, h.Store.Listings.Single(l => l.Title == "Data Engineer").Status);
    }

    [Fact]
    public async Task A_second_search_is_refused_while_one_is_active()
    {
        var h = new Harness().AddCompany("Alpha", () => Helpers.Result(true));
        var service = h.Service();
        await service.QueueAsync(SearchTrigger.Manual, CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => service.QueueAsync(SearchTrigger.Manual, CancellationToken.None));
        Assert.Single(h.Store.Runs);
    }

    [Fact]
    public async Task A_stale_active_run_no_longer_blocks_a_new_search()
    {
        var h = new Harness().AddCompany("Alpha", () => Helpers.Result(true));
        var service = h.Service();
        await service.QueueAsync(SearchTrigger.Manual, CancellationToken.None);
        h.Store.Runs[0].CreatedAt = DateTimeOffset.UtcNow.AddHours(-2);

        await service.QueueAsync(SearchTrigger.Manual, CancellationToken.None);
        Assert.Equal(2, h.Store.Runs.Count);
    }

    [Fact]
    public async Task Only_a_queued_run_can_be_executed()
    {
        var h = new Harness().AddCompany("Alpha", () => Helpers.Result(true, Helpers.Job("1")));
        var run = await h.RunAsync();
        var jobsBefore = h.Store.Listings.Count;

        await h.Service().ExecuteAsync(run.Id, CancellationToken.None);   // already finished
        await h.Service().ExecuteAsync(Guid.NewGuid(), CancellationToken.None); // unknown id

        Assert.Equal(jobsBefore, h.Store.Listings.Count);
        Assert.Single(h.Store.Runs);
    }

    [Fact]
    public async Task Runs_interrupted_by_a_restart_are_marked_failed()
    {
        var h = new Harness();
        h.Store.Runs.Add(new SearchRun { Status = SearchRunStatus.Running });
        h.Store.Runs.Add(new SearchRun { Status = SearchRunStatus.Queued });
        h.Store.Runs.Add(new SearchRun { Status = SearchRunStatus.Succeeded });

        await h.Service().RecoverInterruptedAsync(CancellationToken.None);

        Assert.Equal(2, h.Store.Runs.Count(r => r.Status == SearchRunStatus.Failed));
        Assert.Contains(h.Store.Runs.Where(r => r.Status == SearchRunStatus.Failed), r => r.Errors[0].Contains("Interrupted"));
        Assert.Contains(h.Store.Runs, r => r.Status == SearchRunStatus.Succeeded);
    }

    [Fact]
    public async Task An_unexpected_error_fails_the_run_without_leaking_its_message()
    {
        var h = new Harness().AddCompany("Alpha", () => throw new InvalidOperationException("secret connection string"));
        var run = await h.RunAsync();
        Assert.Equal(SearchRunStatus.Failed, run.Status);
        Assert.DoesNotContain("secret", string.Join(" ", run.Errors));
    }

    [Fact]
    public async Task Changed_preferences_reclassify_existing_jobs_on_the_next_run()
    {
        var h = new Harness().AddCompany("Alpha", () => Helpers.Result(true, Helpers.Job("1")));
        await h.RunAsync();
        Assert.True(h.Store.Listings[0].IsRelevant);

        h.Profile.Preferences.TargetCountries = ["Belgium"];
        await h.RunAsync();

        Assert.False(h.Store.Listings[0].IsRelevant);
        Assert.Equal(GeoFit.NoMatch, h.Store.Listings[0].GeoFit);
    }

    [Fact]
    public async Task The_owners_exclusion_survives_a_run()
    {
        var h = new Harness().AddCompany("Alpha", () => Helpers.Result(true, Helpers.Job("1")));
        await h.RunAsync();
        h.Store.Listings[0].Decision = UserDecision.Excluded;

        await h.RunAsync();

        Assert.Equal(UserDecision.Excluded, h.Store.Listings[0].Decision);
        Assert.Single(h.Store.Listings);
    }
}

public class CompanyServiceTests
{
    private static (CompanyService Service, InMemoryJobStore Store) Create(params IJobSourceConnector[] connectors)
    {
        var store = new InMemoryJobStore();
        return (new CompanyService(store, connectors, new NoopAudit()), store);
    }

    private static CompanyInput Input(string name = "Datadog", string kind = "Greenhouse", string token = "datadog") =>
        new(name, kind, token, false, true);

    [Fact]
    public async Task A_valid_company_is_added()
    {
        var (service, store) = Create();
        var dto = await service.AddAsync(Input(), CancellationToken.None);
        Assert.Equal("Greenhouse", dto.Kind);
        Assert.Single(store.Companies);
    }

    [Theory]
    [InlineData("bad token!", "boardToken")]
    [InlineData("../etc", "boardToken")]
    [InlineData("", "boardToken")]
    public async Task Invalid_board_tokens_are_rejected(string token, string field)
    {
        var (service, store) = Create();
        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => service.AddAsync(Input(token: token), CancellationToken.None));
        Assert.Contains(field, ex.Errors.Keys);
        Assert.Empty(store.Companies);
    }

    [Fact]
    public async Task Unknown_kinds_and_blank_names_are_rejected()
    {
        var (service, _) = Create();
        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => service.AddAsync(Input(name: " ", kind: "Workday"), CancellationToken.None));
        Assert.Contains("name", ex.Errors.Keys);
        Assert.Contains("kind", ex.Errors.Keys);
    }

    [Fact]
    public async Task The_same_board_cannot_be_added_twice_even_with_different_case()
    {
        var (service, _) = Create();
        await service.AddAsync(Input(), CancellationToken.None);
        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => service.AddAsync(Input(token: "DataDog"), CancellationToken.None));
        Assert.Contains("boardToken", ex.Errors.Keys);
    }

    [Fact]
    public async Task The_eu_endpoint_flag_only_applies_to_lever()
    {
        var (service, _) = Create();
        var gh = await service.AddAsync(Input() with { UseEuEndpoint = true }, CancellationToken.None);
        var lv = await service.AddAsync(Input("Spotify", "Lever", "spotify") with { UseEuEndpoint = true }, CancellationToken.None);
        Assert.False(gh.UseEuEndpoint);
        Assert.True(lv.UseEuEndpoint);
    }

    [Fact]
    public async Task Update_can_disable_a_company_and_changing_the_board_resets_its_sync_state()
    {
        var (service, store) = Create();
        var added = await service.AddAsync(Input(), CancellationToken.None);
        store.Companies[0].LastSuccessAt = DateTimeOffset.UtcNow;
        store.Companies[0].LastJobCount = 5;

        var disabled = await service.UpdateAsync(added.Id, Input() with { IsEnabled = false }, CancellationToken.None);
        Assert.False(disabled.IsEnabled);
        Assert.NotNull(disabled.LastSuccessAt);

        var moved = await service.UpdateAsync(added.Id, Input(token: "datadog-eu"), CancellationToken.None);
        Assert.Null(moved.LastSuccessAt);
        Assert.Null(moved.LastJobCount);
    }

    [Fact]
    public async Task Unknown_ids_are_not_found()
    {
        var (service, _) = Create();
        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(Guid.NewGuid(), Input(), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(Guid.NewGuid(), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => service.TestAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Delete_removes_the_company()
    {
        var (service, store) = Create();
        var added = await service.AddAsync(Input(), CancellationToken.None);
        await service.DeleteAsync(added.Id, CancellationToken.None);
        Assert.Empty(store.Companies);
    }

    [Fact]
    public async Task Testing_a_board_reports_the_job_count_or_the_error_and_stores_nothing()
    {
        var good = new FakeConnector(JobSourceKind.Greenhouse, _ => Helpers.Result(true, Helpers.Job("1"), Helpers.Job("2")));
        var (service, store) = Create(good);
        var added = await service.AddAsync(Input(), CancellationToken.None);
        var ok = await service.TestAsync(added.Id, CancellationToken.None);
        Assert.True(ok.Ok);
        Assert.Equal(2, ok.JobCount);
        Assert.Empty(store.Listings);

        var bad = new FakeConnector(JobSourceKind.Lever, _ => throw new SourceFetchException("HTTP 404: this board was not found."));
        var (service2, _) = Create(bad);
        var lever = await service2.AddAsync(Input("X", "Lever", "x"), CancellationToken.None);
        var failed = await service2.TestAsync(lever.Id, CancellationToken.None);
        Assert.False(failed.Ok);
        Assert.Contains("404", failed.Error);
    }
}

public class JobQueryServiceValidationTests
{
    [Fact]
    public async Task An_invalid_decision_is_rejected_and_a_valid_one_is_saved()
    {
        var store = new InMemoryJobStore();
        var job = Helpers.Listing();
        store.Listings.Add(job);
        var service = new JobQueryService(store, new NoopAudit());

        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => service.SetDecisionAsync(job.Id, "Maybe", CancellationToken.None));
        Assert.Contains("decision", ex.Errors.Keys);
        Assert.Equal(UserDecision.None, job.Decision);

        var dto = await service.SetDecisionAsync(job.Id, "Saved", CancellationToken.None);
        Assert.Equal("Saved", dto.Decision);
        await Assert.ThrowsAsync<NotFoundException>(() => service.SetDecisionAsync(Guid.NewGuid(), "Saved", CancellationToken.None));
    }
}
