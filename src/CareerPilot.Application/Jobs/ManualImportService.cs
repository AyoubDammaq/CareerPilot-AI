using CareerPilot.Application.Abstractions;
using CareerPilot.Application.Common;
using CareerPilot.Application.Matching;
using CareerPilot.Domain.Jobs;
using CareerPilot.Domain.Profiles;

namespace CareerPilot.Application.Jobs;

public sealed record FetchedPage(Uri FinalUrl, string Html);

/// <summary>Downloads one public web page. Implementations must refuse non-public destinations (SSRF), cap the size and time, and never log in.</summary>
public interface IJobPageFetcher
{
    Task<FetchedPage> FetchAsync(Uri url, CancellationToken ct);
}

public sealed record ManualJobInput(string? Title, string? Company, string? Location, string? Description, string? Url);
public sealed record ImportUrlInput(string? Url);

public sealed record ManualImportResult(Guid ListingId, bool Created, string Title, string Company, List<string> Warnings);

/// <summary>
/// Adds a single job the owner found elsewhere (LinkedIn, Indeed, Keejob, Tanitjobs, an employer site ...). It goes through the same
/// normalisation, de-duplication, deterministic filtering and scoring as discovered jobs, is attributed to the "Manual" source,
/// and is never verified automatically. Importing a job does not start any application.
/// </summary>
public sealed class ManualImportService(
    IJobStore jobs,
    IProfileStore profiles,
    IJobPageFetcher fetcher,
    DiscoveryOptions options,
    ScoringOptions scoring,
    TimeProvider clock,
    IAuditLog audit)
{
    private const string Actor = "owner";
    private const string ManualName = "Manual imports";

    public async Task<ManualImportResult> ImportUrlAsync(ImportUrlInput input, CancellationToken ct)
    {
        var url = UrlGuard.ParsePublicHttpsUrl(input.Url);
        if (UrlGuard.IsInHostList(url, options.ManualOnlyHosts))
            throw Invalid("url", $"CareerPilot does not fetch pages from {url.Host} automatically (login walls, anti-bot rules or terms that forbid it). Open the job yourself and use 'Paste the job text' instead.");

        FetchedPage page;
        try { page = await fetcher.FetchAsync(url, ct); }
        catch (SourceFetchException ex) { throw Invalid("url", ex.Message); }

        var job = JobPageParser.Parse(page.Html, page.FinalUrl);
        if (job is null)
            throw Invalid("url", "This page does not publish structured job data (schema.org JobPosting), so nothing was imported and nothing was guessed. Use 'Paste the job text' instead.");

        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(job.CompanyName)) warnings.Add("The page names no employer; it is listed under 'Unknown employer'.");
        job = job with { CompanyName = string.IsNullOrWhiteSpace(job.CompanyName) ? "Unknown employer" : job.CompanyName };
        return await IngestAsync(job, warnings, ct);
    }

    public async Task<ManualImportResult> ImportTextAsync(ManualJobInput input, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var title = input.Title?.Trim() ?? "";
        var company = input.Company?.Trim() ?? "";
        var location = input.Location?.Trim() ?? "";
        var description = HtmlText.Normalize(input.Description);

        if (title.Length is 0 or > 200) errors["title"] = ["Required, at most 200 characters."];
        if (company.Length is 0 or > 100) errors["company"] = ["Required, at most 100 characters."];
        if (location.Length > 200) errors["location"] = ["At most 200 characters."];
        if (description.Length is < 20 or > 50_000) errors["description"] = ["Paste the job text (20 to 50,000 characters)."];

        Uri? url = null;
        if (!string.IsNullOrWhiteSpace(input.Url))
        {
            try { url = UrlGuard.ParsePublicHttpsUrl(input.Url); }   // validated only; never fetched here
            catch (ValidationFailedException ex) { errors["url"] = ex.Errors["url"]; }
        }
        if (errors.Count > 0) throw new ValidationFailedException(errors);

        var warnings = new List<string>();
        if (location.Length == 0) warnings.Add("No location was given, so the location check needs your review.");
        if (url is null) warnings.Add("No link was given: you will need the original ad to apply.");

        var job = new SourceJob(
            SourceJobId: url is not null ? ImportIds.ForUrl(url) : ImportIds.ForText(company, title, location),
            Title: title,
            LocationText: location,
            CountryHint: null,
            WorkMode: null,
            EmploymentType: null,
            Description: description,
            PublishedAt: null,
            Deadline: null,
            SourceUrl: url?.ToString() ?? "",
            ApplyUrl: url?.ToString() ?? "",
            CompanyName: company);
        return await IngestAsync(job, warnings, ct);
    }

    private async Task<ManualImportResult> IngestAsync(SourceJob job, List<string> warnings, CancellationToken ct)
    {
        var profile = await profiles.GetAsync(ct);
        var holder = (await jobs.GetCompaniesAsync(enabledOnly: false, ct)).FirstOrDefault(c => c.Kind == JobSourceKind.Manual);
        if (holder is null)
        {
            // A disabled placeholder owns manual postings; search runs only read enabled boards, so it is never fetched.
            holder = new WatchedCompany { Name = ManualName, Kind = JobSourceKind.Manual, BoardToken = "manual", IsEnabled = false };
            jobs.Add(holder);
        }

        var now = clock.GetUtcNow();
        var all = await jobs.GetAllListingsAsync(ct);
        var skills = profile?.Skills.Select(s => s.Name).ToList() ?? [];
        var result = JobIngestion.Apply(holder, new ConnectorResult([job], IsCompleteSnapshot: false), all, options, skills, now);
        foreach (var created in result.Created) jobs.Add(created);

        var listing = result.Created.FirstOrDefault()
                      ?? all.First(l => l.Postings.Any(p => p.WatchedCompanyId == holder.Id && p.SourceJobId == job.SourceJobId));

        JobIngestion.RefreshStatuses([listing], options, now);
        JobClassifier.Classify(listing, profile?.Preferences ?? new JobPreference(), options, profile?.Country);
        if (profile is not null) MatchingService.Apply(listing, profile, scoring, now);
        else warnings.Add("No profile exists yet, so the job was not scored.");

        if (listing.Status == JobStatus.Expired) warnings.Add("The posting's end date has already passed.");
        if (!listing.IsRelevant) warnings.Add("It was imported but is hidden by your filters: " + string.Join(" ", listing.FilterReasons));
        warnings.Add("Imported by hand: CareerPilot cannot confirm that this job is still open.");

        audit.Stage(Actor, "job.imported", nameof(JobListing), listing.Id, result.New > 0 ? "created" : "updated");
        await jobs.SaveChangesAsync(ct);
        return new ManualImportResult(listing.Id, result.New > 0, listing.Title, listing.CompanyName, warnings);
    }

    private static ValidationFailedException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
