using CareerPilot.Application.Abstractions;
using CareerPilot.Application.Ai;
using CareerPilot.Application.Applications;
using CareerPilot.Application.Common;
using CareerPilot.Application.Documents;
using CareerPilot.Domain.Applications;
using CareerPilot.Domain.Documents;
using CareerPilot.Domain.Jobs;
using CareerPilot.Domain.Profiles;
using CareerPilot.Infrastructure.Documents;
using CareerPilot.Tests.Ai;
using CareerPilot.Tests.Documents;
using CareerPilot.Tests.Jobs;

namespace CareerPilot.Tests.Applications;

internal sealed class MemoryApps : IApplicationStore
{
    public List<JobApplication> Items { get; } = [];
    public void Add(JobApplication a) => Items.Add(a);
    public Task<JobApplication?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id));
    public Task<JobApplication?> GetByJobAsync(Guid jobId, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.CanonicalJobId == jobId));
    public Task<List<JobApplication>> ListAsync(CancellationToken ct) => Task.FromResult(Items.ToList());
    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
}

public class ApplicationServiceTests
{
    private const string GoodLetter = """{"paragraphs":["I work with C# and Docker.","At Sagemcom I reduced build time by 30%."]}""";
    private const string BadLetter = """{"paragraphs":["I have deep Rust experience and raised sales by 90%."]}""";

    private sealed class Rig
    {
        public InMemoryJobStore Jobs { get; } = new();
        public MemoryApps Apps { get; } = new();
        public DocumentServiceTests_Docs Docs { get; } = new();
        public NoopAudit Audit { get; } = new();
        public FixedClock Clock { get; } = new(Helpers.Now);
        public UserProfile Profile { get; }
        public JobListing Job { get; } = Helpers.Listing();
        public FakeLlm Llm { get; set; } = new(_ => new LlmResponse(GoodLetter, 1, 1));
        public LlmOptions Options { get; } = new() { Provider = LlmProvider.OpenAiCompatible, Model = "m", DailyCallLimit = 50 };

        public Rig()
        {
            Job.Postings.Add(new JobPosting { SourceUrl = "https://jobs.example.test/1", ApplyUrl = "https://jobs.example.test/1/apply" });
            Jobs.Listings.Add(Job);
            Profile = new UserProfile { FullName = "Ayoub Test", Email = "a@example.test", Headline = "Software Engineer" };
            Profile.WorkExperiences.Add(new WorkExperience { Company = "Sagemcom", Title = "Engineer", StartDate = new DateOnly(2025, 10, 1), IsVerified = true, Description = "Reduced build time by 30%" });
            Profile.Skills.Add(new Skill { Name = "C#", IsVerified = true });
            Profile.Skills.Add(new Skill { Name = "Docker", IsVerified = true });
        }

        private StaticProfileStore ProfileStore => new(Profile);
        public ApplicationService Service() => new(Apps, Jobs, ProfileStore, Docs, Clock, Audit);
        public DocumentService DocService() => new(Jobs, ProfileStore, Docs, new DocxCvRenderer(), Audit);
        public CoverLetterService LetterService() => new(Jobs, ProfileStore, Docs, Llm, Options, Clock, Audit);

        public async Task<ApplicationDto> ReadyForReviewAsync(bool withLetter = true, string letter = GoodLetter)
        {
            var app = await Service().StartAsync(Job.Id, default);
            var cv = await DocService().CreateCvAsync(Job.Id, default);
            Guid? letterId = null;
            if (withLetter)
            {
                Llm = new FakeLlm(_ => new LlmResponse(letter, 1, 1));
                letterId = (await LetterService().DraftAsync(Job.Id, default)).Id;
            }
            await Service().SetDocumentsAsync(app.Id, new SetDocumentsInput(cv.Id, letterId), default);
            return await Service().RequestReviewAsync(app.Id, default);
        }

        public async Task<ApplicationDto> ApprovedAsync()
        {
            var reviewed = await ReadyForReviewAsync();
            return await Service().ApproveAsync(reviewed.Id, new ApproveInput(reviewed.Review!.BundleHash, false), default);
        }
    }

    [Fact]
    public async Task Starting_shortlists_once_per_job()
    {
        var r = new Rig();
        var a = await r.Service().StartAsync(r.Job.Id, default);
        var b = await r.Service().StartAsync(r.Job.Id, default);
        Assert.Equal(a.Id, b.Id);
        Assert.Equal("Shortlisted", a.Status);
        Assert.Equal("https://jobs.example.test/1/apply", a.ApplyUrl);
        Assert.Single(r.Apps.Items);
        Assert.Contains("application.started", r.Audit.Actions);
        await Assert.ThrowsAsync<NotFoundException>(() => r.Service().StartAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Documents_must_belong_to_the_job_and_be_the_right_kind()
    {
        var r = new Rig();
        var app = await r.Service().StartAsync(r.Job.Id, default);
        var cv = await r.DocService().CreateCvAsync(r.Job.Id, default);
        var letter = await r.LetterService().DraftAsync(r.Job.Id, default);

        await Assert.ThrowsAsync<ValidationFailedException>(() => r.Service().SetDocumentsAsync(app.Id, new SetDocumentsInput(null, null), default));
        await Assert.ThrowsAsync<ValidationFailedException>(() => r.Service().SetDocumentsAsync(app.Id, new SetDocumentsInput(letter.Id, null), default)); // letter as CV
        await Assert.ThrowsAsync<ValidationFailedException>(() => r.Service().SetDocumentsAsync(app.Id, new SetDocumentsInput(cv.Id, cv.Id), default)); // CV as letter
        await Assert.ThrowsAsync<ValidationFailedException>(() => r.Service().SetDocumentsAsync(app.Id, new SetDocumentsInput(Guid.NewGuid(), null), default));

        var other = Helpers.Listing("Other role");
        r.Jobs.Listings.Add(other);
        var foreign = await r.DocService().CreateCvAsync(other.Id, default);
        await Assert.ThrowsAsync<ValidationFailedException>(() => r.Service().SetDocumentsAsync(app.Id, new SetDocumentsInput(foreign.Id, null), default));

        var ok = await r.Service().SetDocumentsAsync(app.Id, new SetDocumentsInput(cv.Id, letter.Id), default);
        Assert.Equal(cv.Id, ok.CvDocumentId);
        Assert.NotNull(ok.Review!.BundleHash);
    }

    [Fact]
    public async Task Review_cannot_be_requested_without_a_cv()
    {
        var r = new Rig();
        var app = await r.Service().StartAsync(r.Job.Id, default);
        await Assert.ThrowsAsync<ConflictException>(() => r.Service().RequestReviewAsync(app.Id, default));
    }

    [Fact]
    public async Task Approval_is_bound_to_the_reviewed_bundle()
    {
        var r = new Rig();
        var reviewed = await r.ReadyForReviewAsync();
        Assert.Equal("AwaitingApproval", reviewed.Status);
        Assert.Null(reviewed.ApprovedBundleHash);

        await Assert.ThrowsAsync<ConflictException>(() => r.Service().ApproveAsync(reviewed.Id, new ApproveInput("deadbeef", false), default));
        await Assert.ThrowsAsync<ConflictException>(() => r.Service().ApproveAsync(reviewed.Id, new ApproveInput(null, false), default));
        Assert.Empty(r.Audit.Actions.Where(a => a == "application.approved"));

        var approved = await r.Service().ApproveAsync(reviewed.Id, new ApproveInput(reviewed.Review!.BundleHash, false), default);
        Assert.Equal("Approved", approved.Status);
        Assert.Equal(reviewed.Review.BundleHash, approved.ApprovedBundleHash);
        Assert.NotNull(approved.ApprovedAt);
        Assert.Contains("application.approved", r.Audit.Actions);
    }

    [Fact]
    public async Task An_application_cannot_be_approved_unless_review_was_requested()
    {
        var r = new Rig();
        var app = await r.Service().StartAsync(r.Job.Id, default);
        var cv = await r.DocService().CreateCvAsync(r.Job.Id, default);
        var withDocs = await r.Service().SetDocumentsAsync(app.Id, new SetDocumentsInput(cv.Id, null), default);
        await Assert.ThrowsAsync<ConflictException>(() => r.Service().ApproveAsync(app.Id, new ApproveInput(withDocs.Review!.BundleHash, true), default));
        Assert.Equal("Shortlisted", (await r.Service().GetAsync(app.Id, default)).Status);
    }

    [Fact]
    public async Task Flagged_letters_need_an_explicit_acknowledgement()
    {
        var r = new Rig();
        var reviewed = await r.ReadyForReviewAsync(letter: BadLetter);
        Assert.False(reviewed.Review!.Claims!.Clean);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => r.Service().ApproveAsync(reviewed.Id, new ApproveInput(reviewed.Review.BundleHash, false), default));
        Assert.Contains("unsupported claims", ex.Message);
        var approved = await r.Service().ApproveAsync(reviewed.Id, new ApproveInput(reviewed.Review.BundleHash, true), default);
        Assert.Equal("Approved", approved.Status);
        Assert.Contains(r.Audit.Actions, a => a == "application.approved");
    }

    [Fact]
    public async Task Stale_documents_block_approval_until_they_are_prepared_again()
    {
        var r = new Rig();
        var reviewed = await r.ReadyForReviewAsync();
        r.Profile.Version++;
        var current = await r.Service().GetAsync(reviewed.Id, default);
        Assert.True(current.Review!.DocumentsStale);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => r.Service().ApproveAsync(reviewed.Id, new ApproveInput(current.Review.BundleHash, true), default));
        Assert.Contains("profile changed", ex.Message);
    }

    [Fact]
    public async Task An_empty_cv_or_a_closed_posting_blocks_approval()
    {
        var r = new Rig();
        r.Profile.WorkExperiences.ForEach(w => w.IsVerified = false);
        r.Profile.Skills.ForEach(s => s.IsVerified = false);
        var app = await r.Service().StartAsync(r.Job.Id, default);
        var cv = await r.DocService().CreateCvAsync(r.Job.Id, default);
        await r.Service().SetDocumentsAsync(app.Id, new SetDocumentsInput(cv.Id, null), default);
        var reviewed = await r.Service().RequestReviewAsync(app.Id, default);
        Assert.Contains(reviewed.Review!.Blockers, b => b.Contains("no verified"));
        await Assert.ThrowsAsync<ConflictException>(() => r.Service().ApproveAsync(app.Id, new ApproveInput(reviewed.Review.BundleHash, true), default));

        var r2 = new Rig();
        var ok = await r2.ReadyForReviewAsync();
        r2.Job.Status = JobStatus.Closed;
        var ex = await Assert.ThrowsAsync<ConflictException>(() => r2.Service().ApproveAsync(ok.Id, new ApproveInput(ok.Review!.BundleHash, true), default));
        Assert.Contains("no longer open", ex.Message);
    }

    [Fact]
    public async Task Changing_documents_after_approval_withdraws_the_approval()
    {
        var r = new Rig();
        var approved = await r.ApprovedAsync();
        var newCv = await r.DocService().CreateCvAsync(r.Job.Id, default);
        r.Profile.Headline = "Changed"; // a different CV snapshot
        var another = await r.DocService().CreateCvAsync(r.Job.Id, default);
        Assert.NotEqual(newCv.ContentHash, another.ContentHash);

        var after = await r.Service().SetDocumentsAsync(approved.Id, new SetDocumentsInput(another.Id, approved.CoverLetterDocumentId), default);
        Assert.Equal("AwaitingApproval", after.Status);
        Assert.Null(after.ApprovedBundleHash);
        Assert.Null(after.ApprovedAt);
        Assert.Contains("application.documents.changed.approval-withdrawn", r.Audit.Actions);
        await Assert.ThrowsAsync<ConflictException>(() => r.Service().MarkReadyAsync(approved.Id, default));
    }

    [Fact]
    public async Task Re_choosing_the_same_documents_keeps_the_approval()
    {
        var r = new Rig();
        var approved = await r.ApprovedAsync();
        var same = await r.Service().SetDocumentsAsync(approved.Id, new SetDocumentsInput(approved.CvDocumentId, approved.CoverLetterDocumentId), default);
        Assert.Equal("Approved", same.Status);
    }

    [Fact]
    public async Task The_owner_can_withdraw_an_approval()
    {
        var r = new Rig();
        var approved = await r.ApprovedAsync();
        var back = await r.Service().WithdrawApprovalAsync(approved.Id, default);
        Assert.Equal("AwaitingApproval", back.Status);
        Assert.Null(back.ApprovedBundleHash);
        await Assert.ThrowsAsync<ConflictException>(() => r.Service().WithdrawApprovalAsync(approved.Id, default));
    }

    [Fact]
    public async Task Ready_to_submit_needs_an_approval_and_an_open_posting()
    {
        var r = new Rig();
        var reviewed = await r.ReadyForReviewAsync();
        await Assert.ThrowsAsync<ConflictException>(() => r.Service().MarkReadyAsync(reviewed.Id, default));

        var approved = await r.Service().ApproveAsync(reviewed.Id, new ApproveInput(reviewed.Review!.BundleHash, false), default);
        r.Job.Status = JobStatus.Expired;
        var ex = await Assert.ThrowsAsync<ConflictException>(() => r.Service().MarkReadyAsync(approved.Id, default));
        Assert.Contains("no longer open", ex.Message);
        r.Job.Status = JobStatus.Open;
        Assert.Equal("ReadyToSubmit", (await r.Service().MarkReadyAsync(approved.Id, default)).Status);
    }

    [Fact]
    public async Task Submission_cannot_be_recorded_before_approval_and_readiness()
    {
        var r = new Rig();
        var reviewed = await r.ReadyForReviewAsync();
        await Assert.ThrowsAsync<ConflictException>(() => r.Service().RecordSubmissionAsync(reviewed.Id, new SubmissionInput(true, null), default));
        var approved = await r.Service().ApproveAsync(reviewed.Id, new ApproveInput(reviewed.Review!.BundleHash, false), default);
        await Assert.ThrowsAsync<ConflictException>(() => r.Service().RecordSubmissionAsync(approved.Id, new SubmissionInput(true, null), default));
        Assert.Empty(r.Audit.Actions.Where(a => a.StartsWith("application.submitted")));
    }

    [Fact]
    public async Task An_owner_confirmed_submission_is_recorded_with_their_note()
    {
        var r = new Rig();
        var approved = await r.ApprovedAsync();
        await r.Service().MarkReadyAsync(approved.Id, default);
        var done = await r.Service().RecordSubmissionAsync(approved.Id, new SubmissionInput(true, "  Saw the confirmation page  "), default);
        Assert.Equal("Submitted", done.Status);
        Assert.Equal("Saw the confirmation page", done.SubmissionNote);
        Assert.NotNull(done.SubmittedAt);
        Assert.Contains("application.submitted.owner-confirmed", r.Audit.Actions);
    }

    [Fact]
    public async Task An_unsure_submission_stays_unconfirmed_until_the_owner_confirms()
    {
        var r = new Rig();
        var approved = await r.ApprovedAsync();
        await r.Service().MarkReadyAsync(approved.Id, default);
        var unsure = await r.Service().RecordSubmissionAsync(approved.Id, new SubmissionInput(false, new string('x', 900)), default);
        Assert.Equal("SubmissionUnconfirmed", unsure.Status);
        Assert.Null(unsure.SubmittedAt);
        Assert.Equal(500, unsure.SubmissionNote!.Length);
        Assert.Equal("Submitted", (await r.Service().ConfirmSubmissionAsync(approved.Id, default)).Status);
        await Assert.ThrowsAsync<ConflictException>(() => r.Service().ConfirmSubmissionAsync(approved.Id, default));
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("Submitted")]
    [InlineData("SubmissionUnconfirmed")]
    [InlineData("ReadyToSubmit")]
    [InlineData("PreparingApplication")]
    public async Task Generic_status_changes_cannot_skip_approval_or_fake_a_submission(string target)
    {
        var r = new Rig();
        var app = await r.Service().StartAsync(r.Job.Id, default);
        await Assert.ThrowsAsync<ConflictException>(() => r.Service().SetStatusAsync(app.Id, new StatusInput(target), default));
        Assert.Equal("Shortlisted", (await r.Service().GetAsync(app.Id, default)).Status);
    }

    [Fact]
    public async Task Status_input_is_validated_and_withdrawing_works_and_is_final()
    {
        var r = new Rig();
        var app = await r.Service().StartAsync(r.Job.Id, default);
        await Assert.ThrowsAsync<ValidationFailedException>(() => r.Service().SetStatusAsync(app.Id, new StatusInput("Nonsense"), default));
        await Assert.ThrowsAsync<ValidationFailedException>(() => r.Service().SetStatusAsync(app.Id, new StatusInput(null), default));
        Assert.Equal("Withdrawn", (await r.Service().SetStatusAsync(app.Id, new StatusInput("Withdrawn"), default)).Status);
        await Assert.ThrowsAsync<ConflictException>(() => r.Service().SetStatusAsync(app.Id, new StatusInput("Shortlisted"), default));
    }

    [Fact]
    public async Task After_submission_the_owner_can_track_the_outcome()
    {
        var r = new Rig();
        var approved = await r.ApprovedAsync();
        await r.Service().MarkReadyAsync(approved.Id, default);
        await r.Service().RecordSubmissionAsync(approved.Id, new SubmissionInput(true, null), default);
        Assert.Equal("InterviewScheduled", (await r.Service().SetStatusAsync(approved.Id, new StatusInput("InterviewScheduled"), default)).Status);
    }

    [Fact]
    public async Task Documents_for_a_job_are_listed_newest_first_with_staleness()
    {
        var r = new Rig();
        await r.DocService().CreateCvAsync(r.Job.Id, default);
        r.Profile.Version++;
        await r.LetterService().DraftAsync(r.Job.Id, default);
        var list = await r.Service().DocumentsForJobAsync(r.Job.Id, default);
        Assert.Equal(2, list.Count);
        Assert.Contains(list, d => d.Kind == "Cv" && d.Stale);
        Assert.Contains(list, d => d.Kind == "CoverLetter" && !d.Stale && d.Source == "ai:m");
        await Assert.ThrowsAsync<NotFoundException>(() => r.Service().DocumentsForJobAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Listing_shows_every_application_without_the_heavy_review()
    {
        var r = new Rig();
        await r.ReadyForReviewAsync();
        var list = await r.Service().ListAsync(default);
        var only = Assert.Single(list);
        Assert.Null(only.Review);
        Assert.Equal("AwaitingApproval", only.Status);
    }

    [Fact]
    public void The_bundle_hash_changes_with_any_part_and_is_stable()
    {
        var job = Guid.NewGuid();
        var a = ApplicationService.BundleHash(job, "cv1", "l1");
        Assert.Equal(a, ApplicationService.BundleHash(job, "cv1", "l1"));
        Assert.NotEqual(a, ApplicationService.BundleHash(job, "cv2", "l1"));
        Assert.NotEqual(a, ApplicationService.BundleHash(job, "cv1", "l2"));
        Assert.NotEqual(a, ApplicationService.BundleHash(job, "cv1", null));
        Assert.NotEqual(a, ApplicationService.BundleHash(Guid.NewGuid(), "cv1", "l1"));
    }

    [Fact]
    public void Only_the_application_service_can_touch_applications_and_it_cannot_reach_employers_or_models()
    {
        var assembly = typeof(ApplicationService).Assembly;
        var holders = assembly.GetTypes()
            .Where(t => t.GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(IApplicationStore))))
            .Select(t => t.Name).ToList();
        Assert.Equal(["ApplicationService"], holders);

        // Reporting gets a read-only view only.
        Assert.DoesNotContain(typeof(CareerPilot.Application.Tracking.TrackingService).GetConstructors().Single().GetParameters(), p => p.ParameterType == typeof(IApplicationStore));

        var deps = typeof(ApplicationService).GetConstructors().Single().GetParameters().Select(p => p.ParameterType).ToList();
        Assert.DoesNotContain(typeof(ILlmClient), deps);
        Assert.DoesNotContain(typeof(HttpClient), deps);
        Assert.DoesNotContain(deps, t => t.Name.Contains("Connector"));
    }
}
