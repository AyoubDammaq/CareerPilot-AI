using System.Net.Mail;
using System.Text.RegularExpressions;
using CareerPilot.Domain.Profiles;

namespace CareerPilot.Application.Profiles;

/// <summary>
/// Structural validation of profile input. Error keys are camelCase field paths such as
/// "email", "workExperiences[0].endDate" or "preferences.minSalary". Partially filled profiles
/// are allowed (onboarding), so only format, bounds and obviously required row keys are enforced.
/// </summary>
public static partial class ProfileValidator
{
    private const int MaxItems = 100;
    private const int MaxListValues = 100;
    private static readonly DateOnly MinDate = new(1950, 1, 1);

    private static readonly HashSet<string> WorkModeNames = Enum.GetNames<WorkMode>().ToHashSet(StringComparer.Ordinal);
    private static readonly HashSet<string> EmploymentTypeNames = Enum.GetNames<EmploymentType>().ToHashSet(StringComparer.Ordinal);
    private static readonly HashSet<string> SkillLevelNames = Enum.GetNames<SkillLevel>().ToHashSet(StringComparer.Ordinal);

    [GeneratedRegex("^[A-Za-z]{3}$")]
    private static partial Regex CurrencyRegex();

    public static Dictionary<string, string[]> Validate(ProfileDto dto)
    {
        var errors = new Dictionary<string, List<string>>();

        void Add(string key, string message)
        {
            if (!errors.TryGetValue(key, out var list))
            {
                list = [];
                errors[key] = list;
            }
            list.Add(message);
        }

        void MaxLen(string key, string? value, int max)
        {
            if (value is not null && value.Length > max)
                Add(key, $"Must be at most {max} characters.");
        }

        void Required(string key, string? value, int max)
        {
            if (string.IsNullOrWhiteSpace(value))
                Add(key, "This field is required.");
            else
                MaxLen(key, value, max);
        }

        void HttpUrl(string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            MaxLen(key, value, 500);
            if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                Add(key, "Must be a valid http(s) URL.");
        }

        void DateRange(string prefix, DateOnly? start, DateOnly? end)
        {
            if (start is { } s && s < MinDate) Add($"{prefix}.startDate", "Date is not plausible.");
            if (end is { } e && e < MinDate) Add($"{prefix}.endDate", "Date is not plausible.");
            if (start is { } s2 && end is { } e2 && e2 < s2) Add($"{prefix}.endDate", "End date must not be before the start date.");
        }

        void Values(string key, List<string>? values, int maxLength, HashSet<string>? allowed = null)
        {
            if (values is null) return;
            if (values.Count > MaxListValues) Add(key, $"At most {MaxListValues} values are allowed.");
            foreach (var v in values)
            {
                if (v is null) continue;
                if (v.Length > maxLength) Add(key, $"Values must be at most {maxLength} characters.");
                else if (allowed is not null && !allowed.Contains(v.Trim()))
                    Add(key, $"'{v}' is not one of: {string.Join(", ", allowed.Order())}.");
            }
        }

        MaxLen("fullName", dto.FullName, 200);
        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            var email = dto.Email.Trim();
            if (email.Length > 254 || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
                Add("email", "Must be a valid email address.");
        }
        MaxLen("phone", dto.Phone, 50);
        MaxLen("headline", dto.Headline, 200);
        MaxLen("summary", dto.Summary, 4000);
        MaxLen("country", dto.Country, 100);
        MaxLen("city", dto.City, 100);
        HttpUrl("githubUrl", dto.GithubUrl);
        HttpUrl("linkedinUrl", dto.LinkedinUrl);
        HttpUrl("websiteUrl", dto.WebsiteUrl);

        var experiences = dto.WorkExperiences ?? [];
        if (experiences.Count > MaxItems) Add("workExperiences", $"At most {MaxItems} entries are allowed.");
        for (var i = 0; i < experiences.Count; i++)
        {
            var x = experiences[i];
            var p = $"workExperiences[{i}]";
            Required($"{p}.company", x.Company, 200);
            Required($"{p}.title", x.Title, 200);
            MaxLen($"{p}.location", x.Location, 200);
            MaxLen($"{p}.description", x.Description, 4000);
            DateRange(p, x.StartDate, x.EndDate);
            Values($"{p}.achievements", x.Achievements, 500);
        }

        var education = dto.Education ?? [];
        if (education.Count > MaxItems) Add("education", $"At most {MaxItems} entries are allowed.");
        for (var i = 0; i < education.Count; i++)
        {
            var x = education[i];
            var p = $"education[{i}]";
            Required($"{p}.institution", x.Institution, 200);
            MaxLen($"{p}.degree", x.Degree, 200);
            MaxLen($"{p}.field", x.Field, 200);
            DateRange(p, x.StartDate, x.EndDate);
        }

        var skills = dto.Skills ?? [];
        if (skills.Count > MaxItems * 2) Add("skills", $"At most {MaxItems * 2} entries are allowed.");
        var seenSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < skills.Count; i++)
        {
            var x = skills[i];
            var p = $"skills[{i}]";
            Required($"{p}.name", x.Name, 100);
            MaxLen($"{p}.category", x.Category, 100);
            if (!string.IsNullOrWhiteSpace(x.Level) && !SkillLevelNames.Contains(x.Level.Trim()))
                Add($"{p}.level", $"Must be one of: {string.Join(", ", SkillLevelNames.Order())}.");
            if (!string.IsNullOrWhiteSpace(x.Name) && !seenSkills.Add(x.Name.Trim()))
                Add($"{p}.name", "Duplicate skill.");
        }

        var projects = dto.Projects ?? [];
        if (projects.Count > MaxItems) Add("projects", $"At most {MaxItems} entries are allowed.");
        for (var i = 0; i < projects.Count; i++)
        {
            var x = projects[i];
            var p = $"projects[{i}]";
            Required($"{p}.name", x.Name, 200);
            MaxLen($"{p}.description", x.Description, 4000);
            HttpUrl($"{p}.url", x.Url);
            Values($"{p}.technologies", x.Technologies, 100);
            Values($"{p}.results", x.Results, 500);
        }

        var languages = dto.Languages ?? [];
        if (languages.Count > 30) Add("languages", "At most 30 entries are allowed.");
        for (var i = 0; i < languages.Count; i++)
        {
            var x = languages[i];
            var p = $"languages[{i}]";
            Required($"{p}.name", x.Name, 100);
            MaxLen($"{p}.level", x.Level, 50);
        }

        var pref = dto.Preferences;
        if (pref is null)
        {
            Add("preferences", "Preferences are required.");
        }
        else
        {
            Values("preferences.targetCountries", pref.TargetCountries, 100);
            Values("preferences.targetCities", pref.TargetCities, 100);
            Values("preferences.workModes", pref.WorkModes, 20, WorkModeNames);
            Values("preferences.employmentTypes", pref.EmploymentTypes, 20, EmploymentTypeNames);
            Values("preferences.targetTitles", pref.TargetTitles, 150);
            Values("preferences.workingLanguages", pref.WorkingLanguages, 50);
            MaxLen("preferences.experienceLevel", pref.ExperienceLevel, 100);

            if (pref.MinSalary is < 0) Add("preferences.minSalary", "Must not be negative.");
            if (pref.PreferredSalary is < 0) Add("preferences.preferredSalary", "Must not be negative.");
            if (pref.MinSalary is { } min && pref.PreferredSalary is { } preferred && preferred < min)
                Add("preferences.preferredSalary", "Must not be lower than the minimum salary.");
            if (string.IsNullOrWhiteSpace(pref.SalaryCurrency) || !CurrencyRegex().IsMatch(pref.SalaryCurrency.Trim()))
                Add("preferences.salaryCurrency", "Must be a 3-letter currency code such as EUR.");
        }

        return errors.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
    }
}
