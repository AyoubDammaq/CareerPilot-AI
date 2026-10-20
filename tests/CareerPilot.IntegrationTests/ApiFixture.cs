using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

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

    private static async Task<string> LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { password = Password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResult>();
        return body!.Token;
    }

    public void Dispose() => _factory?.Dispose();

    private sealed record LoginResult(string Token, DateTimeOffset ExpiresAt);
}
