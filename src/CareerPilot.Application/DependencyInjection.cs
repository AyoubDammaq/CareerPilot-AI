using CareerPilot.Application.Jobs;
using CareerPilot.Application.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace CareerPilot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, DiscoveryOptions? discovery = null)
    {
        services.AddSingleton(discovery ?? new DiscoveryOptions());
        services.AddScoped<ProfileService>();
        services.AddScoped<CompanyService>();
        services.AddScoped<JobQueryService>();
        services.AddScoped<JobSearchService>();
        return services;
    }
}
