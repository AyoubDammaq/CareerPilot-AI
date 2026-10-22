using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CareerPilot.API;
using CareerPilot.API.Auth;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CareerPilot.IntegrationTests;

public class HardeningConfigTests
{
    [Theory]
    [InlineData("change-me-too-at-least-12-chars", "a-real-signing-key-that-is-at-least-32-characters-long")]
    [InlineData("a-real-password-123", "replace-with-openssl-rand-base64-48-output")]
    public void Example_values_from_the_env_file_are_refused(string password, string key)
    {
        var o = new AuthOptions { Password = password, JwtKey = key };
        Assert.Throws<InvalidOperationException>(o.EnsureValid);
    }

    [Fact]
    public void Real_values_are_accepted()
    {
        new AuthOptions { Password = "a-real-password-123", JwtKey = "a-real-signing-key-that-is-at-least-32-characters-long" }.EnsureValid();
    }

    [Theory]
    [InlineData(0, 600, 20, 7, 1)]
    [InlineData(5, 5, 20, 7, 1)]
    [InlineData(5, 600, 0, 7, 1)]
    [InlineData(5, 600, 20, 0, 1)]
    [InlineData(5, 600, 20, 7, 0)]
    public void Silly_security_settings_stop_the_start(int login, int global, int expensive, int mb, int hops)
    {
        var o = new SecurityOptions { LoginPerMinute = login, GlobalPerMinute = global, ExpensivePerMinute = expensive, MaxRequestBodyMb = mb, ForwardLimit = hops };
        Assert.Throws<InvalidOperationException>(o.EnsureValid);
    }

    [Fact]
    public void The_defaults_are_valid()
    {
        new SecurityOptions().EnsureValid();
    }
}

public class HardeningTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    [RequiresDatabaseFact]
    public async Task Every_response_carries_the_security_headers_even_a_refusal()
    {
        using var client = api.CreateAnonymousClient();
        foreach (var path in new[] { "/api/profile", "/health/live" })
        {
            var r = await client.GetAsync(path);
            Assert.Equal("nosniff", r.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("DENY", r.Headers.GetValues("X-Frame-Options").Single());
            Assert.Equal("no-referrer", r.Headers.GetValues("Referrer-Policy").Single());
            Assert.Contains("default-src 'none'", r.Headers.GetValues("Content-Security-Policy").Single());
            Assert.Contains("no-store", r.Headers.CacheControl!.ToString());
        }
    }

    [RequiresDatabaseFact]
    public async Task Authenticated_data_is_marked_not_cacheable()
    {
        using var client = await api.CreateAuthenticatedClientAsync();
        var r = await client.GetAsync("/api/profile");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("no-store", r.Headers.CacheControl!.ToString());
    }

    /// <summary>A host with its own limits. Environment variables are read when the host starts, so they are set only around that.</summary>
    private static WebApplicationFactory<Program> HostWith(params (string Name, string Value)[] settings)
    {
        foreach (var (n, v) in settings) Environment.SetEnvironmentVariable(n, v);
        try
        {
            var factory = new WebApplicationFactory<Program>();
            factory.CreateClient().Dispose(); // starts the host now, while the settings are in place
            return factory;
        }
        finally
        {
            foreach (var (n, _) in settings) Environment.SetEnvironmentVariable(n, n == "Security__ExpensivePerMinute" ? "1000" : null);
        }
    }

    [RequiresDatabaseFact]
    public async Task Too_many_wrong_passwords_are_slowed_down_and_do_not_reveal_anything_extra()
    {
        using var factory = HostWith(("Security__LoginPerMinute", "3"));
        using var client = factory.CreateClient();
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
            codes.Add((await client.PostAsJsonAsync("/api/auth/login", new { password = "wrong-password-" + i })).StatusCode);
        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests], codes);
    }

    [RequiresDatabaseFact]
    public async Task The_costly_endpoints_have_their_own_lower_limit()
    {
        using var factory = HostWith(("Security__ExpensivePerMinute", "2"));
        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { password = ApiFixture.Password });
        var token = (await login.Content.ReadFromJsonAsync<LoginResult>())!.Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++) codes.Add((await client.PostAsync("/api/jobs/rescore", null)).StatusCode);
        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, codes.Take(2));
        Assert.Equal(HttpStatusCode.TooManyRequests, codes[2]);

        // Ordinary reading is not affected by the costly-endpoint limit.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/profile")).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task A_body_far_over_the_limit_is_not_accepted_as_a_cv()
    {
        using var client = await api.CreateAuthenticatedClientAsync();
        using var form = new MultipartFormDataContent { { new ByteArrayContent(new byte[6 * 1024 * 1024 - 1000]), "file", "big.txt" } };
        var r = await client.PostAsync("/api/profile/import/preview", form);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    private sealed record LoginResult(string Token);
}
