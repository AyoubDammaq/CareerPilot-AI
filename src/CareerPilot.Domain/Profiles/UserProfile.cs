using CareerPilot.Domain.Common;

namespace CareerPilot.Domain.Profiles;

/// <summary>
/// The canonical career profile: the single source of truth for every generated document.
/// Facts carry an IsVerified flag meaning "the owner confirmed this is true"; later phases
/// will only let verified facts into generated CVs.
/// </summary>
public class UserProfile : Entity
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string Headline { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Country { get; set; } = "";
    public string City { get; set; } = "";
    public string? GithubUrl { get; set; }
    public string? LinkedinUrl { get; set; }
    public string? WebsiteUrl { get; set; }

    /// <summary>Incremented on every save. Also the optimistic-concurrency token.</summary>
    public int Version { get; set; } = 1;

    public List<WorkExperience> WorkExperiences { get; set; } = [];
    public List<Education> Education { get; set; } = [];
    public List<Skill> Skills { get; set; } = [];
    public List<Project> Projects { get; set; } = [];
    public List<ProfileLanguage> Languages { get; set; } = [];
    public JobPreference Preferences { get; set; } = new();
}

/// <summary>A list item owned by a profile; Position preserves the owner's chosen order.</summary>
public abstract class ProfileItem : Entity
{
    public Guid UserProfileId { get; set; }
    public int Position { get; set; }
}

public class WorkExperience : ProfileItem
{
    public string Company { get; set; } = "";
    public string Title { get; set; } = "";
    public string Location { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Description { get; set; } = "";
    public List<string> Achievements { get; set; } = [];
    public bool IsVerified { get; set; }
}

public class Education : ProfileItem
{
    public string Institution { get; set; } = "";
    public string Degree { get; set; } = "";
    public string Field { get; set; } = "";
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}

public class Skill : ProfileItem
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";

    /// <summary>Null means "not yet rated", which is different from a low level.</summary>
    public SkillLevel? Level { get; set; }
    public bool IsVerified { get; set; }
}

public class Project : ProfileItem
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Url { get; set; }
    public List<string> Technologies { get; set; } = [];

    /// <summary>Measurable results. Only ever contains what the owner entered.</summary>
    public List<string> Results { get; set; } = [];
    public bool IsVerified { get; set; }
}

public class ProfileLanguage : ProfileItem
{
    public string Name { get; set; } = "";
    public string Level { get; set; } = "";
}

public class JobPreference : Entity
{
    public Guid UserProfileId { get; set; }
    public List<string> TargetCountries { get; set; } = [];
    public List<string> TargetCities { get; set; } = [];
    public List<string> WorkModes { get; set; } = [];
    public bool WillingToRelocate { get; set; }
    public bool RequiresVisaSponsorship { get; set; }

    /// <summary>Also consider remote roles outside the target countries (eligibility is still checked per job).</summary>
    public bool IncludeInternationalRemote { get; set; }
    public List<string> EmploymentTypes { get; set; } = [];
    public List<string> TargetTitles { get; set; } = [];
    public List<string> WorkingLanguages { get; set; } = [];
    public bool IncludeSeniorRoles { get; set; }
    public string ExperienceLevel { get; set; } = "";
    public decimal? MinSalary { get; set; }
    public decimal? PreferredSalary { get; set; }
    public string SalaryCurrency { get; set; } = "EUR";
}
