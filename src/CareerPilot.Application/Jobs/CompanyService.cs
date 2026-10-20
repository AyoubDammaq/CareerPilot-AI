using System.Text.RegularExpressions;
using CareerPilot.Application.Abstractions;
using CareerPilot.Application.Common;
using CareerPilot.Domain.Jobs;

namespace CareerPilot.Application.Jobs;

/// <summary>Manages the company watchlist. Board tokens are validated and only ever inserted into fixed, official hosts.</summary>
public sealed partial class CompanyService(IJobStore store, IEnumerable<IJobSourceConnector> connectors, IAuditLog audit)
{
    private const string Actor = "owner";

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_.-]{0,99}$")]
    private static partial Regex TokenRegex();

    public async Task<List<CompanyDto>> ListAsync(CancellationToken ct) =>
        [.. (await store.GetCompaniesAsync(false, ct)).OrderBy(c => c.Name).Select(JobMapper.ToDto)];

    public async Task<CompanyDto> AddAsync(CompanyInput input, CancellationToken ct)
    {
        var (name, kind, token) = Validate(input);
        var existing = await store.GetCompaniesAsync(false, ct);
        if (existing.Any(c => c.Kind == kind && c.BoardToken.Equals(token, StringComparison.OrdinalIgnoreCase)))
            throw new ValidationFailedException(new Dictionary<string, string[]>
                { ["boardToken"] = ["This board is already on your list."] });

        var company = new WatchedCompany
        {
            Name = name, Kind = kind, BoardToken = token,
            UseEuEndpoint = kind == JobSourceKind.Lever && input.UseEuEndpoint,
            IsEnabled = input.IsEnabled,
        };
        store.Add(company);
        audit.Stage(Actor, "company.added", nameof(WatchedCompany), company.Id, $"{kind}");
        await store.SaveChangesAsync(ct);
        return JobMapper.ToDto(company);
    }

    public async Task<CompanyDto> UpdateAsync(Guid id, CompanyInput input, CancellationToken ct)
    {
        var company = await store.GetCompanyAsync(id, ct) ?? throw new NotFoundException("Company not found.");
        var (name, kind, token) = Validate(input);
        if (kind != company.Kind || !token.Equals(company.BoardToken, StringComparison.OrdinalIgnoreCase))
        {
            var others = await store.GetCompaniesAsync(false, ct);
            if (others.Any(c => c.Id != id && c.Kind == kind && c.BoardToken.Equals(token, StringComparison.OrdinalIgnoreCase)))
                throw new ValidationFailedException(new Dictionary<string, string[]>
                    { ["boardToken"] = ["This board is already on your list."] });
            // A different board means a different company feed: previous sync state no longer applies.
            company.LastSuccessAt = null;
            company.LastError = null;
            company.LastJobCount = null;
        }

        company.Name = name;
        company.Kind = kind;
        company.BoardToken = token;
        company.UseEuEndpoint = kind == JobSourceKind.Lever && input.UseEuEndpoint;
        company.IsEnabled = input.IsEnabled;
        company.Touch();
        audit.Stage(Actor, "company.updated", nameof(WatchedCompany), company.Id, $"{kind}, enabled={company.IsEnabled}");
        await store.SaveChangesAsync(ct);
        return JobMapper.ToDto(company);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var company = await store.GetCompanyAsync(id, ct) ?? throw new NotFoundException("Company not found.");
        store.Remove(company);
        audit.Stage(Actor, "company.removed", nameof(WatchedCompany), company.Id, null);
        await store.SaveChangesAsync(ct);
    }

    /// <summary>Probes the board once without storing anything, so a wrong token is caught immediately.</summary>
    public async Task<CompanyTestResult> TestAsync(Guid id, CancellationToken ct)
    {
        var company = await store.GetCompanyAsync(id, ct) ?? throw new NotFoundException("Company not found.");
        var connector = connectors.FirstOrDefault(c => c.Kind == company.Kind);
        if (connector is null) return new CompanyTestResult(false, null, $"No connector for {company.Kind}.");
        try
        {
            var result = await connector.FetchAsync(company, ct);
            return new CompanyTestResult(true, result.Jobs.Count, null);
        }
        catch (SourceFetchException ex)
        {
            return new CompanyTestResult(false, null, ex.Message);
        }
    }

    private static (string Name, JobSourceKind Kind, string Token) Validate(CompanyInput input)
    {
        var errors = new Dictionary<string, string[]>();
        var name = input.Name?.Trim() ?? "";
        if (name.Length is 0 or > 100) errors["name"] = ["Required, at most 100 characters."];

        JobSourceKind kind = default;
        if (!Enum.TryParse(input.Kind?.Trim(), ignoreCase: false, out kind) || !Enum.IsDefined(kind))
            errors["kind"] = [$"Must be one of: {string.Join(", ", Enum.GetNames<JobSourceKind>())}."];

        var token = input.BoardToken?.Trim() ?? "";
        if (!TokenRegex().IsMatch(token))
            errors["boardToken"] = ["Letters, digits, '-', '_' and '.' only (1 to 100 characters)."];

        if (errors.Count > 0) throw new ValidationFailedException(errors);
        return (name, kind, token);
    }
}
