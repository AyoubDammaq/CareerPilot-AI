using CareerPilot.Application.Jobs;
using CareerPilot.Domain.Jobs;
using CareerPilot.Domain.Profiles;

namespace CareerPilot.Application.Matching;

/// <summary>
/// Deterministic 100-point match. No LLM is involved: the same listing and profile always give the same
/// result, and every point can be explained. Missing information earns a neutral value and lowers the
/// reported confidence; it never counts as evidence for or against the owner. The profile is only read.
/// </summary>
public static class JobScorer
{
    public static MatchResult Score(JobListing job, UserProfile profile, ScoringOptions o, DateTimeOffset now)
    {
        var weights = o.ScaledWeights().ToDictionary(w => w.Key, w => w.Weight);
        var prefs = profile.Preferences;
        var warnings = new List<string>();
        var parts = new List<MatchComponent>();

        void Add(string key, string label, double fraction, bool known, string note)
        {
            var max = weights[key];
            if (max <= 0) return;
            parts.Add(new MatchComponent(key, label, Round(max), Round(max * Math.Clamp(fraction, 0, 1)), known, note));
        }

        var (matched, missing, skillsFraction, skillsKnown, skillsNote) = Skills(job, profile, o);
        Add("skills", "Skills", skillsFraction, skillsKnown, skillsNote);
        var (tf, tk, tn) = Title(job, prefs);
        Add("title", "Role", tf, tk, tn);
        var (lf, lk, ln) = Location(job);
        Add("location", "Location", lf, lk, ln);
        var (sf, sk, sn) = Seniority(job, profile, prefs, now);
        Add("seniority", "Level", sf, sk, sn);
        var (wf, wk, wn) = WorkMode(job, prefs);
        Add("workMode", "Work arrangement", wf, wk, wn);
        var (gf, gk, gn) = Language(job, profile, prefs);
        Add("language", "Language", gf, gk, gn);
        var (yf, yk, yn) = Salary(job, prefs);
        Add("salary", "Salary", yf, yk, yn);
        var (ff, fk, fn) = Freshness(job, now);
        Add("freshness", "Freshness", ff, fk, fn);

        var total = parts.Sum(p => p.Points);
        var max100 = parts.Sum(p => p.MaxPoints);
        var score = max100 <= 0 ? 0 : (int)Math.Round(total * 100 / max100, MidpointRounding.AwayFromZero);
        var known = max100 <= 0 ? 0 : parts.Where(p => p.Known).Sum(p => p.MaxPoints) * 100 / max100;

        if (job.Status is JobStatus.Closed or JobStatus.Expired)
        {
            warnings.Add($"This posting is {job.Status.ToString().ToLowerInvariant()}; it cannot be applied to.");
            score = 0;
        }
        else if (job.Status == JobStatus.Unverified)
            warnings.Add("This posting has not been re-confirmed recently; check it is still open.");

        if (job.GeoFit == GeoFit.NoMatch) warnings.Add("Outside your target countries.");
        if (job.GeoFit == GeoFit.NeedsReview) warnings.Add("Location eligibility needs your review.");
        if (prefs.RequiresVisaSponsorship && job.GeoFit != GeoFit.NoMatch && job.CountryCodes.Count > 0 && !job.MentionsSponsorship)
            warnings.Add("Visa sponsorship is not mentioned; ask before investing time.");
        if (known < 50) warnings.Add("Score is based on limited information in the posting; read it before trusting the number.");

        var tier = score >= o.StrongAt ? "Strong" : score >= o.GoodAt ? "Good" : score >= o.PossibleAt ? "Possible" : "Weak";
        return new MatchResult(score, tier, (int)Math.Round(known, MidpointRounding.AwayFromZero), parts, matched, missing, warnings);
    }

    private static double Round(double v) => Math.Round(v, 1, MidpointRounding.AwayFromZero);

    // ---- skills -------------------------------------------------------------------------------

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["asp.net"] = ".net", ["asp.net core"] = ".net", ["dotnet"] = ".net", [".net core"] = ".net",
        ["postgres"] = "postgresql", ["js"] = "javascript", ["ts"] = "typescript", ["k8s"] = "kubernetes",
        ["golang"] = "go", ["node"] = "node.js", ["nodejs"] = "node.js", ["gh actions"] = "github actions",
        ["entity framework core"] = "entity framework", ["ef core"] = "entity framework",
        ["rest api"] = "rest", ["restful"] = "rest", ["react.js"] = "react", ["vue.js"] = "vue",
    };

    public static string Canonical(string skill)
    {
        var s = (skill ?? "").Trim();
        return Aliases.TryGetValue(s, out var a) ? a : s.ToLowerInvariant();
    }

    private static (List<string> Matched, List<string> Missing, double Fraction, bool Known, string Note)
        Skills(JobListing job, UserProfile profile, ScoringOptions o)
    {
        var ownSkills = profile.Skills.Select(s => s.Name)
            .Concat(profile.Projects.SelectMany(p => p.Technologies))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(Canonical).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var jobSkills = job.Skills.Where(s => !string.IsNullOrWhiteSpace(s))
            .GroupBy(Canonical, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();

        if (jobSkills.Count == 0)
            return ([], [], 0.5, false, "No known technologies were found in the posting, so skills could not be compared.");
        if (ownSkills.Count == 0)
            return ([], jobSkills, 0.5, false, "Your profile lists no skills yet, so skills could not be compared.");

        var matched = jobSkills.Where(s => ownSkills.Contains(Canonical(s))).ToList();
        var missing = jobSkills.Except(matched).ToList();
        var denominator = Math.Max(1, Math.Min(jobSkills.Count, Math.Max(1, o.SkillCoverageCap)));
        var evidence = Math.Max(1, o.SkillMinEvidence);
        // Few technologies named: pad with neutral half-matches up to the minimum, so a short list cannot score full marks.
        var counted = Math.Min(matched.Count, denominator);
        var fraction = Math.Min(1.0, (counted + 0.5 * Math.Max(0, evidence - denominator)) / Math.Max(denominator, evidence));
        var note = $"{matched.Count} of {jobSkills.Count} technologies in the posting are on your profile.";
        if (denominator < evidence)
            note += $" Only {jobSkills.Count} {(jobSkills.Count == 1 ? "technology was" : "technologies were")} found in the posting, so this counts for less.";
        return (matched, missing, fraction, denominator >= Math.Min(3, evidence), note);
    }

    // ---- role ---------------------------------------------------------------------------------

    private static readonly char[] Splitters = [' ', '-', '/', ',', '(', ')', '&', '|', '–'];

    private static HashSet<string> Tokens(string text) =>
        text.ToLowerInvariant().Split(Splitters, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1 && t is not ("the" or "and" or "of" or "for" or "sr" or "jr" or "senior" or "junior" or "lead" or "staff"))
            .ToHashSet();

    private static (double, bool, string) Title(JobListing job, JobPreference prefs)
    {
        if (prefs.TargetTitles.Count == 0)
            return (0.5, false, "You have not set target job titles.");
        var jt = Tokens(job.Title);
        if (jt.Count == 0) return (0.5, false, "The posting has no usable title.");

        double best = 0;
        string? bestTitle = null;
        foreach (var target in prefs.TargetTitles)
        {
            var tt = Tokens(target);
            if (tt.Count == 0) continue;
            var overlap = (double)tt.Count(jt.Contains) / tt.Count;
            if (overlap > best) { best = overlap; bestTitle = target; }
        }
        var note = best >= 0.99 ? $"Title matches your target '{bestTitle}'."
            : best > 0 ? $"Title partly matches your target '{bestTitle}'."
            : "Title does not match any of your target titles.";
        return (best, true, note);
    }

    // ---- location -----------------------------------------------------------------------------

    private static (double, bool, string) Location(JobListing job) => job.GeoFit switch
    {
        GeoFit.Match => (1.0, true, "In one of your target countries."),
        GeoFit.NeedsReview => (0.4, false, job.GeoNote ?? "Location eligibility is unclear."),
        _ => (0.0, true, job.GeoNote ?? "Outside your target countries."),
    };

    // ---- level --------------------------------------------------------------------------------

    public static double YearsOfExperience(UserProfile profile, DateOnly today)
    {
        var days = profile.WorkExperiences.Sum(w =>
        {
            var end = w.EndDate ?? today;
            return end < w.StartDate ? 0 : end.DayNumber - w.StartDate.DayNumber;
        });
        return days / 365.25;
    }

    private static (double, bool, string) Seniority(JobListing job, UserProfile profile, JobPreference prefs, DateTimeOffset now)
    {
        var years = YearsOfExperience(profile, DateOnly.FromDateTime(now.UtcDateTime));
        double fraction;
        bool known = true;
        string note;
        switch (job.Seniority)
        {
            case SeniorityLevel.Junior: fraction = 1.0; note = "Junior-level role."; break;
            case SeniorityLevel.Mid: fraction = 1.0; note = "Mid-level role."; break;
            case SeniorityLevel.Senior:
                fraction = prefs.IncludeSeniorRoles ? 0.6 : 0.15;
                note = prefs.IncludeSeniorRoles ? "Senior role; you opted in to senior roles." : "Senior role, above the level you are targeting.";
                break;
            case SeniorityLevel.Lead:
                fraction = prefs.IncludeSeniorRoles ? 0.4 : 0.05;
                note = "Lead or management role.";
                break;
            default:
                fraction = 0.6; known = false; note = "The posting does not state a level."; break;
        }

        if (job.ExperienceYearsHint is { } hint)
        {
            if (hint > years + 1.5)
            {
                fraction *= 0.5;
                note += $" It asks for about {hint} years; your profile shows about {years:0.#}.";
            }
            else
                note += $" The ~{hint} years it asks for is within your experience.";
        }
        return (fraction, known, note);
    }

    // ---- work arrangement ---------------------------------------------------------------------

    private static (double, bool, string) WorkMode(JobListing job, JobPreference prefs)
    {
        if (prefs.WorkModes.Count == 0) return (1.0, false, "You have no work-arrangement preference.");
        if (job.WorkMode is not { } mode) return (0.5, false, "The posting does not state remote, hybrid or on-site.");
        return prefs.WorkModes.Contains(mode.ToString(), StringComparer.OrdinalIgnoreCase)
            ? (1.0, true, $"{mode} is among your preferences.")
            : (0.0, true, $"{mode} is not among your preferences.");
    }

    // ---- language -----------------------------------------------------------------------------

    private static readonly Dictionary<string, string> LanguageCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "english", ["fr"] = "french", ["de"] = "german", ["es"] = "spanish", ["ar"] = "arabic",
        ["it"] = "italian", ["nl"] = "dutch", ["pt"] = "portuguese",
    };

    private static (double, bool, string) Language(JobListing job, UserProfile profile, JobPreference prefs)
    {
        if (string.IsNullOrWhiteSpace(job.Language)) return (0.6, false, "The posting's language is unknown.");
        var code = job.Language.Trim().ToLowerInvariant();
        var name = LanguageCodes.TryGetValue(code, out var n) ? n : code;
        var mine = profile.Languages.Select(l => l.Name).Concat(prefs.WorkingLanguages)
            .Select(l => l.Trim().ToLowerInvariant()).ToHashSet();
        return mine.Contains(name) || mine.Contains(code)
            ? (1.0, true, $"Posting is in {name}, which you work in.")
            : (0.0, true, $"Posting is in {name}, which is not on your profile.");
    }

    // ---- salary -------------------------------------------------------------------------------

    private static (double, bool, string) Salary(JobListing job, JobPreference prefs)
    {
        if (prefs.MinSalary is not { } min) return (0.5, false, "You have not set a minimum salary.");
        var stated = job.SalaryMax ?? job.SalaryMin;
        if (stated is null) return (0.5, false, "The posting states no salary.");
        if (!string.Equals(job.SalaryCurrency, prefs.SalaryCurrency, StringComparison.OrdinalIgnoreCase))
            return (0.5, false, $"Salary is stated in {job.SalaryCurrency ?? "an unknown currency"}; no conversion is attempted.");
        var period = job.SalaryPeriod?.ToLowerInvariant();
        if (period is not null && !period.Contains("year") && !period.Contains("annual"))
            return (0.5, false, "Salary is not stated per year; not compared.");
        return stated >= min
            ? (1.0, true, "Stated salary reaches your minimum.")
            : (0.0, true, "Stated salary is below your minimum.");
    }

    // ---- freshness ----------------------------------------------------------------------------

    private static (double, bool, string) Freshness(JobListing job, DateTimeOffset now)
    {
        var status = job.Status == JobStatus.Open ? 1.0 : 0.3;
        if (job.PublishedAt is not { } published)
            return ((status + 0.6) / 2, false, "Publication date is unknown.");
        var days = (now - published).TotalDays;
        var age = days <= 14 ? 1.0 : days <= 45 ? 0.7 : 0.4;
        return ((status + age) / 2, true, $"Published about {Math.Max(0, (int)days)} days ago.");
    }
}
