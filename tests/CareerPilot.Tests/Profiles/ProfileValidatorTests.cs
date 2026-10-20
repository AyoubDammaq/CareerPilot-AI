using CareerPilot.Application.Profiles;

namespace CareerPilot.Tests.Profiles;

public class ProfileValidatorTests
{
    private static ProfileDto Dto => TestData.ValidDto();

    private static void AssertError(ProfileDto dto, string key)
    {
        var errors = ProfileValidator.Validate(dto);
        Assert.True(errors.ContainsKey(key), $"Expected an error for '{key}', got: {string.Join(", ", errors.Keys)}");
    }

    [Fact]
    public void A_partially_filled_profile_is_valid() =>
        Assert.Empty(ProfileValidator.Validate(Dto with { FullName = "", Email = "", Summary = "" }));

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("Name <a@b.com>")]
    public void Invalid_emails_are_rejected(string email) => AssertError(Dto with { Email = email }, "email");

    [Fact]
    public void A_valid_email_is_accepted() =>
        Assert.DoesNotContain("email", ProfileValidator.Validate(Dto with { Email = "ayoub@example.com" }).Keys);

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com")]
    [InlineData("github.com/me")]
    public void Only_absolute_http_urls_are_accepted(string url) => AssertError(Dto with { GithubUrl = url }, "githubUrl");

    [Fact]
    public void Overlong_summary_is_rejected() => AssertError(Dto with { Summary = new string('x', 4001) }, "summary");

    [Fact]
    public void Experience_end_before_start_is_rejected()
    {
        var dto = Dto;
        var bad = dto.WorkExperiences[0] with { EndDate = new DateOnly(2020, 1, 1) };
        AssertError(dto with { WorkExperiences = [bad] }, "workExperiences[0].endDate");
    }

    [Fact]
    public void Experience_requires_company_and_title()
    {
        var dto = Dto;
        var bad = dto.WorkExperiences[0] with { Company = " ", Title = "" };
        var errors = ProfileValidator.Validate(dto with { WorkExperiences = [bad] });
        Assert.Contains("workExperiences[0].company", errors.Keys);
        Assert.Contains("workExperiences[0].title", errors.Keys);
    }

    [Fact]
    public void Duplicate_skills_are_rejected_case_insensitively()
    {
        var dto = Dto;
        var skills = new List<SkillDto>
        {
            new(null, "C#", "Backend", null, false),
            new(null, "c#", "Backend", null, false),
        };
        AssertError(dto with { Skills = skills }, "skills[1].name");
    }

    [Fact]
    public void Unknown_skill_levels_are_rejected_but_unrated_is_allowed()
    {
        var dto = Dto;
        AssertError(dto with { Skills = [new SkillDto(null, "X", "", "Guru", false)] }, "skills[0].level");
        Assert.Empty(ProfileValidator.Validate(dto with { Skills = [new SkillDto(null, "X", "", null, false)] }));
    }

    [Fact]
    public void Unknown_work_modes_and_employment_types_are_rejected()
    {
        var dto = Dto;
        AssertError(dto with { Preferences = dto.Preferences with { WorkModes = ["Teleport"] } }, "preferences.workModes");
        AssertError(dto with { Preferences = dto.Preferences with { EmploymentTypes = ["Forever"] } }, "preferences.employmentTypes");
    }

    [Fact]
    public void Salary_rules_are_enforced()
    {
        var dto = Dto;
        AssertError(dto with { Preferences = dto.Preferences with { MinSalary = -1 } }, "preferences.minSalary");
        AssertError(dto with { Preferences = dto.Preferences with { MinSalary = 50000, PreferredSalary = 40000 } }, "preferences.preferredSalary");
        AssertError(dto with { Preferences = dto.Preferences with { SalaryCurrency = "EURO" } }, "preferences.salaryCurrency");
    }

    [Fact]
    public void Missing_preferences_are_rejected() => AssertError(Dto with { Preferences = null! }, "preferences");
}
