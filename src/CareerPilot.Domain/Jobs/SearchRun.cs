using CareerPilot.Domain.Common;

namespace CareerPilot.Domain.Jobs;

public class SearchRun : Entity
{
    public SearchTrigger Trigger { get; set; }
    public SearchRunStatus Status { get; set; } = SearchRunStatus.Queued;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public int CompaniesTotal { get; set; }
    public int CompaniesSucceeded { get; set; }
    public int JobsFetched { get; set; }
    public int JobsNew { get; set; }
    public int JobsUpdated { get; set; }
    public int JobsClosed { get; set; }

    /// <summary>One entry per failed company, e.g. "GitLab: HTTP 404 (board not found)". Contains no secrets.</summary>
    public List<string> Errors { get; set; } = [];
}
