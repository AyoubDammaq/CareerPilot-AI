using CareerPilot.Application.Jobs;
using CareerPilot.Domain.Jobs;
using CareerPilot.Domain.Profiles;

namespace CareerPilot.Tests.Jobs;

public class JobClassifierTests
{
    private static readonly DiscoveryOptions Opts = Helpers.Options();

    private static JobListing Classified(JobListing job, JobPreference? prefs = null, string? home = "Tunisia")
    {
        JobClassifier.Classify(job, prefs ?? Helpers.Prefs(), Opts, home);
        return job;
    }

    [Fact]
    public void A_backend_role_in_a_target_country_is_relevant()
    {
        var job = Classified(Helpers.Listing("Backend Engineer", "Paris, France"));
        Assert.True(job.IsRelevant);
        Assert.Equal(GeoFit.Match, job.GeoFit);
        Assert.Empty(job.FilterReasons);
    }

    [Theory]
    [InlineData(".NET Developer")]
    [InlineData("C# Software Engineer")]
    [InlineData("AI Research Engineer")]
    [InlineData("Full Stack Developer")]
    [InlineData("Développeur Back-End")]
    public void Typical_target_titles_are_relevant(string title) =>
        Assert.True(Classified(Helpers.Listing(title)).IsRelevant, title);

    [Theory]
    [InlineData("Account Executive")]
    [InlineData("Sales Engineer")]
    [InlineData("Product Designer")]
    [InlineData("Customer Success Engineer")]
    [InlineData("Talent Acquisition Partner")]
    public void Non_engineering_titles_are_filtered_with_a_reason(string title)
    {
        var job = Classified(Helpers.Listing(title));
        Assert.False(job.IsRelevant);
        Assert.NotEmpty(job.FilterReasons);
    }

    [Fact]
    public void Senior_roles_are_filtered_unless_the_owner_enables_them()
    {
        var senior = Helpers.Listing("Senior Backend Engineer");
        Assert.False(Classified(senior).IsRelevant);
        Assert.Contains(senior.FilterReasons, r => r.Contains("Senior"));

        var prefs = Helpers.Prefs();
        prefs.IncludeSeniorRoles = true;
        Assert.True(Classified(Helpers.Listing("Senior Backend Engineer"), prefs).IsRelevant);
    }

    [Fact]
    public void A_manager_title_without_an_engineer_keyword_is_filtered_for_both_reasons()
    {
        var job = Classified(Helpers.Listing("Engineering Manager"));
        Assert.False(job.IsRelevant);
        Assert.True(job.FilterReasons.Count >= 2);
    }

    [Fact]
    public void A_job_outside_the_target_countries_is_a_geographic_mismatch()
    {
        var job = Classified(Helpers.Listing("Software Engineer", "New York, United States"));
        Assert.Equal(GeoFit.NoMatch, job.GeoFit);
        Assert.False(job.IsRelevant);
        Assert.Contains(job.FilterReasons, r => r.Contains("outside your target countries"));
    }

    [Fact]
    public void Remote_abroad_needs_review_instead_of_being_rejected_or_assumed_eligible()
    {
        var job = Classified(Helpers.Listing("Software Engineer", "Remote - US", WorkMode.Remote));
        Assert.Equal(GeoFit.NeedsReview, job.GeoFit);
        Assert.True(job.IsRelevant);
        Assert.Contains("not verified", job.GeoNote);
    }

    [Fact]
    public void Remote_abroad_is_a_mismatch_when_international_remote_is_off()
    {
        var prefs = Helpers.Prefs();
        prefs.IncludeInternationalRemote = false;
        var job = Classified(Helpers.Listing("Software Engineer", "Remote - US", WorkMode.Remote), prefs);
        Assert.Equal(GeoFit.NoMatch, job.GeoFit);
    }

    [Fact]
    public void Remote_within_a_target_country_matches_but_asks_to_confirm_eligibility()
    {
        var job = Classified(Helpers.Listing("Software Engineer", "Remote, France", WorkMode.Remote));
        Assert.Equal(GeoFit.Match, job.GeoFit);
        Assert.Contains("confirm", job.GeoNote);
    }

    [Fact]
    public void An_unresolvable_location_needs_review()
    {
        var job = Classified(Helpers.Listing("Software Engineer", "Gotham"));
        Assert.Equal(GeoFit.NeedsReview, job.GeoFit);
        Assert.True(job.IsRelevant);
    }

    [Fact]
    public void Region_wide_remote_needs_review()
    {
        var job = Classified(Helpers.Listing("Software Engineer", "Remote - European Union", WorkMode.Remote));
        Assert.Equal(GeoFit.NeedsReview, job.GeoFit);
    }

    [Fact]
    public void Gulf_countries_are_targets_by_default()
    {
        Assert.Equal(GeoFit.Match, Classified(Helpers.Listing("Backend Engineer", "Dubai, United Arab Emirates")).GeoFit);
    }

    [Fact]
    public void A_work_arrangement_outside_the_preferences_is_filtered()
    {
        var prefs = Helpers.Prefs();
        prefs.WorkModes = ["Remote"];
        var job = Classified(Helpers.Listing("Backend Engineer", "Paris, France", WorkMode.OnSite), prefs);
        Assert.False(job.IsRelevant);
        Assert.Contains(job.FilterReasons, r => r.Contains("Work arrangement"));
    }

    [Fact]
    public void No_configured_target_countries_means_review_not_rejection()
    {
        var prefs = Helpers.Prefs();
        prefs.TargetCountries = [];
        var job = Classified(Helpers.Listing("Backend Engineer", "Paris, France"), prefs);
        Assert.Equal(GeoFit.NeedsReview, job.GeoFit);
        Assert.True(job.IsRelevant);
    }

    [Fact]
    public void Visa_sponsorship_is_reported_when_the_owner_needs_it_and_the_job_is_abroad()
    {
        var prefs = Helpers.Prefs();
        prefs.RequiresVisaSponsorship = true;

        var silent = Classified(Helpers.Listing("Backend Engineer", "Paris, France"), prefs);
        Assert.Contains("not mentioned", silent.GeoNote);

        var mentioned = Classified(Helpers.Listing("Backend Engineer", "Paris, France", mentionsSponsorship: true), prefs);
        Assert.Contains("mentions visa sponsorship", mentioned.GeoNote);

        var home = Classified(Helpers.Listing("Backend Engineer", "Tunis, Tunisia"), prefs);
        Assert.Null(home.GeoNote);
    }

    [Fact]
    public void Changing_preferences_changes_the_classification_on_the_next_pass()
    {
        var job = Helpers.Listing("Backend Engineer", "Paris, France");
        Assert.True(Classified(job).IsRelevant);

        var prefs = Helpers.Prefs();
        prefs.TargetCountries = ["Belgium"];
        Assert.False(Classified(job, prefs).IsRelevant);
    }
}
