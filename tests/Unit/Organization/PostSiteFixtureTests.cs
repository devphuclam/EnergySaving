using IUMP.Modules.IAM.Application;
using IUMP.Modules.IAM.Domain;
using IUMP.Modules.Organization.Application;
using IUMP.Modules.Organization.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Organization;

public static class PostSiteFixtureTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();
        MissingSiteDoesNotMutate(failures);
        ExistingSiteCreatesRealAssignments(failures);
        ExistingSiteFixtureIsIdempotent(failures);
        NoPreSiteScopeExists(failures);
        AdapterUsesPublicOrganizationQueryOnly(failures);
        return failures;
    }

    private static (FakeOrganizationCommandRepository Org, FakeOrganizationQueryRepository Queries,
        FakeIamCommandRepository Iam, PostSiteFixtureOrganizationAdapter Adapter, Guid SiteId) Create(bool addSite)
    {
        var org = new FakeOrganizationCommandRepository();
        var queries = new FakeOrganizationQueryRepository(org);
        var iam = new FakeIamCommandRepository();
        iam.SeedCapability(new Capability(CapabilityId.New(), "AUDIT_READ", "Audit Review"));
        var fixture = new PocIdentityFixture(iam, new TestHashProvider(), enabled: true);
        var adapter = new PostSiteFixtureOrganizationAdapter(queries, fixture);
        var siteId = SiteId.New().Value;
        if (addSite)
            org.AddSiteAsync(new Site(new SiteId(siteId), "FIXTURE-SITE", "Fixture Site", null, "UTC", SiteStatus.Draft, 1)).GetAwaiter().GetResult();
        return (org, queries, iam, adapter, siteId);
    }

    private static void MissingSiteDoesNotMutate(List<string> failures)
    {
        var (_, _, iam, adapter, siteId) = Create(addSite: false);
        var result = adapter.ExecuteAsync(siteId.ToString()).GetAwaiter().GetResult();
        if (result.IsSuccess || iam.GetAllUsersAsync().GetAwaiter().GetResult().Count != 0 || iam.GetAllScopesForTest().Count != 0)
            failures.Add("Post-Site fixture must fail closed and leave IAM unchanged when the Site is missing.");
    }

    private static void ExistingSiteCreatesRealAssignments(List<string> failures)
    {
        var (_, _, iam, adapter, siteId) = Create(addSite: true);
        var result = adapter.ExecuteAsync(siteId.ToString()).GetAwaiter().GetResult();
        var users = iam.GetAllUsersAsync().GetAwaiter().GetResult();
        var scopes = iam.GetAllScopesForTest();
        var expected = new[] { Role.Engineer, Role.Operator, Role.Manager, Role.Viewer };
        if (result.IsFailure || users.Count != 5 || scopes.Count != 4 ||
            expected.Any(role => !scopes.Any(s => s.SiteId == siteId && users.Single(u => u.Roles.Contains(role)).Id == s.UserId)))
            failures.Add("Post-Site fixture must create real scoped assignments for Engineer, Operator, Manager, and Viewer.");
        var manager = users.Single(u => u.Roles.Contains(Role.Manager));
        var audit = iam.GetAllUserCapabilitiesForTest();
        if (!audit.Any(c => c.UserId == manager.Id))
            failures.Add("Manager must receive the AUDIT_READ capability.");
    }

    private static void ExistingSiteFixtureIsIdempotent(List<string> failures)
    {
        var (_, _, iam, adapter, siteId) = Create(addSite: true);
        var first = adapter.ExecuteAsync(siteId.ToString()).GetAwaiter().GetResult();
        var users = iam.GetAllUsersAsync().GetAwaiter().GetResult();
        var firstScopes = iam.GetAllScopesForTest().Count;
        var firstCaps = iam.GetAllUserCapabilitiesForTest().Count;
        var second = adapter.ExecuteAsync(siteId.ToString()).GetAwaiter().GetResult();
        if (first.IsFailure || second.IsFailure || iam.GetAllScopesForTest().Count != firstScopes ||
            iam.GetAllUserCapabilitiesForTest().Count != firstCaps || users.Count != 5)
            failures.Add("Repeated Post-Site fixture execution must be idempotent.");
    }

    private static void NoPreSiteScopeExists(List<string> failures)
    {
        var (_, _, iam, _, _) = Create(addSite: true);
        if (iam.GetAllScopesForTest().Count != 0)
            failures.Add("No Site scope may exist before the Post-Site fixture runs.");
    }

    private static void AdapterUsesPublicOrganizationQueryOnly(List<string> failures)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Modules", "IAM", "Application", "PostSiteFixtureOrganizationAdapter.cs");
        if (!File.Exists(path)) return;
        var source = File.ReadAllText(path);
        if (source.Contains("IUMP.Modules.Organization.Domain", StringComparison.Ordinal) ||
            source.Contains("IOrganizationCommandRepository", StringComparison.Ordinal) ||
            source.Contains("IUMP.Modules.Organization.Application", StringComparison.Ordinal) ||
            source.Contains("IUMP.Modules.Organization.Infrastructure", StringComparison.Ordinal))
            failures.Add("IAM Post-Site adapter must depend only on public Organization.Contracts and IAM fixture ports.");
    }

    private sealed class TestHashProvider : IPocCredentialHashProvider
    {
        public string GetPasswordHash(string username) => "test-hash";
    }
}
