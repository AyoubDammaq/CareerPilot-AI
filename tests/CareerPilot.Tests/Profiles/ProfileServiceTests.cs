using CareerPilot.Application.Abstractions;
using CareerPilot.Application.Common;
using CareerPilot.Application.Profiles;
using CareerPilot.Domain.Profiles;

namespace CareerPilot.Tests.Profiles;

public class ProfileServiceTests
{
    private sealed class FakeStore : IProfileStore
    {
        public UserProfile? Profile { get; private set; }
        public int SaveCount { get; private set; }
        public Task<UserProfile?> GetAsync(CancellationToken ct) => Task.FromResult(Profile);
        public void Add(UserProfile profile) => Profile = profile;
        public Task SaveChangesAsync(CancellationToken ct)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAudit : IAuditLog
    {
        public List<(string Action, string? Details)> Events { get; } = [];
        public void Stage(string actor, string action, string entityType, Guid? entityId, string? details) =>
            Events.Add((action, details));
    }

    private static (ProfileService Service, FakeStore Store, FakeAudit Audit) Create()
    {
        var store = new FakeStore();
        var audit = new FakeAudit();
        return (new ProfileService(store, audit), store, audit);
    }

    [Fact]
    public async Task First_read_creates_the_default_profile_once()
    {
        var (service, store, audit) = Create();

        var first = await service.GetAsync(CancellationToken.None);
        var second = await service.GetAsync(CancellationToken.None);

        Assert.Equal(1, first.Version);
        Assert.Equal(first.Skills.Count, second.Skills.Count);
        Assert.Equal(1, store.SaveCount);
        Assert.Single(audit.Events, e => e.Action == "profile.created");
    }

    [Fact]
    public async Task Update_increments_version_saves_and_audits_without_personal_data()
    {
        var (service, store, audit) = Create();
        var dto = await service.GetAsync(CancellationToken.None);

        var updated = await service.UpdateAsync(dto with { FullName = "Secret Name", Email = "secret@example.com" }, CancellationToken.None);

        Assert.Equal(2, updated.Version);
        Assert.Equal("Secret Name", updated.FullName);
        var evt = Assert.Single(audit.Events, e => e.Action == "profile.updated");
        Assert.DoesNotContain("Secret", evt.Details);
        Assert.DoesNotContain("secret@example.com", evt.Details);
        Assert.Equal(2, store.SaveCount); // creation + update
    }

    [Fact]
    public async Task A_stale_version_is_rejected_and_nothing_is_saved()
    {
        var (service, store, _) = Create();
        var loaded = await service.GetAsync(CancellationToken.None);
        await service.UpdateAsync(loaded, CancellationToken.None); // version becomes 2
        var savesBefore = store.SaveCount;

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            service.UpdateAsync(loaded with { Headline = "Stale edit" }, CancellationToken.None));

        Assert.Equal(savesBefore, store.SaveCount);
    }

    [Fact]
    public async Task An_invalid_profile_is_rejected_before_anything_is_saved()
    {
        var (service, store, _) = Create();
        var loaded = await service.GetAsync(CancellationToken.None);
        var savesBefore = store.SaveCount;

        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            service.UpdateAsync(loaded with { Email = "nope" }, CancellationToken.None));

        Assert.Contains("email", ex.Errors.Keys);
        Assert.Equal(savesBefore, store.SaveCount);
    }

    [Fact]
    public async Task Dashboard_reports_completeness_and_the_implemented_modules()
    {
        var (service, _, _) = Create();

        var dashboard = await service.GetDashboardAsync(CancellationToken.None);

        Assert.Equal(45, dashboard.ProfileCompleteness);
        Assert.Contains("Professional summary", dashboard.MissingItems);
        Assert.Equal("profile", Assert.Single(dashboard.ImplementedModules));
    }
}
