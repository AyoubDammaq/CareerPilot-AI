using CareerPilot.Domain.Jobs;

namespace CareerPilot.Application.Abstractions;

public sealed record JobQuery(
    string? Search = null,
    string? Country = null,
    string? WorkMode = null,
    string? Status = null,
    bool RelevantOnly = true,
    string? GeoFit = null,
    string? Decision = null,
    string Sort = "newest",
    int Page = 1,
    int PageSize = 25);

/// <summary>
/// Persistence for jobs, the watchlist and search runs. Entities returned by Get*/List* are tracked, so
/// changes made to them are saved by SaveChangesAsync (shared unit of work with the profile store).
/// </summary>
public interface IJobStore
{
    Task<List<WatchedCompany>> GetCompaniesAsync(bool enabledOnly, CancellationToken ct);
    Task<WatchedCompany?> GetCompanyAsync(Guid id, CancellationToken ct);
    void Add(WatchedCompany company);
    void Remove(WatchedCompany company);

    /// <summary>All listings with their postings (the data set is personal-scale: hundreds to low thousands).</summary>
    Task<List<JobListing>> GetAllListingsAsync(CancellationToken ct);
    Task<JobListing?> GetListingAsync(Guid id, CancellationToken ct);
    void Add(JobListing listing);
    Task<(List<JobListing> Items, int Total)> QueryAsync(JobQuery query, CancellationToken ct);

    void Add(SearchRun run);
    Task<SearchRun?> GetRunAsync(Guid id, CancellationToken ct);
    Task<List<SearchRun>> GetRecentRunsAsync(int take, CancellationToken ct);

    /// <summary>True if a Queued or Running search was created after <paramref name="since"/>.</summary>
    Task<bool> HasActiveRunAsync(DateTimeOffset since, CancellationToken ct);
    Task<List<SearchRun>> GetActiveRunsAsync(CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
