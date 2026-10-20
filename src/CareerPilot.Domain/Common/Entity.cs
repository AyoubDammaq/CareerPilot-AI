namespace CareerPilot.Domain.Common;

/// <summary>
/// Base class for persisted entities. Ids are generated client-side (time-ordered GUIDs),
/// so the database model must use ValueGeneratedNever for them.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}

/// <summary>Raised when a domain invariant would be violated.</summary>
public sealed class DomainException(string message) : Exception(message);
