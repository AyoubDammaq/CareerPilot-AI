using CareerPilot.Application.Ai;
using CareerPilot.Application.Documents;
using CareerPilot.Application.Jobs;
using CareerPilot.Application.Matching;
using CareerPilot.Application.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace CareerPilot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, DiscoveryOptions? discovery = null, ScoringOptions? scoring = null, LlmOptions? llm = null)
    {
        services.AddSingleton(discovery ?? new DiscoveryOptions());
        services.AddSingleton(scoring ?? new ScoringOptions());
        services.AddSingleton(llm ?? new LlmOptions());
        services.AddScoped<JobAnalysisService>();
        services.AddScoped<DocumentService>();
        services.AddScoped<CoverLetterService>();
        services.AddScoped<MatchingService>();
        services.AddScoped<ProfileService>();
        services.AddScoped<CompanyService>();
        services.AddScoped<JobQueryService>();
        services.AddScoped<JobSearchService>();
        return services;
    }
}
