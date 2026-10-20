using CareerPilot.Application.Profiles;
using CareerPilot.Domain.Profiles;

namespace CareerPilot.Tests.Profiles;

public class ProfileMapperTests
{
    private static UserProfile Profile() => DefaultProfileFactory.Create();

    [Fact]
    public void Round_trip_preserves_the_profile()
    {
        var original = ProfileMapper.ToDto(Profile());
        var target = new UserProfile();
        ProfileMapper.Apply(target, original);
        var result = ProfileMapper.ToDto(target);

        Assert.Equal(original.Skills.Select(s => s.Name), result.Skills.Select(s => s.Name));
        Assert.Equal(original.Languages.Select(l => (l.Name, l.Level)), result.Languages.Select(l => (l.Name, l.Level)));
        Assert.Equal(original.Preferences.TargetCountries, result.Preferences.TargetCountries);
        Assert.Equal(original.Preferences.IncludeInternationalRemote, result.Preferences.IncludeInternationalRemote);
        Assert.Equal(original.WorkExperiences[0].Company, result.WorkExperiences[0].Company);
    }

    [Fact]
    public void Items_with_unknown_ids_are_created_with_server_generated_ids()
    {
        var profile = Profile();
        var dto = ProfileMapper.ToDto(profile);
        var clientId = Guid.NewGuid();
        var withNew = dto with { Skills = [.. dto.Skills, new SkillDto(clientId, "Rust", "Other", null, false)] };

        ProfileMapper.Apply(profile, withNew);

        var added = profile.Skills.Single(s => s.Name == "Rust");
        Assert.NotEqual(clientId, added.Id);
        Assert.NotEqual(Guid.Empty, added.Id);
    }

    [Fact]
    public void Items_missing_from_the_dto_are_removed_and_existing_ids_are_kept()
    {
        var profile = Profile();
        var keptId = profile.Skills[0].Id;
        var dto = ProfileMapper.ToDto(profile);

        ProfileMapper.Apply(profile, dto with { Skills = [dto.Skills[0]] });

        var only = Assert.Single(profile.Skills);
        Assert.Equal(keptId, only.Id);
    }

    [Fact]
    public void Reordering_updates_positions_and_keeps_identity()
    {
        var profile = Profile();
        var dto = ProfileMapper.ToDto(profile);
        var reversed = dto.Skills.AsEnumerable().Reverse().ToList();
        var firstId = dto.Skills[0].Id;

        ProfileMapper.Apply(profile, dto with { Skills = reversed });

        var result = ProfileMapper.ToDto(profile);
        Assert.Equal(reversed.Select(s => s.Name), result.Skills.Select(s => s.Name));
        Assert.Equal(firstId, result.Skills[^1].Id);
    }

    [Fact]
    public void The_same_id_twice_creates_a_second_item_instead_of_aliasing()
    {
        var profile = Profile();
        var dto = ProfileMapper.ToDto(profile);
        var first = dto.Skills[0];
        var dup = first with { Name = "Duplicate id skill" };

        ProfileMapper.Apply(profile, dto with { Skills = [first, dup] });

        Assert.Equal(2, profile.Skills.Count);
        Assert.NotEqual(profile.Skills[0].Id, profile.Skills[1].Id);
    }

    [Fact]
    public void Text_is_trimmed_blank_optionals_become_null_and_lists_are_cleaned()
    {
        var profile = Profile();
        var dto = ProfileMapper.ToDto(profile);
        var messy = dto with
        {
            FullName = "  Ayoub  ",
            Phone = "   ",
            Preferences = dto.Preferences with
            {
                TargetCountries = [" France ", "france", "", "Belgium"],
                SalaryCurrency = " eur ",
            },
        };

        ProfileMapper.Apply(profile, messy);

        Assert.Equal("Ayoub", profile.FullName);
        Assert.Null(profile.Phone);
        Assert.Equal(new[] { "France", "Belgium" }, profile.Preferences.TargetCountries.ToArray());
        Assert.Equal("EUR", profile.Preferences.SalaryCurrency);
    }

    [Theory]
    [InlineData("Expert", SkillLevel.Expert)]
    [InlineData(" Beginner ", SkillLevel.Beginner)]
    public void Known_skill_levels_are_parsed(string input, SkillLevel expected) =>
        Assert.Equal(expected, ProfileMapper.ParseLevel(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("expert")]
    [InlineData("Wizard")]
    public void Unknown_or_blank_levels_stay_unrated(string? input) =>
        Assert.Null(ProfileMapper.ParseLevel(input));

    [Fact]
    public void Verification_flags_come_only_from_the_dto()
    {
        var profile = Profile();
        var dto = ProfileMapper.ToDto(profile);
        Assert.All(dto.Skills, s => Assert.False(s.IsVerified));

        ProfileMapper.Apply(profile, dto with { Skills = [dto.Skills[0] with { IsVerified = true }] });

        Assert.True(Assert.Single(profile.Skills).IsVerified);
    }
}
