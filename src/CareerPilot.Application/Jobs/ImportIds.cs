using System.Security.Cryptography;
using System.Text;

namespace CareerPilot.Application.Jobs;

/// <summary>Stable ids for manually imported jobs, so importing the same job twice updates it instead of duplicating it.</summary>
public static class ImportIds
{
    private static readonly string[] TrackingPrefixes = ["utm_", "gh_src", "lever-source", "ref", "source", "src", "fbclid", "gclid"];

    public static string ForUrl(Uri url)
    {
        var query = (url.Query.TrimStart('?')).Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !TrackingPrefixes.Any(t => p.StartsWith(t, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(p => p, StringComparer.Ordinal);
        var canonical = $"{url.IdnHost.ToLowerInvariant()}{url.AbsolutePath.TrimEnd('/')}?{string.Join('&', query)}";
        return "url:" + Hash(canonical);
    }

    public static string ForText(string company, string title, string location) =>
        "text:" + Hash($"{company.Trim().ToLowerInvariant()}|{title.Trim().ToLowerInvariant()}|{location.Trim().ToLowerInvariant()}");

    private static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)))[..24].ToLowerInvariant();
}
