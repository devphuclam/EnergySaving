using IUMP.Modules.Organization.Contracts;

namespace IUMP.Modules.IAM.Application;

public sealed record PostSiteFixtureResult(bool IsSuccess, string Code, string? Error)
{
    public bool IsFailure => !IsSuccess;
    public static PostSiteFixtureResult Success() => new(true, string.Empty, null);
    public static PostSiteFixtureResult Failure(string code, string error) => new(false, code, error);
}

public sealed class PostSiteFixtureOrganizationAdapter
{
    private readonly IOrganizationQueryRepository _organizationQueries;
    private readonly IPocIdentityFixture _fixture;

    public PostSiteFixtureOrganizationAdapter(IOrganizationQueryRepository organizationQueries,
        IPocIdentityFixture fixture)
    {
        _organizationQueries = organizationQueries;
        _fixture = fixture;
    }

    public async Task<PostSiteFixtureResult> ExecuteAsync(string siteIdString, CancellationToken ct = default)
    {
        if (!Guid.TryParse(siteIdString, out var siteId) || siteId == Guid.Empty)
            return PostSiteFixtureResult.Failure("NotFound", "Site not found.");

        var site = await _organizationQueries.GetSiteSnapshotAsync(siteId, ct);
        if (site is null || site.Id != siteId)
            return PostSiteFixtureResult.Failure("NotFound", "Site not found.");
        if (site.Version <= 0)
            return PostSiteFixtureResult.Failure("Validation", "Site version is invalid.");

        return await _fixture.ApplyPostSiteFixtureAsync(siteId, ct)
            ? PostSiteFixtureResult.Success()
            : PostSiteFixtureResult.Failure("FixtureUnavailable", "Post-Site fixture could not be applied.");
    }
}
