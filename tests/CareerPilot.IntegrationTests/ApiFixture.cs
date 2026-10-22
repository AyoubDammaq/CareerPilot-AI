using System.Net.Http.Headers;
using System.Net.Http.Json;
using CareerPilot.Application.Jobs;
using CareerPilot.Domain.Jobs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

// The tests share one database and each host runs a background worker; run them one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CareerPilot.IntegrationTests;

/// <summary>
/// These tests need a real PostgreSQL database (set CAREERPILOT_TEST_DB to its connection string).
/// Without it they are reported as skipped, never as passed.
/// </summary>
public sealed class RequiresDatabaseFactAttribute : FactAttribute
{
    public const string Variable = "CAREERPILOT_TEST_DB";

    public RequiresDatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable)))
            Skip = $"Set {Variable} to a PostgreSQL connection string to run the integration tests.";
    }
}

public sealed class ApiFixture : IDisposable
{
    public const string Password = "integration-test-password-123";
    private const string JwtKey = "integration-test-signing-key-that-is-long-enough-0123456789";

    private readonly WebApplicationFactory<Program>? _factory;
    private WebApplicationFactory<Program>? _fakeFactory;
    private string? _token;

    public ApiFixture()
    {
        var db = Environment.GetEnvironmentVariable(RequiresDatabaseFactAttribute.Variable);
        if (string.IsNullOrWhiteSpace(db))
            return; // tests are skipped; do not boot the host

        // Environment variables are visible to Program's top-level configuration reads.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", db);
        Environment.SetEnvironmentVariable("Auth__Password", Password);
        Environment.SetEnvironmentVariable("Auth__JwtKey", JwtKey);
        // The shared host serves many tests in one minute; the limits themselves are tested in HardeningTests.
        Environment.SetEnvironmentVariable("Security__ExpensivePerMinute", "1000");
        _factory = new WebApplicationFactory<Program>();
    }

    public HttpClient CreateAnonymousClient() => _factory!.CreateClient();

    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = _factory!.CreateClient();
        _token ??= await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return client;
    }

    /// <summary>
    /// A host whose job-board connectors are replaced by deterministic fakes, so no live site is ever called.
    /// For every company the fake returns a relevant job, a sales job and a job outside the target countries.
    /// </summary>
    public async Task<HttpClient> CreateFakeConnectorClientAsync()
    {
        _fakeFactory ??= _factory!.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            services.RemoveAll<IJobSourceConnector>();
            foreach (var kind in Enum.GetValues<JobSourceKind>())
                services.AddTransient<IJobSourceConnector>(_ => new FakeConnector(kind));
        }));
        var client = _fakeFactory.CreateClient();
        _token ??= await LoginAsync(_factory!.CreateClient());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        // The profile is created on first read (as when the owner opens the app); searching needs it for
        // location checks and scoring, so tests must not depend on another test having created it.
        (await client.GetAsync("/api/profile")).EnsureSuccessStatusCode();
        return client;
    }

    private sealed class FakeConnector(JobSourceKind kind) : IJobSourceConnector
    {
        public JobSourceKind Kind => kind;

        public Task<ConnectorResult> FetchAsync(WatchedCompany company, CancellationToken ct)
        {
            SourceJob Make(string suffix, string title, string location, string? country = null) => new(
                $"{company.BoardToken}-{suffix}", title, location, country, null, "Full time",
                "We build services with C# and PostgreSQL.", null, null,
                $"https://jobs.example.test/{company.BoardToken}/{suffix}", $"https://jobs.example.test/{company.BoardToken}/{suffix}/apply");

            return Task.FromResult(new ConnectorResult(
            [
                Make("be", $"Backend Engineer {company.BoardToken}", "Paris, France"),
                Make("sales", "Sales Manager", "Paris, France"),
                Make("ny", "Software Engineer", "New York, United States"),
            ], IsCompleteSnapshot: true));
        }
    }

    private static async Task<string> LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { password = Password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResult>();
        return body!.Token;
    }

    public void Dispose()
    {
        _fakeFactory?.Dispose();
        _factory?.Dispose();
    }

    private sealed record LoginResult(string Token, DateTimeOffset ExpiresAt);
}
