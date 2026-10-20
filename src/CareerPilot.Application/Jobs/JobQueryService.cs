using CareerPilot.Application.Abstractions;
using CareerPilot.Application.Common;
using CareerPilot.Domain.Jobs;

namespace CareerPilot.Application.Jobs;

public sealed class JobQueryService(IJobStore store, IAuditLog audit)
{
    public async Task<PagedResult<JobSummaryDto>> QueryAsync(JobQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 100);
        var (items, total) = await store.QueryAsync(query with { Page = page, PageSize = size }, ct);
        return new PagedResult<JobSummaryDto>([.. items.Select(JobMapper.ToSummary)], total, page, size);
    }

    public async Task<JobDetailDto> GetAsync(Guid id, CancellationToken ct) =>
        JobMapper.ToDetail(await store.GetListingAsync(id, ct) ?? throw new NotFoundException("Job not found."));

    public async Task<JobSummaryDto> SetDecisionAsync(Guid id, string? decision, CancellationToken ct)
    {
        if (!Enum.TryParse<UserDecision>(decision, ignoreCase: false, out var parsed) || !Enum.IsDefined(parsed))
            throw new ValidationFailedException(new Dictionary<string, string[]>
                { ["decision"] = [$"Must be one of: {string.Join(", ", Enum.GetNames<UserDecision>())}."] });

        var job = await store.GetListingAsync(id, ct) ?? throw new NotFoundException("Job not found.");
        job.Decision = parsed;
        job.Touch();
        audit.Stage("owner", "job.decision", nameof(JobListing), job.Id, parsed.ToString());
        await store.SaveChangesAsync(ct);
        return JobMapper.ToSummary(job);
    }

    public async Task<List<SearchRunDto>> RecentRunsAsync(CancellationToken ct) =>
        [.. (await store.GetRecentRunsAsync(20, ct)).Select(JobMapper.ToDto)];

    public async Task<SearchRunDto> GetRunAsync(Guid id, CancellationToken ct) =>
        JobMapper.ToDto(await store.GetRunAsync(id, ct) ?? throw new NotFoundException("Search run not found."));
}
