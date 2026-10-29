using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerPilot.Application.Jobs;

namespace CareerPilot.IntegrationTests;

/// <summary>Manual job import through HTTP, EF Core and PostgreSQL. No network is used: every URL case is refused before any request.</summary>
public class ManualImportTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [RequiresDatabaseFact]
    public async Task Import_endpoints_require_authentication()
    {
        using var client = api.CreateAnonymousClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/jobs/import/url", new { url = "https://example.com/a" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/jobs/import/text", new { title = "x" })).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Unsafe_and_manual_only_addresses_are_refused_with_a_reason()
    {
        using var client = await api.CreateAuthenticatedClientAsync();
        foreach (var url in new[] { "https://169.254.169.254/latest", "https://localhost/x", "http://example.com/x", "https://www.linkedin.com/jobs/view/1" })
        {
            var response = await client.PostAsJsonAsync("/api/jobs/import/url", new { url });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("\"url\"", await response.Content.ReadAsStringAsync());
        }
    }

    [RequiresDatabaseFact]
    public async Task Pasted_text_is_imported_once_scored_and_starts_no_application()
    {
        using var client = await api.CreateAuthenticatedClientAsync();
        var title = "Manual Backend Engineer " + Guid.NewGuid().ToString("N")[..8];
        var body = new { title, company = "Imported Co", location = "Tunis, Tunisia", description = "Build services with C# and ASP.NET Core for our customers.", url = "https://careers.example.com/jobs/1" };

        var first = await client.PostAsJsonAsync("/api/jobs/import/text", body);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstResult = (await first.Content.ReadFromJsonAsync<ManualImportResult>(Json))!;
        Assert.True(firstResult.Created);

        var second = (await (await client.PostAsJsonAsync("/api/jobs/import/text", body)).Content.ReadFromJsonAsync<ManualImportResult>(Json))!;
        Assert.False(second.Created);
        Assert.Equal(firstResult.ListingId, second.ListingId);

        var detail = await client.GetStringAsync($"/api/jobs/{firstResult.ListingId}");
        Assert.Contains("Manual", detail);
        Assert.Contains(title, detail);

        var applications = await client.GetStringAsync("/api/applications");
        Assert.DoesNotContain(firstResult.ListingId.ToString(), applications);
    }

    [RequiresDatabaseFact]
    public async Task The_manual_placeholder_is_hidden_and_cannot_be_added_as_a_board()
    {
        using var client = await api.CreateAuthenticatedClientAsync();
        await client.PostAsJsonAsync("/api/jobs/import/text", new { title = "Placeholder check engineer", company = "Co", location = "Tunis", description = "C# services and APIs for customers." });

        var companies = (await client.GetFromJsonAsync<List<CompanyDto>>("/api/sources/companies", Json))!;
        Assert.DoesNotContain(companies, c => c.Kind == "Manual");

        var add = await client.PostAsJsonAsync("/api/sources/companies", new { name = "X", kind = "Manual", boardToken = "x", isEnabled = true });
        Assert.Equal(HttpStatusCode.BadRequest, add.StatusCode);
    }
}
