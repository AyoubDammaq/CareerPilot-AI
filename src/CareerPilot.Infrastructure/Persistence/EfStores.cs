using CareerPilot.Application.Abstractions;
using CareerPilot.Domain.Audit;
using CareerPilot.Domain.Profiles;
using Microsoft.EntityFrameworkCore;

namespace CareerPilot.Infrastructure.Persistence;

public sealed class EfProfileStore(CareerPilotDbContext db) : IProfileStore
{
    public Task<UserProfile?> GetAsync(CancellationToken ct) =>
        db.Profiles
            .Include(p => p.WorkExperiences)
            .Include(p => p.Education)
            .Include(p => p.Skills)
            .Include(p => p.Projects)
            .Include(p => p.Languages)
            .Include(p => p.Preferences)
            .AsSplitQuery()
            .OrderBy(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public void Add(UserProfile profile) => db.Profiles.Add(profile);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class EfAuditLog(CareerPilotDbContext db) : IAuditLog
{
    public void Stage(string actor, string action, string entityType, Guid? entityId, string? details) =>
        db.AuditEvents.Add(new AuditEvent
        {
            Actor = actor,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
        });
}
