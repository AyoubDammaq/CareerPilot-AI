using CareerPilot.Application.Profiles;

namespace CareerPilot.Tests.Profiles;

internal static class TestData
{
    /// <summary>A DTO derived from the shipped default profile (valid by construction).</summary>
    public static ProfileDto ValidDto() => ProfileMapper.ToDto(DefaultProfileFactory.Create());
}
