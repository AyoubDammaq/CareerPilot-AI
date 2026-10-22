using System.Globalization;
using System.Text;

namespace CareerPilot.Infrastructure.Documents;

/// <summary>
/// Minimal, dependency-free PDF writer: A4, one column, selectable text, the two standard Helvetica fonts
/// (nothing is embedded, nothing is drawn as an image, so applicant tracking systems can read it).
/// It is deterministic: the same input produces the same bytes. It is not a general layout engine.
/// </summary>
public sealed class SimplePdfWriter
{
    private const double PageWidth = 595, PageHeight = 842, Margin = 56;

    private readonly record struct Line(string Text, bool Bold, double Size, double SpaceBefore, double Indent);

    private readonly List<Line> lines = [];

    /// <summary>Adds a paragraph that is wrapped to the page width. Hanging indent applies to wrapped lines.</summary>
    public SimplePdfWriter Paragraph(string text, bool bold = false, double size = 10.5, double spaceBefore = 3, double indent = 0)
    {
        var width = PageWidth - 2 * Margin - indent;
        var first = true;
        foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
        {
            foreach (var l in Wrap(Clean(raw), bold, size, width))
            {
                lines.Add(new Line(l, bold, size, first ? spaceBefore : 0, indent));
                first = false;
            }
        }
        return this;
    }

    public byte[] Build()
    {
        var pages = new List<StringBuilder>();
        var page = new StringBuilder();
        var y = PageHeight - Margin;
        var started = false;
        foreach (var l in lines)
        {
            var leading = l.Size * 1.25;
            var need = l.SpaceBefore + leading;
            if (started && y - need < Margin)
            {
                pages.Add(page);
                page = new StringBuilder();
                y = PageHeight - Margin;
                started = false;
            }
            y -= started ? need : leading;
            started = true;
            if (l.Text.Length == 0) continue;
            page.Append("BT /").Append(l.Bold ? "F2" : "F1").Append(' ').Append(Num(l.Size)).Append(" Tf ")
                .Append(Num(Margin + l.Indent)).Append(' ').Append(Num(y)).Append(" Td (")
                .Append(Encode(l.Text)).Append(") Tj ET\n");
        }
        pages.Add(page);

        // Object numbers: 1 catalog, 2 pages, 3 and 4 fonts, then (page, content) pairs.
        var kids = string.Join(' ', pages.Select((_, i) => $"{5 + 2 * i} 0 R"));
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{kids}] /Count {pages.Count} >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>",
        };
        for (var i = 0; i < pages.Count; i++)
        {
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Num(PageWidth)} {Num(PageHeight)}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {6 + 2 * i} 0 R >>");
            var content = pages[i].ToString();
            objects.Add($"<< /Length {content.Length} >>\nstream\n{content}endstream");
        }

        var output = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(output.Length);
            output.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n");
        }
        var xref = output.Length;
        output.Append("xref\n0 ").Append(objects.Count + 1).Append("\n0000000000 65535 f \n");
        foreach (var o in offsets) output.Append(o.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        output.Append("trailer\n<< /Size ").Append(objects.Count + 1).Append(" /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
        // Everything written is ASCII (non-ASCII text is octal-escaped), so one byte per character.
        return Encoding.ASCII.GetBytes(output.ToString());
    }

    private static string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    // Windows-1252 code points outside Latin-1 that we can show.
    private static readonly Dictionary<char, byte> Cp1252 = new()
    {
        ['€'] = 0x80, ['…'] = 0x85, ['‘'] = 0x91, ['’'] = 0x92, ['“'] = 0x93, ['”'] = 0x94, ['•'] = 0x95, ['–'] = 0x96, ['—'] = 0x97,
    };

    private static byte ToByte(char c)
    {
        if (c is >= ' ' and <= '~') return (byte)c;
        if (c is >= ' ' and <= 'ÿ') return (byte)c;
        return Cp1252.TryGetValue(c, out var b) ? b : (byte)'?';
    }

    /// <summary>Tabs become spaces, other control characters are dropped.</summary>
    private static string Clean(string s) =>
        new([.. s.Replace('\t', ' ').Where(c => !char.IsControl(c) && !char.IsSurrogate(c))]);

    private static string Encode(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s)
        {
            var b = ToByte(c);
            if (b is (byte)'(' or (byte)')' or (byte)'\\') sb.Append('\\').Append((char)b);
            else if (b is >= 0x20 and <= 0x7E) sb.Append((char)b);
            else sb.Append('\\').Append(Convert.ToString(b, 8).PadLeft(3, '0'));
        }
        return sb.ToString();
    }

    // Helvetica advance widths (1/1000 em) for ASCII 32..126, from the standard font metrics.
    private static readonly int[] Regular =
    [
        278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278, 584, 584, 584, 556,
        1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556,
        333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556,
        556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584,
    ];

    private static readonly int[] BoldWidths =
    [
        278, 333, 474, 556, 556, 889, 722, 238, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 333, 333, 584, 584, 584, 611,
        975, 722, 722, 722, 722, 667, 611, 778, 722, 278, 556, 722, 611, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 333, 278, 333, 584, 556,
        333, 556, 611, 556, 611, 556, 333, 611, 611, 278, 278, 556, 278, 889, 611, 611,
        611, 611, 389, 556, 333, 611, 556, 778, 556, 556, 500, 389, 280, 389, 584,
    ];

    private const string AccentBase =
        "AAAAAAACEEEEIIIIDNOOOOOxOUUUUYPsaaaaaaaceeeeiiiionooooo/ouuuuypy";

    internal static double Width(string s, bool bold, double size)
    {
        var table = bold ? BoldWidths : Regular;
        double w = 0;
        foreach (var c in s)
        {
            var b = ToByte(c);
            int cw;
            if (b is >= 32 and <= 126) cw = table[b - 32];
            else if (b is >= 0xC0) cw = table[Math.Max(0, AccentBase[b - 0xC0] - 32)];
            else if (b == 0x95) cw = 350;
            else if (b is 0x97) cw = 1000;
            else cw = 556;
            w += cw;
        }
        return w * size / 1000.0 * 1.02; // 2% safety margin so a line never runs past the margin
    }

    private static IEnumerable<string> Wrap(string text, bool bold, double size, double width)
    {
        if (text.Length == 0)
        {
            yield return "";
            yield break;
        }
        var current = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var piece = word;
            // A single word wider than the line (a long URL) is broken by characters.
            while (Width(piece, bold, size) > width)
            {
                var n = piece.Length;
                while (n > 1 && Width(piece[..n], bold, size) > width) n--;
                if (current.Length > 0) { yield return current; current = ""; }
                yield return piece[..n];
                piece = piece[n..];
            }
            var candidate = current.Length == 0 ? piece : current + " " + piece;
            if (current.Length > 0 && Width(candidate, bold, size) > width)
            {
                yield return current;
                current = piece;
            }
            else current = candidate;
        }
        if (current.Length > 0) yield return current;
    }
}
