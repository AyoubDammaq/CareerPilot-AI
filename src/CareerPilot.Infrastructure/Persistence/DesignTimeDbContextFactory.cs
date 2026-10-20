using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CareerPilot.Infrastructure.Persistence;

/// <summary>
/// Used only by `dotnet ef` (migrations add/script). It lets the tooling build the model without
/// starting the API, and never connects to a database: the connection string is a placeholder
/// unless CAREERPILOT_DESIGN_DB is set (e.g. when running `dotnet ef database update` by hand).
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CareerPilotDbContext>
{
    public CareerPilotDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("CAREERPILOT_DESIGN_DB")
                         ?? "Host=localhost;Database=careerpilot_design;Username=design;Password=design";
        var options = new DbContextOptionsBuilder<CareerPilotDbContext>().UseNpgsql(connection).Options;
        return new CareerPilotDbContext(options);
    }
}
