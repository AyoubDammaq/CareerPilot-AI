using CareerPilot.Application.Abstractions;
using CareerPilot.Application.Jobs;
using CareerPilot.Infrastructure.Jobs;
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
        services.AddScoped<IJobStore, EfJobStore>();

        services.AddSingleton(new ConnectorOptions());
        AddConnector<GreenhouseConnector>(services);
        AddConnector<LeverConnector>(services);
        AddConnector<AshbyConnector>(services);
        return services;
    }

    private static void AddConnector<T>(IServiceCollection services) where T : class, IJobSourceConnector
    {
        services.AddHttpClient<T>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(ConnectorSupport.UserAgent);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });
        services.AddTransient<IJobSourceConnector>(sp => sp.GetRequiredService<T>());
    }
}
