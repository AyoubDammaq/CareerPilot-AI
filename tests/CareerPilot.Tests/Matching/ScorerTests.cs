using CareerPilot.Application.Jobs;
using CareerPilot.Application.Matching;
using CareerPilot.Application.Profiles;
using CareerPilot.Domain.Jobs;
using CareerPilot.Domain.Profiles;
using CareerPilot.Tests.Jobs;

namespace CareerPilot.Tests.Matching;

public class ScorerTests
{
    private static readonly DateTimeOffset Now = Helpers.Now;
    private static readonly ScoringOptions Opts = new();

    private static UserProfile Profile()
    {
        var p = DefaultProfileFactory.Create();
        p.Skills.Clear();
        foreach (var s in new[] { "C#", ".NET", "Angular", "PostgreSQL", "Docker" })
            p.Skills.Add(new Skill { Name = s });
        p.Preferences.TargetTitles = ["Software Engineer", "Backend Engineer"];
        p.Preferences.TargetCountries = ["France"];
        p.Preferences.WorkModes = ["Remote", "Hybrid"];
        p.Preferences.WorkingLanguages = ["English", "French"];
        p.Preferences.MinSalary = null;
        return p;
    }

    private static JobListing Job(Action<JobListing>? tweak = null)
    {
        var j = Helpers.Listing("Backend Engineer", "Paris, France", WorkMode.Hybrid);
        j.Skills = ["C#", ".NET", "PostgreSQL", "Kafka"];
        j.GeoFit = GeoFit.Match;
        j.Language = "en";
        j.PublishedAt = Now.AddDays(-3);
        tweak?.Invoke(j);
        return j;
    }

    private static MatchResult Score(JobListing j, UserProfile? p = null, ScoringOptions? o = null) =>
        JobScorer.Score(j, p ?? Profile(), o ?? Opts, Now);

    [Fact]
    public void Default_weights_add_up_to_100()
    {
        Assert.Equal(100, Opts.ScaledWeights().Sum(w => w.Weight), 6);
    }

    [Fact]
    public void Weights_are_scaled_so_the_maximum_is_always_100()
    {
        var o = new ScoringOptions { Skills = 60, Title = 0, Location = 0, Seniority = 0, WorkMode = 0, Language = 0, Salary = 0, Freshness = 60 };
        var weights = o.ScaledWeights();
        Assert.Equal(100, weights.Sum(w => w.Weight), 6);
        Assert.Equal(50, weights.Single(w => w.Key == "skills").Weight, 6);
    }

    [Fact]
    public void All_zero_or_invalid_weights_fall_back_to_defaults()
    {
        var o = new ScoringOptions { Skills = 0, Title = -5, Location = double.NaN, Seniority = 0, WorkMode = 0, Language = 0, Salary = 0, Freshness = 0 };
        Assert.Equal(100, o.ScaledWeights().Sum(w => w.Weight), 6);
        Assert.Equal(30, o.ScaledWeights().Single(w => w.Key == "skills").Weight, 6);
    }

    [Fact]
    public void A_matching_vacancy_scores_well_and_lists_matched_and_missing_skills()
    {
        var r = Score(Job());
        Assert.InRange(r.Score, 75, 100);
        Assert.Equal(["C#", ".NET", "PostgreSQL"], r.MatchedSkills);
        Assert.Equal(["Kafka"], r.MissingSkills);
        Assert.Equal(8, r.Components.Count);
    }

    [Fact]
    public void Component_points_add_up_to_the_score()
    {
        var r = Score(Job());
        Assert.InRange(Math.Abs(r.Score - r.Components.Sum(c => c.Points)), 0, 1);
        Assert.All(r.Components, c => Assert.InRange(c.Points, 0, c.MaxPoints));
    }

    [Fact]
    public void Scoring_is_deterministic()
    {
        var a = Score(Job());
        var b = Score(Job());
        Assert.Equal(a.Score, b.Score);
        Assert.Equal(a.Components.Select(c => c.Points), b.Components.Select(c => c.Points));
    }

    [Fact]
    public void Skills_never_count_that_the_profile_does_not_list()
    {
        var p = Profile();
        var r = Score(Job(j => j.Skills = ["Kafka", "Rust", "Scala"]), p);
        Assert.Empty(r.MatchedSkills);
        var skills = r.Components.Single(c => c.Key == "skills");
        Assert.True(skills.Points < skills.MaxPoints / 4, "no matches must score clearly below the neutral half");
    }

    [Fact]
    public void A_posting_that_names_only_a_couple_of_technologies_cannot_earn_full_skill_marks()
    {
        var few = Score(Job(j => j.Skills = ["C#", ".NET"])).Components.Single(c => c.Key == "skills");
        var many = Score(Job(j => j.Skills = ["C#", ".NET", "PostgreSQL", "Docker"])).Components.Single(c => c.Key == "skills");
        Assert.Equal(many.MaxPoints, many.Points, 1);           // 4 of 4 is full marks
        Assert.Equal(many.MaxPoints * 0.75, few.Points, 1);     // 2 of 2 is only three quarters
        Assert.False(few.Known);                                 // and lowers the confidence
        Assert.Contains("counts for less", few.Note);
        Assert.DoesNotContain("counts for less", many.Note);
    }

    [Fact]
    public void One_matched_technology_is_weaker_evidence_than_one_missing_is_bad()
    {
        var one = Score(Job(j => j.Skills = ["C#"])).Components.Single(c => c.Key == "skills");
        var none = Score(Job(j => j.Skills = ["Rust"])).Components.Single(c => c.Key == "skills");
        Assert.True(one.Points > none.Points);
        Assert.True(one.Points < one.MaxPoints);
    }

    [Theory]
    [InlineData("ASP.NET Core")]
    [InlineData("dotnet")]
    public void Skill_aliases_are_recognised(string jobSkill)
    {
        var r = Score(Job(j => j.Skills = [jobSkill]));
        Assert.Single(r.MatchedSkills);
    }

    [Fact]
    public void Project_technologies_count_as_skills()
    {
        var p = Profile();
        p.Projects.Add(new Project { Name = "X", Technologies = ["Kafka"] });
        Assert.Contains("Kafka", Score(Job(), p).MatchedSkills);
    }

    [Fact]
    public void Missing_information_is_neutral_not_zero_and_lowers_confidence()
    {
        var full = Score(Job());
        var sparse = Score(Job(j => { j.Skills = []; j.WorkMode = null; j.Language = null; j.PublishedAt = null; }));
        var skills = sparse.Components.Single(c => c.Key == "skills");
        Assert.False(skills.Known);
        Assert.Equal(skills.MaxPoints / 2, skills.Points, 1);
        Assert.True(sparse.Confidence < full.Confidence);
    }

    [Fact]
    public void Low_confidence_adds_a_warning()
    {
        var p = Profile();
        p.Preferences.TargetTitles = [];
        p.Preferences.WorkModes = [];
        var r = Score(Job(j => { j.Skills = []; j.Language = null; j.PublishedAt = null; j.GeoFit = GeoFit.NeedsReview; j.Seniority = SeniorityLevel.Unspecified; }), p);
        Assert.Contains(r.Warnings, w => w.Contains("limited information"));
    }

    [Theory]
    [InlineData(GeoFit.Match, true)]
    [InlineData(GeoFit.NeedsReview, false)]
    [InlineData(GeoFit.NoMatch, true)]
    public void Location_points_follow_geo_fit(GeoFit fit, bool known)
    {
        var c = Score(Job(j => j.GeoFit = fit)).Components.Single(x => x.Key == "location");
        Assert.Equal(known, c.Known);
        Assert.Equal(fit == GeoFit.Match ? c.MaxPoints : fit == GeoFit.NoMatch ? 0 : Math.Round(c.MaxPoints * 0.4, 1), c.Points, 1);
    }

    [Fact]
    public void Outside_target_countries_scores_lower_and_warns()
    {
        var inside = Score(Job());
        var outside = Score(Job(j => j.GeoFit = GeoFit.NoMatch));
        Assert.True(outside.Score < inside.Score);
        Assert.Contains(outside.Warnings, w => w.Contains("target countries"));
    }

    [Theory]
    [InlineData(SeniorityLevel.Junior, false, 1.0)]
    [InlineData(SeniorityLevel.Mid, false, 1.0)]
    [InlineData(SeniorityLevel.Senior, false, 0.15)]
    [InlineData(SeniorityLevel.Senior, true, 0.6)]
    [InlineData(SeniorityLevel.Lead, false, 0.05)]
    public void Seniority_points(SeniorityLevel level, bool includeSenior, double expectedFraction)
    {
        var p = Profile();
        p.Preferences.IncludeSeniorRoles = includeSenior;
        var c = Score(Job(j => j.Seniority = level), p).Components.Single(x => x.Key == "seniority");
        Assert.InRange(Math.Abs(c.MaxPoints * expectedFraction - c.Points), 0, 0.06);
    }

    [Fact]
    public void Asking_for_far_more_experience_than_the_profile_halves_the_level_points()
    {
        var plain = Score(Job(j => j.Seniority = SeniorityLevel.Mid)).Components.Single(x => x.Key == "seniority");
        var demanding = Score(Job(j => { j.Seniority = SeniorityLevel.Mid; j.ExperienceYearsHint = 8; })).Components.Single(x => x.Key == "seniority");
        Assert.Equal(plain.Points / 2, demanding.Points, 1);
        Assert.Contains("8 years", demanding.Note);
    }

    [Fact]
    public void Title_matching_uses_target_titles()
    {
        var exact = Score(Job()).Components.Single(c => c.Key == "title");
        var other = Score(Job(j => j.Title = "Data Analyst")).Components.Single(c => c.Key == "title");
        Assert.Equal(exact.MaxPoints, exact.Points, 1);
        Assert.Equal(0, other.Points);
    }

    [Fact]
    public void Title_without_target_titles_is_unknown()
    {
        var p = Profile();
        p.Preferences.TargetTitles = [];
        Assert.False(Score(Job(), p).Components.Single(c => c.Key == "title").Known);
    }

    [Fact]
    public void Work_mode_outside_preferences_scores_zero()
    {
        var c = Score(Job(j => j.WorkMode = WorkMode.OnSite)).Components.Single(x => x.Key == "workMode");
        Assert.Equal(0, c.Points);
        Assert.True(c.Known);
    }

    [Fact]
    public void Posting_language_not_spoken_scores_zero_and_known_language_scores_full()
    {
        Assert.Equal(0, Score(Job(j => j.Language = "es")).Components.Single(c => c.Key == "language").Points);
        var en = Score(Job(j => j.Language = "en")).Components.Single(c => c.Key == "language");
        Assert.Equal(en.MaxPoints, en.Points, 1);
    }

    [Fact]
    public void Salary_is_only_compared_in_the_same_currency_per_year()
    {
        var p = Profile();
        p.Preferences.MinSalary = 40000;
        p.Preferences.SalaryCurrency = "EUR";
        Func<Action<JobListing>, MatchComponent> salary = t => Score(Job(t), p).Components.Single(c => c.Key == "salary");

        Assert.Equal(salary(j => { }).MaxPoints / 2, salary(j => { }).Points, 1); // not stated: neutral
        Assert.True(salary(j => { j.SalaryMax = 50000; j.SalaryCurrency = "EUR"; j.SalaryPeriod = "year"; }).Points > 0);
        Assert.Equal(0, salary(j => { j.SalaryMax = 30000; j.SalaryCurrency = "EUR"; j.SalaryPeriod = "year"; }).Points);
        Assert.False(salary(j => { j.SalaryMax = 90000; j.SalaryCurrency = "USD"; j.SalaryPeriod = "year"; }).Known);
        Assert.False(salary(j => { j.SalaryMax = 50; j.SalaryCurrency = "EUR"; j.SalaryPeriod = "hour"; }).Known);
    }

    [Theory]
    [InlineData(JobStatus.Closed)]
    [InlineData(JobStatus.Expired)]
    public void Closed_or_expired_postings_score_zero(JobStatus status)
    {
        var r = Score(Job(j => j.Status = status));
        Assert.Equal(0, r.Score);
        Assert.Contains(r.Warnings, w => w.Contains("cannot be applied to"));
    }

    [Fact]
    public void Unverified_posting_warns_but_still_scores()
    {
        var r = Score(Job(j => j.Status = JobStatus.Unverified));
        Assert.True(r.Score > 0);
        Assert.Contains(r.Warnings, w => w.Contains("re-confirmed"));
    }

    [Fact]
    public void Missing_visa_sponsorship_is_flagged_when_the_owner_needs_it()
    {
        var p = Profile();
        p.Preferences.RequiresVisaSponsorship = true;
        Assert.Contains(Score(Job(), p).Warnings, w => w.Contains("sponsorship"));
        Assert.DoesNotContain(Score(Job(j => j.MentionsSponsorship = true), p).Warnings, w => w.Contains("sponsorship"));
    }

    [Fact]
    public void A_disabled_criterion_is_left_out_and_the_rest_rescaled()
    {
        var o = new ScoringOptions { Salary = 0 };
        var r = Score(Job(), null, o);
        Assert.DoesNotContain(r.Components, c => c.Key == "salary");
        Assert.Equal(100, r.Components.Sum(c => c.MaxPoints), 0);
    }

    [Fact]
    public void Tier_thresholds_are_applied()
    {
        var weak = Score(Job(j => { j.Skills = ["Rust"]; j.Title = "Data Analyst"; j.GeoFit = GeoFit.NoMatch; j.WorkMode = WorkMode.OnSite; j.Language = "es"; }));
        Assert.Equal("Weak", weak.Tier);
        Assert.Equal("Strong", Score(Job()).Tier);
    }

    [Fact]
    public void Years_of_experience_come_from_the_profile_history()
    {
        var p = new UserProfile();
        p.WorkExperiences.Add(new WorkExperience { StartDate = new DateOnly(2024, 1, 1), EndDate = new DateOnly(2026, 1, 1) });
        Assert.Equal(2, JobScorer.YearsOfExperience(p, new DateOnly(2026, 10, 1)), 1);
    }

    [Fact]
    public void Result_survives_a_serialization_round_trip()
    {
        var j = Job();
        MatchingService.Apply(j, Profile(), Opts, Now);
        var back = MatchingService.Deserialize(j.MatchJson)!;
        Assert.Equal(j.MatchScore, back.Score);
        Assert.Equal(8, back.Components.Count);
        Assert.Equal(["Kafka"], back.MissingSkills);
        Assert.Null(MatchingService.Deserialize("not json"));
        Assert.Null(MatchingService.Deserialize(null));
    }
}
