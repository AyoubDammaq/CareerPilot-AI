using CareerPilot.Application.Abstractions;
using CareerPilot.Application.Jobs;
using CareerPilot.Application.Profiles;
using CareerPilot.Domain.Jobs;
using CareerPilot.Domain.Profiles;

namespace CareerPilot.Tests.Jobs;

internal static class Helpers
{
    public static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    public static DiscoveryOptions Options() => new() { DelayBetweenCompaniesMs = 0 };

    public static JobPreference Prefs() => DefaultProfileFactory.Create().Preferences;

    public static WatchedCompany Company(string name = "Acme", JobSourceKind kind = JobSourceKind.Greenhouse, string token = "acme") =>
        new() { Name = name, Kind = kind, BoardToken = token };

    public static SourceJob Job(string id, string title = "Backend Engineer", string location = "Paris, France",
        string description = "Build services.", WorkMode? mode = null, DateTimeOffset? deadline = null,
        string? url = null, string? country = null) =>
        new(id, title, location, country, mode, null, description, null, deadline,
            url ?? $"https://example.test/jobs/{id}", url ?? $"https://example.test/jobs/{id}/apply");

    public static ConnectorResult Result(bool complete, params SourceJob[] jobs) => new(jobs, complete);

    /// <summary>A listing as the ingestion would build it (place resolved from the location text).</summary>
    public static JobListing Listing(string title = "Backend Engineer", string location = "Paris, France", WorkMode? mode = null,
        bool mentionsSponsorship = false)
    {
        var place = CountryCatalog.Resolve(location, null);
        return new JobListing
        {
            Title = title,
            LocationText = location,
            CountryCodes = place.CountryCodes,
            Country = place.Country,
            City = place.City,
            WorkMode = mode,
            Seniority = JobEnricher.DetectSeniority(title, Options()),
            MentionsSponsorship = mentionsSponsorship,
        };
    }
}

internal sealed class NoopAudit : IAuditLog
{
    public List<string> Actions { get; } = [];
    public void Stage(string actor, string action, string entityType, Guid? entityId, string? details) => Actions.Add(action);
}

internal sealed class StaticProfileStore(UserProfile? profile) : IProfileStore
{
    public UserProfile? Profile { get; } = profile;
    public Task<UserProfile?> GetAsync(CancellationToken ct) => Task.FromResult(Profile);
    public void Add(UserProfile p) => throw new NotSupportedException();
    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
}

internal sealed class FakeConnector(JobSourceKind kind, Func<WatchedCompany, ConnectorResult> fetch) : IJobSourceConnector
{
    public JobSourceKind Kind => kind;
    public Task<ConnectorResult> FetchAsync(WatchedCompany company, CancellationToken ct) => Task.FromResult(fetch(company));
}

internal sealed class InMemoryJobStore : IJobStore
{
    public List<WatchedCompany> Companies { get; } = [];
    public List<JobListing> Listings { get; } = [];
    public List<SearchRun> Runs { get; } = [];
    public int SaveCount { get; private set; }

    public Task<List<WatchedCompany>> GetCompaniesAsync(bool enabledOnly, CancellationToken ct) =>
        Task.FromResult(Companies.Where(c => !enabledOnly || c.IsEnabled).ToList());
    public Task<WatchedCompany?> GetCompanyAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Companies.FirstOrDefault(c => c.Id == id));
    public void Add(WatchedCompany company) => Companies.Add(company);
    public void Remove(WatchedCompany company) => Companies.Remove(company);
    public Task<List<JobListing>> GetAllListingsAsync(CancellationToken ct) => Task.FromResult(Listings.ToList());
    public Task<JobListing?> GetListingAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Listings.FirstOrDefault(l => l.Id == id));
    public void Add(JobListing listing) => Listings.Add(listing);
    public Task<(List<JobListing> Items, int Total)> QueryAsync(JobQuery query, CancellationToken ct) =>
        throw new NotSupportedException();
    public void Add(SearchRun run) => Runs.Add(run);
    public Task<SearchRun?> GetRunAsync(Guid id, CancellationToken ct) => Task.FromResult(Runs.FirstOrDefault(r => r.Id == id));
    public Task<List<SearchRun>> GetRecentRunsAsync(int take, CancellationToken ct) =>
        Task.FromResult(Runs.OrderByDescending(r => r.CreatedAt).Take(take).ToList());
    public Task<bool> HasActiveRunAsync(DateTimeOffset since, CancellationToken ct) =>
        Task.FromResult(Runs.Any(r => r.Status is SearchRunStatus.Queued or SearchRunStatus.Running && r.CreatedAt > since));
    public Task<List<SearchRun>> GetActiveRunsAsync(CancellationToken ct) =>
        Task.FromResult(Runs.Where(r => r.Status is SearchRunStatus.Queued or SearchRunStatus.Running).ToList());
    public Task SaveChangesAsync(CancellationToken ct)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
