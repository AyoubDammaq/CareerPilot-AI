using CareerPilot.Application.Abstractions;
using CareerPilot.Application.Jobs;
using CareerPilot.Domain.Jobs;
using CareerPilot.Domain.Profiles;
using Microsoft.EntityFrameworkCore;

namespace CareerPilot.Infrastructure.Persistence;

public sealed class EfJobStore(CareerPilotDbContext db) : IJobStore
{
    public Task<List<WatchedCompany>> GetCompaniesAsync(bool enabledOnly, CancellationToken ct) =>
        db.WatchedCompanies.Where(c => !enabledOnly || c.IsEnabled).OrderBy(c => c.Name).ToListAsync(ct);

    public Task<WatchedCompany?> GetCompanyAsync(Guid id, CancellationToken ct) =>
        db.WatchedCompanies.FirstOrDefaultAsync(c => c.Id == id, ct);

    public void Add(WatchedCompany company) => db.WatchedCompanies.Add(company);
    public void Remove(WatchedCompany company) => db.WatchedCompanies.Remove(company);

    public Task<List<JobListing>> GetAllListingsAsync(CancellationToken ct) =>
        db.JobListings.Include(l => l.Postings).AsSplitQuery().ToListAsync(ct);

    public Task<JobListing?> GetListingAsync(Guid id, CancellationToken ct) =>
        db.JobListings.Include(l => l.Postings).FirstOrDefaultAsync(l => l.Id == id, ct);

    public void Add(JobListing listing) => db.JobListings.Add(listing);

    public async Task<(List<JobListing> Items, int Total)> QueryAsync(JobQuery query, CancellationToken ct)
    {
        IQueryable<JobListing> q = db.JobListings.AsNoTracking();

        if (query.RelevantOnly) q = q.Where(l => l.IsRelevant);

        // The owner's decisions: excluded jobs are hidden unless explicitly asked for.
        if (Enum.TryParse<UserDecision>(query.Decision, out var decision))
            q = q.Where(l => l.Decision == decision);
        else
            q = q.Where(l => l.Decision != UserDecision.Excluded);

        if (string.Equals(query.Status, "All", StringComparison.OrdinalIgnoreCase)) { }
        else if (Enum.TryParse<JobStatus>(query.Status, out var status))
            q = q.Where(l => l.Status == status);
        else
            q = q.Where(l => l.Status == JobStatus.Open || l.Status == JobStatus.Unverified);

        if (!string.IsNullOrWhiteSpace(query.Country))
        {
            var code = CountryCatalog.CodeFor(query.Country);
            var text = query.Country.Trim();
            q = code is not null
                ? q.Where(l => l.CountryCodes.Contains(code))
                : q.Where(l => l.Country == text);
        }

        if (Enum.TryParse<WorkMode>(query.WorkMode, out var mode))
            q = q.Where(l => l.WorkMode == mode);

        if (Enum.TryParse<GeoFit>(query.GeoFit, out var fit))
            q = q.Where(l => l.GeoFit == fit);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = "%" + query.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            q = q.Where(l => EF.Functions.ILike(l.Title, pattern) || EF.Functions.ILike(l.CompanyName, pattern));
        }

        if (query.MinScore is { } minScore)
            q = q.Where(l => l.MatchScore != null && l.MatchScore >= minScore);

        var total = await q.CountAsync(ct);

        q = query.Sort switch
        {
            "company" => q.OrderBy(l => l.CompanyName).ThenBy(l => l.Title).ThenBy(l => l.Id),
            "score" => q.OrderByDescending(l => l.MatchScore ?? -1).ThenByDescending(l => l.PublishedAt ?? l.DiscoveredAt).ThenBy(l => l.Id),
            "title" => q.OrderBy(l => l.Title).ThenBy(l => l.Id),
            "verified" => q.OrderByDescending(l => l.LastVerifiedAt).ThenBy(l => l.Id),
            "deadline" => q.OrderBy(l => l.ApplicationDeadline == null).ThenBy(l => l.ApplicationDeadline).ThenBy(l => l.Id),
            _ => q.OrderByDescending(l => l.PublishedAt ?? l.DiscoveredAt).ThenBy(l => l.Id),
        };

        var items = await q.Include(l => l.Postings).AsSplitQuery()
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return (items, total);
    }

    public void Add(SearchRun run) => db.SearchRuns.Add(run);

    public Task<SearchRun?> GetRunAsync(Guid id, CancellationToken ct) =>
        db.SearchRuns.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<List<SearchRun>> GetRecentRunsAsync(int take, CancellationToken ct) =>
        db.SearchRuns.AsNoTracking().OrderByDescending(r => r.CreatedAt).Take(take).ToListAsync(ct);

    public Task<bool> HasActiveRunAsync(DateTimeOffset since, CancellationToken ct) =>
        db.SearchRuns.AnyAsync(r => (r.Status == SearchRunStatus.Queued || r.Status == SearchRunStatus.Running) && r.CreatedAt > since, ct);

    public Task<List<SearchRun>> GetActiveRunsAsync(CancellationToken ct) =>
        db.SearchRuns.Where(r => r.Status == SearchRunStatus.Queued || r.Status == SearchRunStatus.Running).ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
