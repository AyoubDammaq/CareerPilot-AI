using System.Text.RegularExpressions;
using CareerPilot.Application.Jobs;
using CareerPilot.Application.Matching;
using CareerPilot.Domain.Profiles;

namespace CareerPilot.Application.Documents;

public sealed record ClaimFlag(string Kind, string Text, string Message);

/// <summary>Result of checking a letter against the profile. A clean result means "nothing the checker could detect", not "verified true".</summary>
public sealed record ClaimCheck(List<ClaimFlag> Flags)
{
    public bool Clean => Flags.Count == 0;
}

/// <summary>
/// Deterministic safety net for written text. It flags technologies the owner has not verified, figures that appear
/// nowhere in the verified profile, and claims of more experience than the verified history shows. It cannot judge
/// whether free-form statements are true, so the owner still has to read the letter.
/// </summary>
public static class ClaimChecker
{
    private static readonly Regex Figure = new(
        @"(?<![\p{L}\d.,])(\d+(?:[.,]\d+)?)\s*(%|x\b|\+?\s*(?:years?|yrs?|ans)\b|k\b|m\b)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static ClaimCheck Check(string text, UserProfile profile, DateTimeOffset now)
    {
        var flags = new List<ClaimFlag>();
        if (string.IsNullOrWhiteSpace(text)) return new ClaimCheck(flags);

        // ---- technologies --------------------------------------------------------------
        var verified = profile.Skills.Where(s => s.IsVerified).Select(s => s.Name)
            .Concat(profile.Projects.Where(p => p.IsVerified).SelectMany(p => p.Technologies))
            .Select(JobScorer.Canonical).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unverified = profile.Skills.Where(s => !s.IsVerified).Select(s => s.Name)
            .Select(JobScorer.Canonical).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var extra = profile.Skills.Select(s => s.Name).Concat(profile.Projects.SelectMany(p => p.Technologies));
        foreach (var term in SkillVocabulary.Extract(text, extra))
        {
            var c = JobScorer.Canonical(term);
            if (verified.Contains(c)) continue;
            flags.Add(unverified.Contains(c)
                ? new ClaimFlag("UnverifiedSkill", term, $"\"{term}\" is on your profile but not marked verified. Verify it or remove it from the letter.")
                : new ClaimFlag("Skill", term, $"\"{term}\" is not a skill on your profile. Remove it unless it is true and you add it to your profile."));
        }

        // ---- figures and experience claims ---------------------------------------------
        var evidence = Evidence(profile);
        var verifiedYears = VerifiedYears(profile, now);
        var seen = new HashSet<string>();
        foreach (Match m in Figure.Matches(text))
        {
            var number = m.Groups[1].Value;
            var unit = m.Groups[2].Value.Trim();
            var shown = m.Value.Trim();
            if (!seen.Add(shown)) continue;
            if (double.TryParse(number.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
                && unit.Length == 0 && value is >= 1900 and <= 2100) continue; // a calendar year

            var isYears = Regex.IsMatch(unit, "year|yr|ans", RegexOptions.IgnoreCase);
            if (isYears)
            {
                if (double.TryParse(number.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y) && y > verifiedYears + 0.5)
                    flags.Add(new ClaimFlag("Experience", shown,
                        $"\"{shown}\" is more than the {verifiedYears:0.#} years your verified work history shows."));
                continue;
            }
            if (unit.Length == 0 && number.Length < 2) continue; // single digits ("3 paragraphs", "one of 2")
            if (!evidence.Contains(number))
                flags.Add(new ClaimFlag("Figure", shown, $"The figure \"{shown}\" does not appear in your verified profile. Remove it or add the verified fact first."));
        }
        return new ClaimCheck(flags);
    }

    private static HashSet<string> Evidence(UserProfile p)
    {
        var parts = new List<string> { p.Summary ?? "", p.Headline ?? "" };
        foreach (var w in p.WorkExperiences.Where(w => w.IsVerified))
            parts.AddRange([w.Title, w.Description, .. w.Achievements]);
        foreach (var pr in p.Projects.Where(x => x.IsVerified))
            parts.AddRange([pr.Name, pr.Description, .. pr.Results]);
        var set = new HashSet<string>();
        foreach (Match m in Regex.Matches(string.Join(' ', parts), @"\d+(?:[.,]\d+)?"))
            set.Add(m.Value);
        return set;
    }

    private static double VerifiedYears(UserProfile p, DateTimeOffset now)
    {
        var copy = new UserProfile();
        copy.WorkExperiences.AddRange(p.WorkExperiences.Where(w => w.IsVerified));
        return JobScorer.YearsOfExperience(copy, DateOnly.FromDateTime(now.UtcDateTime));
    }
}
