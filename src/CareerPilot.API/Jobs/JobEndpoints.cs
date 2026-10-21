using CareerPilot.Application.Abstractions;
using CareerPilot.Application.Ai;
using CareerPilot.Application.Applications;
using CareerPilot.Application.Documents;
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
        jobs.MapPost("{id:guid}/analysis", async (Guid id, bool? refresh, JobAnalysisService a, CancellationToken ct) =>
            Results.Ok(await a.AnalyzeAsync(id, refresh ?? false, ct)));
        jobs.MapPost("{id:guid}/documents/cv", async (Guid id, DocumentService d, CancellationToken ct) =>
        {
            var doc = await d.CreateCvAsync(id, ct);
            return Results.Created($"/api/documents/{doc.Id}", doc);
        });
        jobs.MapPost("{id:guid}/documents/cover-letter", async (Guid id, CoverLetterService c, CancellationToken ct) =>
        {
            var letter = await c.DraftAsync(id, ct);
            return Results.Created($"/api/documents/{letter.Id}/cover-letter", letter);
        });
        jobs.MapGet("{id:guid}/documents", async (Guid id, ApplicationService a, CancellationToken ct) => Results.Ok(await a.DocumentsForJobAsync(id, ct)));
        jobs.MapPost("{id:guid}/application", async (Guid id, ApplicationService a, CancellationToken ct) => Results.Ok(await a.StartAsync(id, ct)));

        var applications = app.MapGroup("/api/applications");
        applications.MapGet("", async (ApplicationService a, CancellationToken ct) => Results.Ok(await a.ListAsync(ct)));
        applications.MapGet("{id:guid}", async (Guid id, ApplicationService a, CancellationToken ct) => Results.Ok(await a.GetAsync(id, ct)));
        applications.MapPut("{id:guid}/documents", async (Guid id, SetDocumentsInput input, ApplicationService a, CancellationToken ct) => Results.Ok(await a.SetDocumentsAsync(id, input, ct)));
        applications.MapPost("{id:guid}/request-review", async (Guid id, ApplicationService a, CancellationToken ct) => Results.Ok(await a.RequestReviewAsync(id, ct)));
        applications.MapPost("{id:guid}/approve", async (Guid id, ApproveInput input, ApplicationService a, CancellationToken ct) => Results.Ok(await a.ApproveAsync(id, input, ct)));
        applications.MapPost("{id:guid}/withdraw-approval", async (Guid id, ApplicationService a, CancellationToken ct) => Results.Ok(await a.WithdrawApprovalAsync(id, ct)));
        applications.MapPost("{id:guid}/ready", async (Guid id, ApplicationService a, CancellationToken ct) => Results.Ok(await a.MarkReadyAsync(id, ct)));
        applications.MapPost("{id:guid}/submission", async (Guid id, SubmissionInput input, ApplicationService a, CancellationToken ct) => Results.Ok(await a.RecordSubmissionAsync(id, input, ct)));
        applications.MapPost("{id:guid}/confirm-submission", async (Guid id, ApplicationService a, CancellationToken ct) => Results.Ok(await a.ConfirmSubmissionAsync(id, ct)));
        applications.MapPost("{id:guid}/status", async (Guid id, StatusInput input, ApplicationService a, CancellationToken ct) => Results.Ok(await a.SetStatusAsync(id, input, ct)));

        var documents = app.MapGroup("/api/documents");
        documents.MapGet("{id:guid}/cover-letter", async (Guid id, CoverLetterService c, CancellationToken ct) => Results.Ok(await c.GetAsync(id, ct)));
        documents.MapPost("{id:guid}/revise", async (Guid id, ReviseLetterInput input, CoverLetterService c, CancellationToken ct) =>
        {
            var letter = await c.ReviseAsync(id, input, ct);
            return Results.Created($"/api/documents/{letter.Id}/cover-letter", letter);
        });
        documents.MapGet("{id:guid}", async (Guid id, DocumentService d, CancellationToken ct) => Results.Ok(await d.GetAsync(id, ct)));
        documents.MapGet("{id:guid}/download", async (Guid id, DocumentService d, CancellationToken ct) =>
        {
            var f = await d.DownloadDocxAsync(id, ct);
            return Results.File(f.Bytes, f.ContentType, f.FileName);
        });
        app.MapGet("/api/ai/status", async (JobAnalysisService a, CancellationToken ct) => Results.Ok(await a.StatusAsync(ct)));
        jobs.MapGet("{id:guid}", async (Guid id, JobQueryService s, CancellationToken ct) => Results.Ok(await s.GetAsync(id, ct)));
        jobs.MapPut("{id:guid}/decision", async (Guid id, DecisionInput input, JobQueryService s, CancellationToken ct) =>
            Results.Ok(await s.SetDecisionAsync(id, input.Decision, ct)));
    }
}
