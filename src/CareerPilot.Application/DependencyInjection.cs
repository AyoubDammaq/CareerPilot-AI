using CareerPilot.Application.Ai;
using CareerPilot.Application.Applications;
using CareerPilot.Application.Documents;
using CareerPilot.Application.Import;
using CareerPilot.Application.Jobs;
using CareerPilot.Application.Matching;
using CareerPilot.Application.Profiles;
using CareerPilot.Application.Tracking;
using Microsoft.Extensions.DependencyInjection;

namespace CareerPilot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, DiscoveryOptions? discovery = null, ScoringOptions? scoring = null, LlmOptions? llm = null, TrackingOptions? tracking = null)
    {
        services.AddSingleton(discovery ?? new DiscoveryOptions());
        services.AddSingleton(scoring ?? new ScoringOptions());
        services.AddSingleton(llm ?? new LlmOptions());
        services.AddSingleton(tracking ?? new TrackingOptions());
        services.AddScoped<TrackingService>();
        services.AddScoped<JobAnalysisService>();
        services.AddScoped<DocumentService>();
        services.AddScoped<ApplicationService>();
        services.AddScoped<CoverLetterService>();
        services.AddScoped<ProfileImportService>();
        services.AddScoped<MatchingService>();
        services.AddScoped<ProfileService>();
        services.AddScoped<CompanyService>();
        services.AddScoped<JobQueryService>();
        services.AddScoped<JobSearchService>();
        services.AddScoped<ManualImportService>();
        return services;
    }
}
