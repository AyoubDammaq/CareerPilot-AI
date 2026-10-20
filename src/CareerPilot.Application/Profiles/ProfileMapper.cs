using CareerPilot.Domain.Profiles;

namespace CareerPilot.Application.Profiles;

public static class ProfileMapper
{
    public static ProfileDto ToDto(UserProfile p) => new(
        p.FullName,
        p.Email,
        p.Phone,
        p.Headline,
        p.Summary,
        p.Country,
        p.City,
        p.GithubUrl,
        p.LinkedinUrl,
        p.WebsiteUrl,
        p.Version,
        p.WorkExperiences.OrderBy(x => x.Position)
            .Select(x => new WorkExperienceDto(x.Id, x.Company, x.Title, x.Location, x.StartDate, x.EndDate,
                x.Description, x.Achievements.ToList(), x.IsVerified)).ToList(),
        p.Education.OrderBy(x => x.Position)
            .Select(x => new EducationDto(x.Id, x.Institution, x.Degree, x.Field, x.StartDate, x.EndDate)).ToList(),
        p.Skills.OrderBy(x => x.Position)
            .Select(x => new SkillDto(x.Id, x.Name, x.Category, x.Level?.ToString(), x.IsVerified)).ToList(),
        p.Projects.OrderBy(x => x.Position)
            .Select(x => new ProjectDto(x.Id, x.Name, x.Description, x.Url, x.Technologies.ToList(),
                x.Results.ToList(), x.IsVerified)).ToList(),
        p.Languages.OrderBy(x => x.Position)
            .Select(x => new LanguageDto(x.Id, x.Name, x.Level)).ToList(),
        new PreferencesDto(
            p.Preferences.TargetCountries.ToList(),
            p.Preferences.TargetCities.ToList(),
            p.Preferences.WorkModes.ToList(),
            p.Preferences.WillingToRelocate,
            p.Preferences.RequiresVisaSponsorship,
            p.Preferences.IncludeInternationalRemote,
            p.Preferences.EmploymentTypes.ToList(),
            p.Preferences.TargetTitles.ToList(),
            p.Preferences.WorkingLanguages.ToList(),
            p.Preferences.IncludeSeniorRoles,
            p.Preferences.ExperienceLevel,
            p.Preferences.MinSalary,
            p.Preferences.PreferredSalary,
            p.Preferences.SalaryCurrency));

    /// <summary>
    /// Applies a validated DTO onto the tracked entity. Items are matched by id; unknown ids are
    /// treated as new items (the server always generates ids); items missing from the DTO are removed.
    /// </summary>
    public static void Apply(UserProfile p, ProfileDto dto)
    {
        p.FullName = Clean(dto.FullName);
        p.Email = Clean(dto.Email);
        p.Phone = CleanOrNull(dto.Phone);
        p.Headline = Clean(dto.Headline);
        p.Summary = Clean(dto.Summary);
        p.Country = Clean(dto.Country);
        p.City = Clean(dto.City);
        p.GithubUrl = CleanOrNull(dto.GithubUrl);
        p.LinkedinUrl = CleanOrNull(dto.LinkedinUrl);
        p.WebsiteUrl = CleanOrNull(dto.WebsiteUrl);

        Sync(p.WorkExperiences, dto.WorkExperiences ?? [], d => d.Id, () => new WorkExperience(), (e, d) =>
        {
            e.Company = Clean(d.Company);
            e.Title = Clean(d.Title);
            e.Location = Clean(d.Location);
            e.StartDate = d.StartDate;
            e.EndDate = d.EndDate;
            e.Description = Clean(d.Description);
            e.Achievements = CleanList(d.Achievements);
            e.IsVerified = d.IsVerified;
        });

        Sync(p.Education, dto.Education ?? [], d => d.Id, () => new Education(), (e, d) =>
        {
            e.Institution = Clean(d.Institution);
            e.Degree = Clean(d.Degree);
            e.Field = Clean(d.Field);
            e.StartDate = d.StartDate;
            e.EndDate = d.EndDate;
        });

        Sync(p.Skills, dto.Skills ?? [], d => d.Id, () => new Skill(), (e, d) =>
        {
            e.Name = Clean(d.Name);
            e.Category = Clean(d.Category);
            e.Level = ParseLevel(d.Level);
            e.IsVerified = d.IsVerified;
        });

        Sync(p.Projects, dto.Projects ?? [], d => d.Id, () => new Project(), (e, d) =>
        {
            e.Name = Clean(d.Name);
            e.Description = Clean(d.Description);
            e.Url = CleanOrNull(d.Url);
            e.Technologies = CleanList(d.Technologies);
            e.Results = CleanList(d.Results);
            e.IsVerified = d.IsVerified;
        });

        Sync(p.Languages, dto.Languages ?? [], d => d.Id, () => new ProfileLanguage(), (e, d) =>
        {
            e.Name = Clean(d.Name);
            e.Level = Clean(d.Level);
        });

        var pref = dto.Preferences;
        var target = p.Preferences;
        target.TargetCountries = CleanList(pref.TargetCountries);
        target.TargetCities = CleanList(pref.TargetCities);
        target.WorkModes = CleanList(pref.WorkModes);
        target.WillingToRelocate = pref.WillingToRelocate;
        target.RequiresVisaSponsorship = pref.RequiresVisaSponsorship;
        target.IncludeInternationalRemote = pref.IncludeInternationalRemote;
        target.EmploymentTypes = CleanList(pref.EmploymentTypes);
        target.TargetTitles = CleanList(pref.TargetTitles);
        target.WorkingLanguages = CleanList(pref.WorkingLanguages);
        target.IncludeSeniorRoles = pref.IncludeSeniorRoles;
        target.ExperienceLevel = Clean(pref.ExperienceLevel);
        target.MinSalary = pref.MinSalary;
        target.PreferredSalary = pref.PreferredSalary;
        target.SalaryCurrency = Clean(pref.SalaryCurrency).ToUpperInvariant();
        target.Touch();
    }

    private static void Sync<TEntity, TDto>(
        List<TEntity> entities,
        IReadOnlyList<TDto> dtos,
        Func<TDto, Guid?> getId,
        Func<TEntity> create,
        Action<TEntity, TDto> map)
        where TEntity : ProfileItem
    {
        var existing = entities.ToDictionary(e => e.Id);
        var keep = new HashSet<Guid>();

        for (var i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];
            TEntity entity;
            if (getId(dto) is { } id && existing.TryGetValue(id, out var found) && keep.Add(id))
            {
                entity = found;
            }
            else
            {
                entity = create();
                entities.Add(entity);
                keep.Add(entity.Id);
            }

            map(entity, dto);
            entity.Position = i;
            entity.Touch();
        }

        entities.RemoveAll(e => !keep.Contains(e.Id));
    }

    public static SkillLevel? ParseLevel(string? level) =>
        !string.IsNullOrWhiteSpace(level) && Enum.GetNames<SkillLevel>().Contains(level.Trim(), StringComparer.Ordinal)
            ? Enum.Parse<SkillLevel>(level.Trim())
            : null;

    private static string Clean(string? value) => value?.Trim() ?? "";

    private static string? CleanOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<string> CleanList(IEnumerable<string>? values) =>
        (values ?? [])
            .Select(v => v?.Trim() ?? "")
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
