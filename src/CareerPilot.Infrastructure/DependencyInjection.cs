using CareerPilot.Application.Abstractions;
using CareerPilot.Application.Ai;
using CareerPilot.Application.Applications;
using CareerPilot.Application.Documents;
using CareerPilot.Infrastructure.Documents;
using CareerPilot.Infrastructure.Ai;
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
        services.AddScoped<IDocumentStore, EfDocumentStore>();
        services.AddScoped<IApplicationStore, EfApplicationStore>();
        services.AddScoped<IApplicationReader>(sp => sp.GetRequiredService<IApplicationStore>());
        services.AddSingleton<ICvRenderer, DocxCvRenderer>();
        services.AddSingleton<IPdfRenderer, PdfDocumentRenderer>();
        services.AddSingleton<CareerPilot.Application.Import.ITextExtractor, DocumentTextExtractor>();

        services.AddHttpClient<AnthropicLlmClient>((sp, c) => c.Timeout = TimeSpan.FromSeconds(sp.GetRequiredService<LlmOptions>().TimeoutSeconds));
        services.AddHttpClient<OpenAiCompatibleLlmClient>((sp, c) => c.Timeout = TimeSpan.FromSeconds(sp.GetRequiredService<LlmOptions>().TimeoutSeconds));
        services.AddScoped<ILlmClient>(sp => sp.GetRequiredService<LlmOptions>().Provider switch
        {
            LlmProvider.Anthropic => sp.GetRequiredService<AnthropicLlmClient>(),
            LlmProvider.OpenAiCompatible => sp.GetRequiredService<OpenAiCompatibleLlmClient>(),
            _ => new DisabledLlmClient(),
        });

        services.AddSingleton(new ConnectorOptions());
        AddConnector<GreenhouseConnector>(services);
        AddConnector<LeverConnector>(services);
        AddConnector<AshbyConnector>(services);
        AddConnector<HimalayasConnector>(services);
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
