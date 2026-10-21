using CareerPilot.Application.Applications;
using CareerPilot.Domain.Applications;
using Microsoft.EntityFrameworkCore;

namespace CareerPilot.Infrastructure.Persistence;

public sealed class EfApplicationStore(CareerPilotDbContext db) : IApplicationStore
{
    public void Add(JobApplication application) => db.JobApplications.Add(application);
    public Task<JobApplication?> GetAsync(Guid id, CancellationToken ct) => db.JobApplications.FirstOrDefaultAsync(a => a.Id == id, ct);
    public Task<JobApplication?> GetByJobAsync(Guid jobId, CancellationToken ct) => db.JobApplications.FirstOrDefaultAsync(a => a.CanonicalJobId == jobId, ct);
    public Task<List<JobApplication>> ListAsync(CancellationToken ct) => db.JobApplications.ToListAsync(ct);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
