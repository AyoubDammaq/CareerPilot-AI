namespace CareerPilot.Application.Profiles;

public sealed record ProfileDto(
    string FullName,
    string Email,
    string? Phone,
    string Headline,
    string Summary,
    string Country,
    string City,
    string? GithubUrl,
    string? LinkedinUrl,
    string? WebsiteUrl,
    int Version,
    List<WorkExperienceDto> WorkExperiences,
    List<EducationDto> Education,
    List<SkillDto> Skills,
    List<ProjectDto> Projects,
    List<LanguageDto> Languages,
    PreferencesDto Preferences);

public sealed record WorkExperienceDto(
    Guid? Id,
    string Company,
    string Title,
    string Location,
    DateOnly StartDate,
    DateOnly? EndDate,
    string Description,
    List<string> Achievements,
    bool IsVerified);

public sealed record EducationDto(
    Guid? Id,
    string Institution,
    string Degree,
    string Field,
    DateOnly? StartDate,
    DateOnly? EndDate);

public sealed record SkillDto(
    Guid? Id,
    string Name,
    string Category,
    string? Level,
    bool IsVerified);

public sealed record ProjectDto(
    Guid? Id,
    string Name,
    string Description,
    string? Url,
    List<string> Technologies,
    List<string> Results,
    bool IsVerified);

public sealed record LanguageDto(Guid? Id, string Name, string Level);

public sealed record PreferencesDto(
    List<string> TargetCountries,
    List<string> TargetCities,
    List<string> WorkModes,
    bool WillingToRelocate,
    bool RequiresVisaSponsorship,
    bool IncludeInternationalRemote,
    List<string> EmploymentTypes,
    List<string> TargetTitles,
    List<string> WorkingLanguages,
    bool IncludeSeniorRoles,
    string ExperienceLevel,
    decimal? MinSalary,
    decimal? PreferredSalary,
    string SalaryCurrency);

public sealed record DashboardDto(
    int ProfileCompleteness,
    IReadOnlyList<string> MissingItems,
    IReadOnlyList<string> ImplementedModules);
