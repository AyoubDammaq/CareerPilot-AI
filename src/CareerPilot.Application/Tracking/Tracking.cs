using CareerPilot.Application.Abstractions;
using CareerPilot.Application.Applications;
using CareerPilot.Domain.Applications;
using CareerPilot.Domain.Jobs;

namespace CareerPilot.Application.Tracking;

public sealed class TrackingOptions
{
    public const string Section = "Tracking";

    /// <summary>A submitted application with no reply after this many days gets a follow-up reminder.</summary>
    public int FollowUpAfterDays { get; set; } = 7;

    /// <summary>An approved application that is still not submitted after this many days gets a reminder.</summary>
    public int StaleUnsubmittedDays { get; set; } = 5;

    /// <summary>Warn this many days before an application deadline.</summary>
    public int DeadlineWarningDays { get; set; } = 3;

    /// <summary>Below this many submitted applications, percentages are shown with a "too few to conclude" note.</summary>
    public int MinSampleForConclusions { get; set; } = 10;
}

/// <summary>What the calculator needs about one application. Plain data, so the rules can be tested without a database.</summary>
public sealed record TrackingRow(
    Guid ApplicationId, string JobTitle, string CompanyName, string Country, ApplicationStatus Status,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? ApprovedAt, DateTimeOffset? SubmittedAt, DateTimeOffset? RespondedAt,
    int? MatchScore, DateTimeOffset? Deadline, JobStatus JobStatus);

public sealed record Reminder(Guid ApplicationId, string Kind, string JobTitle, string CompanyName, string Message, int Days);

public sealed record StatusCount(string Status, int Count);
public sealed record GroupStat(string Name, int Submitted, int Replied);
public sealed record WeekCount(DateOnly WeekStart, int Submitted);

public sealed record TrackingSummary(
    int Total, int Active, int Submitted, int Replied, int Rejected, int Interviewing, int Offers,
    double? ResponseRate, double? MedianDaysToResponse, bool SmallSample, List<StatusCount> ByStatus,
    List<GroupStat> ByCountry, List<GroupStat> ByScoreBand, List<WeekCount> Weekly, List<string> Notes);

/// <summary>
/// Reminders and statistics derived from the applications as they are recorded. Everything here is computed from
/// owner-recorded facts: nothing is estimated, nothing is sent, and no message is written to anyone.
/// </summary>
public static class TrackingCalculator
{
    private static readonly ApplicationStatus[] PreSubmit =
    [
        ApplicationStatus.Shortlisted, ApplicationStatus.AwaitingApproval, ApplicationStatus.Approved,
        ApplicationStatus.PreparingApplication, ApplicationStatus.ReadyToSubmit,
    ];

    private static readonly ApplicationStatus[] Approved = [ApplicationStatus.Approved, ApplicationStatus.PreparingApplication, ApplicationStatus.ReadyToSubmit];
    private static readonly ApplicationStatus[] Interviewing = [ApplicationStatus.InterviewScheduled, ApplicationStatus.InterviewCompleted];
    private static readonly ApplicationStatus[] Offers = [ApplicationStatus.OfferReceived, ApplicationStatus.OfferAccepted, ApplicationStatus.OfferDeclined];

    public static List<Reminder> Reminders(IEnumerable<TrackingRow> rows, DateTimeOffset now, TrackingOptions o)
    {
        var result = new List<Reminder>();
        foreach (var r in rows)
        {
            if (r.Status == ApplicationStatus.Submitted && r.RespondedAt is null && r.SubmittedAt is { } sent
                && Days(now, sent) >= o.FollowUpAfterDays)
                result.Add(new(r.ApplicationId, "FollowUp", r.JobTitle, r.CompanyName,
                    $"No reply for {Days(now, sent)} days. Consider a short, polite follow-up, or mark it closed.", Days(now, sent)));

            if (r.Status == ApplicationStatus.SubmissionUnconfirmed)
                result.Add(new(r.ApplicationId, "ConfirmSubmission", r.JobTitle, r.CompanyName,
                    "You could not confirm this was sent. Check the employer's site or your email, then confirm it or withdraw it.", Days(now, r.UpdatedAt)));

            if (Approved.Contains(r.Status) && r.ApprovedAt is { } approved && Days(now, approved) >= o.StaleUnsubmittedDays)
                result.Add(new(r.ApplicationId, "FinishSubmission", r.JobTitle, r.CompanyName,
                    $"Approved {Days(now, approved)} days ago and still not submitted.", Days(now, approved)));

            if (PreSubmit.Contains(r.Status))
            {
                if (r.JobStatus is JobStatus.Closed or JobStatus.Expired)
                    result.Add(new(r.ApplicationId, "JobClosed", r.JobTitle, r.CompanyName,
                        "The source no longer lists this vacancy. Check it before you spend more time, or close it.", 0));
                else if (r.Deadline is { } d)
                {
                    var left = (int)Math.Floor((d - now).TotalDays);
                    if (d < now)
                        result.Add(new(r.ApplicationId, "DeadlinePassed", r.JobTitle, r.CompanyName,
                            "The stated deadline has passed. Submit only if the employer still accepts applications, otherwise close it.", Math.Abs(left)));
                    else if (left <= o.DeadlineWarningDays)
                        result.Add(new(r.ApplicationId, "Deadline", r.JobTitle, r.CompanyName,
                            left <= 0 ? "The stated deadline is today." : $"The stated deadline is in {left} day{(left == 1 ? "" : "s")}.", left));
                }
            }

            if ((Interviewing.Contains(r.Status) || r.Status == ApplicationStatus.OfferReceived) && Days(now, r.UpdatedAt) >= o.FollowUpAfterDays)
                result.Add(new(r.ApplicationId, "UpdateStatus", r.JobTitle, r.CompanyName,
                    $"Nothing recorded for {Days(now, r.UpdatedAt)} days. Update the status if you heard anything.", Days(now, r.UpdatedAt)));
        }
        return [.. result.OrderByDescending(x => Priority(x.Kind)).ThenByDescending(x => x.Days)];
    }

    private static int Priority(string kind) => kind switch
    {
        "Deadline" => 5, "DeadlinePassed" => 4, "ConfirmSubmission" => 4, "FollowUp" => 3, "FinishSubmission" => 3, "JobClosed" => 2, _ => 1,
    };

    public static TrackingSummary Summary(IReadOnlyCollection<TrackingRow> rows, DateTimeOffset now, TrackingOptions o)
    {
        var submitted = rows.Where(r => r.SubmittedAt is not null).ToList();
        var replied = submitted.Where(r => r.RespondedAt is not null).ToList();
        var small = submitted.Count < o.MinSampleForConclusions;

        var days = replied.Select(r => (r.RespondedAt!.Value - r.SubmittedAt!.Value).TotalDays).Where(d => d >= 0).Order().ToList();
        double? median = days.Count == 0 ? null : days.Count % 2 == 1 ? days[days.Count / 2] : (days[days.Count / 2 - 1] + days[days.Count / 2]) / 2;

        var notes = new List<string>();
        if (rows.Count == 0) notes.Add("No applications yet. Statistics appear once you start recording applications.");
        else if (small) notes.Add($"Only {submitted.Count} submitted application{(submitted.Count == 1 ? "" : "s")}: too few to draw conclusions. Treat the percentages as anecdotes.");
        notes.Add("Counts use each application's current status. An interview that later ended in a rejection counts as rejected.");
        notes.Add("A reply means the employer answered with a rejection, an interview or an offer, as you recorded it. Silence is not counted as a reply.");

        var byCountry = submitted.GroupBy(r => string.IsNullOrWhiteSpace(r.Country) ? "Unknown" : r.Country)
            .Select(g => new GroupStat(g.Key, g.Count(), g.Count(r => r.RespondedAt is not null)))
            .OrderByDescending(g => g.Submitted).ThenBy(g => g.Name).ToList();

        string Band(int? s) => s is null ? "Not scored" : s >= 70 ? "70 to 100" : s >= 50 ? "50 to 69" : "Under 50";
        var order = new[] { "70 to 100", "50 to 69", "Under 50", "Not scored" };
        var byBand = submitted.GroupBy(r => Band(r.MatchScore))
            .Select(g => new GroupStat(g.Key, g.Count(), g.Count(r => r.RespondedAt is not null)))
            .OrderBy(g => Array.IndexOf(order, g.Name)).ToList();

        var thisWeek = StartOfWeek(now);
        var weekly = Enumerable.Range(0, 8).Select(i => thisWeek.AddDays(-7 * (7 - i)))
            .Select(w => new WeekCount(w, submitted.Count(r => StartOfWeek(r.SubmittedAt!.Value) == w))).ToList();

        return new TrackingSummary(
            rows.Count, rows.Count(r => !ApplicationWorkflow.IsTerminal(r.Status)), submitted.Count, replied.Count,
            rows.Count(r => r.Status == ApplicationStatus.RejectedByEmployer), rows.Count(r => Interviewing.Contains(r.Status)), rows.Count(r => Offers.Contains(r.Status)),
            submitted.Count == 0 ? null : Math.Round(100.0 * replied.Count / submitted.Count, 0), median is null ? null : Math.Round(median.Value, 1), small,
            [.. rows.GroupBy(r => r.Status).OrderBy(g => g.Key).Select(g => new StatusCount(g.Key.ToString(), g.Count()))],
            byCountry, byBand, weekly, notes);
    }

    private static int Days(DateTimeOffset now, DateTimeOffset then) => Math.Max(0, (int)Math.Floor((now - then).TotalDays));

    private static DateOnly StartOfWeek(DateTimeOffset t)
    {
        var d = DateOnly.FromDateTime(t.UtcDateTime);
        return d.AddDays(-(((int)d.DayOfWeek + 6) % 7));
    }
}

public sealed class TrackingService(IApplicationReader apps, IJobStore jobs, TimeProvider clock, TrackingOptions options)
{
    public async Task<List<Reminder>> RemindersAsync(CancellationToken ct) =>
        TrackingCalculator.Reminders(await RowsAsync(ct), clock.GetUtcNow(), options);

    public async Task<TrackingSummary> SummaryAsync(CancellationToken ct) =>
        TrackingCalculator.Summary(await RowsAsync(ct), clock.GetUtcNow(), options);

    private async Task<List<TrackingRow>> RowsAsync(CancellationToken ct)
    {
        var listings = (await jobs.GetAllListingsAsync(ct)).ToDictionary(l => l.Id);
        var rows = new List<TrackingRow>();
        foreach (var a in await apps.ListAsync(ct))
        {
            if (!listings.TryGetValue(a.CanonicalJobId, out var j)) continue;
            rows.Add(new TrackingRow(a.Id, j.Title, j.CompanyName, j.Country, a.Status, a.CreatedAt, a.UpdatedAt, a.ApprovedAt, a.SubmittedAt,
                a.RespondedAt, j.MatchScore, j.ApplicationDeadline, j.Status));
        }
        return rows;
    }
}
