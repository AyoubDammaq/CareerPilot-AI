using CareerPilot.Domain.Applications;
using CareerPilot.Domain.Common;

namespace CareerPilot.Tests.Domain;

/// <summary>
/// Proves the "never apply without approval" rule at the domain level, independent of any UI or LLM.
/// </summary>
public class JobApplicationApprovalTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);
    private const string Hash = "hash-of-reviewed-content";

    private static JobApplication Awaiting()
    {
        var app = new JobApplication(Guid.NewGuid());
        app.TransitionTo(ApplicationStatus.Shortlisted);
        app.TransitionTo(ApplicationStatus.AwaitingApproval);
        return app;
    }

    private static JobApplication Ready()
    {
        var app = Awaiting();
        app.Approve(Hash, Now);
        app.TransitionTo(ApplicationStatus.PreparingApplication);
        app.TransitionTo(ApplicationStatus.ReadyToSubmit);
        return app;
    }

    [Fact]
    public void A_new_application_cannot_be_approved_before_it_awaits_approval()
    {
        var app = new JobApplication(Guid.NewGuid());
        Assert.Throws<DomainException>(() => app.Approve(Hash, Now));
        Assert.Null(app.ApprovedContentHash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Approval_must_be_bound_to_a_content_hash(string hash)
    {
        var app = Awaiting();
        Assert.Throws<DomainException>(() => app.Approve(hash, Now));
        Assert.Equal(ApplicationStatus.AwaitingApproval, app.Status);
    }

    [Fact]
    public void Approval_records_hash_and_time()
    {
        var app = Awaiting();
        app.Approve(Hash, Now);
        Assert.Equal(ApplicationStatus.Approved, app.Status);
        Assert.Equal(Hash, app.ApprovedContentHash);
        Assert.Equal(Now, app.ApprovedAt);
    }

    [Fact]
    public void Approved_cannot_be_reached_through_TransitionTo()
    {
        var app = Awaiting();
        Assert.Throws<DomainException>(() => app.TransitionTo(ApplicationStatus.Approved));
        Assert.Equal(ApplicationStatus.AwaitingApproval, app.Status);
    }

    [Theory]
    [InlineData(ApplicationStatus.Submitted)]
    [InlineData(ApplicationStatus.SubmissionUnconfirmed)]
    public void Submission_states_cannot_be_reached_through_TransitionTo(ApplicationStatus target)
    {
        var app = Ready();
        Assert.Throws<DomainException>(() => app.TransitionTo(target));
        Assert.Equal(ApplicationStatus.ReadyToSubmit, app.Status);
    }

    [Theory]
    [InlineData(ApplicationStatus.PreparingApplication)]
    [InlineData(ApplicationStatus.ReadyToSubmit)]
    public void Preparing_or_readying_is_impossible_without_approval(ApplicationStatus target)
    {
        var app = Awaiting();
        Assert.Throws<DomainException>(() => app.TransitionTo(target));
        Assert.Equal(ApplicationStatus.AwaitingApproval, app.Status);
    }

    [Fact]
    public void Happy_path_ends_in_Submitted_with_a_timestamp()
    {
        var app = Ready();
        app.RecordSubmission(Hash, confirmed: true, Now.AddMinutes(5));
        Assert.Equal(ApplicationStatus.Submitted, app.Status);
        Assert.Equal(Now.AddMinutes(5), app.SubmittedAt);
    }

    [Fact]
    public void Submission_is_refused_when_content_changed_after_approval()
    {
        var app = Ready();
        Assert.Throws<DomainException>(() => app.RecordSubmission("a-different-hash", confirmed: true, Now));
        Assert.Equal(ApplicationStatus.ReadyToSubmit, app.Status);
        Assert.Null(app.SubmittedAt);
    }

    [Fact]
    public void Submission_is_refused_unless_the_application_is_ready()
    {
        var app = Awaiting();
        app.Approve(Hash, Now);
        Assert.Throws<DomainException>(() => app.RecordSubmission(Hash, confirmed: true, Now));
        Assert.Equal(ApplicationStatus.Approved, app.Status);
    }

    [Fact]
    public void Submitting_twice_is_refused()
    {
        var app = Ready();
        app.RecordSubmission(Hash, confirmed: true, Now);
        Assert.Throws<DomainException>(() => app.RecordSubmission(Hash, confirmed: true, Now));
    }

    [Fact]
    public void An_unconfirmed_submission_is_not_recorded_as_submitted()
    {
        var app = Ready();
        app.RecordSubmission(Hash, confirmed: false, Now);
        Assert.Equal(ApplicationStatus.SubmissionUnconfirmed, app.Status);
        Assert.Null(app.SubmittedAt);
    }

    [Fact]
    public void The_owner_can_confirm_an_unconfirmed_submission()
    {
        var app = Ready();
        app.RecordSubmission(Hash, confirmed: false, Now);
        app.ConfirmSubmissionByUser(Now.AddHours(1));
        Assert.Equal(ApplicationStatus.Submitted, app.Status);
        Assert.Equal(Now.AddHours(1), app.SubmittedAt);
    }

    [Fact]
    public void Owner_confirmation_is_only_possible_for_unconfirmed_submissions()
    {
        var app = Ready();
        Assert.Throws<DomainException>(() => app.ConfirmSubmissionByUser(Now));
        Assert.Equal(ApplicationStatus.ReadyToSubmit, app.Status);
    }

    [Theory]
    [InlineData(ApplicationStatus.Approved)]
    [InlineData(ApplicationStatus.PreparingApplication)]
    [InlineData(ApplicationStatus.ReadyToSubmit)]
    public void Editing_reviewed_content_revokes_the_approval(ApplicationStatus stage)
    {
        var app = Ready();
        if (stage == ApplicationStatus.Approved)
        {
            app = Awaiting();
            app.Approve(Hash, Now);
        }
        else if (stage == ApplicationStatus.PreparingApplication)
        {
            app = Awaiting();
            app.Approve(Hash, Now);
            app.TransitionTo(ApplicationStatus.PreparingApplication);
        }

        app.InvalidateApproval();

        Assert.Equal(ApplicationStatus.AwaitingApproval, app.Status);
        Assert.Null(app.ApprovedContentHash);
        Assert.Null(app.ApprovedAt);
        Assert.Throws<DomainException>(() => app.EnsureApprovedFor(Hash));
    }

    [Fact]
    public void Invalidating_a_submitted_application_does_nothing()
    {
        var app = Ready();
        app.RecordSubmission(Hash, confirmed: true, Now);
        app.InvalidateApproval();
        Assert.Equal(ApplicationStatus.Submitted, app.Status);
    }

    [Fact]
    public void Returning_to_AwaitingApproval_clears_the_previous_approval()
    {
        var app = Awaiting();
        app.Approve(Hash, Now);
        app.TransitionTo(ApplicationStatus.AwaitingApproval);
        Assert.Null(app.ApprovedContentHash);
        Assert.Throws<DomainException>(() => app.EnsureApprovedFor(Hash));
    }

    [Fact]
    public void EnsureApprovedFor_fails_when_never_approved()
    {
        var app = Awaiting();
        Assert.Throws<DomainException>(() => app.EnsureApprovedFor(Hash));
    }

    [Fact]
    public void A_withdrawn_application_cannot_be_revived()
    {
        var app = Awaiting();
        app.TransitionTo(ApplicationStatus.Withdrawn);
        Assert.Throws<DomainException>(() => app.TransitionTo(ApplicationStatus.AwaitingApproval));
    }
}
