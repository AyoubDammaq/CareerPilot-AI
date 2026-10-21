using CareerPilot.Application.Documents;
using CareerPilot.Domain.Documents;
using CareerPilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareerPilot.Infrastructure.Documents;

public sealed class EfDocumentStore(CareerPilotDbContext db) : IDocumentStore
{
    public void Add(GeneratedDocument document) => db.GeneratedDocuments.Add(document);

    public Task<GeneratedDocument?> GetAsync(Guid id, CancellationToken ct) =>
        db.GeneratedDocuments.FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
