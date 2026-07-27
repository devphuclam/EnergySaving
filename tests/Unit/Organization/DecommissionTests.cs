using IUMP.Modules.Organization.Application;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Organization;

public static class DecommissionTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var (site, area, asset) = Setup(repo);
        var caller = new OrganizationCallerSnapshot("admin", "admin@example", true,
            new[] { "Administrator" }, Array.Empty<string>(), Array.Empty<string>());
        var auth = new FakeOrganizationAuthorization(caller);

        var quietAsset = new Asset(AssetId.New(), site.Id, area.Id, "ASSET-QUIET", "Quiet", null, AssetStatus.Active, 1);
        repo.AddAssetAsync(quietAsset).GetAwaiter().GetResult();
        repo.AddPointAsync(new MeasurementPoint(PointId.New(), site.Id, area.Id, quietAsset.Id, "QUIET-PT", null,
            "M", "U", "owner", 60, 300, PointStatus.Inactive, 1)).GetAwaiter().GetResult();
        var quietResult = new OrganizationCommandHandler(repo, auth, new TestRunningSimulatorQuery())
            .HandleAsync(new DecommissionAssetCommand(quietAsset.Id, 1, "admin"), new OrganizationCommandContext("admin", "c1", "x1"))
            .GetAwaiter().GetResult();
        if (quietResult.IsFailure || repo.GetAssetAsync(quietAsset.Id).GetAwaiter().GetResult()?.Status != AssetStatus.Decommissioned)
            failures.Add("Asset decommission with no Active child Point should succeed.");

        var activeAsset = new Asset(AssetId.New(), site.Id, area.Id, "ASSET-ACTIVE", "Active Child", null, AssetStatus.Active, 1);
        repo.AddAssetAsync(activeAsset).GetAwaiter().GetResult();
        var activePoint = new MeasurementPoint(PointId.New(), site.Id, area.Id, activeAsset.Id, "ACTIVE-PT", null,
            "M", "U", "owner", 60, 300, PointStatus.Active, 1);
        repo.AddPointAsync(activePoint).GetAwaiter().GetResult();
        var rejectedAsset = new OrganizationCommandHandler(repo, auth, new TestRunningSimulatorQuery())
            .HandleAsync(new DecommissionAssetCommand(activeAsset.Id, 1, "admin"), new OrganizationCommandContext("admin", "c2", "x2"))
            .GetAwaiter().GetResult();
        if (rejectedAsset.IsSuccess || repo.GetPointAsync(activePoint.Id).GetAwaiter().GetResult()?.Status != PointStatus.Active)
            failures.Add("Asset with Active child Point must fail atomically without cascade.");

        var running = new SimulatorQuery(true);
        var runningHandler = new OrganizationCommandHandler(repo, auth, running);
        var runningResult = runningHandler.HandleAsync(new DecommissionPointCommand(activePoint.Id, 1, "admin"),
            new OrganizationCommandContext("admin", "c3", "x3")).GetAwaiter().GetResult();
        if (runningResult.Code != "RUNNING_SIMULATOR" || runningHandler.HasEvents)
            failures.Add("Running Simulator must block Point decommission without an event.");

        var clearHandler = new OrganizationCommandHandler(repo, auth, new SimulatorQuery(false));
        var clearResult = clearHandler.HandleAsync(new DecommissionPointCommand(activePoint.Id, 1, "admin"),
            new OrganizationCommandContext("admin", "c4", "x4")).GetAwaiter().GetResult();
        var history = repo.GetLifecycleForPointAsync(activePoint.Id.ToString()).GetAwaiter().GetResult();
        if (clearResult.IsFailure || history.Count != 1 || history[0].OldStatus != PointStatus.Active ||
            history[0].ActorUsername != "admin@example" || clearHandler.Events.Count != 1)
            failures.Add("Accepted Point decommission must write one history row with actual old status and actor username.");

        var noopHandler = new OrganizationCommandHandler(repo, auth, new SimulatorQuery(false));
        var noop = noopHandler.HandleAsync(new DecommissionPointCommand(activePoint.Id, 2, "admin"),
            new OrganizationCommandContext("admin", "c5", "x5")).GetAwaiter().GetResult();
        if (noop.IsSuccess || repo.GetLifecycleForPointAsync(activePoint.Id.ToString()).GetAwaiter().GetResult().Count != 1 || noopHandler.HasEvents)
            failures.Add("Rejected terminal Point decommission must not append history or emit an event.");

        // Point code remains reserved after decommission
        var decomReserved = repo.IsPointCodeReservedAsync(site.Id, "ACTIVE-PT").GetAwaiter().GetResult();
        if (!decomReserved) failures.Add("Point code must remain reserved after decommission.");

        var unavailablePoint = new MeasurementPoint(PointId.New(), site.Id, area.Id, asset.Id, "UNAVAILABLE-PT", null,
            "M", "U", "owner", 60, 300, PointStatus.Active, 1);
        repo.AddPointAsync(unavailablePoint).GetAwaiter().GetResult();
        var unavailableHandler = new OrganizationCommandHandler(repo, auth, new ThrowingSimulatorQuery());
        var unavailable = unavailableHandler.HandleAsync(new DecommissionPointCommand(unavailablePoint.Id, 1, "admin"),
            new OrganizationCommandContext("admin", "c6", "x6")).GetAwaiter().GetResult();
        if (unavailable.Code != "DEPENDENCY_UNAVAILABLE" || repo.GetPointAsync(unavailablePoint.Id).GetAwaiter().GetResult()?.Status != PointStatus.Active ||
            repo.GetLifecycleForPointAsync(unavailablePoint.Id.ToString()).GetAwaiter().GetResult().Count != 0 || unavailableHandler.HasEvents)
            failures.Add("Unavailable Running Simulator dependency must fail closed without mutation, history, or event.");

        return failures;
    }

    private static (Site site, Area area, Asset asset) Setup(FakeOrganizationCommandRepository repo)
    {
        var site = new Site(SiteId.New(), "DECOM-SITE", "Decom Site", null, "UTC", SiteStatus.Active, 1);
        var area = new Area(AreaId.New(), site.Id, "DECOM-AREA", "Decom Area", null, AreaStatus.Active, 1);
        var asset = new Asset(AssetId.New(), site.Id, area.Id, "DECOM-ASSET", "Decom Asset", null, AssetStatus.Active, 1);
        repo.AddSiteAsync(site).GetAwaiter().GetResult();
        repo.AddAreaAsync(area).GetAwaiter().GetResult();
        repo.AddAssetAsync(asset).GetAwaiter().GetResult();
        return (site, area, asset);
    }

    private sealed class SimulatorQuery : IRunningSimulatorQuery
    {
        private readonly bool _running;
        public SimulatorQuery(bool running) => _running = running;
        public Task<bool> HasRunningSimulatorAsync(string pointId, CancellationToken ct = default) => Task.FromResult(_running);
    }

    private sealed class ThrowingSimulatorQuery : IRunningSimulatorQuery
    {
        public Task<bool> HasRunningSimulatorAsync(string pointId, CancellationToken ct = default) =>
            throw new InvalidOperationException("simulator unavailable");
    }
}
