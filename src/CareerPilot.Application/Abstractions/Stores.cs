using CareerPilot.Domain.Profiles;

namespace CareerPilot.Application.Abstractions;

/// <summary>
/// Persistence of the (single) career profile aggregate. GetAsync returns a tracked entity,
/// so changes made to it are persisted by SaveChangesAsync. SaveChangesAsync is the unit of
/// work: staged audit events are committed in the same transaction.
/// </summary>
public interface IProfileStore
{
    Task<UserProfile?> GetAsync(CancellationToken ct);
    void Add(UserProfile profile);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IAuditLog
{
    /// <summary>Stages an event; it is persisted by the next IProfileStore.SaveChangesAsync.</summary>
    void Stage(string actor, string action, string entityType, Guid? entityId, string? details);
}
