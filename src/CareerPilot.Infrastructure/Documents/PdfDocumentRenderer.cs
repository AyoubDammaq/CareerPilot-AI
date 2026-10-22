using CareerPilot.Application.Documents;

namespace CareerPilot.Infrastructure.Documents;

/// <summary>Plain single-column PDF of a stored snapshot, mirroring the Word layout (same content, same order).</summary>
public sealed class PdfDocumentRenderer : IPdfRenderer
{
    public byte[] RenderPdf(CvContent cv)
    {
        var w = new SimplePdfWriter();
        w.Paragraph(cv.FullName, bold: true, size: 18, spaceBefore: 0);
        if (!string.IsNullOrWhiteSpace(cv.Headline)) w.Paragraph(cv.Headline, size: 12);
        var contact = new[] { cv.Email, cv.Phone, cv.Location }.Concat(cv.Links).Where(x => !string.IsNullOrWhiteSpace(x));
        w.Paragraph(string.Join("  |  ", contact!), size: 9.5);

        if (!string.IsNullOrWhiteSpace(cv.Summary)) { Heading(w, "Profile"); w.Paragraph(cv.Summary); }

        if (cv.Experience.Count > 0)
        {
            Heading(w, "Experience");
            foreach (var e in cv.Experience)
            {
                w.Paragraph($"{e.Title}, {e.Company}", bold: true, spaceBefore: 6);
                var meta = string.Join("  |  ", new[] { e.Period, e.Location }.Where(x => !string.IsNullOrWhiteSpace(x)));
                if (meta.Length > 0) w.Paragraph(meta, size: 9.5);
                if (!string.IsNullOrWhiteSpace(e.Description)) w.Paragraph(e.Description);
                foreach (var a in e.Achievements) w.Paragraph("• " + a, indent: 8);
            }
        }

        if (cv.Skills.Count > 0) { Heading(w, "Skills"); w.Paragraph(string.Join(", ", cv.Skills)); }

        if (cv.Projects.Count > 0)
        {
            Heading(w, "Projects");
            foreach (var p in cv.Projects)
            {
                w.Paragraph(p.Name, bold: true, spaceBefore: 6);
                if (!string.IsNullOrWhiteSpace(p.Description)) w.Paragraph(p.Description);
                if (p.Technologies.Count > 0) w.Paragraph("Technologies: " + string.Join(", ", p.Technologies), size: 9.5);
                foreach (var r in p.Results) w.Paragraph("• " + r, indent: 8);
                if (!string.IsNullOrWhiteSpace(p.Url)) w.Paragraph(p.Url!, size: 9.5);
            }
        }

        if (cv.Education.Count > 0)
        {
            Heading(w, "Education");
            foreach (var e in cv.Education)
            {
                var title = string.Join(", ", new[] { e.Degree, e.Field }.Where(x => !string.IsNullOrWhiteSpace(x)));
                w.Paragraph($"{title}, {e.Institution}".Trim(' ', ','), bold: true, spaceBefore: 6);
                if (!string.IsNullOrWhiteSpace(e.Period)) w.Paragraph(e.Period, size: 9.5);
            }
        }

        if (cv.Languages.Count > 0)
        {
            Heading(w, "Languages");
            w.Paragraph(string.Join(", ", cv.Languages.Select(l => string.IsNullOrWhiteSpace(l.Level) ? l.Name : $"{l.Name} ({l.Level})")));
        }
        return w.Build();
    }

    public byte[] RenderPdf(CoverLetterContent l)
    {
        var w = new SimplePdfWriter();
        w.Paragraph(l.FullName, bold: true, size: 14, spaceBefore: 0);
        var contact = new[] { l.Email, l.Phone, l.Location }.Where(x => !string.IsNullOrWhiteSpace(x));
        w.Paragraph(string.Join("  |  ", contact!), size: 9.5);
        w.Paragraph($"Application: {l.JobTitle}, {l.Company}", bold: true, spaceBefore: 18);
        w.Paragraph(l.Greeting, spaceBefore: 12);
        foreach (var p in l.Paragraphs) w.Paragraph(p, spaceBefore: 10);
        var closing = l.Closing.Split('\n');
        for (var i = 0; i < closing.Length; i++) w.Paragraph(closing[i], spaceBefore: i == 0 ? 14 : 0);
        return w.Build();
    }

    private static void Heading(SimplePdfWriter w, string text) => w.Paragraph(text.ToUpperInvariant(), bold: true, size: 11.5, spaceBefore: 12);
}
