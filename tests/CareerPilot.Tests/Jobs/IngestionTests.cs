using CareerPilot.Application.Jobs;
using CareerPilot.Domain.Jobs;

namespace CareerPilot.Tests.Jobs;

public class JobIngestionTests
{
    private static readonly DateTimeOffset T0 = Helpers.Now;

    private static (WatchedCompany Company, List<JobListing> Listings) Seeded(params SourceJob[] jobs)
    {
        var company = Helpers.Company();
        var listings = new List<JobListing>();
        var result = JobIngestion.Apply(company, Helpers.Result(true, jobs), listings, Helpers.Options(), [], T0);
        listings.AddRange(result.Created);
        return (company, listings);
    }

    private static IngestionResult Run(WatchedCompany company, List<JobListing> listings, bool complete, DateTimeOffset at, params SourceJob[] jobs)
    {
        var result = JobIngestion.Apply(company, Helpers.Result(complete, jobs), listings, Helpers.Options(), [], at);
        listings.AddRange(result.Created);
        return result;
    }

    [Fact]
    public void New_jobs_become_open_listings_with_a_posting_and_source_url()
    {
        var (_, listings) = Seeded(Helpers.Job("1"), Helpers.Job("2", "Frontend Engineer"));
        Assert.Equal(2, listings.Count);
        var first = listings.Single(l => l.Title == "Backend Engineer");
        Assert.Equal(JobStatus.Open, first.Status);
        Assert.Equal("Acme", first.CompanyName);
        Assert.Equal("FR", first.CountryCodes[0]);
        var posting = Assert.Single(first.Postings);
        Assert.Equal("https://example.test/jobs/1", posting.SourceUrl);
        Assert.True(posting.IsPresent);
        Assert.Equal(T0, first.DiscoveredAt);
    }

    [Fact]
    public void Missing_salary_deadline_and_publication_date_stay_empty()
    {
        var (_, listings) = Seeded(Helpers.Job("1"));
        var l = listings[0];
        Assert.Null(l.SalaryMin);
        Assert.Null(l.ApplicationDeadline);
        Assert.Null(l.PublishedAt);
    }

    [Fact]
    public void Re_reading_an_unchanged_board_creates_and_updates_nothing()
    {
        var (company, listings) = Seeded(Helpers.Job("1"));
        var result = Run(company, listings, true, T0.AddDays(1), Helpers.Job("1"));
        Assert.Equal(0, result.New);
        Assert.Equal(0, result.Updated);
        Assert.Single(listings);
        Assert.Equal(T0.AddDays(1), listings[0].LastVerifiedAt);
    }

    [Fact]
    public void A_changed_description_is_an_update()
    {
        var (company, listings) = Seeded(Helpers.Job("1"));
        var result = Run(company, listings, true, T0.AddDays(1), Helpers.Job("1", description: "New description."));
        Assert.Equal(1, result.Updated);
        Assert.Equal("New description.", listings[0].Description);
    }

    [Fact]
    public void A_job_missing_from_a_complete_snapshot_is_closed()
    {
        var (company, listings) = Seeded(Helpers.Job("1"), Helpers.Job("2", "Frontend Engineer"));
        var result = Run(company, listings, true, T0.AddDays(1), Helpers.Job("1"));
        Assert.Equal(1, result.Closed);
        var gone = listings.Single(l => l.Title == "Frontend Engineer");
        Assert.Equal(JobStatus.Closed, gone.Status);
        Assert.Equal(T0.AddDays(1), gone.ClosedAt);
        Assert.False(gone.Postings[0].IsPresent);
        Assert.Equal(JobStatus.Open, listings.Single(l => l.Title == "Backend Engineer").Status);
    }

    [Fact]
    public void A_job_missing_from_an_incomplete_snapshot_is_never_closed()
    {
        var (company, listings) = Seeded(Helpers.Job("1"), Helpers.Job("2", "Frontend Engineer"));
        var result = Run(company, listings, false, T0.AddDays(1), Helpers.Job("1"));
        Assert.Equal(0, result.Closed);
        Assert.All(listings, l => Assert.Equal(JobStatus.Open, l.Status));
    }

    [Fact]
    public void A_feed_larger_than_the_cap_cannot_close_the_jobs_it_did_not_process()
    {
        var (company, listings) = Seeded(Helpers.Job("1"), Helpers.Job("2", "Frontend Engineer"), Helpers.Job("3", "Data Engineer"));
        var options = Helpers.Options();
        options.MaxJobsPerCompany = 1;
        var result = JobIngestion.Apply(company, Helpers.Result(true, Helpers.Job("1"), Helpers.Job("2", "Frontend Engineer"), Helpers.Job("3", "Data Engineer")),
            listings, options, [], T0.AddDays(1));
        Assert.Equal(0, result.Closed);
    }

    [Fact]
    public void A_job_that_reappears_is_reopened()
    {
        var (company, listings) = Seeded(Helpers.Job("1"));
        Run(company, listings, true, T0.AddDays(1));                       // disappears
        Assert.Equal(JobStatus.Closed, listings[0].Status);

        var result = Run(company, listings, true, T0.AddDays(2), Helpers.Job("1"));
        Assert.Equal(JobStatus.Open, listings[0].Status);
        Assert.Null(listings[0].ClosedAt);
        Assert.Equal(1, result.Updated);
        Assert.Single(listings);
    }

    [Fact]
    public void The_same_vacancy_on_a_different_source_is_linked_not_duplicated()
    {
        var (_, listings) = Seeded(Helpers.Job("gh-1", "Backend Engineer (m/f/d)"));
        var lever = Helpers.Company("Acme", JobSourceKind.Lever, "acme");

        var result = Run(lever, listings, true, T0.AddDays(1), Helpers.Job("lv-9", "Backend Engineer"));

        Assert.Empty(result.Created);
        var listing = Assert.Single(listings);
        Assert.Equal(2, listing.Postings.Count);
        Assert.Contains(listing.Postings, p => p.Kind == JobSourceKind.Lever && p.SourceJobId == "lv-9");
    }

    [Fact]
    public void Two_postings_on_the_same_board_are_never_merged()
    {
        var (_, listings) = Seeded(Helpers.Job("1"), Helpers.Job("2"));
        Assert.Equal(2, listings.Count);
    }

    [Fact]
    public void A_vacancy_stays_open_while_any_of_its_postings_is_still_listed()
    {
        var (_, listings) = Seeded(Helpers.Job("gh-1"));
        var lever = Helpers.Company("Acme", JobSourceKind.Lever, "acme");
        Run(lever, listings, true, T0.AddDays(1), Helpers.Job("lv-1"));

        // The Greenhouse posting disappears from a complete snapshot, the Lever one is still there.
        var greenhouse = listings[0].Postings.Single(p => p.Kind == JobSourceKind.Greenhouse);
        var ghCompany = Helpers.Company();
        ghCompany.Id = greenhouse.WatchedCompanyId!.Value;
        var result = Run(ghCompany, listings, true, T0.AddDays(2));

        Assert.Equal(0, result.Closed);
        Assert.Equal(JobStatus.Open, listings[0].Status);
        Assert.False(greenhouse.IsPresent);
    }

    [Fact]
    public void Duplicate_ids_inside_one_feed_and_unusable_records_are_ignored()
    {
        var (_, listings) = Seeded(Helpers.Job("1"), Helpers.Job("1"), Helpers.Job("", "No id"), Helpers.Job("3", " "));
        Assert.Single(listings);
    }

    [Fact]
    public void Non_http_links_from_untrusted_feeds_are_dropped()
    {
        var (_, listings) = Seeded(Helpers.Job("1", url: "javascript:alert(1)"));
        Assert.Equal("", listings[0].Postings[0].SourceUrl);
        Assert.Equal("", listings[0].Postings[0].ApplyUrl);
        Assert.Equal("https://example.com/a", JobIngestion.SafeUrl("https://example.com/a"));
        Assert.Equal("", JobIngestion.SafeUrl("ftp://example.com"));
        Assert.Equal("", JobIngestion.SafeUrl(null));
    }

    [Fact]
    public void The_owners_decision_survives_later_searches()
    {
        var (company, listings) = Seeded(Helpers.Job("1"));
        listings[0].Decision = UserDecision.Excluded;
        Run(company, listings, true, T0.AddDays(1), Helpers.Job("1", description: "Changed."));
        Assert.Equal(UserDecision.Excluded, listings[0].Decision);
    }

    [Fact]
    public void Expired_and_stale_listings_get_the_right_status()
    {
        var (_, listings) = Seeded(
            Helpers.Job("1", deadline: T0.AddDays(2)),
            Helpers.Job("2", "Frontend Engineer"),
            Helpers.Job("3", "Data Engineer"));
        listings.Single(l => l.Title == "Data Engineer").LastVerifiedAt = T0.AddDays(10);

        JobIngestion.RefreshStatuses(listings, Helpers.Options(), T0.AddDays(10));

        Assert.Equal(JobStatus.Expired, listings.Single(l => l.Title == "Backend Engineer").Status);
        Assert.Equal(JobStatus.Unverified, listings.Single(l => l.Title == "Frontend Engineer").Status);
        Assert.Equal(JobStatus.Open, listings.Single(l => l.Title == "Data Engineer").Status);
    }

    [Fact]
    public void Profile_skills_are_recognised_in_job_text()
    {
        var company = Helpers.Company();
        var listings = new List<JobListing>();
        var result = JobIngestion.Apply(company,
            Helpers.Result(true, Helpers.Job("1", description: "You know Semantic Kernel and Foobar well.")),
            listings, Helpers.Options(), ["Foobar"], T0);
        Assert.Contains("Semantic Kernel", result.Created[0].Skills);
        Assert.Contains("Foobar", result.Created[0].Skills);
    }
}
