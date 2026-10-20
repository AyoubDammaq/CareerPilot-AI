using CareerPilot.Domain.Applications;

namespace CareerPilot.Tests.Domain;

public class ApplicationWorkflowTests
{
    private static readonly ApplicationStatus[] All = Enum.GetValues<ApplicationStatus>();

    [Fact]
    public void Every_status_has_a_transition_definition() =>
        Assert.Equal(All.Length, ApplicationWorkflow.DefinedStatuses.Count);

    [Theory]
    [InlineData(ApplicationStatus.RejectedByEmployer)]
    [InlineData(ApplicationStatus.OfferAccepted)]
    [InlineData(ApplicationStatus.OfferDeclined)]
    [InlineData(ApplicationStatus.Withdrawn)]
    [InlineData(ApplicationStatus.ClosedOrExpired)]
    public void Terminal_states_allow_no_transitions(ApplicationStatus status)
    {
        Assert.True(ApplicationWorkflow.IsTerminal(status));
        foreach (var to in All)
            Assert.False(ApplicationWorkflow.CanTransition(status, to));
    }

    [Fact]
    public void No_status_transitions_to_itself()
    {
        foreach (var s in All)
            Assert.False(ApplicationWorkflow.CanTransition(s, s), $"{s} -> {s}");
    }

    [Fact]
    public void Approved_is_reachable_only_from_AwaitingApproval() =>
        AssertOnlyReachableFrom(ApplicationStatus.Approved, ApplicationStatus.AwaitingApproval);

    [Fact]
    public void PreparingApplication_is_reachable_only_from_Approved() =>
        AssertOnlyReachableFrom(ApplicationStatus.PreparingApplication, ApplicationStatus.Approved);

    [Fact]
    public void ReadyToSubmit_is_reachable_only_from_PreparingApplication() =>
        AssertOnlyReachableFrom(ApplicationStatus.ReadyToSubmit, ApplicationStatus.PreparingApplication);

    [Fact]
    public void Submitted_is_reachable_only_from_ReadyToSubmit_or_SubmissionUnconfirmed() =>
        AssertOnlyReachableFrom(ApplicationStatus.Submitted, ApplicationStatus.ReadyToSubmit, ApplicationStatus.SubmissionUnconfirmed);

    [Fact]
    public void SubmissionUnconfirmed_is_reachable_only_from_ReadyToSubmit() =>
        AssertOnlyReachableFrom(ApplicationStatus.SubmissionUnconfirmed, ApplicationStatus.ReadyToSubmit);

    [Fact]
    public void A_submitted_application_cannot_be_submitted_again()
    {
        Assert.False(ApplicationWorkflow.CanTransition(ApplicationStatus.Submitted, ApplicationStatus.Submitted));
        Assert.False(ApplicationWorkflow.CanTransition(ApplicationStatus.Submitted, ApplicationStatus.ReadyToSubmit));
    }

    private static void AssertOnlyReachableFrom(ApplicationStatus target, params ApplicationStatus[] allowedSources)
    {
        foreach (var from in All)
        {
            var expected = allowedSources.Contains(from);
            Assert.True(expected == ApplicationWorkflow.CanTransition(from, target),
                $"{from} -> {target} should be {(expected ? "allowed" : "forbidden")}.");
        }
    }
}
