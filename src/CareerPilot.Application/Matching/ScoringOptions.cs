namespace CareerPilot.Application.Matching;

/// <summary>
/// Tunable matching weights (appsettings section "Scoring"). The weights are relative: they are scaled
/// so the maximum possible score is always 100. Setting a weight to 0 switches that criterion off.
/// </summary>
public sealed class ScoringOptions
{
    public const string Section = "Scoring";

    public double Skills { get; set; } = 30;
    public double Title { get; set; } = 20;
    public double Location { get; set; } = 15;
    public double Seniority { get; set; } = 15;
    public double WorkMode { get; set; } = 5;
    public double Language { get; set; } = 5;
    public double Salary { get; set; } = 5;
    public double Freshness { get; set; } = 5;

    /// <summary>Skills compared against: a job asking for more than this many known skills is not penalised further.</summary>
    public int SkillCoverageCap { get; set; } = 8;

    /// <summary>
    /// Fewest technologies a posting must name before its skills match can earn full marks. Fewer than this and the missing
    /// evidence counts as a neutral half-match, so "2 of 2" cannot look as convincing as "6 of 6".
    /// </summary>
    public int SkillMinEvidence { get; set; } = 4;

    public int StrongAt { get; set; } = 75;
    public int GoodAt { get; set; } = 60;
    public int PossibleAt { get; set; } = 45;

    /// <summary>Returns the weights as key/weight pairs, scaled so they add up to 100.</summary>
    public IReadOnlyList<(string Key, double Weight)> ScaledWeights()
    {
        (string, double)[] raw =
        [
            ("skills", Skills), ("title", Title), ("location", Location), ("seniority", Seniority),
            ("workMode", WorkMode), ("language", Language), ("salary", Salary), ("freshness", Freshness),
        ];
        var clean = raw.Select(r => (r.Item1, W: double.IsFinite(r.Item2) && r.Item2 > 0 ? r.Item2 : 0)).ToArray();
        var sum = clean.Sum(c => c.W);
        if (sum <= 0) return [.. new ScoringOptions().ScaledWeights()];
        return [.. clean.Select(c => (c.Item1, c.W * 100 / sum))];
    }
}
