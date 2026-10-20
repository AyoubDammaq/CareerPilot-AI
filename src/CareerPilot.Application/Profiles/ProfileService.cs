using CareerPilot.Application.Abstractions;
using CareerPilot.Application.Common;
using CareerPilot.Domain.Profiles;

namespace CareerPilot.Application.Profiles;

public sealed class ProfileService(IProfileStore store, IAuditLog audit)
{
    private const string Actor = "owner";

    public async Task<ProfileDto> GetAsync(CancellationToken ct)
    {
        var profile = await GetOrCreateAsync(ct);
        return ProfileMapper.ToDto(profile);
    }

    public async Task<ProfileDto> UpdateAsync(ProfileDto dto, CancellationToken ct)
    {
        var errors = ProfileValidator.Validate(dto);
        if (errors.Count > 0)
            throw new ValidationFailedException(errors);

        var profile = await GetOrCreateAsync(ct);
        if (profile.Version != dto.Version)
            throw new ConcurrencyConflictException(
                "The profile was changed elsewhere since it was loaded. Reload it and re-apply your edits.");

        ProfileMapper.Apply(profile, dto);
        profile.Version++;
        profile.Touch();

        // Details deliberately exclude personal data.
        audit.Stage(Actor, "profile.updated", nameof(UserProfile), profile.Id, $"version {profile.Version}");
        await store.SaveChangesAsync(ct);

        return ProfileMapper.ToDto(profile);
    }

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct)
    {
        var profile = await GetOrCreateAsync(ct);
        var completeness = ProfileCompleteness.Compute(profile);
        return new DashboardDto(completeness.Percent, completeness.Missing, ["profile"]);
    }

    private async Task<UserProfile> GetOrCreateAsync(CancellationToken ct)
    {
        var existing = await store.GetAsync(ct);
        if (existing is not null)
            return existing;

        var created = DefaultProfileFactory.Create();
        store.Add(created);
        audit.Stage(Actor, "profile.created", nameof(UserProfile), created.Id, "initial profile created");
        await store.SaveChangesAsync(ct);
        return created;
    }
}
