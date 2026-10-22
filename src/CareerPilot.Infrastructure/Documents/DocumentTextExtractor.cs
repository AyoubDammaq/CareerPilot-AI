using System.IO.Compression;
using System.Text;
using CareerPilot.Application.Common;
using CareerPilot.Application.Import;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace CareerPilot.Infrastructure.Documents;

/// <summary>
/// Reads text from PDF, DOCX and plain text. The file type is decided by the content, not the name. Scanned
/// documents (images without text) are not supported: there is no OCR.
/// </summary>
public sealed class DocumentTextExtractor : ITextExtractor
{
    private const int MaxChars = 60_000;
    private const long MaxUncompressedDocx = 50L * 1024 * 1024;
    private const int MaxPdfPages = 30;

    public string Extract(byte[] content, string fileName)
    {
        string text;
        try
        {
            if (content.Length >= 5 && content[0] == '%' && content[1] == 'P' && content[2] == 'D' && content[3] == 'F' && content[4] == '-')
                text = FromPdf(content);
            else if (content.Length >= 4 && content[0] == 'P' && content[1] == 'K')
                text = FromDocx(content);
            else if (IsPlainTextName(fileName))
                text = FromText(content);
            else
                throw Invalid("Unsupported file. Upload a PDF, a DOCX or a plain text file.");
        }
        catch (ValidationFailedException) { throw; }
        catch (Exception)
        {
            // Library errors can include internals; give the owner a plain message instead.
            throw Invalid("The file could not be read. It may be damaged or password protected.");
        }

        text = text.Replace("\0", "").Trim();
        if (text.Length < 50)
            throw Invalid("No readable text was found. Scanned images are not supported (there is no OCR); export the CV as a text-based PDF or DOCX.");
        return text.Length > MaxChars ? text[..MaxChars] : text;
    }

    private static bool IsPlainTextName(string name) =>
        name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".md", StringComparison.OrdinalIgnoreCase);

    private static string FromText(byte[] content)
    {
        if (Array.IndexOf(content, (byte)0) >= 0) throw Invalid("That does not look like a text file.");
        try { return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(content); }
        catch (DecoderFallbackException) { throw Invalid("The text file must be UTF-8."); }
    }

    private static string FromPdf(byte[] content)
    {
        using var pdf = PdfDocument.Open(content);
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages().Take(MaxPdfPages))
        {
            sb.AppendLine(page.Text);
            if (sb.Length > MaxChars * 2) break;
        }
        return sb.ToString();
    }

    private static string FromDocx(byte[] content)
    {
        using (var zip = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read))
        {
            if (zip.GetEntry("word/document.xml") is null) throw Invalid("Unsupported file. Upload a PDF, a DOCX or a plain text file.");
            if (zip.Entries.Sum(e => e.Length) > MaxUncompressedDocx) throw Invalid("The document is too large to read.");
        }
        using var doc = WordprocessingDocument.Open(new MemoryStream(content), false);
        var body = doc.MainDocumentPart?.Document?.Body ?? throw Invalid("The document is empty.");
        var sb = new StringBuilder();
        foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
            sb.AppendLine(p.InnerText);
        return sb.ToString();
    }

    private static ValidationFailedException Invalid(string message) =>
        new(new Dictionary<string, string[]> { ["file"] = [message] });
}
