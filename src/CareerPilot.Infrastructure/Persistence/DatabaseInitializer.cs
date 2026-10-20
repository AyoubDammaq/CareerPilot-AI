using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CareerPilot.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    private const int MaxAttempts = 10;

    /// <summary>
    /// Applies EF migrations. If the project has no migrations yet, falls back to EnsureCreated
    /// (development convenience only: create a real migration before relying on this data).
    /// Retries while the database container is still starting.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, ILogger logger, CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CareerPilotDbContext>();

                if (db.Database.GetMigrations().Any())
                {
                    await db.Database.MigrateAsync(ct);
                }
                else
                {
                    logger.LogWarning("No EF migrations found; using EnsureCreated. Create an initial migration before storing real data.");
                    await db.Database.EnsureCreatedAsync(ct);
                }

                await WatchlistSeeder.SeedAsync(db, ct);
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Database not ready (attempt {Attempt}/{Max}); retrying in 3 seconds.", attempt, MaxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }
    }
}
