using CareerPilot.Application.Profiles;

namespace CareerPilot.Tests.Profiles;

public class ProfileCompletenessTests
{
    [Fact]
    public void An_empty_profile_scores_zero_and_lists_everything_missing()
    {
        var result = ProfileCompleteness.Compute(new CareerPilot.Domain.Profiles.UserProfile());
        Assert.Equal(0, result.Percent);
        Assert.Equal(11, result.Missing.Count);
    }

    [Fact]
    public void A_fully_filled_profile_scores_100()
    {
        var p = DefaultProfileFactory.Create();
        p.FullName = "Ayoub";
        p.Email = "ayoub@example.com";
        p.City = "Tunis";
        p.Summary = "Summary";
        p.WorkExperiences[0].Description = "Did things";
        p.GithubUrl = "https://github.com/example";

        var result = ProfileCompleteness.Compute(p);

        Assert.Equal(100, result.Percent);
        Assert.Empty(result.Missing);
    }

    [Fact]
    public void The_default_profile_is_partially_complete()
    {
        var result = ProfileCompleteness.Compute(DefaultProfileFactory.Create());
        // Headline 5 + education 10 + skills 10 + project 10 + languages 5 + preferences 5.
        Assert.Equal(45, result.Percent);
        Assert.Contains("Professional summary", result.Missing);
    }
}
