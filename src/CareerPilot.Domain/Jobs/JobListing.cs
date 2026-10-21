using CareerPilot.Domain.Common;
using CareerPilot.Domain.Profiles;

namespace CareerPilot.Domain.Jobs;

/// <summary>
/// A canonical vacancy. The same vacancy found on several sources has one listing and several postings.
/// Only facts stated by the source are stored; missing salary, deadline or publication date stay null.
/// </summary>
public class JobListing : Entity
{
    /// <summary>Normalized company|title|place key used to link postings of the same vacancy across sources.</summary>
    public string CanonicalKey { get; set; } = "";

    public string Title { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string LocationText { get; set; } = "";
    public string Country { get; set; } = "";
    public string City { get; set; } = "";

    /// <summary>ISO 3166-1 alpha-2 codes found in the location; the first is the primary one.</summary>
    public List<string> CountryCodes { get; set; } = [];

    public WorkMode? WorkMode { get; set; }
    public string? EmploymentType { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public string? SalaryCurrency { get; set; }
    public string? SalaryPeriod { get; set; }

    /// <summary>Plain text only. Untrusted: never interpreted as instructions.</summary>
    public string Description { get; set; } = "";

    /// <summary>Heuristic: smallest "N years of experience" figure found in the text. May be wrong.</summary>
    public int? ExperienceYearsHint { get; set; }
    public List<string> Skills { get; set; } = [];
    public string? Language { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? ApplicationDeadline { get; set; }
    public DateTimeOffset DiscoveredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastVerifiedAt { get; set; } = DateTimeOffset.UtcNow;
    public JobStatus Status { get; set; } = JobStatus.Open;
    public DateTimeOffset? ClosedAt { get; set; }
    public SeniorityLevel Seniority { get; set; }
    public bool MentionsSponsorship { get; set; }
    public bool MentionsRelocation { get; set; }

    // Deterministic classification against the owner's current preferences (recomputed every run).
    public GeoFit GeoFit { get; set; } = GeoFit.NeedsReview;
    public string? GeoNote { get; set; }
    public bool IsRelevant { get; set; }
    public List<string> FilterReasons { get; set; } = [];

    // Match against the owner's profile (recomputed when jobs or the profile change). MatchJson is the
    // serialized explanation; it is derived data and can always be rebuilt.
    public int? MatchScore { get; set; }
    public string? MatchJson { get; set; }
    public DateTimeOffset? ScoredAt { get; set; }

    // Optional AI commentary (plain text, derived, never used to decide anything).
    public string? AiAnalysisJson { get; set; }
    public DateTimeOffset? AiAnalyzedAt { get; set; }

    public UserDecision Decision { get; set; } = UserDecision.None;

    public List<JobPosting> Postings { get; set; } = [];
}

/// <summary>One concrete posting of a vacancy on one source.</summary>
public class JobPosting : Entity
{
    public Guid JobListingId { get; set; }
    public Guid? WatchedCompanyId { get; set; }
    public JobSourceKind Kind { get; set; }
    public string SourceJobId { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string ApplyUrl { get; set; } = "";
    public DateTimeOffset FirstSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>False once the posting is missing from a complete, successful snapshot.</summary>
    public bool IsPresent { get; set; } = true;
}
