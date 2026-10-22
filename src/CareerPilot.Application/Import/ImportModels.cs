namespace CareerPilot.Application.Import;

public sealed record ProposedContact(string? FullName, string? Email, string? Phone, string? GithubUrl, string? LinkedinUrl);

public sealed record ProposedExperience(
    string Company, string Title, string Location, DateOnly StartDate, DateOnly? EndDate, string Description, List<string> Achievements);

public sealed record ProposedEducation(string Institution, string Degree, string Field);

public sealed record ProposedProject(string Name, string Description, List<string> Technologies, List<string> Results);

public sealed record ProposedLanguage(string Name, string Level);

/// <summary>What the CV text appears to say. Nothing here is on the profile until the owner applies it.</summary>
public sealed record ImportProposal(
    ProposedContact Contact,
    List<ProposedExperience> Experiences,
    List<ProposedEducation> Education,
    List<string> Skills,
    List<ProposedProject> Projects,
    List<ProposedLanguage> Languages,
    List<string> Notes,
    bool AiUsed,
    int TextCharacters,
    int ProfileVersion);

public sealed record ImportApplyInput(
    int Version,
    bool MarkVerified,
    ProposedContact? Contact,
    List<ProposedExperience>? Experiences,
    List<ProposedEducation>? Education,
    List<string>? Skills,
    List<ProposedProject>? Projects,
    List<ProposedLanguage>? Languages);

public sealed record ImportApplyResult(Profiles.ProfileDto Profile, int Added, List<string> Skipped);

/// <summary>Reads plain text out of an uploaded file. Implemented in Infrastructure.</summary>
public interface ITextExtractor
{
    /// <summary>Throws ValidationFailedException (field "file") for unsupported, unreadable or empty files.</summary>
    string Extract(byte[] content, string fileName);
}
