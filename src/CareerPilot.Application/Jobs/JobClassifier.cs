using CareerPilot.Domain.Jobs;
using CareerPilot.Domain.Profiles;

namespace CareerPilot.Application.Jobs;

/// <summary>
/// Deterministic first-pass filter against the owner's current preferences. It never uses an LLM and
/// never deletes anything: a filtered job keeps its reasons and can still be shown on request.
/// Uncertain eligibility yields GeoFit.NeedsReview, not a rejection.
/// </summary>
public static class JobClassifier
{
    public static void Classify(JobListing job, JobPreference prefs, DiscoveryOptions options, string? homeCountry)
    {
        var reasons = new List<string>();

        var title = JobEnricher.NormalizeTitle(job.Title);
        if (JobEnricher.HasAny(title, options.ExcludeTitleKeywords))
            reasons.Add("Title looks like a non-engineering role.");
        else if (!JobEnricher.HasAny(title, options.IncludeTitleKeywords))
            reasons.Add("Title does not look like a software role.");

        if (!prefs.IncludeSeniorRoles && job.Seniority is SeniorityLevel.Senior or SeniorityLevel.Lead)
            reasons.Add("Senior or lead role (turn on 'include senior roles' to see these).");

        var (fit, note) = EvaluateGeo(job, prefs, CountryCatalog.CodeFor(homeCountry));
        if (fit == GeoFit.NoMatch && note is not null)
            reasons.Add(note);

        if (job.WorkMode is { } mode && prefs.WorkModes.Count > 0
            && !prefs.WorkModes.Contains(mode.ToString(), StringComparer.OrdinalIgnoreCase))
            reasons.Add($"Work arrangement ({mode}) is not among your preferences.");

        job.GeoFit = fit;
        job.GeoNote = note;
        job.FilterReasons = reasons;
        job.IsRelevant = reasons.Count == 0;
    }

    public static (GeoFit Fit, string? Note) EvaluateGeo(JobListing job, JobPreference prefs, string? homeCode)
    {
        var targets = prefs.TargetCountries
            .Select(CountryCatalog.CodeFor).OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (targets.Count == 0)
            return (GeoFit.NeedsReview, "No target countries are configured, so location cannot be checked.");

        var codes = job.CountryCodes;
        var names = string.Join(", ", codes.Select(c => CountryCatalog.NameFor(c) ?? c));
        var remote = job.WorkMode == WorkMode.Remote
                     || job.LocationText.Contains("remote", StringComparison.OrdinalIgnoreCase);
        var sponsorshipNote = SponsorshipNote(job, prefs, homeCode, codes);

        if (codes.Any(targets.Contains))
        {
            var note = remote
                ? $"Remote role listed for {names}: confirm you may work from your location."
                : null;
            return (GeoFit.Match, Join(note, sponsorshipNote));
        }

        if (remote && prefs.IncludeInternationalRemote)
        {
            var note = codes.Count > 0
                ? $"Remote role listed for {names}: eligibility from your location is not verified."
                : "Remote role with no stated country: eligibility from your location is not verified.";
            return (GeoFit.NeedsReview, Join(note, sponsorshipNote));
        }

        if (codes.Count == 0)
            return (GeoFit.NeedsReview, Join($"Location '{job.LocationText}' could not be resolved to a country.", sponsorshipNote));

        return (GeoFit.NoMatch, $"Located in {names}, outside your target countries.");
    }

    private static string? SponsorshipNote(JobListing job, JobPreference prefs, string? homeCode, List<string> codes)
    {
        if (!prefs.RequiresVisaSponsorship) return null;
        var abroad = codes.Count > 0 && (homeCode is null || !codes.Contains(homeCode, StringComparer.OrdinalIgnoreCase));
        if (!abroad) return null;
        return job.MentionsSponsorship
            ? "The posting mentions visa sponsorship."
            : "Visa sponsorship is not mentioned in the posting.";
    }

    private static string? Join(string? a, string? b) =>
        a is null ? b : b is null ? a : $"{a} {b}";
}
