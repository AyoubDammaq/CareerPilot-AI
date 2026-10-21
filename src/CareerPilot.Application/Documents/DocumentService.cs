using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CareerPilot.Application.Abstractions;
using CareerPilot.Application.Common;
using CareerPilot.Domain.Documents;

namespace CareerPilot.Application.Documents;

public interface IDocumentStore
{
    void Add(GeneratedDocument document);
    Task<GeneratedDocument?> GetAsync(Guid id, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>Turns a CV into a downloadable file. Implemented in Infrastructure.</summary>
public interface ICvRenderer
{
    byte[] RenderDocx(CvContent cv);
}

public sealed record DocumentDto(
    Guid Id, Guid JobId, string Kind, string Title, DateTimeOffset CreatedAt, string ContentHash, bool Stale,
    CvContent Content, CvReport? Report);

public sealed record DocumentFile(byte[] Bytes, string FileName, string ContentType);

public sealed class DocumentService(IJobStore jobs, IProfileStore profiles, IDocumentStore docs, ICvRenderer renderer, IAuditLog audit)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public const string DocxType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public async Task<DocumentDto> CreateCvAsync(Guid jobId, CancellationToken ct)
    {
        var job = await jobs.GetListingAsync(jobId, ct) ?? throw new NotFoundException("Job not found.");
        var profile = await profiles.GetAsync(ct) ?? throw new ConflictException("Create your profile first: the CV is built from it.");

        var built = CvBuilder.Build(profile, job);
        var json = JsonSerializer.Serialize(built.Content, Json);
        var doc = new GeneratedDocument
        {
            JobListingId = job.Id,
            Kind = DocumentKind.Cv,
            Title = $"CV for {job.Title} at {job.CompanyName}",
            ContentJson = json,
            ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant(),
            ProfileVersion = profile.Version,
        };
        docs.Add(doc);
        audit.Stage("owner", "document.cv.created", nameof(GeneratedDocument), doc.Id, $"job {job.Id}, hash {doc.ContentHash[..12]}");
        await docs.SaveChangesAsync(ct);
        return ToDto(doc, built.Content, built.Report, stale: false);
    }

    public async Task<DocumentDto> GetAsync(Guid id, CancellationToken ct)
    {
        var (doc, content) = await LoadAsync(id, ct);
        var profile = await profiles.GetAsync(ct);
        return ToDto(doc, content, null, stale: profile is not null && profile.Version != doc.ProfileVersion);
    }

    public async Task<DocumentFile> DownloadDocxAsync(Guid id, CancellationToken ct)
    {
        var (doc, content) = await LoadAsync(id, ct);
        audit.Stage("owner", "document.cv.downloaded", nameof(GeneratedDocument), doc.Id, null);
        await docs.SaveChangesAsync(ct);
        return new DocumentFile(renderer.RenderDocx(content), FileName(content, doc), DocxType);
    }

    private async Task<(GeneratedDocument, CvContent)> LoadAsync(Guid id, CancellationToken ct)
    {
        var doc = await docs.GetAsync(id, ct) ?? throw new NotFoundException("Document not found.");
        var content = JsonSerializer.Deserialize<CvContent>(doc.ContentJson, Json) ?? throw new NotFoundException("Document not found.");
        return (doc, content);
    }

    private static DocumentDto ToDto(GeneratedDocument d, CvContent c, CvReport? r, bool stale) =>
        new(d.Id, d.JobListingId, d.Kind.ToString(), d.Title, d.CreatedAt, d.ContentHash, stale, c, r);

    /// <summary>ASCII-only file name safe for headers and file systems.</summary>
    public static string FileName(CvContent c, GeneratedDocument d)
    {
        var name = new string([.. (string.IsNullOrWhiteSpace(c.FullName) ? "CV" : "CV " + c.FullName)
            .Select(ch => char.IsAsciiLetterOrDigit(ch) ? ch : '-')]);
        return $"{name.Trim('-')}-{d.ContentHash[..8]}.docx";
    }
}
