using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CareerPilot.Application.Abstractions;
using CareerPilot.Application.Common;
using CareerPilot.Application.Documents;
using CareerPilot.Domain.Applications;
using CareerPilot.Domain.Common;
using CareerPilot.Domain.Documents;
using CareerPilot.Domain.Jobs;

namespace CareerPilot.Application.Applications;

public interface IApplicationStore
{
    void Add(JobApplication application);
    Task<JobApplication?> GetAsync(Guid id, CancellationToken ct);
    Task<JobApplication?> GetByJobAsync(Guid jobId, CancellationToken ct);
    Task<List<JobApplication>> ListAsync(CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public sealed record SetDocumentsInput(Guid? CvDocumentId, Guid? CoverLetterDocumentId);
public sealed record ApproveInput(string? BundleHash, bool AcknowledgeFlags);
public sealed record SubmissionInput(bool Confirmed, string? Note);
public sealed record StatusInput(string? Status);

public sealed record ApplicationReview(
    string? BundleHash, CvContent? Cv, CoverLetterContent? Letter, ClaimCheck? Claims, List<string> Blockers, bool DocumentsStale);

public sealed record ApplicationDto(
    Guid Id, Guid JobId, string JobTitle, string CompanyName, string ApplyUrl, string JobStatus, string Status,
    Guid? CvDocumentId, Guid? CoverLetterDocumentId, DateTimeOffset CreatedAt, DateTimeOffset? ApprovedAt, DateTimeOffset? SubmittedAt,
    string? SubmissionNote, string? ApprovedBundleHash, List<string> AllowedNext, ApplicationReview? Review);

public sealed record DocumentSummaryDto(Guid Id, string Kind, string Title, DateTimeOffset CreatedAt, string ContentHash, bool Stale, string? Source);

/// <summary>
/// The only place an application moves toward submission. Rules enforced here and in the domain, never in the UI:
/// approval is bound to the exact documents reviewed, is withdrawn when they change, and submission is only ever
/// recorded from what the owner reports. No component here can contact an employer.
/// </summary>
public sealed class ApplicationService(
    IApplicationStore apps, IJobStore jobs, IProfileStore profiles, IDocumentStore docs, TimeProvider clock, IAuditLog audit)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>One fingerprint for everything the owner reviews: the target posting plus the exact CV and letter.</summary>
    public static string BundleHash(Guid jobId, string cvHash, string? letterHash) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"job:{jobId}|cv:{cvHash}|letter:{letterHash ?? "none"}"))).ToLowerInvariant();

    public async Task<ApplicationDto> StartAsync(Guid jobId, CancellationToken ct)
    {
        var job = await jobs.GetListingAsync(jobId, ct) ?? throw new NotFoundException("Job not found.");
        var existing = await apps.GetByJobAsync(jobId, ct);
        if (existing is not null) return await ToDtoAsync(existing, job, ct);

        var app = new JobApplication(jobId);
        Run(() => app.TransitionTo(ApplicationStatus.Shortlisted));
        apps.Add(app);
        audit.Stage("owner", "application.started", nameof(JobApplication), app.Id, $"job {jobId}");
        await apps.SaveChangesAsync(ct);
        return await ToDtoAsync(app, job, ct);
    }

    public async Task<List<ApplicationDto>> ListAsync(CancellationToken ct)
    {
        var all = await apps.ListAsync(ct);
        var listings = (await jobs.GetAllListingsAsync(ct)).ToDictionary(l => l.Id);
        var result = new List<ApplicationDto>();
        foreach (var a in all.OrderByDescending(a => a.UpdatedAt))
            if (listings.TryGetValue(a.CanonicalJobId, out var job)) result.Add(await ToDtoAsync(a, job, ct, withReview: false));
        return result;
    }

    public async Task<ApplicationDto> GetAsync(Guid id, CancellationToken ct)
    {
        var (app, job) = await LoadAsync(id, ct);
        return await ToDtoAsync(app, job, ct);
    }

    public async Task<List<DocumentSummaryDto>> DocumentsForJobAsync(Guid jobId, CancellationToken ct)
    {
        _ = await jobs.GetListingAsync(jobId, ct) ?? throw new NotFoundException("Job not found.");
        var profile = await profiles.GetAsync(ct);
        var list = await docs.ListForJobAsync(jobId, ct);
        return [.. list.OrderByDescending(d => d.CreatedAt).Select(d => new DocumentSummaryDto(
            d.Id, d.Kind.ToString(), d.Title, d.CreatedAt, d.ContentHash, profile is not null && profile.Version != d.ProfileVersion, SourceOf(d)))];
    }

    public async Task<ApplicationDto> SetDocumentsAsync(Guid id, SetDocumentsInput input, CancellationToken ct)
    {
        var (app, job) = await LoadAsync(id, ct);
        if (input.CvDocumentId is not { } cvId)
            throw new ValidationFailedException(new Dictionary<string, string[]> { ["cvDocumentId"] = ["Choose a CV."] });

        var cv = await docs.GetAsync(cvId, ct);
        if (cv is null || cv.Kind != DocumentKind.Cv || cv.JobListingId != job.Id)
            throw new ValidationFailedException(new Dictionary<string, string[]> { ["cvDocumentId"] = ["Choose a CV prepared for this job."] });
        if (input.CoverLetterDocumentId is { } letterId)
        {
            var letter = await docs.GetAsync(letterId, ct);
            if (letter is null || letter.Kind != DocumentKind.CoverLetter || letter.JobListingId != job.Id)
                throw new ValidationFailedException(new Dictionary<string, string[]> { ["coverLetterDocumentId"] = ["Choose a cover letter prepared for this job."] });
        }

        var before = app.Status;
        Run(() => app.SetDocuments(cvId, input.CoverLetterDocumentId));
        audit.Stage("owner", before != app.Status ? "application.documents.changed.approval-withdrawn" : "application.documents.set", nameof(JobApplication), app.Id, null);
        await apps.SaveChangesAsync(ct);
        return await ToDtoAsync(app, job, ct);
    }

    public async Task<ApplicationDto> RequestReviewAsync(Guid id, CancellationToken ct)
    {
        var (app, job) = await LoadAsync(id, ct);
        if (app.CvDocumentId is null) throw new ConflictException("Choose a CV before asking for review.");
        if (app.Status != ApplicationStatus.AwaitingApproval)
            Run(() => app.TransitionTo(ApplicationStatus.AwaitingApproval));
        audit.Stage("owner", "application.review.requested", nameof(JobApplication), app.Id, null);
        await apps.SaveChangesAsync(ct);
        return await ToDtoAsync(app, job, ct);
    }

    public async Task<ApplicationDto> ApproveAsync(Guid id, ApproveInput input, CancellationToken ct)
    {
        var (app, job) = await LoadAsync(id, ct);
        if (app.Status != ApplicationStatus.AwaitingApproval)
            throw new ConflictException($"Only an application awaiting approval can be approved (it is {app.Status}).");

        var review = await BuildReviewAsync(app, job, ct);
        if (review.Blockers.Count > 0)
            throw new ConflictException("Cannot approve yet: " + string.Join(" ", review.Blockers));
        if (!string.Equals(input.BundleHash, review.BundleHash, StringComparison.Ordinal))
            throw new ConflictException("What you reviewed is not what is attached now. Reload and review the current documents.");
        if (review.Claims is { Clean: false } && !input.AcknowledgeFlags)
            throw new ConflictException("The cover letter has unsupported claims flagged. Fix them, or confirm that you accept them.");

        Run(() => app.Approve(review.BundleHash!, clock.GetUtcNow()));
        audit.Stage("owner", "application.approved", nameof(JobApplication), app.Id,
            $"bundle {review.BundleHash![..12]}, flags {(review.Claims?.Flags.Count ?? 0)}{(input.AcknowledgeFlags && review.Claims is { Clean: false } ? " acknowledged" : "")}");
        await apps.SaveChangesAsync(ct);
        return await ToDtoAsync(app, job, ct);
    }

    /// <summary>The owner takes an approval back (for example to edit a document).</summary>
    public async Task<ApplicationDto> WithdrawApprovalAsync(Guid id, CancellationToken ct)
    {
        var (app, job) = await LoadAsync(id, ct);
        if (app.ApprovedContentHash is null) throw new ConflictException("There is no approval to withdraw.");
        app.InvalidateApproval();
        audit.Stage("owner", "application.approval.withdrawn", nameof(JobApplication), app.Id, null);
        await apps.SaveChangesAsync(ct);
        return await ToDtoAsync(app, job, ct);
    }

    /// <summary>Approved to ready: the owner may now submit on the employer's site themselves.</summary>
    public async Task<ApplicationDto> MarkReadyAsync(Guid id, CancellationToken ct)
    {
        var (app, job) = await LoadAsync(id, ct);
        if (app.Status != ApplicationStatus.Approved)
            throw new ConflictException($"Only an approved application can be made ready (it is {app.Status}).");
        if (job.Status is JobStatus.Closed or JobStatus.Expired)
            throw new ConflictException("This posting is no longer open.");

        var hash = await CurrentHashAsync(app, ct);
        Run(() => app.EnsureApprovedFor(hash));
        Run(() => app.TransitionTo(ApplicationStatus.PreparingApplication));
        Run(() => app.TransitionTo(ApplicationStatus.ReadyToSubmit));
        audit.Stage("owner", "application.ready", nameof(JobApplication), app.Id, null);
        await apps.SaveChangesAsync(ct);
        return await ToDtoAsync(app, job, ct);
    }

    /// <summary>Records what the owner says happened. Confirmed means they saw the employer's confirmation.</summary>
    public async Task<ApplicationDto> RecordSubmissionAsync(Guid id, SubmissionInput input, CancellationToken ct)
    {
        var (app, job) = await LoadAsync(id, ct);
        var hash = await CurrentHashAsync(app, ct);
        Run(() => app.RecordSubmission(hash, input.Confirmed, clock.GetUtcNow(), input.Note));
        audit.Stage("owner", input.Confirmed ? "application.submitted.owner-confirmed" : "application.submitted.unconfirmed", nameof(JobApplication), app.Id, null);
        await apps.SaveChangesAsync(ct);
        return await ToDtoAsync(app, job, ct);
    }

    public async Task<ApplicationDto> ConfirmSubmissionAsync(Guid id, CancellationToken ct)
    {
        var (app, job) = await LoadAsync(id, ct);
        Run(() => app.ConfirmSubmissionByUser(clock.GetUtcNow()));
        audit.Stage("owner", "application.submitted.owner-confirmed", nameof(JobApplication), app.Id, "after unconfirmed");
        await apps.SaveChangesAsync(ct);
        return await ToDtoAsync(app, job, ct);
    }

    /// <summary>Other moves (withdraw, interview, offer...) follow the domain's allowed transitions.</summary>
    public async Task<ApplicationDto> SetStatusAsync(Guid id, StatusInput input, CancellationToken ct)
    {
        var (app, job) = await LoadAsync(id, ct);
        if (!Enum.TryParse<ApplicationStatus>(input.Status, ignoreCase: false, out var next) || !Enum.IsDefined(next))
            throw new ValidationFailedException(new Dictionary<string, string[]> { ["status"] = ["Unknown status."] });
        var from = app.Status;
        Run(() => app.TransitionTo(next));
        audit.Stage("owner", "application.status", nameof(JobApplication), app.Id, $"{from} -> {next}");
        await apps.SaveChangesAsync(ct);
        return await ToDtoAsync(app, job, ct);
    }

    // ---- internals ----------------------------------------------------------------------

    private async Task<(JobApplication, JobListing)> LoadAsync(Guid id, CancellationToken ct)
    {
        var app = await apps.GetAsync(id, ct) ?? throw new NotFoundException("Application not found.");
        var job = await jobs.GetListingAsync(app.CanonicalJobId, ct) ?? throw new NotFoundException("Job not found.");
        return (app, job);
    }

    /// <summary>Turns domain rule violations into conflicts the owner can read.</summary>
    private static void Run(Action action)
    {
        try { action(); }
        catch (DomainException ex) { throw new ConflictException(ex.Message); }
    }

    private async Task<string> CurrentHashAsync(JobApplication app, CancellationToken ct)
    {
        if (app.CvDocumentId is not { } cvId) throw new ConflictException("No CV is attached.");
        var cv = await docs.GetAsync(cvId, ct) ?? throw new ConflictException("The attached CV no longer exists.");
        string? letterHash = null;
        if (app.CoverLetterDocumentId is { } lid)
            letterHash = (await docs.GetAsync(lid, ct) ?? throw new ConflictException("The attached cover letter no longer exists.")).ContentHash;
        return BundleHash(app.CanonicalJobId, cv.ContentHash, letterHash);
    }

    private async Task<ApplicationReview> BuildReviewAsync(JobApplication app, JobListing job, CancellationToken ct)
    {
        var blockers = new List<string>();
        if (app.CvDocumentId is not { } cvId)
            return new ApplicationReview(null, null, null, null, ["Choose a CV."], false);

        var cvDoc = await docs.GetAsync(cvId, ct);
        if (cvDoc is null) return new ApplicationReview(null, null, null, null, ["The attached CV no longer exists."], false);
        var cv = JsonSerializer.Deserialize<CvContent>(cvDoc.ContentJson, Json);

        GeneratedDocument? letterDoc = null;
        CoverLetterContent? letter = null;
        if (app.CoverLetterDocumentId is { } lid)
        {
            letterDoc = await docs.GetAsync(lid, ct);
            if (letterDoc is null) blockers.Add("The attached cover letter no longer exists.");
            else letter = JsonSerializer.Deserialize<CoverLetterContent>(letterDoc.ContentJson, Json);
        }

        var profile = await profiles.GetAsync(ct);
        var stale = profile is not null && (profile.Version != cvDoc.ProfileVersion || (letterDoc is not null && profile.Version != letterDoc.ProfileVersion));
        if (stale) blockers.Add("Your profile changed after these documents were prepared. Prepare them again so they reflect it.");

        if (cv is null || string.IsNullOrWhiteSpace(cv.FullName) || string.IsNullOrWhiteSpace(cv.Email))
            blockers.Add("The CV has no name or email. Complete your profile and prepare the CV again.");
        else if (cv.Experience.Count == 0 && cv.Skills.Count == 0 && cv.Projects.Count == 0)
            blockers.Add("The CV has no verified experience, skills or projects. Verify your facts and prepare it again.");

        if (job.Status is JobStatus.Closed or JobStatus.Expired)
            blockers.Add("This posting is no longer open.");

        ClaimCheck? claims = null;
        if (letter is not null && profile is not null)
            claims = ClaimChecker.Check(string.Join("\n", letter.Paragraphs), profile, clock.GetUtcNow());

        var hash = BundleHash(job.Id, cvDoc.ContentHash, letterDoc?.ContentHash);
        return new ApplicationReview(hash, cv, letter, claims, blockers, stale);
    }

    private async Task<ApplicationDto> ToDtoAsync(JobApplication app, JobListing job, CancellationToken ct, bool withReview = true)
    {
        var primary = job.Postings.FirstOrDefault(p => p.IsPresent) ?? job.Postings.FirstOrDefault();
        var review = withReview && app.CvDocumentId is not null && app.Status is not (ApplicationStatus.Withdrawn or ApplicationStatus.ClosedOrExpired)
            ? await BuildReviewAsync(app, job, ct) : null;
        var next = ApplicationWorkflow.DefinedStatuses
            .Where(s => ApplicationWorkflow.CanTransition(app.Status, s) && s is not (ApplicationStatus.Approved or ApplicationStatus.Submitted or ApplicationStatus.SubmissionUnconfirmed))
            .Select(s => s.ToString()).ToList();
        return new ApplicationDto(
            app.Id, job.Id, job.Title, job.CompanyName, primary?.ApplyUrl ?? primary?.SourceUrl ?? "", job.Status.ToString(), app.Status.ToString(),
            app.CvDocumentId, app.CoverLetterDocumentId, app.CreatedAt, app.ApprovedAt, app.SubmittedAt, app.SubmissionNote,
            app.ApprovedContentHash, next, review);
    }

    private static string? SourceOf(GeneratedDocument d)
    {
        if (d.Kind != DocumentKind.CoverLetter) return null;
        try { return JsonSerializer.Deserialize<CoverLetterContent>(d.ContentJson, Json)?.Source; }
        catch (JsonException) { return null; }
    }
}
