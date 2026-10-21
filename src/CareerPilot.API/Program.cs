using System.Text;
using System.Threading.RateLimiting;
using CareerPilot.API.Auth;
using CareerPilot.API.Jobs;
using CareerPilot.Application;
using CareerPilot.Application.Ai;
using CareerPilot.Application.Common;
using CareerPilot.Application.Jobs;
using CareerPilot.Application.Matching;
using CareerPilot.Application.Profiles;
using CareerPilot.Infrastructure;
using CareerPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ---- configuration -------------------------------------------------------------------
var authOptions = builder.Configuration.GetSection(AuthOptions.Section).Get<AuthOptions>() ?? new AuthOptions();
authOptions.EnsureValid(); // fail fast: never start with a missing or weak secret
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.Section));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<TokenService>();

var discovery = builder.Configuration.GetSection(DiscoveryOptions.Section).Get<DiscoveryOptions>() ?? new DiscoveryOptions();
var scoring = builder.Configuration.GetSection(ScoringOptions.Section).Get<ScoringOptions>() ?? new ScoringOptions();
var llm = builder.Configuration.GetSection(LlmOptions.Section).Get<LlmOptions>() ?? new LlmOptions();
builder.Services.AddApplication(discovery, scoring, llm);
builder.Services.AddSingleton<SearchRunQueue>();
builder.Services.AddHostedService<SearchRunWorker>();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.Configure<JsonOptions>(o => o.SerializerOptions.PropertyNameCaseInsensitive = true);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = authOptions.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.JwtKey)),
        };
    });
builder.Services.AddAuthorization(o =>
    o.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser().Build()); // everything requires auth unless explicitly anonymous

// Brute-force protection for the login endpoint.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<CareerPilotDbContext>("database", tags: ["ready"]);

var app = builder.Build();

// ---- pipeline ------------------------------------------------------------------------
app.UseExceptionHandler(handler => handler.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Errors");

    switch (ex)
    {
        case ValidationFailedException v:
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsJsonAsync(new { title = "One or more validation errors occurred.", status = 400, errors = v.Errors });
            break;
        case ConcurrencyConflictException or DbUpdateConcurrencyException:
            ctx.Response.StatusCode = StatusCodes.Status409Conflict;
            await ctx.Response.WriteAsJsonAsync(new
            {
                title = "The profile was changed elsewhere. Reload it and re-apply your edits.",
                status = 409,
            });
            break;
        case NotFoundException nf:
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            await ctx.Response.WriteAsJsonAsync(new { title = nf.Message, status = 404 });
            break;
        case ConflictException conflict:
            ctx.Response.StatusCode = StatusCodes.Status409Conflict;
            await ctx.Response.WriteAsJsonAsync(new { title = conflict.Message, status = 409 });
            break;
        case AiUnavailableException ai:
            ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await ctx.Response.WriteAsJsonAsync(new { title = ai.Message, status = 503 });
            break;
        case BadHttpRequestException bad:
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsJsonAsync(new { title = "The request body is malformed.", status = 400, errors = new Dictionary<string, string[]>() });
            logger.LogInformation("Bad request: {Message}", bad.Message);
            break;
        default:
            // Never leak internals to the client.
            logger.LogError(ex, "Unhandled exception");
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await ctx.Response.WriteAsJsonAsync(new { title = "An unexpected error occurred.", status = 500 });
            break;
    }
}));

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ---- endpoints -----------------------------------------------------------------------
app.MapHealthChecks("/health/live", new() { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new() { Predicate = r => r.Tags.Contains("ready") }).AllowAnonymous();

app.MapPost("/api/auth/login", (LoginRequest request, TokenService tokens) =>
{
    if (!tokens.VerifyPassword(request.Password))
        return Results.Json(new { title = "Invalid password.", status = 401 }, statusCode: StatusCodes.Status401Unauthorized);

    var issued = tokens.Issue();
    return Results.Ok(new { token = issued.Token, expiresAt = issued.ExpiresAt });
}).AllowAnonymous().RequireRateLimiting("login");

app.MapGet("/api/profile", async (ProfileService profiles, CancellationToken ct) =>
    Results.Ok(await profiles.GetAsync(ct)));

app.MapPut("/api/profile", async (ProfileDto dto, ProfileService profiles, CancellationToken ct) =>
    Results.Ok(await profiles.UpdateAsync(dto, ct)));

app.MapGet("/api/dashboard", async (ProfileService profiles, CancellationToken ct) =>
    Results.Ok(await profiles.GetDashboardAsync(ct)));

app.MapJobEndpoints();

// ---- startup -------------------------------------------------------------------------
var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
await DatabaseInitializer.InitializeAsync(app.Services, startupLogger);

app.Run();

public sealed record LoginRequest(string? Password);

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program;
