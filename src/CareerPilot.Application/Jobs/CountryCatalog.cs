using System.Text.RegularExpressions;

namespace CareerPilot.Application.Jobs;

public sealed record PlaceInfo(List<string> CountryCodes, string Country, string City, bool IsRemote, string? RegionHint);

/// <summary>
/// Small reference table used to resolve free-text locations. Countries are limited to those relevant
/// to the search regions plus the most common others; an unknown place resolves to no country and is
/// flagged for review rather than guessed.
/// </summary>
public static class CountryCatalog
{
    private sealed record Entry(string Code, string Name, string[] Aliases);

    private static readonly Entry[] Entries =
    [
        new("TN", "Tunisia", ["Tunisie"]),
        new("FR", "France", []),
        new("BE", "Belgium", ["Belgique", "Belgie", "België"]),
        new("LU", "Luxembourg", []),
        new("CH", "Switzerland", ["Suisse", "Schweiz"]),
        new("DE", "Germany", ["Deutschland", "Allemagne"]),
        new("NL", "Netherlands", ["The Netherlands", "Holland", "Pays-Bas"]),
        new("ES", "Spain", ["España", "Espagne"]),
        new("PT", "Portugal", []),
        new("IT", "Italy", ["Italia", "Italie"]),
        new("IE", "Ireland", ["Irlande"]),
        new("GB", "United Kingdom", ["UK", "England", "Scotland", "Wales", "Great Britain", "Royaume-Uni"]),
        new("AT", "Austria", ["Österreich", "Autriche"]),
        new("PL", "Poland", ["Polska", "Pologne"]),
        new("SE", "Sweden", ["Sverige", "Suède"]),
        new("NO", "Norway", ["Norge", "Norvège"]),
        new("DK", "Denmark", ["Danmark", "Danemark"]),
        new("FI", "Finland", ["Suomi", "Finlande"]),
        new("CZ", "Czechia", ["Czech Republic"]),
        new("RO", "Romania", []),
        new("HU", "Hungary", []),
        new("GR", "Greece", []),
        new("BG", "Bulgaria", []),
        new("HR", "Croatia", []),
        new("SK", "Slovakia", []),
        new("SI", "Slovenia", []),
        new("EE", "Estonia", []),
        new("LV", "Latvia", []),
        new("LT", "Lithuania", []),
        new("MT", "Malta", []),
        new("CY", "Cyprus", []),
        new("RS", "Serbia", []),
        new("UA", "Ukraine", []),
        new("SA", "Saudi Arabia", ["KSA", "Arabie saoudite"]),
        new("AE", "United Arab Emirates", ["UAE", "Emirats arabes unis"]),
        new("QA", "Qatar", []),
        new("KW", "Kuwait", []),
        new("BH", "Bahrain", []),
        new("OM", "Oman", []),
        new("MA", "Morocco", ["Maroc"]),
        new("DZ", "Algeria", ["Algérie"]),
        new("EG", "Egypt", ["Égypte"]),
        new("ZA", "South Africa", []),
        new("NG", "Nigeria", []),
        new("KE", "Kenya", []),
        new("TR", "Turkey", ["Türkiye", "Turkiye"]),
        new("IL", "Israel", []),
        new("US", "United States", ["USA", "US", "United States of America"]),
        new("CA", "Canada", []),
        new("MX", "Mexico", ["México"]),
        new("BR", "Brazil", ["Brasil"]),
        new("AR", "Argentina", []),
        new("CO", "Colombia", []),
        new("CL", "Chile", []),
        new("IN", "India", []),
        new("SG", "Singapore", []),
        new("AU", "Australia", []),
        new("NZ", "New Zealand", []),
        new("JP", "Japan", []),
        new("CN", "China", []),
        new("KR", "South Korea", ["Korea"]),
        new("VN", "Vietnam", []),
        new("ID", "Indonesia", []),
        new("PH", "Philippines", []),
        new("MY", "Malaysia", []),
        new("TH", "Thailand", []),
        new("PK", "Pakistan", []),
    ];

    private static readonly Dictionary<string, string> CityToCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Paris"] = "FR", ["Lyon"] = "FR", ["Marseille"] = "FR", ["Toulouse"] = "FR", ["Nantes"] = "FR",
        ["Bordeaux"] = "FR", ["Lille"] = "FR", ["Nice"] = "FR", ["Sophia Antipolis"] = "FR", ["Grenoble"] = "FR",
        ["Brussels"] = "BE", ["Bruxelles"] = "BE", ["Antwerp"] = "BE", ["Ghent"] = "BE", ["Leuven"] = "BE", ["Liège"] = "BE",
        ["Tunis"] = "TN", ["Sfax"] = "TN", ["Sousse"] = "TN", ["Ariana"] = "TN",
        ["Dubai"] = "AE", ["Abu Dhabi"] = "AE", ["Riyadh"] = "SA", ["Jeddah"] = "SA", ["Doha"] = "QA",
        ["Kuwait City"] = "KW", ["Manama"] = "BH", ["Muscat"] = "OM",
        ["London"] = "GB", ["Manchester"] = "GB", ["Edinburgh"] = "GB", ["Dublin"] = "IE",
        ["Berlin"] = "DE", ["Munich"] = "DE", ["Hamburg"] = "DE", ["Amsterdam"] = "NL",
        ["Barcelona"] = "ES", ["Madrid"] = "ES", ["Lisbon"] = "PT", ["Milan"] = "IT", ["Rome"] = "IT",
        ["Stockholm"] = "SE", ["Copenhagen"] = "DK", ["Oslo"] = "NO", ["Helsinki"] = "FI",
        ["Warsaw"] = "PL", ["Krakow"] = "PL", ["Prague"] = "CZ", ["Vienna"] = "AT", ["Zurich"] = "CH", ["Geneva"] = "CH",
        ["New York"] = "US", ["San Francisco"] = "US", ["Seattle"] = "US", ["Boston"] = "US", ["Austin"] = "US",
        ["Toronto"] = "CA", ["Vancouver"] = "CA", ["Mexico City"] = "MX", ["Sao Paulo"] = "BR", ["São Paulo"] = "BR",
        ["Casablanca"] = "MA", ["Cairo"] = "EG", ["Bangalore"] = "IN", ["Singapore"] = "SG", ["Sydney"] = "AU",
        ["Ho Chi Minh"] = "VN",
    };

    private static readonly string[] RegionKeywords =
        ["EMEA", "European Union", "Europe", "Worldwide", "Global", "Anywhere", "APAC", "LATAM", "North America", "Americas"];

    private static readonly Dictionary<string, Entry> ByCode =
        Entries.ToDictionary(e => e.Code, StringComparer.OrdinalIgnoreCase);

    private static readonly (Regex Pattern, Entry Entry)[] Matchers = BuildMatchers();

    private static (Regex, Entry)[] BuildMatchers()
    {
        var list = new List<(Regex, Entry)>();
        foreach (var e in Entries)
        {
            foreach (var name in new[] { e.Name }.Concat(e.Aliases))
            {
                // Short upper-case aliases (US, UK, UAE, KSA) must match case-sensitively to avoid "us" in prose.
                var caseSensitive = name.Length <= 3 && name.All(char.IsUpper);
                var options = RegexOptions.CultureInvariant | (caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
                list.Add((new Regex($@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(name)}(?![\p{{L}}\p{{N}}])", options), e));
            }
        }
        return [.. list];
    }

    /// <summary>ISO code for a country given as a code, name or alias ("France", "uae", "TN"); null if unknown.</summary>
    public static string? CodeFor(string? nameOrCode)
    {
        if (string.IsNullOrWhiteSpace(nameOrCode)) return null;
        var t = nameOrCode.Trim();
        if (t.Length == 2 && ByCode.TryGetValue(t, out var byCode)) return byCode.Code;
        foreach (var e in Entries)
        {
            if (e.Name.Equals(t, StringComparison.OrdinalIgnoreCase)
                || e.Aliases.Any(a => a.Equals(t, StringComparison.OrdinalIgnoreCase)))
                return e.Code;
        }
        return null;
    }

    public static string? NameFor(string code) => ByCode.TryGetValue(code, out var e) ? e.Name : null;

    public static PlaceInfo Resolve(string? locationText, string? countryHint)
    {
        var text = locationText ?? "";
        // Collect (position, code) pairs so the primary country is the first one mentioned, not the first in the table.
        var found = new List<(int Index, string Code)>();
        if (CodeFor(countryHint) is { } hinted) found.Add((-1, hinted));
        foreach (var (pattern, entry) in Matchers)
        {
            var m = pattern.Match(text);
            if (m.Success) found.Add((m.Index, entry.Code));
        }

        var tokens = Regex.Split(text, @"[,;|/()•]|\s[-–—]\s")
            .Select(t => t.Trim()).Where(t => t.Length > 0).ToList();

        var city = "";
        foreach (var token in tokens)
        {
            if (CityToCode.TryGetValue(token, out var cityCode))
            {
                found.Add((Math.Max(0, text.IndexOf(token, StringComparison.OrdinalIgnoreCase)), cityCode));
                if (city.Length == 0) city = token;
            }
        }
        if (city.Length == 0)
        {
            city = tokens.FirstOrDefault(t =>
                !t.Contains("remote", StringComparison.OrdinalIgnoreCase)
                && CodeFor(t) is null
                && !RegionKeywords.Contains(t, StringComparer.OrdinalIgnoreCase)
                && !t.Contains("hybrid", StringComparison.OrdinalIgnoreCase)) ?? "";
        }

        var codes = found.OrderBy(f => f.Index).Select(f => f.Code.ToUpperInvariant()).Distinct().ToList();

        var region = RegionKeywords.FirstOrDefault(k =>
            Regex.IsMatch(text, $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(k)}(?![\p{{L}}\p{{N}}])", RegexOptions.IgnoreCase));

        var isRemote = text.Contains("remote", StringComparison.OrdinalIgnoreCase)
                       || text.Contains("télétravail", StringComparison.OrdinalIgnoreCase);

        return new PlaceInfo(codes, codes.Count > 0 ? NameFor(codes[0]) ?? "" : "", city, isRemote, region);
    }
}
