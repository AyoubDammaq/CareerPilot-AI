using CareerPilot.Domain.Profiles;

namespace CareerPilot.Application.Profiles;

public sealed record CompletenessResult(int Percent, IReadOnlyList<string> Missing);

/// <summary>Deterministic profile-completeness score (weights sum to 100).</summary>
public static class ProfileCompleteness
{
    private sealed record Check(string Label, int Weight, Func<UserProfile, bool> Passed);

    private static bool Has(string? s) => !string.IsNullOrWhiteSpace(s);

    private static readonly Check[] Checks =
    [
        new("Full name and email", 10, p => Has(p.FullName) && Has(p.Email)),
        new("Country and city", 5, p => Has(p.Country) && Has(p.City)),
        new("Professional headline", 5, p => Has(p.Headline)),
        new("Professional summary", 15, p => Has(p.Summary)),
        new("Work experience with a description", 15, p => p.WorkExperiences.Any(x => Has(x.Description))),
        new("Education", 10, p => p.Education.Count > 0),
        new("At least 5 skills", 10, p => p.Skills.Count >= 5),
        new("At least one project", 10, p => p.Projects.Count > 0),
        new("Languages", 5, p => p.Languages.Count > 0),
        new("Target countries and work modes", 5, p => p.Preferences.TargetCountries.Count > 0 && p.Preferences.WorkModes.Count > 0),
        new("GitHub or LinkedIn link", 10, p => Has(p.GithubUrl) || Has(p.LinkedinUrl)),
    ];

    public static CompletenessResult Compute(UserProfile profile)
    {
        var score = 0;
        var missing = new List<string>();
        foreach (var check in Checks)
        {
            if (check.Passed(profile)) score += check.Weight;
            else missing.Add(check.Label);
        }
        return new CompletenessResult(score, missing);
    }
}
