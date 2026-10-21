using System.Text.Json;
using CareerPilot.Application.Abstractions;
using CareerPilot.Domain.Jobs;
using CareerPilot.Domain.Profiles;

namespace CareerPilot.Application.Matching;

public sealed record RescoreResult(int Scored, bool ProfileMissing);

/// <summary>Computes and stores match scores. Scores are derived data: rescoring never touches decisions or statuses.</summary>
public sealed class MatchingService(IJobStore jobs, IProfileStore profiles, ScoringOptions options, TimeProvider clock)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static MatchResult? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<MatchResult>(json, Json); }
        catch (JsonException) { return null; }
    }

    public static void Apply(JobListing job, UserProfile profile, ScoringOptions options, DateTimeOffset now)
    {
        var result = JobScorer.Score(job, profile, options, now);
        job.MatchScore = result.Score;
        job.MatchJson = JsonSerializer.Serialize(result, Json);
        job.ScoredAt = now;
    }

    /// <summary>Scores the given listings in memory (caller saves). Used by the search run.</summary>
    public static int ApplyAll(IEnumerable<JobListing> listings, UserProfile profile, ScoringOptions options, DateTimeOffset now)
    {
        var n = 0;
        foreach (var l in listings) { Apply(l, profile, options, now); n++; }
        return n;
    }

    public async Task<RescoreResult> RescoreAllAsync(CancellationToken ct)
    {
        var profile = await profiles.GetAsync(ct);
        if (profile is null) return new RescoreResult(0, true);
        var listings = await jobs.GetAllListingsAsync(ct);
        var n = ApplyAll(listings, profile, options, clock.GetUtcNow());
        await jobs.SaveChangesAsync(ct);
        return new RescoreResult(n, false);
    }
}
