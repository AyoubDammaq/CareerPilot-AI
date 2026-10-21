namespace CareerPilot.Application.Matching;

/// <summary>One criterion of the score. Known=false means the posting or profile did not say enough, so a neutral value was used.</summary>
public sealed record MatchComponent(string Key, string Label, double MaxPoints, double Points, bool Known, string Note);

/// <summary>
/// Explainable, deterministic fit between a vacancy and the owner's profile.
/// Confidence is the share of the 100 points that rested on facts rather than neutral defaults.
/// </summary>
public sealed record MatchResult(
    int Score,
    string Tier,
    int Confidence,
    List<MatchComponent> Components,
    List<string> MatchedSkills,
    List<string> MissingSkills,
    List<string> Warnings);
