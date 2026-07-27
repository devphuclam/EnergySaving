using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;

namespace IUMP.Modules.IAM.Application;

public sealed class PostSiteFixtureOrganizationAdapter
{
    private readonly IOrganizationCommandRepository _orgRepo;

    public PostSiteFixtureOrganizationAdapter(IOrganizationCommandRepository orgRepo)
    {
        _orgRepo = orgRepo;
    }

    public async Task<Result> ExecuteAsync(string siteIdStr, CancellationToken ct = default)
    {
        if (!Guid.TryParse(siteIdStr, out var guid))
            return Result.Failure("NotFound", "Invalid Site ID.");
        var siteId = new SiteId(guid);
        var site = await _orgRepo.GetSiteAsync(siteId, ct);
        if (site is null)
            return Result.Failure("NotFound", "Site not found.");

        // Verify Site identity/version
        if (site.Version <= 0)
            return Result.Failure("Validation", "Site version is invalid.");

        // In a real implementation, this would invoke IAM's post-Site fixture
        // to assign Engineer/Operator/Manager/Viewer scopes idempotently.
        // Phase 3 implements only the Organization public-contract surface.
        return Result.Success();
    }
}
