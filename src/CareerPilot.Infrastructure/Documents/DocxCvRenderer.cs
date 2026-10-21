using CareerPilot.Application.Documents;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CareerPilot.Infrastructure.Documents;

/// <summary>Plain single-column Word layout: no tables, text boxes or images, so applicant tracking systems read it reliably.</summary>
public sealed class DocxCvRenderer : ICvRenderer
{
    public byte[] RenderDocx(CvContent cv)
    {
        using var stream = new MemoryStream();
        using (var word = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = word.AddMainDocumentPart();
            main.Document = new Document(new Body());
            var body = main.Document.Body!;

            body.Append(Para(cv.FullName, bold: true, size: 36));
            if (!string.IsNullOrWhiteSpace(cv.Headline)) body.Append(Para(cv.Headline, size: 24));
            var contact = new[] { cv.Email, cv.Phone, cv.Location }.Concat(cv.Links).Where(x => !string.IsNullOrWhiteSpace(x));
            body.Append(Para(string.Join("  |  ", contact!), size: 20));

            if (!string.IsNullOrWhiteSpace(cv.Summary))
            {
                body.Append(Heading("Profile"));
                body.Append(Para(cv.Summary));
            }

            if (cv.Experience.Count > 0)
            {
                body.Append(Heading("Experience"));
                foreach (var e in cv.Experience)
                {
                    body.Append(Para($"{e.Title}, {e.Company}", bold: true));
                    var meta = string.Join("  |  ", new[] { e.Period, e.Location }.Where(x => !string.IsNullOrWhiteSpace(x)));
                    if (meta.Length > 0) body.Append(Para(meta, size: 20));
                    if (!string.IsNullOrWhiteSpace(e.Description)) body.Append(Para(e.Description));
                    foreach (var a in e.Achievements) body.Append(Para("• " + a));
                }
            }

            if (cv.Skills.Count > 0)
            {
                body.Append(Heading("Skills"));
                body.Append(Para(string.Join(", ", cv.Skills)));
            }

            if (cv.Projects.Count > 0)
            {
                body.Append(Heading("Projects"));
                foreach (var p in cv.Projects)
                {
                    body.Append(Para(p.Name, bold: true));
                    if (!string.IsNullOrWhiteSpace(p.Description)) body.Append(Para(p.Description));
                    if (p.Technologies.Count > 0) body.Append(Para("Technologies: " + string.Join(", ", p.Technologies), size: 20));
                    foreach (var r in p.Results) body.Append(Para("• " + r));
                    if (!string.IsNullOrWhiteSpace(p.Url)) body.Append(Para(p.Url!, size: 20));
                }
            }

            if (cv.Education.Count > 0)
            {
                body.Append(Heading("Education"));
                foreach (var e in cv.Education)
                {
                    var title = string.Join(", ", new[] { e.Degree, e.Field }.Where(x => !string.IsNullOrWhiteSpace(x)));
                    body.Append(Para($"{title}, {e.Institution}".Trim(' ', ','), bold: true));
                    if (!string.IsNullOrWhiteSpace(e.Period)) body.Append(Para(e.Period, size: 20));
                }
            }

            if (cv.Languages.Count > 0)
            {
                body.Append(Heading("Languages"));
                body.Append(Para(string.Join(", ", cv.Languages.Select(l => string.IsNullOrWhiteSpace(l.Level) ? l.Name : $"{l.Name} ({l.Level})"))));
            }

            main.Document.Save();
        }
        return stream.ToArray();
    }

    private static Paragraph Heading(string text) => Para(text.ToUpperInvariant(), bold: true, size: 24, spaceBefore: 240);

    private static Paragraph Para(string text, bool bold = false, int size = 22, int spaceBefore = 60)
    {
        var props = new RunProperties(new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" }, new FontSize { Val = size.ToString() });
        if (bold) props.Append(new Bold());
        return new Paragraph(
            new ParagraphProperties(new SpacingBetweenLines { Before = spaceBefore.ToString(), After = "0" }),
            new Run(props, new Text(XmlSafe(text)) { Space = SpaceProcessingModeValues.Preserve }));
    }

    /// <summary>Removes characters that are not allowed in XML 1.0 (they would corrupt the file).</summary>
    internal static string XmlSafe(string s) =>
        new([.. (s ?? "").Where(c => c is '\t' or '\n' or '\r' || (c >= 0x20 && c <= 0xD7FF) || (c >= 0xE000 && c <= 0xFFFD) || char.IsSurrogate(c))]);
}
