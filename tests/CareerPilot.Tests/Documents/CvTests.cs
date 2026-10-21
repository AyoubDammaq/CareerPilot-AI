using System.IO.Compression;
using System.Text.Json;
using CareerPilot.Application.Common;
using CareerPilot.Application.Documents;
using CareerPilot.Application.Profiles;
using CareerPilot.Domain.Documents;
using CareerPilot.Domain.Jobs;
using CareerPilot.Domain.Profiles;
using CareerPilot.Infrastructure.Documents;
using CareerPilot.Tests.Jobs;
using DocumentFormat.OpenXml.Packaging;

namespace CareerPilot.Tests.Documents;

public class CvBuilderTests
{
    private static UserProfile Profile()
    {
        var p = new UserProfile { FullName = "Ayoub Test", Email = "a@example.test", Phone = "+216 1", Headline = "Software Engineer", City = "Tunis", Country = "Tunisia", Summary = "I build backend systems." };
        p.WorkExperiences.Add(new WorkExperience { Position = 0, Company = "Sagemcom", Title = "Engineer", StartDate = new DateOnly(2025, 10, 1), IsVerified = true, Description = "Built things", Achievements = ["Shipped X"] });
        p.WorkExperiences.Add(new WorkExperience { Position = 1, Company = "Unverified Co", Title = "Wizard", StartDate = new DateOnly(2020, 1, 1), IsVerified = false });
        foreach (var (name, verified, pos) in new[] { ("Docker", true, 0), ("C#", true, 1), ("Kafka", false, 2), ("PostgreSQL", true, 3) })
            p.Skills.Add(new Skill { Name = name, IsVerified = verified, Position = pos });
        p.Projects.Add(new Project { Name = "Alpha", Position = 0, IsVerified = true, Technologies = ["Python"], Results = ["Cut cost 20%"] });
        p.Projects.Add(new Project { Name = "Beta", Position = 1, IsVerified = true, Technologies = ["C#", "PostgreSQL"] });
        p.Projects.Add(new Project { Name = "Gamma", Position = 2, IsVerified = false, Technologies = ["C#"] });
        p.Education.Add(new Education { Institution = "ENIT", Degree = "Engineering degree", Field = "CS" });
        p.Languages.Add(new ProfileLanguage { Name = "French", Level = "B2" });
        p.LinkedinUrl = "https://linkedin.example.test/in/a";
        return p;
    }

    private static JobListing Job(params string[] skills)
    {
        var j = Helpers.Listing();
        j.Skills = [.. skills];
        return j;
    }

    [Fact]
    public void Only_verified_experience_skills_and_projects_are_included()
    {
        var r = CvBuilder.Build(Profile(), Job("C#"));
        Assert.Single(r.Content.Experience);
        Assert.Equal("Sagemcom", r.Content.Experience[0].Company);
        Assert.DoesNotContain("Kafka", r.Content.Skills);
        Assert.DoesNotContain(r.Content.Projects, p => p.Name == "Gamma");
        Assert.Contains(r.Report.Omitted, o => o.Contains("Unverified Co"));
        Assert.Contains(r.Report.Omitted, o => o.Contains("Kafka"));
        Assert.Contains(r.Report.Omitted, o => o.Contains("Gamma"));
    }

    [Fact]
    public void Nothing_is_added_that_the_profile_does_not_contain()
    {
        var p = Profile();
        var r = CvBuilder.Build(p, Job("Rust", "Kafka", "C#"));
        var profileSkills = p.Skills.Select(s => s.Name).ToHashSet();
        Assert.All(r.Content.Skills, s => Assert.Contains(s, profileSkills));
        Assert.DoesNotContain("Rust", r.Content.Skills);
        Assert.Equal(["Rust", "Kafka"], r.Report.NotOnCv);
    }

    [Fact]
    public void Skills_the_vacancy_mentions_come_first_and_the_rest_keep_the_owners_order()
    {
        var r = CvBuilder.Build(Profile(), Job("PostgreSQL", "C#"));
        Assert.Equal(["C#", "PostgreSQL", "Docker"], r.Content.Skills);
        Assert.Equal(["Docker"], r.Content.Skills.TakeLast(1));
    }

    [Fact]
    public void Most_relevant_projects_first_and_at_most_three()
    {
        var p = Profile();
        for (var i = 0; i < 5; i++) p.Projects.Add(new Project { Name = $"Extra{i}", Position = 10 + i, IsVerified = true });
        var r = CvBuilder.Build(p, Job("C#", "PostgreSQL"));
        Assert.Equal(CvBuilder.MaxProjects, r.Content.Projects.Count);
        Assert.Equal("Beta", r.Content.Projects[0].Name);
        Assert.Contains(r.Report.Omitted, o => o.Contains("Extra4") && o.Contains("left out"));
    }

    [Fact]
    public void The_owners_text_is_copied_unchanged()
    {
        var r = CvBuilder.Build(Profile(), Job());
        Assert.Equal("I build backend systems.", r.Content.Summary);
        Assert.Equal("Software Engineer", r.Content.Headline);
        Assert.Equal(["Shipped X"], r.Content.Experience[0].Achievements);
        Assert.Equal("Oct 2025 – Present", r.Content.Experience[0].Period);
        Assert.Equal(["Cut cost 20%"], r.Content.Projects.First(x => x.Name == "Alpha").Results);
    }

    [Fact]
    public void An_unverified_profile_gives_warnings_not_invented_content()
    {
        var r = CvBuilder.Build(DefaultProfileFactory.Create(), Job("C#"));
        Assert.Empty(r.Content.Experience);
        Assert.Empty(r.Content.Skills);
        Assert.Contains(r.Report.Warnings, w => w.Contains("No work experience is marked verified"));
        Assert.Contains(r.Report.Warnings, w => w.Contains("full name"));
        Assert.Contains(r.Report.Warnings, w => w.Contains("email"));
    }

    [Fact]
    public void Contact_details_and_links_come_from_the_profile()
    {
        var c = CvBuilder.Build(Profile(), Job()).Content;
        Assert.Equal("Tunis, Tunisia", c.Location);
        Assert.Equal(["https://linkedin.example.test/in/a"], c.Links);
        Assert.Equal("+216 1", c.Phone);
    }
}

public class DocxRendererTests
{
    private static CvContent Sample(string name = "Ayoub Test") => new(
        name, "Software Engineer", "a@example.test", null, "Tunis, Tunisia", [], "Summary here.",
        [new CvExperience("Engineer", "Sagemcom", "Tunisia", "Oct 2025 – Present", "Built things", ["Shipped X"])],
        [new CvEducation("Engineering degree", "CS", "ENIT", "")], ["C#", "Docker"],
        [new CvProject("Beta", "A project", null, ["C#"], [])], [new CvLanguage("French", "B2")]);

    private static string TextOf(byte[] docx)
    {
        using var ms = new MemoryStream(docx);
        using var doc = WordprocessingDocument.Open(ms, false);
        return doc.MainDocumentPart!.Document!.Body!.InnerText;
    }

    [Fact]
    public void Produces_a_valid_docx_with_the_content()
    {
        var bytes = new DocxCvRenderer().RenderDocx(Sample());
        Assert.Equal((byte)'P', bytes[0]); // zip container
        using (var zip = new ZipArchive(new MemoryStream(bytes))) Assert.NotNull(zip.GetEntry("word/document.xml"));
        var text = TextOf(bytes);
        Assert.Contains("Ayoub Test", text);
        Assert.Contains("Sagemcom", text);
        Assert.Contains("Shipped X", text);
        Assert.Contains("C#, Docker", text);
        Assert.Contains("French (B2)", text);
        Assert.Contains("SKILLS", text);
    }

    [Fact]
    public void Characters_that_would_corrupt_the_file_are_removed()
    {
        var bytes = new DocxCvRenderer().RenderDocx(Sample("Ayoub\u0001 Te\u0000st"));
        Assert.Contains("Ayoub Test", TextOf(bytes));
    }

    [Fact]
    public void Empty_sections_are_left_out()
    {
        var bytes = new DocxCvRenderer().RenderDocx(Sample() with { Experience = [], Skills = [], Summary = "" });
        var text = TextOf(bytes);
        Assert.DoesNotContain("EXPERIENCE", text);
        Assert.DoesNotContain("SKILLS", text);
        Assert.DoesNotContain("PROFILE", text);
    }
}

public class DocumentServiceTests
{
    private sealed class MemoryDocs : IDocumentStore
    {
        public List<GeneratedDocument> Items { get; } = [];
        public int Saves { get; private set; }
        public void Add(GeneratedDocument d) => Items.Add(d);
        public Task<GeneratedDocument?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(d => d.Id == id));
        public Task SaveChangesAsync(CancellationToken ct) { Saves++; return Task.CompletedTask; }
    }

    private sealed class Rig
    {
        public InMemoryJobStore Jobs { get; } = new();
        public MemoryDocs Docs { get; } = new();
        public NoopAudit Audit { get; } = new();
        public UserProfile? Profile { get; set; } = DefaultProfileFactory.Create();
        public JobListing Job { get; } = Helpers.Listing();
        public Rig()
        {
            Jobs.Listings.Add(Job);
            Profile!.FullName = "Ayoub Test";
            foreach (var w in Profile.WorkExperiences) w.IsVerified = true;
        }
        public DocumentService Service() => new(Jobs, new StaticProfileStore(Profile), Docs, new DocxCvRenderer(), Audit);
    }

    [Fact]
    public async Task Creating_a_cv_stores_an_immutable_snapshot_with_a_matching_hash()
    {
        var r = new Rig();
        var dto = await r.Service().CreateCvAsync(r.Job.Id, default);
        var stored = Assert.Single(r.Docs.Items);
        Assert.Equal(dto.ContentHash, stored.ContentHash);
        Assert.Equal(64, stored.ContentHash.Length);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(stored.ContentJson))).ToLowerInvariant(), stored.ContentHash);
        Assert.Equal(r.Profile!.Version, stored.ProfileVersion);
        Assert.Contains("document.cv.created", r.Audit.Actions);
        Assert.NotNull(dto.Report);
        Assert.False(dto.Stale);
    }

    [Fact]
    public async Task The_same_profile_and_job_give_the_same_hash()
    {
        var r = new Rig();
        var a = await r.Service().CreateCvAsync(r.Job.Id, default);
        var b = await r.Service().CreateCvAsync(r.Job.Id, default);
        Assert.Equal(a.ContentHash, b.ContentHash);
    }

    [Fact]
    public async Task Changing_the_profile_changes_the_hash_and_marks_old_documents_stale()
    {
        var r = new Rig();
        var a = await r.Service().CreateCvAsync(r.Job.Id, default);
        r.Profile!.Headline = "Different headline";
        r.Profile.Version++;
        var b = await r.Service().CreateCvAsync(r.Job.Id, default);
        Assert.NotEqual(a.ContentHash, b.ContentHash);
        Assert.True((await r.Service().GetAsync(a.Id, default)).Stale);
        Assert.False((await r.Service().GetAsync(b.Id, default)).Stale);
    }

    [Fact]
    public async Task Download_renders_the_stored_snapshot_not_the_current_profile()
    {
        var r = new Rig();
        var a = await r.Service().CreateCvAsync(r.Job.Id, default);
        r.Profile!.FullName = "Someone Else";
        var file = await r.Service().DownloadDocxAsync(a.Id, default);
        Assert.EndsWith(".docx", file.FileName);
        Assert.Equal(DocumentService.DocxType, file.ContentType);
        using var ms = new MemoryStream(file.Bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var text = doc.MainDocumentPart!.Document!.Body!.InnerText;
        Assert.Contains("Ayoub Test", text);
        Assert.DoesNotContain("Someone Else", text);
        Assert.Contains("document.cv.downloaded", r.Audit.Actions);
    }

    [Fact]
    public async Task File_names_are_plain_ascii()
    {
        var c = new CvContent("Aÿoub/../Test", "", "", null, "", [], "", [], [], [], [], []);
        var name = DocumentService.FileName(c, new GeneratedDocument { ContentHash = new string('a', 64) });
        Assert.Matches("^[A-Za-z0-9-]+\\.docx$", name);
        Assert.DoesNotContain("/", name);
    }

    [Fact]
    public async Task Unknown_job_document_or_missing_profile_are_reported()
    {
        var r = new Rig();
        await Assert.ThrowsAsync<NotFoundException>(() => r.Service().CreateCvAsync(Guid.NewGuid(), default));
        await Assert.ThrowsAsync<NotFoundException>(() => r.Service().GetAsync(Guid.NewGuid(), default));
        await Assert.ThrowsAsync<NotFoundException>(() => r.Service().DownloadDocxAsync(Guid.NewGuid(), default));
        r.Profile = null;
        await Assert.ThrowsAsync<ConflictException>(() => r.Service().CreateCvAsync(r.Job.Id, default));
    }
}
