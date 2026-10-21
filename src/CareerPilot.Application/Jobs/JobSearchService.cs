using CareerPilot.Application.Abstractions;
using CareerPilot.Application.Common;
using CareerPilot.Application.Matching;
using CareerPilot.Domain.Jobs;
using CareerPilot.Domain.Profiles;
using Microsoft.Extensions.Logging;

namespace CareerPilot.Application.Jobs;

/// <summary>
/// Runs a discovery pass over the enabled watchlist. A failing company never aborts the run; a partial
/// read never closes vacancies. This service can read and store jobs but has no way to apply to any.
/// </summary>
public sealed class JobSearchService(
    IJobStore jobs,
    IProfileStore profiles,
    IEnumerable<IJobSourceConnector> connectors,
    DiscoveryOptions options,
    ScoringOptions scoring,
    TimeProvider clock,
    IAuditLog audit,
    ILogger<JobSearchService> logger)
{
    private const string Actor = "owner";

    /// <summary>Creates a Queued run. Throws ConflictException if another search is already active.</summary>
    public async Task<SearchRunDto> QueueAsync(SearchTrigger trigger, CancellationToken ct)
    {
        var since = clock.GetUtcNow().AddMinutes(-options.ActiveRunTimeoutMinutes);
        if (await jobs.HasActiveRunAsync(since, ct))
            throw new ConflictException("A search is already running. Wait for it to finish.");

        var run = new SearchRun { Trigger = trigger };
        jobs.Add(run);
        audit.Stage(trigger == SearchTrigger.Manual ? Actor : "scheduler", "search.queued", nameof(SearchRun), run.Id, null);
        await jobs.SaveChangesAsync(ct);
        return JobMapper.ToDto(run);
    }

    /// <summary>Marks runs left Queued/Running by a previous process as failed so they stop blocking new searches.</summary>
    public async Task RecoverInterruptedAsync(CancellationToken ct)
    {
        foreach (var run in await jobs.GetActiveRunsAsync(ct))
        {
            run.Status = SearchRunStatus.Failed;
            run.FinishedAt = clock.GetUtcNow();
            run.Errors.Add("Interrupted: the service stopped before this search finished.");
        }
        await jobs.SaveChangesAsync(ct);
    }

    public async Task ExecuteAsync(Guid runId, CancellationToken ct)
    {
        var run = await jobs.GetRunAsync(runId, ct);
        if (run is null || run.Status != SearchRunStatus.Queued) return;

        run.Status = SearchRunStatus.Running;
        run.StartedAt = clock.GetUtcNow();
        await jobs.SaveChangesAsync(ct);

        try
        {
            await RunCoreAsync(run, ct);
        }
        catch (OperationCanceledException)
        {
            run.Status = SearchRunStatus.Failed;
            run.FinishedAt = clock.GetUtcNow();
            run.Errors.Add("Interrupted: the service is shutting down.");
            await jobs.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Search run {RunId} failed unexpectedly", run.Id);
            run.Status = SearchRunStatus.Failed;
            run.FinishedAt = clock.GetUtcNow();
            run.Errors.Add("Unexpected internal error (see server logs).");
            await jobs.SaveChangesAsync(CancellationToken.None);
        }
    }

    private async Task RunCoreAsync(SearchRun run, CancellationToken ct)
    {
        var profile = await profiles.GetAsync(ct);
        var prefs = profile?.Preferences ?? new JobPreference();
        var profileSkills = profile?.Skills.Select(s => s.Name).ToList() ?? [];

        if (profile is null)
            run.Errors.Add("No profile exists yet, so location checks use no preferences and jobs were not scored. Open your profile, then re-score.");

        var companies = await jobs.GetCompaniesAsync(enabledOnly: true, ct);
        run.CompaniesTotal = companies.Count;
        var first = true;

        foreach (var company in companies)
        {
            ct.ThrowIfCancellationRequested();
            if (!first && options.DelayBetweenCompaniesMs > 0)
                await Task.Delay(options.DelayBetweenCompaniesMs, ct); // be polite to public job boards
            first = false;

            company.LastSyncAt = clock.GetUtcNow();
            try
            {
                var connector = connectors.FirstOrDefault(c => c.Kind == company.Kind)
                                ?? throw new SourceFetchException($"No connector is available for {company.Kind}.");
                var fetched = await connector.FetchAsync(company, ct);

                var now = clock.GetUtcNow();
                var all = await jobs.GetAllListingsAsync(ct);
                var result = JobIngestion.Apply(company, fetched, all, options, profileSkills, now);
                foreach (var created in result.Created) jobs.Add(created);

                company.LastSuccessAt = now;
                company.LastError = null;
                company.LastJobCount = fetched.Jobs.Count;
                run.CompaniesSucceeded++;
                run.JobsFetched += fetched.Jobs.Count;
                run.JobsNew += result.New;
                run.JobsUpdated += result.Updated;
                run.JobsClosed += result.Closed;
            }
            catch (SourceFetchException ex)
            {
                company.LastError = ex.Message;
                run.Errors.Add($"{company.Name}: {ex.Message}");
            }
            await jobs.SaveChangesAsync(ct);
        }

        // Re-evaluate every listing against the current preferences, then refresh expiry/staleness.
        var listings = await jobs.GetAllListingsAsync(ct);
        var now2 = clock.GetUtcNow();
        JobIngestion.RefreshStatuses(listings, options, now2);
        foreach (var l in listings)
            JobClassifier.Classify(l, prefs, options, profile?.Country);
        if (profile is not null)
            MatchingService.ApplyAll(listings, profile, scoring, now2);

        run.Status = run.CompaniesTotal == 0 || run.CompaniesSucceeded == run.CompaniesTotal
            ? SearchRunStatus.Succeeded
            : run.CompaniesSucceeded == 0 ? SearchRunStatus.Failed : SearchRunStatus.PartiallyFailed;
        run.FinishedAt = now2;
        audit.Stage("system", "search.finished", nameof(SearchRun), run.Id,
            $"{run.Status}: {run.JobsNew} new, {run.JobsUpdated} updated, {run.JobsClosed} closed");
        await jobs.SaveChangesAsync(ct);
    }
}
