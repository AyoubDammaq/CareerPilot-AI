using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerPilot.Application.Profiles;

namespace CareerPilot.IntegrationTests;

/// <summary>Runs the real HTTP pipeline, EF Core and PostgreSQL. Tests run sequentially and share one database.</summary>
public class ApiTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [RequiresDatabaseFact]
    public async Task Liveness_and_readiness_are_anonymous_and_healthy()
    {
        using var client = api.CreateAnonymousClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Protected_endpoints_reject_anonymous_callers()
    {
        using var client = api.CreateAnonymousClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PutAsJsonAsync("/api/profile", new { })).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task A_garbage_token_is_rejected()
    {
        using var client = api.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "not.a.token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/profile")).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Login_with_a_wrong_password_is_refused()
    {
        using var client = api.CreateAnonymousClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { password = "definitely-wrong-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task The_profile_is_readable_and_contains_no_pre_verified_facts()
    {
        using var client = await api.CreateAuthenticatedClientAsync();

        var profile = await client.GetFromJsonAsync<ProfileDto>("/api/profile", Json);

        Assert.NotNull(profile);
        Assert.NotEmpty(profile.Skills);
        Assert.All(profile.Skills, s => Assert.False(s.IsVerified));
        Assert.Contains("France", profile.Preferences.TargetCountries);
    }

    [RequiresDatabaseFact]
    public async Task An_update_is_persisted_and_bumps_the_version()
    {
        using var client = await api.CreateAuthenticatedClientAsync();
        var current = (await client.GetFromJsonAsync<ProfileDto>("/api/profile", Json))!;
        var headline = $"Integration headline {Guid.NewGuid():N}";

        var put = await client.PutAsJsonAsync("/api/profile", current with { Headline = headline }, Json);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var updated = (await put.Content.ReadFromJsonAsync<ProfileDto>(Json))!;
        Assert.Equal(current.Version + 1, updated.Version);

        var reread = (await client.GetFromJsonAsync<ProfileDto>("/api/profile", Json))!;
        Assert.Equal(headline, reread.Headline);
        Assert.Equal(updated.Version, reread.Version);
        Assert.Equal(current.Skills.Count, reread.Skills.Count);
    }

    [RequiresDatabaseFact]
    public async Task A_stale_update_gets_409_Conflict()
    {
        using var client = await api.CreateAuthenticatedClientAsync();
        var stale = (await client.GetFromJsonAsync<ProfileDto>("/api/profile", Json))!;
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/profile", stale, Json)).StatusCode);

        var response = await client.PutAsJsonAsync("/api/profile", stale with { Headline = "stale" }, Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task An_invalid_update_gets_400_with_field_errors()
    {
        using var client = await api.CreateAuthenticatedClientAsync();
        var current = (await client.GetFromJsonAsync<ProfileDto>("/api/profile", Json))!;

        var response = await client.PutAsJsonAsync("/api/profile", current with { Email = "not-an-email" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"email\"", body);
    }

    [RequiresDatabaseFact]
    public async Task The_dashboard_reports_completeness()
    {
        using var client = await api.CreateAuthenticatedClientAsync();
        var dashboard = await client.GetFromJsonAsync<DashboardDto>("/api/dashboard", Json);
        Assert.NotNull(dashboard);
        Assert.InRange(dashboard.ProfileCompleteness, 0, 100);
    }
}
