using IUMP.Modules.Organization.Application;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Organization;

public sealed class FakeOrganizationAuthorization : IOrganizationAuthorization
{
    private readonly OrganizationCallerSnapshot? _caller;
    public bool WasCalled { get; private set; }

    public FakeOrganizationAuthorization(OrganizationCallerSnapshot? caller) => _caller = caller;

    public Task<OrganizationAuthorizationDecision> AuthorizeAsync(string requestedByUserId,
        OrganizationResource resource, string? targetSiteId = null, CancellationToken ct = default)
    {
        WasCalled = true;
        if (_caller is null || !_caller.IsActive) return Task.FromResult(OrganizationAuthorizationDecision.Forbidden());
        if (_caller.HasRole("Administrator")) return Task.FromResult(OrganizationAuthorizationDecision.Allowed());
        if (!_caller.HasRole("Engineer")) return Task.FromResult(OrganizationAuthorizationDecision.Forbidden());
        if (resource == OrganizationResource.RootSite) return Task.FromResult(OrganizationAuthorizationDecision.Forbidden("Engineers cannot create root Sites."));
        if (string.IsNullOrWhiteSpace(targetSiteId))
            return Task.FromResult(_caller.SiteScopes.Count > 0
                ? OrganizationAuthorizationDecision.Allowed()
                : OrganizationAuthorizationDecision.Forbidden());
        return Task.FromResult(_caller.HasSiteScope(targetSiteId)
            ? OrganizationAuthorizationDecision.Allowed()
            : OrganizationAuthorizationDecision.NotFound());
    }

    public Task<OrganizationCallerSnapshot?> ResolveCallerAsync(string requestedByUserId, CancellationToken ct = default) =>
        Task.FromResult(_caller);
}

public sealed class TestRunningSimulatorQuery : IRunningSimulatorQuery
{
    private readonly bool _running;
    public TestRunningSimulatorQuery(bool running = false) => _running = running;
    public Task<bool> HasRunningSimulatorAsync(string pointId, CancellationToken ct = default) => Task.FromResult(_running);
}

public static class HierarchyCommandTests
{
    private static readonly OrganizationCommandContext AdminCtx = new("admin-user", "corr-1", "caus-1");
    private static readonly OrganizationCommandContext EngCtx = new("eng-user", "corr-2", "caus-2");
    private static readonly OrganizationCommandContext OpCtx = new("op-user", "corr-3", null);

    private static OrganizationCallerSnapshot AdminCaller() => new("admin-user", "Admin", true,
        new[] { "Administrator" }, Array.Empty<string>(), Array.Empty<string>());

    private static OrganizationCallerSnapshot ScopedEngineer(string siteId) => new("eng-user", "Engineer", true,
        new[] { "Engineer" }, new[] { siteId }, Array.Empty<string>());

    private static OrganizationCallerSnapshot NoScopeEngineer() => new("eng-user", "Engineer", true,
        new[] { "Engineer" }, Array.Empty<string>(), Array.Empty<string>());

    public static List<string> Run()
    {
        var failures = new List<string>();

        // Root Site Administrator-only
        failures.AddRange(AuthorizeRootSiteOnly());
        // Scoped Engineer lower-hierarchy mutation
        failures.AddRange(ScopedEngineerCanMutate());
        // Unscoped/out-of-scope Engineer denial
        failures.AddRange(NoScopeEngineerDenied());
        // Operator/Manager/Viewer denial
        failures.AddRange(NonEngineerRolesDenied());
        // Trusted Site scope, not command claims
        failures.AddRange(TrustedSiteScope());
        // Authorization before target/dependency details
        failures.AddRange(AuthorizationBeforeDetails());
        // All approved event families
        failures.AddRange(AllEventFamilies());
        // Exact event field allowlists
        failures.AddRange(EventFieldAllowlists());
        // Distinct correlation/causation
        failures.AddRange(DistinctCorrelationCausation());
        // No-op/rejected commands emit no event
        failures.AddRange(NoOpEmitsNoEvent());
        failures.AddRange(AllFiveRolesAreEvaluated());
        failures.AddRange(CreateCommandsRejectSpoofedAncestry());
        failures.AddRange(PointActivationDeferredToPhaseFive());
        failures.AddRange(ActorUsernameIsSnapshotted());
        failures.AddRange(UpdateCommandsAndExpectedVersions());
        failures.AddRange(InvalidParentStatusCreates());
        failures.AddRange(CompleteEventContractCoverage());

        return failures;
    }

    private static List<string> AuthorizeRootSiteOnly()
    {
        var f = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var auth = new FakeOrganizationAuthorization(ScopedEngineer("site-1"));
        var handler = new OrganizationCommandHandler(repo, auth, new TestRunningSimulatorQuery());

        var result = handler.HandleAsync(new CreateSiteCommand("SITE-X", "Site X", null, "UTC", "eng-user"), AdminCtx).GetAwaiter().GetResult();
        if (result.IsSuccess) f.Add("Engineer must not be able to create root Site");

        var adminAuth = new FakeOrganizationAuthorization(AdminCaller());
        var adminHandler = new OrganizationCommandHandler(repo, adminAuth, new TestRunningSimulatorQuery());
        result = adminHandler.HandleAsync(new CreateSiteCommand("SITE-X", "Site X", null, "UTC", "admin-user"), AdminCtx).GetAwaiter().GetResult();
        if (result.IsFailure) f.Add("Administrator must be able to create root Site");

        return f;
    }

    private static List<string> ScopedEngineerCanMutate()
    {
        var f = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var siteId = SiteId.New();
        var site = new Site(siteId, "SITE-A", "Site A", null, "UTC", SiteStatus.Active, 1);
        repo.AddSiteAsync(site).GetAwaiter().GetResult();

        var auth = new FakeOrganizationAuthorization(ScopedEngineer(siteId.ToString()));
        var handler = new OrganizationCommandHandler(repo, auth, new TestRunningSimulatorQuery());

        var result = handler.HandleAsync(new CreateAreaCommand(siteId, "AREA-B", "Area B", null, "eng-user"),
            EngCtx).GetAwaiter().GetResult();
        if (result.IsFailure) f.Add("Scoped Engineer must be able to create Area under scoped Site");

        return f;
    }

    private static List<string> NoScopeEngineerDenied()
    {
        var f = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var siteId = SiteId.New();
        var site = new Site(siteId, "SITE-B", "Site B", null, "UTC", SiteStatus.Active, 1);
        repo.AddSiteAsync(site).GetAwaiter().GetResult();

        var auth = new FakeOrganizationAuthorization(NoScopeEngineer());
        var handler = new OrganizationCommandHandler(repo, auth, new TestRunningSimulatorQuery());

        var result = handler.HandleAsync(new CreateSiteCommand("SITE-C", "Site C", null, "UTC", "eng-user"),
            EngCtx).GetAwaiter().GetResult();
        if (result.IsSuccess) f.Add("No-scope Engineer must be denied root Site creation");

        result = handler.HandleAsync(new CreateAreaCommand(siteId, "AREA-C", "Area C", null, "eng-user"),
            EngCtx).GetAwaiter().GetResult();
        if (result.IsSuccess) f.Add("No-scope Engineer must be denied Area creation");

        return f;
    }

    private static List<string> NonEngineerRolesDenied()
    {
        var f = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var siteId = SiteId.New();
        var site = new Site(siteId, "SITE-D", "Site D", null, "UTC", SiteStatus.Active, 1);
        repo.AddSiteAsync(site).GetAwaiter().GetResult();

        var opCaller = new OrganizationCallerSnapshot("op-user", "Operator", true,
            new[] { "Operator" }, new[] { siteId.ToString() }, Array.Empty<string>());
        var opAuth = new FakeOrganizationAuthorization(opCaller);
        var opHandler = new OrganizationCommandHandler(repo, opAuth, new TestRunningSimulatorQuery());
        var result = opHandler.HandleAsync(new CreateAreaCommand(siteId, "AREA-D", "Area D", null, "op-user"),
            OpCtx).GetAwaiter().GetResult();
        if (result.IsSuccess) f.Add("Operator must not be able to create Area");

        return f;
    }

    private static List<string> TrustedSiteScope()
    {
        var f = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var siteId = SiteId.New();
        var site = new Site(siteId, "SITE-E", "Site E", null, "UTC", SiteStatus.Active, 1);
        repo.AddSiteAsync(site).GetAwaiter().GetResult();

        var otherSiteId = SiteId.New();
        var otherSite = new Site(otherSiteId, "SITE-F", "Site F", null, "UTC", SiteStatus.Active, 1);
        repo.AddSiteAsync(otherSite).GetAwaiter().GetResult();

        var auth = new FakeOrganizationAuthorization(ScopedEngineer(otherSiteId.ToString()));
        var handler = new OrganizationCommandHandler(repo, auth, new TestRunningSimulatorQuery());

        var result = handler.HandleAsync(new CreateAreaCommand(siteId, "AREA-E", "Area E", null, "eng-user"),
            EngCtx).GetAwaiter().GetResult();
        if (result.IsSuccess) f.Add("Engineer scoped to Site F must not create Area in Site E");

        var notFoundAuth = new FakeOrganizationAuthorization(ScopedEngineer(otherSiteId.ToString()));
        var notFoundHandler = new OrganizationCommandHandler(repo, notFoundAuth, new TestRunningSimulatorQuery());
        result = notFoundHandler.HandleAsync(new CreateAreaCommand(siteId, "AREA-E2", "Area E2", null, "eng-user"),
            EngCtx).GetAwaiter().GetResult();
        if (result.IsSuccess) f.Add("Out-of-scope Site access must be blocked");

        return f;
    }

    private static List<string> AuthorizationBeforeDetails()
    {
        var f = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var auth = new FakeOrganizationAuthorization(NoScopeEngineer());
        var handler = new OrganizationCommandHandler(repo, auth, new TestRunningSimulatorQuery());

        var result = handler.HandleAsync(new CreateSiteCommand("SITE-G", "Site G", null, "UTC", "eng-user"),
            EngCtx).GetAwaiter().GetResult();
        if (result.IsSuccess) f.Add("Authorization must happen before target validation");

        if (!auth.WasCalled) f.Add("Authorization must be called before any mutation");

        return f;
    }

    private static List<string> AllEventFamilies()
    {
        var f = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var auth = new FakeOrganizationAuthorization(AdminCaller());
        var handler = new OrganizationCommandHandler(repo, auth, new TestRunningSimulatorQuery());

        handler.HandleAsync(new CreateSiteCommand("SITE-H", "Site H", null, "UTC", "admin-user"), AdminCtx).GetAwaiter().GetResult();
        var eventTypes = handler.Events.Select(e => e.EventType).Distinct().ToList();
        if (!eventTypes.Contains("SiteStatusChanged.v1")) f.Add("Site create must emit SiteStatusChanged.v1");

        var createdSite = repo.GetAllSitesAsync().GetAwaiter().GetResult().FirstOrDefault();
        if (createdSite is null) { f.Add("Created site not found in repo"); return f; }

        handler = new OrganizationCommandHandler(repo, auth, new TestRunningSimulatorQuery());
        var areaCmd = new CreateAreaCommand(createdSite.Id, "AREA-H", "Area H", null, "admin-user");
        handler.HandleAsync(areaCmd, AdminCtx).GetAwaiter().GetResult();
        eventTypes = handler.Events.Select(e => e.EventType).Distinct().ToList();
        if (!eventTypes.Contains("AreaStatusChanged.v1")) f.Add("Area create must emit AreaStatusChanged.v1");

        return f;
    }

    private static List<string> EventFieldAllowlists()
    {
        var f = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var auth = new FakeOrganizationAuthorization(AdminCaller());
        var handler = new OrganizationCommandHandler(repo, auth, new TestRunningSimulatorQuery());

        handler.HandleAsync(new CreateSiteCommand("SITE-I", "Site I", null, "UTC", "admin-user"), AdminCtx).GetAwaiter().GetResult();
        var ev = handler.Events.FirstOrDefault();
        if (ev is null) { f.Add("Site create should produce an event"); return f; }

        var allowedBefore = new[] { "code", "name", "description", "timezone", "status" };
        var allowedAfter = new[] { "code", "name", "description", "timezone", "status" };
        foreach (var key in ev.Before.Keys)
        {
            if (key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("hash", StringComparison.OrdinalIgnoreCase))
                f.Add($"Event Before must not contain sensitive key: {key}");
            if (!allowedBefore.Contains(key))
                f.Add($"Unexpected Before key: {key}. Expected one of: {string.Join(", ", allowedBefore)}");
        }
        foreach (var key in ev.After.Keys)
        {
            if (key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("hash", StringComparison.OrdinalIgnoreCase))
                f.Add($"Event After must not contain sensitive key: {key}");
            if (!allowedAfter.Contains(key))
                f.Add($"Unexpected After key: {key}. Expected one of: {string.Join(", ", allowedAfter)}");
        }

        return f;
    }

    private static List<string> DistinctCorrelationCausation()
    {
        var f = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var auth = new FakeOrganizationAuthorization(AdminCaller());

        var ctx = new OrganizationCommandContext("admin-user", "my-correlation", "my-causation");
        var handler = new OrganizationCommandHandler(repo, auth, new TestRunningSimulatorQuery());
        handler.HandleAsync(new CreateSiteCommand("SITE-J", "Site J", null, "UTC", "admin-user"), ctx).GetAwaiter().GetResult();
        var ev = handler.Events.FirstOrDefault();
        if (ev is null) { f.Add("Event should exist"); return f; }
        if (ev.CorrelationId != "my-correlation") f.Add("CorrelationId must be preserved from context");
        if (ev.CausationId != "my-causation") f.Add("CausationId must be distinct and preserved from context");

        return f;
    }

    private static List<string> NoOpEmitsNoEvent()
    {
        var f = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var siteId = SiteId.New();
        var site = new Site(siteId, "SITE-K", "Site K", null, "UTC", SiteStatus.Active, 1);
        repo.AddSiteAsync(site).GetAwaiter().GetResult();

        var auth = new FakeOrganizationAuthorization(AdminCaller());
        var handler = new OrganizationCommandHandler(repo, auth, new TestRunningSimulatorQuery());
        handler.HandleAsync(new UpdateSiteStatusCommand(siteId, "activate", 1, "admin-user"), AdminCtx).GetAwaiter().GetResult();
        if (handler.HasEvents) f.Add("Activating an already-Active Site must produce no event");

        return f;
    }

    private static List<string> AllFiveRolesAreEvaluated()
    {
        var f = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var siteId = SiteId.New();
        repo.AddSiteAsync(new Site(siteId, "ROLES-SITE", "Roles", null, "UTC", SiteStatus.Active, 1)).GetAwaiter().GetResult();
        foreach (var role in new[] { "Operator", "Manager", "Viewer" })
        {
            var caller = new OrganizationCallerSnapshot(role.ToLowerInvariant(), role, true,
                new[] { role }, new[] { siteId.ToString() }, Array.Empty<string>());
            var result = new OrganizationCommandHandler(repo, new FakeOrganizationAuthorization(caller), new TestRunningSimulatorQuery())
                .HandleAsync(new CreateAreaCommand(siteId, $"AREA-{role}", role, null, caller.UserId),
                    new OrganizationCommandContext(caller.UserId, null, null)).GetAwaiter().GetResult();
            if (result.IsSuccess) f.Add($"{role} must not mutate Organization hierarchy.");
        }
        return f;
    }

    private static List<string> CreateCommandsRejectSpoofedAncestry()
    {
        var f = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var siteA = new Site(SiteId.New(), "SPOOF-A", "A", null, "UTC", SiteStatus.Active, 1);
        var siteB = new Site(SiteId.New(), "SPOOF-B", "B", null, "UTC", SiteStatus.Active, 1);
        var areaB = new Area(AreaId.New(), siteB.Id, "SPOOF-AREA", "B", null, AreaStatus.Active, 1);
        var assetB = new Asset(AssetId.New(), siteB.Id, areaB.Id, "SPOOF-ASSET", "B", null, AssetStatus.Active, 1);
        repo.AddSiteAsync(siteA).GetAwaiter().GetResult(); repo.AddSiteAsync(siteB).GetAwaiter().GetResult();
        repo.AddAreaAsync(areaB).GetAwaiter().GetResult(); repo.AddAssetAsync(assetB).GetAwaiter().GetResult();
        var auth = new FakeOrganizationAuthorization(new OrganizationCallerSnapshot("admin-user", "Admin", true,
            new[] { "Administrator" }, Array.Empty<string>(), Array.Empty<string>()));
        var handler = new OrganizationCommandHandler(repo, auth, new TestRunningSimulatorQuery());
        var assetResult = handler.HandleAsync(new CreateAssetCommand(siteA.Id, areaB.Id, "SPOOF-NEW", "New", null, "admin-user"), AdminCtx)
            .GetAwaiter().GetResult();
        var pointResult = handler.HandleAsync(new CreatePointCommand(siteA.Id, AreaId.New(), assetB.Id, "SPOOF-POINT", null,
            "M", "U", "owner", 60, 300, "admin-user"), AdminCtx).GetAwaiter().GetResult();
        if (assetResult.Code != "NotFound" || pointResult.Code != "NotFound")
            f.Add("CreateAsset/CreatePoint must reject command-supplied ancestry that conflicts with trusted parents.");
        return f;
    }

    private static List<string> PointActivationDeferredToPhaseFive()
    {
        var f = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var site = new Site(SiteId.New(), "PHASE5-SITE", "Phase5", null, "UTC", SiteStatus.Active, 1);
        var area = new Area(AreaId.New(), site.Id, "PHASE5-AREA", "Area", null, AreaStatus.Active, 1);
        var asset = new Asset(AssetId.New(), site.Id, area.Id, "PHASE5-ASSET", "Asset", null, AssetStatus.Active, 1);
        var point = new MeasurementPoint(PointId.New(), site.Id, area.Id, asset.Id, "PHASE5-POINT", null,
            "M", "U", "owner", 60, 300, PointStatus.Draft, 1);
        repo.AddSiteAsync(site).GetAwaiter().GetResult(); repo.AddAreaAsync(area).GetAwaiter().GetResult();
        repo.AddAssetAsync(asset).GetAwaiter().GetResult(); repo.AddPointAsync(point).GetAwaiter().GetResult();
        var auth = new FakeOrganizationAuthorization(AdminCaller());
        foreach (var action in new[] { "activate", "reactivate" })
        {
            var handler = new OrganizationCommandHandler(repo, auth, new TestRunningSimulatorQuery());
            var result = handler.HandleAsync(new UpdatePointStatusCommand(point.Id, action, 1, "admin-user"), AdminCtx)
                .GetAwaiter().GetResult();
            if (result.Code != "PHASE5_REQUIRED" || handler.HasEvents) f.Add($"Point {action} must be deferred to Phase 5 with no event.");
        }
        var persisted = repo.GetPointAsync(point.Id).GetAwaiter().GetResult();
        if (persisted?.Status != PointStatus.Draft) f.Add("Deferred Point activation must not change status.");
        return f;
    }

    private static List<string> ActorUsernameIsSnapshotted()
    {
        var f = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var caller = new OrganizationCallerSnapshot("admin-user", "admin@example", true,
            new[] { "Administrator" }, Array.Empty<string>(), Array.Empty<string>());
        var handler = new OrganizationCommandHandler(repo, new FakeOrganizationAuthorization(caller), new TestRunningSimulatorQuery());
        handler.HandleAsync(new CreateSiteCommand("ACTOR-SITE", "Actor", null, "UTC", "admin-user"), AdminCtx)
            .GetAwaiter().GetResult();
        if (handler.Events.SingleOrDefault()?.ActorUsername != "admin@example")
            f.Add("Organization events must snapshot the resolved actor username.");
        return f;
    }

    private static List<string> UpdateCommandsAndExpectedVersions()
    {
        var f = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var site = new Site(SiteId.New(), "UPDATE-SITE", "Old", "old", "UTC", SiteStatus.Active, 1);
        var area = new Area(AreaId.New(), site.Id, "UPDATE-AREA", "Old Area", "old", AreaStatus.Active, 1);
        var asset = new Asset(AssetId.New(), site.Id, area.Id, "UPDATE-ASSET", "Old Asset", "old", AssetStatus.Active, 1);
        var point = new MeasurementPoint(PointId.New(), site.Id, area.Id, asset.Id, "UPDATE-POINT", "old", "M", "U", "owner", 60, 300, PointStatus.Active, 1);
        repo.AddSiteAsync(site).GetAwaiter().GetResult(); repo.AddAreaAsync(area).GetAwaiter().GetResult();
        repo.AddAssetAsync(asset).GetAwaiter().GetResult(); repo.AddPointAsync(point).GetAwaiter().GetResult();
        var auth = new FakeOrganizationAuthorization(AdminCaller());
        var context = new OrganizationCommandContext("admin-user", "update-corr", "update-caus");

        var handler = new OrganizationCommandHandler(repo, auth, new TestRunningSimulatorQuery());
        var siteUpdate = handler.HandleAsync(new UpdateSiteCommand(site.Id, "New", "new", "Asia/Ho_Chi_Minh", 1, "admin-user"), context).GetAwaiter().GetResult();
        var areaUpdate = handler.HandleAsync(new UpdateAreaCommand(area.Id, "New Area", "new", 1, "admin-user"), context).GetAwaiter().GetResult();
        var assetUpdate = handler.HandleAsync(new UpdateAssetCommand(asset.Id, "New Asset", "new", 1, "admin-user"), context).GetAwaiter().GetResult();
        var pointUpdate = handler.HandleAsync(new UpdatePointConfigurationCommand(point.Id, "new", "M2", "U2", "owner2", 120, 600, 1, "admin-user"), context).GetAwaiter().GetResult();
        if (siteUpdate.IsFailure || areaUpdate.IsFailure || assetUpdate.IsFailure || pointUpdate.IsFailure)
            f.Add("All explicit configuration update commands should succeed with the current ExpectedVersion.");
        if (repo.GetSiteAsync(site.Id).GetAwaiter().GetResult()?.Version != 2 ||
            repo.GetAreaAsync(area.Id).GetAwaiter().GetResult()?.Version != 2 ||
            repo.GetAssetAsync(asset.Id).GetAwaiter().GetResult()?.Version != 2 ||
            repo.GetPointAsync(point.Id).GetAwaiter().GetResult()?.Version != 2)
            f.Add("Accepted updates must increment each aggregate version exactly once.");

        var staleBeforeEvents = handler.Events.Count;
        var staleBefore = repo.GetSiteAsync(site.Id).GetAwaiter().GetResult()!;
        var stale = handler.HandleAsync(new UpdateSiteCommand(site.Id, "Stale", null, "UTC", 1, "admin-user"), context).GetAwaiter().GetResult();
        var staleAfter = repo.GetSiteAsync(site.Id).GetAwaiter().GetResult()!;
        if (stale.Code != "VERSION_CONFLICT" || handler.Events.Count != staleBeforeEvents ||
            staleAfter.Version != staleBefore.Version || staleAfter.Name != staleBefore.Name)
            f.Add("Stale ExpectedVersion must fail before mutation, history, or event emission.");
        var currentVersion = repo.GetSiteAsync(site.Id).GetAwaiter().GetResult()!.Version;
        var beforeEvents = handler.Events.Count;
        var noop = handler.HandleAsync(new UpdateSiteCommand(site.Id, "New", "new", "Asia/Ho_Chi_Minh", currentVersion, "admin-user"), context).GetAwaiter().GetResult();
        if (noop.IsFailure || repo.GetSiteAsync(site.Id).GetAwaiter().GetResult()!.Version != currentVersion || handler.Events.Count != beforeEvents)
            f.Add("No-op update must preserve version and emit no event.");
        return f;
    }

    private static List<string> InvalidParentStatusCreates()
    {
        var f = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var site = new Site(SiteId.New(), "PARENT-SITE", "Parent", null, "UTC", SiteStatus.Inactive, 1);
        repo.AddSiteAsync(site).GetAwaiter().GetResult();
        var auth = new FakeOrganizationAuthorization(AdminCaller());
        var handler = new OrganizationCommandHandler(repo, auth, new TestRunningSimulatorQuery());
        var areaResult = handler.HandleAsync(new CreateAreaCommand(site.Id, "BAD-AREA", "Bad", null, "admin-user"), AdminCtx).GetAwaiter().GetResult();
        if (areaResult.Code != "PARENT_NOT_CONFIGURABLE" || handler.HasEvents) f.Add("Inactive Site must reject Area creation without an event.");

        var activeSite = new Site(SiteId.New(), "PARENT-SITE-2", "Parent", null, "UTC", SiteStatus.Active, 1);
        var inactiveArea = new Area(AreaId.New(), activeSite.Id, "BAD-AREA-2", "Bad", null, AreaStatus.Inactive, 1);
        repo.AddSiteAsync(activeSite).GetAwaiter().GetResult(); repo.AddAreaAsync(inactiveArea).GetAwaiter().GetResult();
        var assetResult = handler.HandleAsync(new CreateAssetCommand(activeSite.Id, inactiveArea.Id, "BAD-ASSET", "Bad", null, "admin-user"), AdminCtx).GetAwaiter().GetResult();
        if (assetResult.Code != "PARENT_NOT_CONFIGURABLE") f.Add("Inactive Area must reject Asset creation.");

        var inactiveAsset = new Asset(AssetId.New(), activeSite.Id, inactiveArea.Id, "BAD-ASSET-2", "Bad", null, AssetStatus.Inactive, 1);
        repo.AddAssetAsync(inactiveAsset).GetAwaiter().GetResult();
        var pointResult = handler.HandleAsync(new CreatePointCommand(activeSite.Id, inactiveArea.Id, inactiveAsset.Id, "BAD-POINT", null, "M", "U", "owner", 60, 300, "admin-user"), AdminCtx).GetAwaiter().GetResult();
        if (pointResult.Code != "PARENT_NOT_CONFIGURABLE") f.Add("Inactive Asset must reject Point creation.");
        return f;
    }

    private static List<string> CompleteEventContractCoverage()
    {
        var f = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var auth = new FakeOrganizationAuthorization(AdminCaller());
        var handler = new OrganizationCommandHandler(repo, auth, new TestRunningSimulatorQuery());
        var ctx = new OrganizationCommandContext("admin-user", "event-corr", "event-caus");
        if (handler.HandleAsync(new CreateSiteCommand("EVENT-SITE", "Event Site", "desc", "UTC", "admin-user"), ctx).GetAwaiter().GetResult().IsFailure)
        { f.Add("Site event setup failed."); return f; }
        var site = repo.GetAllSitesAsync().GetAwaiter().GetResult().Single();
        handler.HandleAsync(new UpdateSiteStatusCommand(site.Id, "activate", 1, "admin-user"), ctx).GetAwaiter().GetResult();
        handler.HandleAsync(new CreateAreaCommand(site.Id, "EVENT-AREA", "Area", "desc", "admin-user"), ctx).GetAwaiter().GetResult();
        var area = repo.GetAreasForSiteAsync(site.Id).GetAwaiter().GetResult().Single();
        handler.HandleAsync(new UpdateAreaStatusCommand(area.Id, "activate", 1, "admin-user"), ctx).GetAwaiter().GetResult();
        handler.HandleAsync(new CreateAssetCommand(site.Id, area.Id, "EVENT-ASSET", "Asset", "desc", "admin-user"), ctx).GetAwaiter().GetResult();
        var asset = repo.GetAssetsForAreaAsync(area.Id).GetAwaiter().GetResult().Single();
        handler.HandleAsync(new UpdateAssetStatusCommand(asset.Id, "activate", 1, "admin-user"), ctx).GetAwaiter().GetResult();
        handler.HandleAsync(new CreatePointCommand(site.Id, area.Id, asset.Id, "EVENT-POINT", "desc", "M", "U", "owner", 60, 300, "admin-user"), ctx).GetAwaiter().GetResult();
        var point = repo.GetPointsForAssetAsync(asset.Id).GetAwaiter().GetResult().Single();
        var activePoint = new MeasurementPoint(PointId.New(), site.Id, area.Id, asset.Id, "EVENT-STATUS", null, "M", "U", "owner", 60, 300, PointStatus.Active, 1);
        repo.AddPointAsync(activePoint).GetAwaiter().GetResult();
        handler.HandleAsync(new UpdatePointStatusCommand(activePoint.Id, "inactivate", 1, "admin-user"), ctx).GetAwaiter().GetResult();

        var expected = new[] { "SiteStatusChanged.v1", "AreaStatusChanged.v1", "AssetStatusChanged.v1", "PointConfigurationChanged.v1", "PointStatusChanged.v1" };
        foreach (var eventType in expected)
        {
            var events = handler.Events.Where(e => e.EventType == eventType).ToList();
            if (events.Count == 0) { f.Add($"Missing event family {eventType}."); continue; }
            foreach (var ev in events)
            {
                if (ev.SchemaVersion != "1" || ev.Producer != "IUMP.Organization" || ev.ActorId != "admin-user" ||
                    ev.ActorUsername != "Admin" || ev.CorrelationId != "event-corr" || ev.CausationId != "event-caus" ||
                    ev.OccurredAt.Kind != DateTimeKind.Utc || ev.SiteId != site.Id.ToString())
                    f.Add($"Event contract metadata invalid for {eventType}.");

                var expectedKeys = eventType switch
                {
                    "SiteStatusChanged.v1" => new[] { "code", "name", "description", "timezone", "status" },
                    "AreaStatusChanged.v1" => new[] { "siteId", "areaId", "code", "name", "description", "status" },
                    "AssetStatusChanged.v1" => new[] { "siteId", "areaId", "code", "name", "description", "status" },
                    "PointConfigurationChanged.v1" => new[] { "siteId", "areaId", "assetId", "code", "description", "metricId", "unitId", "dataOwnerUserId", "expectedIntervalSeconds", "noDataAfterSeconds", "status" },
                    _ => new[] { "siteId", "areaId", "assetId", "code", "description", "metricId", "unitId", "dataOwnerUserId", "expectedIntervalSeconds", "noDataAfterSeconds", "status" }
                };
                var expectedBefore = ev.Action == "Created" ? Array.Empty<string>() : expectedKeys;
                if (!ev.Before.Keys.OrderBy(k => k).SequenceEqual(expectedBefore.OrderBy(k => k)) ||
                    !ev.After.Keys.OrderBy(k => k).SequenceEqual(expectedKeys.OrderBy(k => k)))
                    f.Add($"Event snapshots for {eventType} must use its exact before/after contract keys.");
            }
        }
        var siteEvent = handler.Events.Last(e => e.EventType == "SiteStatusChanged.v1");
        var areaEvent = handler.Events.Last(e => e.EventType == "AreaStatusChanged.v1");
        var assetEvent = handler.Events.Last(e => e.EventType == "AssetStatusChanged.v1");
        var pointConfigurationEvent = handler.Events.Last(e => e.EventType == "PointConfigurationChanged.v1");
        var pointEvent = handler.Events.Last(e => e.EventType == "PointStatusChanged.v1");
        if (siteEvent.AreaId is not null || areaEvent.AreaId != area.Id.ToString() || assetEvent.AreaId != area.Id.ToString() ||
            pointConfigurationEvent.AreaId != area.Id.ToString() || pointEvent.AreaId != area.Id.ToString())
            f.Add("Events must preserve trusted AreaId scope (Site null, Area/Asset/Point target ancestry).");
        if (!new[] { "code", "name", "description", "timezone", "status" }.All(siteEvent.After.ContainsKey) ||
            !new[] { "siteId", "areaId", "code", "name", "description", "status" }.All(areaEvent.After.ContainsKey) ||
            !new[] { "siteId", "areaId", "code", "name", "description", "status" }.All(assetEvent.After.ContainsKey) ||
            !new[] { "siteId", "areaId", "assetId", "code", "description", "status" }.All(pointEvent.After.ContainsKey))
            f.Add("Event before/after snapshots must use exact owner keys.");
        return f;
    }
}
