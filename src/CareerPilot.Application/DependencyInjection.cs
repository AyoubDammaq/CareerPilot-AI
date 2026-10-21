using CareerPilot.Application.Jobs;
using CareerPilot.Application.Matching;
using CareerPilot.Application.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace CareerPilot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, DiscoveryOptions? discovery = null, ScoringOptions? scoring = null)
    {
        services.AddSingleton(discovery ?? new DiscoveryOptions());
        services.AddSingleton(scoring ?? new ScoringOptions());
        services.AddScoped<MatchingService>();
        services.AddScoped<ProfileService>();
        services.AddScoped<CompanyService>();
        services.AddScoped<JobQueryService>();
        services.AddScoped<JobSearchService>();
        return services;
    }
}
