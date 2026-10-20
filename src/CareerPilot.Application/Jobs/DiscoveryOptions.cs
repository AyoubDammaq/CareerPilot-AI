namespace CareerPilot.Application.Jobs;

/// <summary>
/// Tunable discovery rules (appsettings section "Discovery"). Defaults live in code; set a list in
/// configuration to extend the defaults, not replace them.
/// </summary>
public sealed class DiscoveryOptions
{
    public const string Section = "Discovery";

    /// <summary>A title must contain one of these (word match) to be considered a software role.</summary>
    public List<string> IncludeTitleKeywords { get; set; } =
    [
        "engineer", "developer", "software", "backend", "back end", "back-end", "full stack", "full-stack",
        "fullstack", "dotnet", "csharp", "ai", "llm", "machine learning", "ml", "programmer",
    ];

    /// <summary>Titles containing any of these are filtered out (clearly not engineering work).</summary>
    public List<string> ExcludeTitleKeywords { get; set; } =
    [
        "sales", "marketing", "recruiter", "recruiting", "talent", "account executive", "customer success",
        "customer support", "support engineer", "legal", "counsel", "finance", "accountant", "payroll",
        "designer", "design", "copywriter", "hr", "people partner", "office manager", "paralegal",
        "intern", "internship", "mechanical", "electrical", "hardware", "civil", "technician",
    ];

    public List<string> SeniorTitleKeywords { get; set; } = ["senior", "sr", "staff", "principal"];
    public List<string> LeadTitleKeywords { get; set; } = ["lead", "head", "director", "vp", "vice president", "manager", "chief", "cto"];
    public List<string> JuniorTitleKeywords { get; set; } = ["junior", "jr", "graduate", "entry level", "entry-level", "new grad", "apprentice", "alternance"];

    /// <summary>An Open listing not re-confirmed for this many days becomes Unverified.</summary>
    public int StaleAfterDays { get; set; } = 7;

    public int MaxJobsPerCompany { get; set; } = 3000;
    public int DelayBetweenCompaniesMs { get; set; } = 500;

    /// <summary>A queued or running search older than this is considered dead and no longer blocks a new one.</summary>
    public int ActiveRunTimeoutMinutes { get; set; } = 30;
}
