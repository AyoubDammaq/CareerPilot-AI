using CareerPilot.Application.Abstractions;
using CareerPilot.Application.Jobs;
using CareerPilot.Application.Matching;
using CareerPilot.Domain.Jobs;

namespace CareerPilot.API.Jobs;

public static class JobEndpoints
{
    public static void MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        var companies = app.MapGroup("/api/sources/companies");
        companies.MapGet("", async (CompanyService s, CancellationToken ct) => Results.Ok(await s.ListAsync(ct)));
        companies.MapPost("", async (CompanyInput input, CompanyService s, CancellationToken ct) =>
        {
            var created = await s.AddAsync(input, ct);
            return Results.Created($"/api/sources/companies/{created.Id}", created);
        });
        companies.MapPut("{id:guid}", async (Guid id, CompanyInput input, CompanyService s, CancellationToken ct) =>
            Results.Ok(await s.UpdateAsync(id, input, ct)));
        companies.MapDelete("{id:guid}", async (Guid id, CompanyService s, CancellationToken ct) =>
        {
            await s.DeleteAsync(id, ct);
            return Results.NoContent();
        });
        companies.MapPost("{id:guid}/test", async (Guid id, CompanyService s, CancellationToken ct) =>
            Results.Ok(await s.TestAsync(id, ct)));

        var runs = app.MapGroup("/api/search-runs");
        runs.MapPost("", async (JobSearchService search, SearchRunQueue queue, CancellationToken ct) =>
        {
            var run = await search.QueueAsync(SearchTrigger.Manual, ct);
            queue.Enqueue(run.Id);
            return Results.Accepted($"/api/search-runs/{run.Id}", run);
        });
        runs.MapGet("", async (JobQueryService s, CancellationToken ct) => Results.Ok(await s.RecentRunsAsync(ct)));
        runs.MapGet("{id:guid}", async (Guid id, JobQueryService s, CancellationToken ct) => Results.Ok(await s.GetRunAsync(id, ct)));

        var jobs = app.MapGroup("/api/jobs");
        jobs.MapGet("", async (
            JobQueryService s, CancellationToken ct,
            string? q, string? country, string? workMode, string? status, bool? relevantOnly,
            string? geoFit, string? decision, string? sort, int? page, int? pageSize, int? minScore) =>
            Results.Ok(await s.QueryAsync(new JobQuery(
                q, country, workMode, status, relevantOnly ?? true, geoFit, decision,
                sort ?? "newest", page ?? 1, pageSize ?? 25, minScore), ct)));
        jobs.MapPost("rescore", async (MatchingService m, CancellationToken ct) => Results.Ok(await m.RescoreAllAsync(ct)));
        jobs.MapGet("{id:guid}", async (Guid id, JobQueryService s, CancellationToken ct) => Results.Ok(await s.GetAsync(id, ct)));
        jobs.MapPut("{id:guid}/decision", async (Guid id, DecisionInput input, JobQueryService s, CancellationToken ct) =>
            Results.Ok(await s.SetDecisionAsync(id, input.Decision, ct)));
    }
}
