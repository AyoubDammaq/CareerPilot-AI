using CareerPilot.Domain.Common;

namespace CareerPilot.Domain.Jobs;

/// <summary>
/// One company job board the owner wants searched. Greenhouse, Lever and Ashby expose one company at a
/// time, so discovery is driven by this configurable watchlist.
/// </summary>
public class WatchedCompany : Entity
{
    public string Name { get; set; } = "";
    public JobSourceKind Kind { get; set; }

    /// <summary>Greenhouse board token, Lever site name or Ashby job-board name.</summary>
    public string BoardToken { get; set; } = "";

    /// <summary>Lever only: use the EU data-center API host.</summary>
    public bool UseEuEndpoint { get; set; }

    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset? LastSyncAt { get; set; }
    public DateTimeOffset? LastSuccessAt { get; set; }
    public string? LastError { get; set; }
    public int? LastJobCount { get; set; }
}
