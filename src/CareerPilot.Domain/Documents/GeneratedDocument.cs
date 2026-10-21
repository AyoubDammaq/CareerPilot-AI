using CareerPilot.Domain.Common;

namespace CareerPilot.Domain.Documents;

public enum DocumentKind { Cv, CoverLetter }

/// <summary>
/// A document prepared for one vacancy. The exact content is stored as a snapshot and fingerprinted, so a later
/// approval can be bound to precisely what the owner reviewed. Immutable once created: changes make a new document.
/// </summary>
public class GeneratedDocument : Entity
{
    public Guid JobListingId { get; set; }
    public DocumentKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string ContentJson { get; set; } = "";

    /// <summary>SHA-256 (hex) of ContentJson.</summary>
    public string ContentHash { get; set; } = "";

    /// <summary>Version of the profile the document was built from; a newer profile makes it stale.</summary>
    public int ProfileVersion { get; set; }
}
