using System.Text;
using System.Text.RegularExpressions;
using CareerPilot.Domain.Jobs;
using CareerPilot.Domain.Profiles;

namespace CareerPilot.Application.Jobs;

/// <summary>Derived facts for a job. Everything here is computed from the source text, nothing is invented.</summary>
public sealed record JobEnrichment(
    PlaceInfo Place,
    List<string> Skills,
    SeniorityLevel Seniority,
    int? ExperienceYearsHint,
    string? Language,
    bool MentionsSponsorship,
    bool MentionsRelocation);

public static partial class JobEnricher
{
    public static JobEnrichment Enrich(SourceJob job, DiscoveryOptions options, IEnumerable<string>? profileSkills)
    {
        var place = CountryCatalog.Resolve(job.LocationText, job.CountryHint);
        var text = $"{job.Title}\n{job.Description}";
        return new JobEnrichment(
            place,
            SkillVocabulary.Extract(text, profileSkills),
            DetectSeniority(job.Title, options),
            ExperienceYears(job.Description),
            job.Language ?? DetectLanguage(job.Description),
            SponsorshipRegex().IsMatch(job.Description),
            RelocationRegex().IsMatch(job.Description));
    }

    // ---- canonical key ---------------------------------------------------------------------------

    [GeneratedRegex(@"\((?:[mfwxdh]\s*/\s*)+[mfwxdh]\)", RegexOptions.IgnoreCase)]
    private static partial Regex GenderTag();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonAlnum();

    private const string From = "àâäáãåçéèêëíìîïñóòôöõúùûüýÿœæß";
    private const string To = "aaaaaaceeeeiiiinooooouuuuyyoas";

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var lower = GenderTag().Replace(value, " ").ToLowerInvariant();
        var sb = new StringBuilder(lower.Length);
        foreach (var ch in lower)
        {
            var i = From.IndexOf(ch);
            sb.Append(i >= 0 ? To[i] : ch);
        }
        return NonAlnum().Replace(sb.ToString(), " ").Trim();
    }

    /// <summary>company|title|place. Postings from different sources with the same key are one vacancy.</summary>
    public static string CanonicalKey(string company, string title, PlaceInfo place)
    {
        var where = place.CountryCodes.Count > 0 ? place.CountryCodes[0].ToLowerInvariant() : Normalize(place.City);
        var city = place.City.Length > 0 ? Normalize(place.City) : "-";
        return $"{Normalize(company)}|{Normalize(title)}|{where}|{city}";
    }

    // ---- seniority -------------------------------------------------------------------------------

    public static SeniorityLevel DetectSeniority(string title, DiscoveryOptions options)
    {
        var t = Normalize(title);
        if (HasAny(t, options.LeadTitleKeywords)) return SeniorityLevel.Lead;
        if (HasAny(t, options.SeniorTitleKeywords)) return SeniorityLevel.Senior;
        if (HasAny(t, options.JuniorTitleKeywords)) return SeniorityLevel.Junior;
        return SeniorityLevel.Unspecified;
    }

    /// <summary>Whole-word (or whole-phrase) match of any keyword in an already normalized title.</summary>
    public static bool HasAny(string normalizedTitle, IEnumerable<string> keywords)
    {
        var padded = $" {normalizedTitle} ";
        foreach (var k in keywords)
        {
            var nk = Normalize(k.Replace("#", "sharp").Replace(".net", "dotnet", StringComparison.OrdinalIgnoreCase));
            if (nk.Length > 0 && padded.Contains($" {nk} ", StringComparison.Ordinal)) return true;
        }
        return false;
    }

    /// <summary>Normalizes a raw title the same way keywords are normalized ("C#" -> "csharp", ".NET" -> "dotnet").</summary>
    public static string NormalizeTitle(string title) =>
        Normalize(title.Replace("C#", "csharp", StringComparison.OrdinalIgnoreCase)
                       .Replace(".NET", "dotnet", StringComparison.OrdinalIgnoreCase));

    // ---- experience hint -------------------------------------------------------------------------

    [GeneratedRegex(@"(?<![\d.])(\d{1,2})\s*\+?\s*(?:-\s*\d{1,2}\s*)?(?:years?|yrs?|ans)\b", RegexOptions.IgnoreCase)]
    private static partial Regex YearsRegex();

    /// <summary>Smallest "N years" figure that sits next to the word experience/expérience. A hint, not a fact.</summary>
    public static int? ExperienceYears(string description)
    {
        int? best = null;
        foreach (Match m in YearsRegex().Matches(description))
        {
            var start = Math.Max(0, m.Index - 60);
            var end = Math.Min(description.Length, m.Index + m.Length + 60);
            var context = description[start..end];
            if (!context.Contains("experience", StringComparison.OrdinalIgnoreCase)
                && !context.Contains("expérience", StringComparison.OrdinalIgnoreCase))
                continue;
            if (int.TryParse(m.Groups[1].Value, out var years) && years is > 0 and < 40)
                best = best is null ? years : Math.Min(best.Value, years);
        }
        return best;
    }

    // ---- language --------------------------------------------------------------------------------

    private static readonly HashSet<string> French =
        ["le", "la", "les", "des", "et", "pour", "vous", "nous", "avec", "une", "dans", "est", "sur", "votre", "notre", "au", "du"];

    private static readonly HashSet<string> English =
        ["the", "and", "for", "you", "with", "our", "are", "will", "your", "to", "of", "in", "is", "we"];

    public static string? DetectLanguage(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        int fr = 0, en = 0;
        foreach (var raw in text.Split([' ', '\n', '\t', ',', '.', ';', ':', '(', ')'], StringSplitOptions.RemoveEmptyEntries).Take(500))
        {
            var w = raw.ToLowerInvariant();
            if (French.Contains(w)) fr++;
            if (English.Contains(w)) en++;
        }
        if (fr == 0 && en == 0) return null;
        return fr > en ? "fr" : "en";
    }

    // ---- sponsorship / relocation mentions ---------------------------------------------------------

    [GeneratedRegex(@"visa\s+sponsorship|sponsor(?:ship)?\s+(?:a\s+)?visa|work\s+permit\s+(?:support|sponsorship)|parrainage\s+visa", RegexOptions.IgnoreCase)]
    private static partial Regex SponsorshipRegex();

    [GeneratedRegex(@"relocation\s+(?:support|assistance|package|bonus|allowance|help)|relocate\s+you|aide\s+(?:à|a)\s+la\s+mobilit", RegexOptions.IgnoreCase)]
    private static partial Regex RelocationRegex();
}
