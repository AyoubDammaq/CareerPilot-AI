using CareerPilot.Domain.Jobs;
using Microsoft.EntityFrameworkCore;

namespace CareerPilot.Infrastructure.Persistence;

/// <summary>
/// On a brand-new database only (no companies and no search ever run), adds a small starter watchlist so
/// the first search has something to read. These boards were checked against the public feeds on
/// 2026-10-20. The owner can disable, edit or delete every entry; deleting them all does not re-seed.
/// </summary>
public static class WatchlistSeeder
{
    public static readonly (string Name, JobSourceKind Kind, string Token)[] Starter =
    [
        ("Datadog", JobSourceKind.Greenhouse, "datadog"),
        ("Algolia", JobSourceKind.Greenhouse, "algolia"),
        ("GitLab", JobSourceKind.Greenhouse, "gitlab"),
        ("Spotify", JobSourceKind.Lever, "spotify"),
        ("Ashby", JobSourceKind.Ashby, "ashby"),
    ];

    public static async Task SeedAsync(CareerPilotDbContext db, CancellationToken ct)
    {
        if (await db.WatchedCompanies.AnyAsync(ct) || await db.SearchRuns.AnyAsync(ct))
            return;

        foreach (var (name, kind, token) in Starter)
            db.WatchedCompanies.Add(new WatchedCompany { Name = name, Kind = kind, BoardToken = token });
        await db.SaveChangesAsync(ct);
    }
}
