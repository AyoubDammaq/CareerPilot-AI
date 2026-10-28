namespace CareerPilot.Domain.Jobs;

public enum JobSourceKind { Greenhouse, Lever, Ashby, Himalayas }

/// <summary>Open = seen in the latest successful snapshot; Closed = disappeared from a complete snapshot;
/// Expired = application deadline passed; Unverified = not re-confirmed recently.</summary>
public enum JobStatus { Open, Closed, Expired, Unverified }

public enum SeniorityLevel { Unspecified, Junior, Mid, Senior, Lead }

/// <summary>Result of the geographic check against the owner's target countries.
/// NeedsReview is used whenever eligibility is uncertain: such jobs are flagged, never silently dropped.</summary>
public enum GeoFit { Match, NeedsReview, NoMatch }

/// <summary>The owner's own decision about a listing. Search runs never change it.</summary>
public enum UserDecision { None, Saved, Excluded }

public enum SearchTrigger { Manual, Scheduled }

public enum SearchRunStatus { Queued, Running, Succeeded, PartiallyFailed, Failed }
