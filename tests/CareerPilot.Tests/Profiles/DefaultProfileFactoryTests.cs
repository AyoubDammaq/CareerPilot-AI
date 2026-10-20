using CareerPilot.Application.Profiles;

namespace CareerPilot.Tests.Profiles;

/// <summary>The initial profile must contain only what the owner stated: nothing invented, nothing pre-verified.</summary>
public class DefaultProfileFactoryTests
{
    [Fact]
    public void No_skill_is_pre_verified_or_pre_rated()
    {
        var p = DefaultProfileFactory.Create();
        Assert.NotEmpty(p.Skills);
        Assert.All(p.Skills, s =>
        {
            Assert.False(s.IsVerified);
            Assert.Null(s.Level);
        });
    }

    [Fact]
    public void No_experience_or_project_is_pre_verified_and_no_results_are_invented()
    {
        var p = DefaultProfileFactory.Create();
        Assert.All(p.WorkExperiences, w =>
        {
            Assert.False(w.IsVerified);
            Assert.Empty(w.Achievements);
        });
        Assert.All(p.Projects, x =>
        {
            Assert.False(x.IsVerified);
            Assert.Empty(x.Results);
        });
    }

    [Fact]
    public void Personal_contact_details_are_left_blank_for_the_owner()
    {
        var p = DefaultProfileFactory.Create();
        Assert.Equal("", p.FullName);
        Assert.Equal("", p.Email);
        Assert.Null(p.Phone);
        Assert.Null(p.GithubUrl);
        Assert.Null(p.LinkedinUrl);
        Assert.Equal("", p.Summary);
    }

    [Fact]
    public void Languages_match_the_stated_levels()
    {
        var langs = DefaultProfileFactory.Create().Languages.ToDictionary(l => l.Name, l => l.Level);
        Assert.Equal("B2", langs["French"]);
        Assert.Equal("B2", langs["English"]);
        Assert.Equal("Beginner", langs["German"]);
    }

    [Fact]
    public void Initial_preferences_cover_the_requested_regions_and_stay_editable_data()
    {
        var pref = DefaultProfileFactory.Create().Preferences;
        Assert.Contains("Tunisia", pref.TargetCountries);
        Assert.Contains("France", pref.TargetCountries);
        Assert.Contains("Belgium", pref.TargetCountries);
        Assert.Contains("Saudi Arabia", pref.TargetCountries);
        Assert.Contains("United Arab Emirates", pref.TargetCountries);
        Assert.Contains("Qatar", pref.TargetCountries);
        Assert.True(pref.IncludeInternationalRemote);
        Assert.False(pref.IncludeSeniorRoles);
        Assert.Equal("Permanent", Assert.Single(pref.EmploymentTypes));
        Assert.Equal(7, pref.TargetTitles.Count);
    }

    [Fact]
    public void The_default_profile_passes_validation()
    {
        Assert.Empty(ProfileValidator.Validate(TestData.ValidDto()));
    }
}
