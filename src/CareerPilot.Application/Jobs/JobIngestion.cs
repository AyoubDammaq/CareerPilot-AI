using CareerPilot.Domain.Jobs;

namespace CareerPilot.Application.Jobs;

public sealed record IngestionResult(int New, int Updated, int Closed, List<JobListing> Created);

/// <summary>
/// Turns one company's fetched jobs into listings/postings: creates, updates, links postings of the same
/// vacancy across different sources, and closes vacancies that vanished from a COMPLETE snapshot.
/// Pure in-memory logic (the caller persists), so every rule is unit-testable.
/// </summary>
public static class JobIngestion
{
    public static IngestionResult Apply(
        WatchedCompany company,
        ConnectorResult fetched,
        IReadOnlyCollection<JobListing> allListings,
        DiscoveryOptions options,
        IReadOnlyCollection<string> profileSkills,
        DateTimeOffset now)
    {
        var ownPostings = new Dictionary<string, (JobListing Listing, JobPosting Posting)>(StringComparer.Ordinal);
        foreach (var listing in allListings)
            foreach (var posting in listing.Postings.Where(p => p.WatchedCompanyId == company.Id))
                ownPostings[posting.SourceJobId] = (listing, posting);

        // Candidates for cross-source linking: listings that have no posting from THIS board.
        // Two postings on the same board are never merged: they have distinct ids and may be distinct roles.
        var byKey = allListings
            .Where(l => l.Postings.All(p => p.WatchedCompanyId != company.Id))
            .GroupBy(l => l.CanonicalKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var created = new List<JobListing>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int newCount = 0, updated = 0;

        foreach (var job in fetched.Jobs.Take(options.MaxJobsPerCompany))
        {
            if (string.IsNullOrWhiteSpace(job.SourceJobId) || string.IsNullOrWhiteSpace(job.Title))
                continue; // unusable record: never invent identifiers
            if (!seen.Add(job.SourceJobId))
                continue; // duplicate id inside one feed

            var enrichment = JobEnricher.Enrich(job, options, profileSkills);
            var key = JobEnricher.CanonicalKey(company.Name, job.Title, enrichment.Place);

            if (ownPostings.TryGetValue(job.SourceJobId, out var existing))
            {
                var changed = ApplySource(existing.Listing, job, enrichment, key, company);
                var reopened = existing.Listing.Status is JobStatus.Closed or JobStatus.Unverified;
                existing.Posting.IsPresent = true;
                existing.Posting.LastSeenAt = now;
                existing.Posting.SourceUrl = SafeUrl(job.SourceUrl);
                existing.Posting.ApplyUrl = SafeUrl(job.ApplyUrl);
                existing.Listing.LastVerifiedAt = now;
                if (reopened) { existing.Listing.Status = JobStatus.Open; existing.Listing.ClosedAt = null; }
                if (changed || reopened) { existing.Listing.Touch(); updated++; }
            }
            else if (byKey.TryGetValue(key, out var sameVacancy))
            {
                sameVacancy.Postings.Add(NewPosting(company, job, now));
                sameVacancy.LastVerifiedAt = now;
                if (sameVacancy.Status is JobStatus.Closed or JobStatus.Unverified)
                {
                    sameVacancy.Status = JobStatus.Open;
                    sameVacancy.ClosedAt = null;
                }
                sameVacancy.Touch();
                updated++;
            }
            else
            {
                var listing = new JobListing { DiscoveredAt = now };
                ApplySource(listing, job, enrichment, key, company);
                listing.LastVerifiedAt = now;
                listing.Status = JobStatus.Open;
                listing.Postings.Add(NewPosting(company, job, now));
                created.Add(listing);
                newCount++;
            }
        }

        // A feed larger than the cap was only partly processed, so it cannot prove that other jobs vanished.
        var closed = 0;
        var complete = fetched.IsCompleteSnapshot && fetched.Jobs.Count <= options.MaxJobsPerCompany;
        if (complete)
        {
            foreach (var (id, entry) in ownPostings)
            {
                if (seen.Contains(id) || !entry.Posting.IsPresent) continue;
                entry.Posting.IsPresent = false;
                if (entry.Listing.Status != JobStatus.Closed && entry.Listing.Postings.All(p => !p.IsPresent))
                {
                    entry.Listing.Status = JobStatus.Closed;
                    entry.Listing.ClosedAt = now;
                    entry.Listing.Touch();
                    closed++;
                }
            }
        }

        return new IngestionResult(newCount, updated, closed, created);
    }

    /// <summary>Open listings whose deadline passed become Expired; Open listings not re-verified recently become Unverified.</summary>
    public static void RefreshStatuses(IEnumerable<JobListing> listings, DiscoveryOptions options, DateTimeOffset now)
    {
        var staleBefore = now.AddDays(-options.StaleAfterDays);
        foreach (var l in listings)
        {
            if (l.Status is JobStatus.Open or JobStatus.Unverified && l.ApplicationDeadline is { } d && d < now)
            {
                l.Status = JobStatus.Expired;
                l.Touch();
            }
            else if (l.Status == JobStatus.Open && l.LastVerifiedAt < staleBefore)
            {
                l.Status = JobStatus.Unverified;
                l.Touch();
            }
        }
    }

    private static bool ApplySource(JobListing l, SourceJob job, JobEnrichment e, string key, WatchedCompany company)
    {
        var description = HtmlText.Normalize(job.Description);
        var before = (l.Title, l.Description, l.LocationText, l.ApplicationDeadline, l.SalaryMin, l.SalaryMax);

        l.CanonicalKey = key;
        l.Title = job.Title.Trim();
        l.CompanyName = company.Name;
        l.LocationText = job.LocationText.Trim();
        l.Country = e.Place.Country;
        l.City = e.Place.City;
        l.CountryCodes = e.Place.CountryCodes;
        l.WorkMode = job.WorkMode ?? (e.Place.IsRemote ? Domain.Profiles.WorkMode.Remote : null);
        l.EmploymentType = string.IsNullOrWhiteSpace(job.EmploymentType) ? null : job.EmploymentType.Trim();
        l.SalaryMin = job.SalaryMin;
        l.SalaryMax = job.SalaryMax;
        l.SalaryCurrency = job.SalaryCurrency;
        l.SalaryPeriod = job.SalaryPeriod;
        l.Description = description;
        l.ExperienceYearsHint = e.ExperienceYearsHint;
        l.Skills = e.Skills;
        l.Language = e.Language;
        l.PublishedAt = job.PublishedAt;
        l.ApplicationDeadline = job.Deadline;
        l.Seniority = e.Seniority;
        l.MentionsSponsorship = e.MentionsSponsorship;
        l.MentionsRelocation = e.MentionsRelocation;

        var after = (l.Title, l.Description, l.LocationText, l.ApplicationDeadline, l.SalaryMin, l.SalaryMax);
        return before != after;
    }

    private static JobPosting NewPosting(WatchedCompany company, SourceJob job, DateTimeOffset now) => new()
    {
        WatchedCompanyId = company.Id,
        Kind = company.Kind,
        SourceJobId = job.SourceJobId,
        SourceUrl = SafeUrl(job.SourceUrl),
        ApplyUrl = SafeUrl(job.ApplyUrl),
        FirstSeenAt = now,
        LastSeenAt = now,
        IsPresent = true,
    };

    /// <summary>Only absolute http(s) URLs are kept: feed content is untrusted and must not smuggle other schemes.</summary>
    public static string SafeUrl(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp)
            ? u.ToString()
            : "";
}
