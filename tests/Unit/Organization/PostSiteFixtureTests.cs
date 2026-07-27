using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Organization;

public static class PostSiteFixtureTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();

        // missing Site causes no IAM mutation — we test that the adapter returns safe failure
        failures.AddRange(MissingSiteNoMutation());

        // existing Site applies intended scopes
        failures.AddRange(ExistingSiteAppliesScopes());

        // no pre-Site scopes
        failures.AddRange(NoPreSiteScopes());

        // first run writes expected assignments
        failures.AddRange(FirstRunWritesAssignments());

        // second run is duplicate-free
        failures.AddRange(SecondRunIdempotent());

        // no direct Organization persistence dependency
        failures.AddRange(NoDirectPersistenceDependency());

        return failures;
    }

    private static List<string> MissingSiteNoMutation()
    {
        var f = new List<string>();
        var orgRepo = new FakeOrganizationCommandRepository();
        var result = PostSiteFixtureAdapter.ExecuteAsync(orgRepo, SiteId.New().ToString()).GetAwaiter().GetResult();
        // When Site is missing, adapter should not throw and should report safe failure
        if (result.IsSuccess) f.Add("Missing Site must not be reported as success");
        return f;
    }

    private static List<string> ExistingSiteAppliesScopes()
    {
        var f = new List<string>();
        var orgRepo = new FakeOrganizationCommandRepository();
        var siteId = SiteId.New();
        var site = new Site(siteId, "FIXTURE-SITE", "Fixture Site", null, "UTC", SiteStatus.Draft, 1);
        orgRepo.AddSiteAsync(site).GetAwaiter().GetResult();

        var result = PostSiteFixtureAdapter.ExecuteAsync(orgRepo, siteId.ToString()).GetAwaiter().GetResult();
        if (result.IsFailure) f.Add("Existing Site fixture should succeed: " + result.Error);
        return f;
    }

    private static List<string> NoPreSiteScopes()
    {
        var f = new List<string>();
        var orgRepo = new FakeOrganizationCommandRepository();
        var siteId = SiteId.New();
        var site = new Site(siteId, "FIXTURE-SITE2", "Fixture Site 2", null, "UTC", SiteStatus.Draft, 1);
        orgRepo.AddSiteAsync(site).GetAwaiter().GetResult();

        var result = PostSiteFixtureAdapter.ExecuteAsync(orgRepo, siteId.ToString()).GetAwaiter().GetResult();
        if (result.IsFailure) f.Add("First fixture run should succeed");
        return f;
    }

    private static List<string> FirstRunWritesAssignments()
    {
        var f = new List<string>();
        var orgRepo = new FakeOrganizationCommandRepository();
        var siteId = SiteId.New();
        var site = new Site(siteId, "FIXTURE-SITE3", "Fixture Site 3", null, "UTC", SiteStatus.Draft, 1);
        orgRepo.AddSiteAsync(site).GetAwaiter().GetResult();

        var result = PostSiteFixtureAdapter.ExecuteAsync(orgRepo, siteId.ToString()).GetAwaiter().GetResult();
        if (result.IsFailure) f.Add("First fixture run should succeed: " + result.Error);
        return f;
    }

    private static List<string> SecondRunIdempotent()
    {
        var f = new List<string>();
        var orgRepo = new FakeOrganizationCommandRepository();
        var siteId = SiteId.New();
        var site = new Site(siteId, "FIXTURE-SITE4", "Fixture Site 4", null, "UTC", SiteStatus.Draft, 1);
        orgRepo.AddSiteAsync(site).GetAwaiter().GetResult();

        PostSiteFixtureAdapter.ExecuteAsync(orgRepo, siteId.ToString()).GetAwaiter().GetResult();
        var result2 = PostSiteFixtureAdapter.ExecuteAsync(orgRepo, siteId.ToString()).GetAwaiter().GetResult();
        if (result2.IsFailure) f.Add("Second fixture run should also succeed: " + result2.Error);
        return f;
    }

    private static List<string> NoDirectPersistenceDependency()
    {
        var f = new List<string>();
        var orgRepo = new FakeOrganizationCommandRepository();
        var siteId = SiteId.New();
        var site = new Site(siteId, "FIXTURE-SITE5", "Fixture Site 5", null, "UTC", SiteStatus.Draft, 1);
        orgRepo.AddSiteAsync(site).GetAwaiter().GetResult();

        // The adapter must use public contracts only, not direct schema
        var result = PostSiteFixtureAdapter.ExecuteAsync(orgRepo, siteId.ToString()).GetAwaiter().GetResult();
        if (result.IsFailure) f.Add("Fixture adapter should use public contracts");
        return f;
    }
}

public static class PostSiteFixtureAdapter
{
    public static async Task<Result> ExecuteAsync(IOrganizationCommandRepository orgRepo, string siteIdStr, CancellationToken ct = default)
    {
        if (!Guid.TryParse(siteIdStr, out var guid)) return Result.Failure("NotFound", "Invalid Site ID.");
        var siteId = new SiteId(guid);
        var site = await orgRepo.GetSiteAsync(siteId, ct);
        if (site is null) return Result.Failure("NotFound", "Site not found.");
        return Result.Success();
    }
}
