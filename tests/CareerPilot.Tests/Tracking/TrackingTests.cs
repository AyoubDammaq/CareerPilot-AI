using CareerPilot.Application.Tracking;
using CareerPilot.Domain.Applications;
using CareerPilot.Domain.Jobs;

namespace CareerPilot.Tests.Tracking;

public class TrackingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly TrackingOptions Opt = new();

    private static TrackingRow Row(ApplicationStatus status, int? daysSinceSubmitted = null, int? daysToReply = null, int? score = null,
        string country = "France", int? deadlineInDays = null, JobStatus job = JobStatus.Open, int? approvedDaysAgo = null, int updatedDaysAgo = 0)
    {
        DateTimeOffset? submitted = daysSinceSubmitted is { } d ? Now.AddDays(-d) : null;
        DateTimeOffset? replied = submitted is { } s && daysToReply is { } r ? s.AddDays(r) : null;
        return new TrackingRow(Guid.NewGuid(), "Engineer", "Acme", country, status, Now.AddDays(-30), Now.AddDays(-updatedDaysAgo),
            approvedDaysAgo is { } a ? Now.AddDays(-a) : null, submitted, replied, score, deadlineInDays is { } dl ? Now.AddDays(dl) : null, job);
    }

    private static List<Reminder> Rem(params TrackingRow[] rows) => TrackingCalculator.Reminders(rows, Now, Opt);

    [Fact]
    public void A_submitted_application_with_no_reply_gets_a_follow_up_only_after_the_set_number_of_days()
    {
        Assert.Empty(Rem(Row(ApplicationStatus.Submitted, daysSinceSubmitted: 6)));
        var r = Assert.Single(Rem(Row(ApplicationStatus.Submitted, daysSinceSubmitted: 7)));
        Assert.Equal("FollowUp", r.Kind);
        Assert.Equal(7, r.Days);
    }

    [Fact]
    public void A_reply_stops_the_follow_up_reminder()
    {
        Assert.Empty(Rem(Row(ApplicationStatus.InterviewScheduled, daysSinceSubmitted: 20, daysToReply: 3, updatedDaysAgo: 1)));
    }

    [Fact]
    public void An_unconfirmed_submission_always_asks_for_confirmation()
    {
        Assert.Equal("ConfirmSubmission", Assert.Single(Rem(Row(ApplicationStatus.SubmissionUnconfirmed))).Kind);
    }

    [Fact]
    public void Approved_but_not_submitted_for_too_long_is_flagged()
    {
        Assert.Empty(Rem(Row(ApplicationStatus.ReadyToSubmit, approvedDaysAgo: 4)));
        Assert.Equal("FinishSubmission", Assert.Single(Rem(Row(ApplicationStatus.ReadyToSubmit, approvedDaysAgo: 5))).Kind);
    }

    [Fact]
    public void Deadlines_warn_before_and_after_and_only_before_submission()
    {
        Assert.Empty(Rem(Row(ApplicationStatus.Shortlisted, deadlineInDays: 10)));
        Assert.Equal("Deadline", Assert.Single(Rem(Row(ApplicationStatus.Shortlisted, deadlineInDays: 2))).Kind);
        Assert.Equal("DeadlinePassed", Assert.Single(Rem(Row(ApplicationStatus.AwaitingApproval, deadlineInDays: -2))).Kind);
        Assert.Empty(Rem(Row(ApplicationStatus.Submitted, daysSinceSubmitted: 1, deadlineInDays: -2)));
    }

    [Fact]
    public void A_closed_posting_is_flagged_only_before_submission()
    {
        Assert.Equal("JobClosed", Assert.Single(Rem(Row(ApplicationStatus.Shortlisted, job: JobStatus.Closed))).Kind);
        Assert.Empty(Rem(Row(ApplicationStatus.Submitted, daysSinceSubmitted: 1, job: JobStatus.Closed)));
    }

    [Fact]
    public void Quiet_interviews_prompt_a_status_update_and_finished_applications_never_remind()
    {
        Assert.Equal("UpdateStatus", Assert.Single(Rem(Row(ApplicationStatus.InterviewCompleted, daysSinceSubmitted: 30, daysToReply: 2, updatedDaysAgo: 9))).Kind);
        Assert.Empty(Rem(Row(ApplicationStatus.RejectedByEmployer, daysSinceSubmitted: 30, daysToReply: 2, updatedDaysAgo: 20),
                          Row(ApplicationStatus.Withdrawn, updatedDaysAgo: 40), Row(ApplicationStatus.ClosedOrExpired, updatedDaysAgo: 40)));
    }

    [Fact]
    public void Reminders_are_ordered_by_urgency()
    {
        var list = Rem(Row(ApplicationStatus.Submitted, daysSinceSubmitted: 30), Row(ApplicationStatus.Shortlisted, deadlineInDays: 1));
        Assert.Equal(["Deadline", "FollowUp"], list.Select(r => r.Kind));
    }

    [Fact]
    public void An_empty_history_gives_zeroes_and_an_honest_note_not_invented_rates()
    {
        var s = TrackingCalculator.Summary([], Now, Opt);
        Assert.Equal(0, s.Total);
        Assert.Null(s.ResponseRate);
        Assert.Null(s.MedianDaysToResponse);
        Assert.Contains(s.Notes, n => n.Contains("No applications yet"));
        Assert.Equal(8, s.Weekly.Count);
    }

    [Fact]
    public void Response_rate_and_median_days_come_only_from_recorded_replies()
    {
        TrackingRow[] rows =
        [
            Row(ApplicationStatus.InterviewScheduled, 20, 4, score: 80),
            Row(ApplicationStatus.RejectedByEmployer, 20, 10, score: 75),
            Row(ApplicationStatus.Submitted, 15, score: 55),
            Row(ApplicationStatus.Submitted, 12, score: 40, country: "Belgium"),
            Row(ApplicationStatus.Shortlisted),
            Row(ApplicationStatus.Withdrawn),
        ];
        var s = TrackingCalculator.Summary(rows, Now, Opt);
        Assert.Equal(6, s.Total);
        Assert.Equal(4, s.Submitted);
        Assert.Equal(2, s.Replied);
        Assert.Equal(50, s.ResponseRate);
        Assert.Equal(7, s.MedianDaysToResponse);
        Assert.Equal(1, s.Rejected);
        Assert.Equal(1, s.Interviewing);
        Assert.Equal(4, s.Active); // Shortlisted + 2 Submitted + Interview; rejected and withdrawn are terminal
        Assert.True(s.SmallSample);
        Assert.Contains(s.Notes, n => n.Contains("too few"));
        var high = s.ByScoreBand.Single(b => b.Name == "70 to 100");
        Assert.Equal((2, 2), (high.Submitted, high.Replied));
        Assert.Equal(0, s.ByScoreBand.Single(b => b.Name == "Under 50").Replied);
        Assert.Equal("France", s.ByCountry[0].Name);
    }

    [Fact]
    public void Weekly_counts_cover_eight_weeks_starting_on_monday()
    {
        var s = TrackingCalculator.Summary([Row(ApplicationStatus.Submitted, 1), Row(ApplicationStatus.Submitted, 2), Row(ApplicationStatus.Submitted, 40)], Now, Opt);
        Assert.Equal(8, s.Weekly.Count);
        Assert.All(s.Weekly, w => Assert.Equal(DayOfWeek.Monday, w.WeekStart.DayOfWeek));
        Assert.Equal(2, s.Weekly[^1].Submitted); // 2026-10-22 is a Thursday: both fall in the week of 10-19
        Assert.Equal(1, s.Weekly.Sum(w => w.Submitted) - 2); // the 40-day-old one is within eight weeks
    }

    [Fact]
    public void Only_the_first_reply_is_timestamped_and_only_from_submitted()
    {
        var a = new JobApplication(Guid.NewGuid());
        a.TransitionTo(ApplicationStatus.Shortlisted);
        a.TransitionTo(ApplicationStatus.AwaitingApproval);
        a.Approve("h", Now);
        a.TransitionTo(ApplicationStatus.PreparingApplication);
        a.TransitionTo(ApplicationStatus.ReadyToSubmit);
        Assert.Null(a.RespondedAt);
        a.RecordSubmission("h", confirmed: true, Now);
        Assert.Null(a.RespondedAt);

        a.TransitionTo(ApplicationStatus.InterviewScheduled);
        var first = a.RespondedAt;
        Assert.NotNull(first);
        a.TransitionTo(ApplicationStatus.RejectedByEmployer);
        Assert.Equal(first, a.RespondedAt);
    }

    [Fact]
    public void Withdrawing_is_not_a_reply()
    {
        var a = new JobApplication(Guid.NewGuid());
        a.TransitionTo(ApplicationStatus.Shortlisted);
        a.TransitionTo(ApplicationStatus.Withdrawn);
        Assert.Null(a.RespondedAt);
    }
}
