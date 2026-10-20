using System.Net;
using System.Text.RegularExpressions;

namespace CareerPilot.Application.Jobs;

/// <summary>Turns untrusted HTML (possibly entity-escaped, as Greenhouse sends it) into inert plain text.</summary>
public static partial class HtmlText
{
    public const int MaxLength = 20_000;

    [GeneratedRegex(@"<\s*(script|style)\b[^>]*>.*?<\s*/\s*\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptOrStyle();

    [GeneratedRegex(@"<\s*/?\s*(p|div|br|h[1-6]|ul|ol|tr|section)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockTag();

    [GeneratedRegex(@"<\s*li\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItem();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex AnyTag();

    [GeneratedRegex(@"[ \t ]+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\s*\n\s*(\n\s*)+")]
    private static partial Regex BlankLines();

    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";

        var s = WebUtility.HtmlDecode(html); // Greenhouse sends escaped HTML: decode once to get markup
        s = ScriptOrStyle().Replace(s, " ");
        s = ListItem().Replace(s, "\n- ");
        s = BlockTag().Replace(s, "\n");
        s = AnyTag().Replace(s, " ");
        s = WebUtility.HtmlDecode(s);
        return Normalize(s);
    }

    /// <summary>For text that is already plain: tidy whitespace and cap the length.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var s = text.Replace("\r\n", "\n").Replace('\r', '\n');
        s = Spaces().Replace(s, " ");
        s = Regex.Replace(s, @" *\n *", "\n");
        s = BlankLines().Replace(s, "\n\n").Trim();
        return s.Length > MaxLength ? s[..MaxLength] : s;
    }
}
