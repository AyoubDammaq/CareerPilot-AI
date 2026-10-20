using CareerPilot.Domain.Jobs;
using CareerPilot.Domain.Profiles;

namespace CareerPilot.Application.Jobs;

/// <summary>A job in the common schema, as produced by a source connector (before enrichment).</summary>
public sealed record SourceJob(
    string SourceJobId,
    string Title,
    string LocationText,
    string? CountryHint,          // ISO alpha-2 code or a country name, when the source states one
    WorkMode? WorkMode,
    string? EmploymentType,
    string Description,           // plain text
    DateTimeOffset? PublishedAt,
    DateTimeOffset? Deadline,
    string SourceUrl,
    string ApplyUrl,
    decimal? SalaryMin = null,
    decimal? SalaryMax = null,
    string? SalaryCurrency = null,
    string? SalaryPeriod = null,
    string? Language = null);

/// <param name="IsCompleteSnapshot">True only when the whole board was read successfully. Closing of
/// missing jobs is allowed only for complete snapshots, so a partial read never closes live vacancies.</param>
public sealed record ConnectorResult(IReadOnlyList<SourceJob> Jobs, bool IsCompleteSnapshot);

/// <summary>Reads one company's public job board through its official public feed.</summary>
public interface IJobSourceConnector
{
    JobSourceKind Kind { get; }
    Task<ConnectorResult> FetchAsync(WatchedCompany company, CancellationToken ct);
}

public sealed class SourceFetchException(string message, bool notFound = false, Exception? inner = null)
    : Exception(message, inner)
{
    public bool NotFound { get; } = notFound;
}
