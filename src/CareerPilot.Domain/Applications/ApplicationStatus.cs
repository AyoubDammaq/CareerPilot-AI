namespace CareerPilot.Domain.Applications;

public enum ApplicationStatus
{
    Discovered,
    UnderEvaluation,
    Shortlisted,
    AwaitingApproval,
    Approved,
    PreparingApplication,
    ReadyToSubmit,
    Submitted,
    SubmissionUnconfirmed,
    RejectedByEmployer,
    InterviewScheduled,
    InterviewCompleted,
    OfferReceived,
    OfferAccepted,
    OfferDeclined,
    Withdrawn,
    ClosedOrExpired
}

/// <summary>The allowed status transitions. Terminal states have an empty list.</summary>
public static class ApplicationWorkflow
{
    private static readonly Dictionary<ApplicationStatus, ApplicationStatus[]> Allowed = new()
    {
        [ApplicationStatus.Discovered] = [ApplicationStatus.UnderEvaluation, ApplicationStatus.Shortlisted, ApplicationStatus.Withdrawn, ApplicationStatus.ClosedOrExpired],
        [ApplicationStatus.UnderEvaluation] = [ApplicationStatus.Shortlisted, ApplicationStatus.Withdrawn, ApplicationStatus.ClosedOrExpired],
        [ApplicationStatus.Shortlisted] = [ApplicationStatus.AwaitingApproval, ApplicationStatus.Withdrawn, ApplicationStatus.ClosedOrExpired],
        [ApplicationStatus.AwaitingApproval] = [ApplicationStatus.Approved, ApplicationStatus.Shortlisted, ApplicationStatus.Withdrawn, ApplicationStatus.ClosedOrExpired],
        [ApplicationStatus.Approved] = [ApplicationStatus.PreparingApplication, ApplicationStatus.AwaitingApproval, ApplicationStatus.Withdrawn, ApplicationStatus.ClosedOrExpired],
        [ApplicationStatus.PreparingApplication] = [ApplicationStatus.ReadyToSubmit, ApplicationStatus.AwaitingApproval, ApplicationStatus.Withdrawn, ApplicationStatus.ClosedOrExpired],
        [ApplicationStatus.ReadyToSubmit] = [ApplicationStatus.Submitted, ApplicationStatus.SubmissionUnconfirmed, ApplicationStatus.AwaitingApproval, ApplicationStatus.Withdrawn, ApplicationStatus.ClosedOrExpired],
        [ApplicationStatus.SubmissionUnconfirmed] = [ApplicationStatus.Submitted, ApplicationStatus.Withdrawn, ApplicationStatus.ClosedOrExpired],
        [ApplicationStatus.Submitted] = [ApplicationStatus.RejectedByEmployer, ApplicationStatus.InterviewScheduled, ApplicationStatus.OfferReceived, ApplicationStatus.Withdrawn, ApplicationStatus.ClosedOrExpired],
        [ApplicationStatus.InterviewScheduled] = [ApplicationStatus.InterviewCompleted, ApplicationStatus.RejectedByEmployer, ApplicationStatus.Withdrawn],
        [ApplicationStatus.InterviewCompleted] = [ApplicationStatus.InterviewScheduled, ApplicationStatus.OfferReceived, ApplicationStatus.RejectedByEmployer, ApplicationStatus.Withdrawn],
        [ApplicationStatus.OfferReceived] = [ApplicationStatus.OfferAccepted, ApplicationStatus.OfferDeclined, ApplicationStatus.Withdrawn],
        [ApplicationStatus.RejectedByEmployer] = [],
        [ApplicationStatus.OfferAccepted] = [],
        [ApplicationStatus.OfferDeclined] = [],
        [ApplicationStatus.Withdrawn] = [],
        [ApplicationStatus.ClosedOrExpired] = [],
    };

    public static bool CanTransition(ApplicationStatus from, ApplicationStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static bool IsTerminal(ApplicationStatus status) =>
        Allowed.TryGetValue(status, out var targets) && targets.Length == 0;

    public static IReadOnlyCollection<ApplicationStatus> DefinedStatuses => Allowed.Keys;
}
