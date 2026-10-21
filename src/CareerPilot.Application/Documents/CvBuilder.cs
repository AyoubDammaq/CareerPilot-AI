using CareerPilot.Application.Matching;
using CareerPilot.Domain.Jobs;
using CareerPilot.Domain.Profiles;

namespace CareerPilot.Application.Documents;

public sealed record CvExperience(string Title, string Company, string Location, string Period, string Description, List<string> Achievements);
public sealed record CvEducation(string Degree, string Field, string Institution, string Period);
public sealed record CvProject(string Name, string Description, string? Url, List<string> Technologies, List<string> Results);
public sealed record CvLanguage(string Name, string Level);

/// <summary>Everything that will appear on the CV. Built only from facts the owner entered and verified.</summary>
public sealed record CvContent(
    string FullName, string Headline, string Email, string? Phone, string Location, List<string> Links, string Summary,
    List<CvExperience> Experience, List<CvEducation> Education, List<string> Skills, List<CvProject> Projects, List<CvLanguage> Languages);

/// <summary>What was left out and why, plus what the vacancy asks for that the CV cannot honestly claim.</summary>
public sealed record CvReport(List<string> Omitted, List<string> NotOnCv, List<string> Warnings);

public sealed record CvBuildResult(CvContent Content, CvReport Report);

/// <summary>
/// Deterministic CV tailoring. It only selects and orders what is already in the profile; it never writes,
/// rephrases or adds claims. Unverified work, skills and projects are excluded. Education and languages have
/// no verification flag and are included as entered.
/// </summary>
public static class CvBuilder
{
    public const int MaxProjects = 3;

    public static CvBuildResult Build(UserProfile profile, JobListing job)
    {
        var omitted = new List<string>();
        var warnings = new List<string>();
        var wanted = job.Skills.Select(JobScorer.Canonical).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Experience: verified only, most recent first.
        var experience = new List<CvExperience>();
        foreach (var w in profile.WorkExperiences.OrderByDescending(w => w.EndDate is null).ThenByDescending(w => w.EndDate ?? w.StartDate).ThenBy(w => w.Position))
        {
            if (!w.IsVerified)
            {
                omitted.Add($"Experience \"{w.Title} at {w.Company}\" is not marked verified.");
                continue;
            }
            experience.Add(new CvExperience(
                w.Title, w.Company, w.Location, Period(w.StartDate, w.EndDate), w.Description,
                [.. w.Achievements.Where(a => !string.IsNullOrWhiteSpace(a))]));
        }

        // Skills: verified only; those the vacancy mentions first, otherwise the owner's order.
        var verifiedSkills = profile.Skills.Where(s => s.IsVerified && !string.IsNullOrWhiteSpace(s.Name)).OrderBy(s => s.Position).ToList();
        foreach (var s in profile.Skills.Where(s => !s.IsVerified && !string.IsNullOrWhiteSpace(s.Name)))
            omitted.Add($"Skill \"{s.Name}\" is not marked verified.");
        var skills = verifiedSkills
            .Select((s, i) => (s.Name.Trim(), Rank: wanted.Contains(JobScorer.Canonical(s.Name)) ? 0 : 1, Index: i))
            .OrderBy(x => x.Rank).ThenBy(x => x.Index).Select(x => x.Item1).ToList();

        // Projects: verified only, the most relevant few.
        var verifiedProjects = profile.Projects.Where(p => p.IsVerified).ToList();
        foreach (var p in profile.Projects.Where(p => !p.IsVerified))
            omitted.Add($"Project \"{p.Name}\" is not marked verified.");
        var projects = verifiedProjects
            .OrderByDescending(p => p.Technologies.Count(t => wanted.Contains(JobScorer.Canonical(t)))).ThenBy(p => p.Position)
            .ToList();
        foreach (var p in projects.Skip(MaxProjects))
            omitted.Add($"Project \"{p.Name}\" left out to keep the CV focused (only the {MaxProjects} most relevant are shown).");
        var shownProjects = projects.Take(MaxProjects)
            .Select(p => new CvProject(p.Name, p.Description, p.Url, [.. p.Technologies], [.. p.Results.Where(r => !string.IsNullOrWhiteSpace(r))])).ToList();

        var education = profile.Education.OrderBy(e => e.Position)
            .Select(e => new CvEducation(e.Degree, e.Field, e.Institution, Period(e.StartDate, e.EndDate))).ToList();
        var languages = profile.Languages.OrderBy(l => l.Position).Select(l => new CvLanguage(l.Name, l.Level)).ToList();

        var links = new[] { profile.LinkedinUrl, profile.GithubUrl, profile.WebsiteUrl }.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l!).ToList();
        var location = string.Join(", ", new[] { profile.City, profile.Country }.Where(x => !string.IsNullOrWhiteSpace(x)));

        var content = new CvContent(
            profile.FullName, profile.Headline, profile.Email, string.IsNullOrWhiteSpace(profile.Phone) ? null : profile.Phone,
            location, links, profile.Summary?.Trim() ?? "", experience, education, skills, shownProjects, languages);

        // What the vacancy mentions that the CV does not (honest gaps, never to be added automatically).
        var onCv = skills.Concat(shownProjects.SelectMany(p => p.Technologies)).Select(JobScorer.Canonical).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var notOnCv = job.Skills.Where(s => !onCv.Contains(JobScorer.Canonical(s))).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (string.IsNullOrWhiteSpace(profile.FullName)) warnings.Add("Your profile has no full name yet.");
        if (string.IsNullOrWhiteSpace(profile.Email)) warnings.Add("Your profile has no email address yet.");
        if (experience.Count == 0) warnings.Add("No work experience is marked verified, so the CV has no experience section. Verify the facts in your profile first.");
        if (skills.Count == 0) warnings.Add("No skills are marked verified, so the CV has no skills section.");

        return new CvBuildResult(content, new CvReport(omitted, notOnCv, warnings));
    }

    private static string Period(DateOnly? start, DateOnly? end)
    {
        string F(DateOnly d) => d.ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
        if (start is null && end is null) return "";
        if (start is null) return F(end!.Value);
        return end is null ? $"{F(start.Value)} – Present" : $"{F(start.Value)} – {F(end.Value)}";
    }
}
