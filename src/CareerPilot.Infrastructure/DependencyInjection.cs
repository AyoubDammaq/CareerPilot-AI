using CareerPilot.Application.Abstractions;
using CareerPilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CareerPilot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:Default is not configured (env var ConnectionStrings__Default).");

        services.AddDbContext<CareerPilotDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<IProfileStore, EfProfileStore>();
        services.AddScoped<IAuditLog, EfAuditLog>();
        return services;
    }
}
