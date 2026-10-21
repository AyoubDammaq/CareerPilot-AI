using CareerPilot.Domain.Jobs;

namespace CareerPilot.Application.Jobs;

public sealed record CompanyDto(
    Guid Id, string Name, string Kind, string BoardToken, bool UseEuEndpoint, bool IsEnabled,
    DateTimeOffset? LastSyncAt, DateTimeOffset? LastSuccessAt, string? LastError, int? LastJobCount);

public sealed record CompanyInput(string? Name, string? Kind, string? BoardToken, bool UseEuEndpoint, bool IsEnabled);

public sealed record CompanyTestResult(bool Ok, int? JobCount, string? Error);

public sealed record PostingDto(string Source, string SourceUrl, string ApplyUrl, bool IsPresent, DateTimeOffset LastSeenAt);

public sealed record JobSummaryDto(
    Guid Id, string Title, string CompanyName, string LocationText, string Country, string City,
    string? WorkMode, string? EmploymentType, string Status, string GeoFit, string? GeoNote,
    bool IsRelevant, List<string> FilterReasons, string Seniority, List<string> Skills,
    DateTimeOffset? PublishedAt, DateTimeOffset DiscoveredAt, DateTimeOffset LastVerifiedAt,
    DateTimeOffset? ApplicationDeadline, decimal? SalaryMin, decimal? SalaryMax, string? SalaryCurrency,
    string? SalaryPeriod, string Decision, string? Language, bool MentionsSponsorship, bool MentionsRelocation,
    string SourceUrl, string ApplyUrl, List<string> Sources, int? MatchScore = null, string? MatchTier = null);

public sealed record JobDetailDto(JobSummaryDto Summary, string Description, int? ExperienceYearsHint, List<PostingDto> Postings,
    CareerPilot.Application.Matching.MatchResult? Match = null);

public sealed record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);

public sealed record SearchRunDto(
    Guid Id, string Trigger, string Status, DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt,
    int CompaniesTotal, int CompaniesSucceeded, int JobsFetched, int JobsNew, int JobsUpdated, int JobsClosed, List<string> Errors);

public sealed record DecisionInput(string? Decision);

public static class JobMapper
{
    public static CompanyDto ToDto(WatchedCompany c) => new(
        c.Id, c.Name, c.Kind.ToString(), c.BoardToken, c.UseEuEndpoint, c.IsEnabled,
        c.LastSyncAt, c.LastSuccessAt, c.LastError, c.LastJobCount);

    public static JobSummaryDto ToSummary(JobListing l)
    {
        var primary = l.Postings.FirstOrDefault(p => p.IsPresent) ?? l.Postings.FirstOrDefault();
        return new JobSummaryDto(
            l.Id, l.Title, l.CompanyName, l.LocationText, l.Country, l.City,
            l.WorkMode?.ToString(), l.EmploymentType, l.Status.ToString(), l.GeoFit.ToString(), l.GeoNote,
            l.IsRelevant, [.. l.FilterReasons], l.Seniority.ToString(), [.. l.Skills],
            l.PublishedAt, l.DiscoveredAt, l.LastVerifiedAt, l.ApplicationDeadline,
            l.SalaryMin, l.SalaryMax, l.SalaryCurrency, l.SalaryPeriod, l.Decision.ToString(), l.Language,
            l.MentionsSponsorship, l.MentionsRelocation,
            primary?.SourceUrl ?? "", primary?.ApplyUrl ?? "",
            [.. l.Postings.Select(p => p.Kind.ToString()).Distinct()],
            l.MatchScore, CareerPilot.Application.Matching.MatchingService.Deserialize(l.MatchJson)?.Tier);
    }

    public static JobDetailDto ToDetail(JobListing l) => new(
        ToSummary(l), l.Description, l.ExperienceYearsHint,
        [.. l.Postings.Select(p => new PostingDto(p.Kind.ToString(), p.SourceUrl, p.ApplyUrl, p.IsPresent, p.LastSeenAt))],
        CareerPilot.Application.Matching.MatchingService.Deserialize(l.MatchJson));

    public static SearchRunDto ToDto(SearchRun r) => new(
        r.Id, r.Trigger.ToString(), r.Status.ToString(), r.CreatedAt, r.StartedAt, r.FinishedAt,
        r.CompaniesTotal, r.CompaniesSucceeded, r.JobsFetched, r.JobsNew, r.JobsUpdated, r.JobsClosed, [.. r.Errors]);
}
