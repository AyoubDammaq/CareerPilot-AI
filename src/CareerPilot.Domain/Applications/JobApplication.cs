using System.Security.Cryptography;
using System.Text;
using CareerPilot.Domain.Common;

namespace CareerPilot.Domain.Applications;

/// <summary>
/// One application to one vacancy. The approval rules live here so that no caller
/// (API, worker, LLM-driven component) can bypass them:
///  - approval is recorded against a hash of the exact documents/answers reviewed;
///  - preparing/submitting requires an approval, and submitting requires that the
///    content hash still matches what was approved;
///  - Submitted/SubmissionUnconfirmed are reachable only through RecordSubmission.
/// Not yet mapped to the database; that arrives with the job model (Phase 2/5).
/// </summary>
public class JobApplication : Entity
{
    public JobApplication() { }

    public JobApplication(Guid canonicalJobId) => CanonicalJobId = canonicalJobId;

    public Guid CanonicalJobId { get; set; }
    public ApplicationStatus Status { get; private set; } = ApplicationStatus.Discovered;
    public string? ApprovedContentHash { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }

    public void TransitionTo(ApplicationStatus next)
    {
        if (next is ApplicationStatus.Approved)
            throw new DomainException("Use Approve() to approve an application.");
        if (next is ApplicationStatus.Submitted or ApplicationStatus.SubmissionUnconfirmed)
            throw new DomainException("Use RecordSubmission() or ConfirmSubmissionByUser() to record a submission.");
        if (!ApplicationWorkflow.CanTransition(Status, next))
            throw new DomainException($"Transition {Status} -> {next} is not allowed.");
        if (next is ApplicationStatus.PreparingApplication or ApplicationStatus.ReadyToSubmit && ApprovedContentHash is null)
            throw new DomainException($"Cannot move to {next} without an approval.");

        if (next == ApplicationStatus.AwaitingApproval)
            ClearApproval();

        Status = next;
        Touch();
    }

    public void Approve(string contentHash, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(contentHash))
            throw new DomainException("An approval must be bound to a content hash.");
        if (Status != ApplicationStatus.AwaitingApproval)
            throw new DomainException($"Only an application awaiting approval can be approved (current: {Status}).");

        ApprovedContentHash = contentHash;
        ApprovedAt = now;
        Status = ApplicationStatus.Approved;
        Touch();
    }

    /// <summary>Call when the reviewed content (CV, letter, answers) changed after approval.</summary>
    public void InvalidateApproval()
    {
        if (Status is not (ApplicationStatus.Approved or ApplicationStatus.PreparingApplication or ApplicationStatus.ReadyToSubmit))
            return;

        ClearApproval();
        Status = ApplicationStatus.AwaitingApproval;
        Touch();
    }

    /// <summary>Throws unless an approval exists and matches the content about to be sent.</summary>
    public void EnsureApprovedFor(string currentContentHash)
    {
        if (ApprovedContentHash is null)
            throw new DomainException("The application has not been approved.");

        var approved = Encoding.UTF8.GetBytes(ApprovedContentHash);
        var current = Encoding.UTF8.GetBytes(currentContentHash ?? "");
        if (!CryptographicOperations.FixedTimeEquals(approved, current))
            throw new DomainException("The content changed after approval; it must be reviewed and approved again.");
    }

    /// <param name="confirmed">True only when there is real evidence (confirmation page, verifiable response).</param>
    public void RecordSubmission(string currentContentHash, bool confirmed, DateTimeOffset now)
    {
        if (Status != ApplicationStatus.ReadyToSubmit)
            throw new DomainException($"Only a ready application can be submitted (current: {Status}).");
        EnsureApprovedFor(currentContentHash);

        if (confirmed)
        {
            Status = ApplicationStatus.Submitted;
            SubmittedAt = now;
        }
        else
        {
            Status = ApplicationStatus.SubmissionUnconfirmed;
        }
        Touch();
    }

    /// <summary>The owner verified the submission themselves (user-reported confirmation).</summary>
    public void ConfirmSubmissionByUser(DateTimeOffset now)
    {
        if (Status != ApplicationStatus.SubmissionUnconfirmed)
            throw new DomainException($"Only an unconfirmed submission can be confirmed (current: {Status}).");

        Status = ApplicationStatus.Submitted;
        SubmittedAt = now;
        Touch();
    }

    private void ClearApproval()
    {
        ApprovedContentHash = null;
        ApprovedAt = null;
    }
}
