using CareerPilot.Application.Documents;
using CareerPilot.Infrastructure.Documents;
using UglyToad.PdfPig;

namespace CareerPilot.Tests.Documents;

public class PdfTests
{
    private static string[] Pages(byte[] pdf)
    {
        using var doc = PdfDocument.Open(pdf);
        return [.. doc.GetPages().Select(p => p.Text)];
    }

    private static CvContent Cv(int achievements = 1) =>
        new("Ayoub Dammak", "Software Engineer", "a@example.test", "+216 20 000 000", "Tunis, Tunisia", ["https://github.com/example"], "Ingénieur développement (café, été).",
            [new CvExperience("Engineer", "Sagemcom", "Tunisia", "Oct 2025 – Present", "Built things (and more)",
                [.. Enumerable.Range(1, achievements).Select(i => $"Shipped item number {i} with a reasonably long description so that it wraps")])],
            [new CvEducation("Engineering degree", "CS", "ENIT", "2020 – 2023")], ["C#", "Angular"],
            [new CvProject("Alpha", "A project", "https://example.test/alpha", ["C#"], ["Result one"])], [new CvLanguage("French", "B2")]);

    [Fact]
    public void The_cv_is_a_valid_pdf_with_selectable_text()
    {
        var bytes = new PdfDocumentRenderer().RenderPdf(Cv());
        Assert.StartsWith("%PDF-1.4", System.Text.Encoding.ASCII.GetString(bytes, 0, 8));
        var text = string.Join("\n", Pages(bytes));
        Assert.Contains("Ayoub Dammak", text);
        Assert.Contains("Sagemcom", text);
        Assert.Contains("EXPERIENCE", text);
        Assert.Contains("Angular", text);
        Assert.Contains("café", text);
        Assert.Contains("Oct 2025", text);
    }

    [Fact]
    public void Parentheses_and_backslashes_do_not_break_the_file()
    {
        var cv = Cv() with { Summary = @"Uses (nested (parens)) and a back\slash" };
        var text = string.Join("\n", Pages(new PdfDocumentRenderer().RenderPdf(cv)));
        Assert.Contains("(nested (parens))", text);
        Assert.Contains(@"back\slash", text);
    }

    [Fact]
    public void Long_content_continues_on_more_pages_and_nothing_is_lost()
    {
        var pages = Pages(new PdfDocumentRenderer().RenderPdf(Cv(achievements: 120)));
        Assert.True(pages.Length >= 2);
        var all = string.Join("\n", pages);
        Assert.Contains("item number 1 ", all.Replace("\n", " "));
        Assert.Contains("item number 120", all.Replace("\n", " "));
    }

    [Fact]
    public void A_very_long_word_is_broken_instead_of_running_off_the_page()
    {
        var url = "https://example.test/" + new string('a', 300);
        var cv = Cv() with { Links = [url] };
        using var doc = PdfDocument.Open(new PdfDocumentRenderer().RenderPdf(cv));
        var right = doc.GetPage(1).Letters.Max(l => l.GlyphRectangle.Right);
        Assert.True(right <= 595 - 40, $"text reached x={right}");
    }

    [Fact]
    public void Output_is_deterministic()
    {
        var r = new PdfDocumentRenderer();
        Assert.Equal(r.RenderPdf(Cv()), r.RenderPdf(Cv()));
    }

    [Fact]
    public void A_cover_letter_renders_greeting_paragraphs_and_closing()
    {
        var l = new CoverLetterContent("Ayoub Dammak", "a@example.test", null, "Tunis", "Acme", "Backend Engineer", "Dear Hiring Team,",
            ["First paragraph.", "Second paragraph."], "Kind regards,\nAyoub Dammak", "ai:m");
        var text = string.Join("\n", Pages(new PdfDocumentRenderer().RenderPdf(l)));
        Assert.Contains("Application: Backend Engineer, Acme", text);
        Assert.Contains("Dear Hiring Team,", text);
        Assert.Contains("Second paragraph.", text);
        Assert.Contains("Kind regards,", text);
    }

    [Fact]
    public void Characters_outside_the_font_become_question_marks_not_errors()
    {
        var cv = Cv() with { Summary = "Hello 世界 😀 done" };
        var text = string.Join("\n", Pages(new PdfDocumentRenderer().RenderPdf(cv)));
        Assert.Contains("Hello", text);
        Assert.Contains("done", text);
    }
}
