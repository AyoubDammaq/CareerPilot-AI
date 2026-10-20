using CareerPilot.Application.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace CareerPilot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ProfileService>();
        return services;
    }
}
