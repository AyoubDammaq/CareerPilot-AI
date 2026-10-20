using CareerPilot.Domain.Common;

namespace CareerPilot.Domain.Audit;

/// <summary>Append-only record of an important action. Keep Details free of personal data.</summary>
public class AuditEvent : Entity
{
    public string Actor { get; set; } = "";
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public Guid? EntityId { get; set; }
    public string? Details { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
