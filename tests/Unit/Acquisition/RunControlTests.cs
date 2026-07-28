using IUMP.Modules.Acquisition.Application;
using IUMP.Modules.Acquisition.Contracts;
using IUMP.Modules.Acquisition.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Acquisition;

public static class RunControlTests
{
    public static int TestCount { get; private set; }
    public static int CheckCount { get; private set; }

    public static List<string> Run()
    {
        TestCount = 0;
        CheckCount = 0;
        var failures = new List<string>();
        RunAsync(failures).GetAwaiter().GetResult();
        return failures;
    }

    private static async Task RunAsync(List<string> failures)
    {
        var clock = new FakeUtcClock(Phase6Fixtures.Now);
        var callers = new FakeRunCallerSnapshotProvider();
        callers.Callers["admin"] = Phase6Fixtures.Administrator;
        callers.Callers["engineer"] = new RunCallerSnapshot(
            "engineer", "scoped-engineer", true, ["Engineer"], ["site-a", "site-b"]);
        callers.Callers["unscoped"] = new RunCallerSnapshot(
            "unscoped", "unscoped-engineer", true, ["Engineer"], ["site-a"]);
        var snapshots = new FakeSimulatorStartSnapshotProvider
        {
            Snapshot = Phase6Fixtures.StartSnapshot(twoPoints: true)
        };
        var repositories = new FakeAcquisitionRunRepositories();
        var service = new SimulatorRunCommandService(
            callers, snapshots, repositories, repositories, repositories,
            new DeterministicGenerator(), clock);

        TestCount++;
        var started = await service.StartAsync(new StartSimulatorCommand(
            Phase6Fixtures.SourceId, "admin", "corr-start", "cause-start"));
        Check(started.IsSuccess && started.RunId is not null && started.Version == 1,
            "Administrator can start a Run at aggregate version 1", failures);
        var run = await repositories.GetAsync(started.RunId!.Value);
        var points = await repositories.ListPointStatesAsync(started.RunId.Value);
        Check(run is { Status: SimulatorRunStatus.Running, GeneratedCount: 0,
            AcceptedCount: 0, RejectedCount: 0 }, "new Run is Running with zero counters", failures);
        Check(run!.ConfigurationId == Phase6Fixtures.ConfigurationId &&
              run.ConfigurationVersion == 7 && run.SourceVersion == 3,
            "Run pins source and immutable configuration versions", failures);
        Check(points.Count == 2 && points.SequenceEqual(points.OrderBy(point => point.PointId)),
            "multiple Run-Point snapshots are stored in deterministic PointId order", failures);
        Check(points.All(point => point.NextSourceSequence == 0 &&
              point.NextDueAtUtc == Phase6Fixtures.Now && point.PrngState.Length == 25),
            "each Point starts at sequence zero, injected clock, and a 25-byte PRNG state", failures);
        Check(points.Select(point => point.MappingId).ToHashSet().SetEquals(
              snapshots.Snapshot!.Points.Select(point => point.MappingId)),
            "Point Mapping/metric/unit snapshots are pinned", failures);
        Check(repositories.CommittedEvents.Count == 1 &&
              repositories.CommittedEvents[0].Action == "Start",
            "Start commits exactly one owner event", failures);
        Check(snapshots.ResolveCount == 1 && snapshots.RecheckCount == 1,
            "provider state is resolved then rechecked inside the start transaction", failures);
        Check(snapshots.LastRecheckLockTrace.Select(item => item.Target).Distinct()
              .SequenceEqual([
                  SimulatorStartLockTarget.OrganizationSite,
                  SimulatorStartLockTarget.OrganizationArea,
                  SimulatorStartLockTarget.OrganizationAsset,
                  SimulatorStartLockTarget.OrganizationPoint,
                  SimulatorStartLockTarget.CatalogSourceMapping,
                  SimulatorStartLockTarget.AcquisitionRun
              ]),
            "Start locks Site/Area/Asset/Point then Catalog and Acquisition before recheck",
            failures);

        TestCount++;
        var scopedRepositories = new FakeAcquisitionRunRepositories();
        var scoped = new SimulatorRunCommandService(
            callers, snapshots, scopedRepositories, scopedRepositories, scopedRepositories,
            new DeterministicGenerator(), clock);
        var scopedStart = await scoped.StartAsync(new StartSimulatorCommand(
            Phase6Fixtures.SourceId, "engineer", "corr-scoped", null));
        Check(scopedStart.IsSuccess, "Engineer scoped to every trusted Site can start", failures);

        TestCount++;
        var unauthorizedRepositories = new FakeAcquisitionRunRepositories();
        var unauthorized = new SimulatorRunCommandService(
            callers, snapshots, unauthorizedRepositories, unauthorizedRepositories,
            unauthorizedRepositories, new DeterministicGenerator(), clock);
        var forbidden = await unauthorized.StartAsync(new StartSimulatorCommand(
            Phase6Fixtures.SourceId, "unscoped", "corr-forbidden", null));
        Check(!forbidden.IsSuccess && forbidden.Code == "NOT_FOUND",
            "partially scoped Engineer receives non-disclosing NOT_FOUND", failures);
        Check(await unauthorizedRepositories.GetCurrentBySourceAsync(Phase6Fixtures.SourceId) is null &&
              unauthorizedRepositories.CommittedEvents.Count == 0,
            "authorization failure publishes no Run, Run-Point, or event", failures);

        TestCount++;
        var driftRepositories = new FakeAcquisitionRunRepositories();
        var driftSnapshots = new FakeSimulatorStartSnapshotProvider
        {
            Snapshot = Phase6Fixtures.StartSnapshot(),
            RecheckResult = false
        };
        var drift = new SimulatorRunCommandService(
            callers, driftSnapshots, driftRepositories, driftRepositories, driftRepositories,
            new DeterministicGenerator(), clock);
        var driftResult = await drift.StartAsync(new StartSimulatorCommand(
            Phase6Fixtures.SourceId, "admin", "corr-drift", null));
        Check(!driftResult.IsSuccess && driftResult.Code == "PROVIDER_VERSION_DRIFT",
            "provider version drift rejects Start", failures);
        Check(await driftRepositories.GetCurrentBySourceAsync(Phase6Fixtures.SourceId) is null &&
              driftRepositories.CommittedEvents.Count == 0,
            "provider drift rolls back all Run state and owner events", failures);

        TestCount++;
        var beforePoint = points[0];
        var paused = await service.ChangeStatusAsync(new ChangeSimulatorRunStatusCommand(
            run.RunId, run.Version, SimulatorRunStatus.Paused, "admin", "corr-pause", null));
        Check(paused.IsSuccess && paused.Version == 2, "Running transitions to Paused", failures);
        var pausedAgain = await service.ChangeStatusAsync(new ChangeSimulatorRunStatusCommand(
            run.RunId, 2, SimulatorRunStatus.Paused, "admin", "corr-pause-again", null));
        Check(pausedAgain.IsSuccess && repositories.CommittedEvents.Count == 2,
            "equivalent lifecycle command is an event-free idempotent no-op", failures);
        var resumed = await service.ChangeStatusAsync(new ChangeSimulatorRunStatusCommand(
            run.RunId, 2, SimulatorRunStatus.Running, "admin", "corr-resume", null));
        Check(resumed.IsSuccess && resumed.RunId == run.RunId && resumed.Version == 3,
            "Paused resumes the same Run", failures);
        var preserved = await repositories.GetPointStateAsync(run.RunId, beforePoint.PointId);
        Check(preserved!.NextSourceSequence == beforePoint.NextSourceSequence &&
              preserved.PrngState.SequenceEqual(beforePoint.PrngState),
            "pause/resume preserves cursor and PRNG state", failures);
        var stale = await service.ChangeStatusAsync(new ChangeSimulatorRunStatusCommand(
            run.RunId, 1, SimulatorRunStatus.Stopped, "admin", "corr-stale", null));
        Check(!stale.IsSuccess && stale.Code == "VERSION_CONFLICT",
            "stale ExpectedVersion returns VERSION_CONFLICT", failures);
        var stopped = await service.ChangeStatusAsync(new ChangeSimulatorRunStatusCommand(
            run.RunId, 3, SimulatorRunStatus.Stopped, "admin", "corr-stop", null));
        Check(stopped.IsSuccess && stopped.Version == 4, "Running transitions to terminal Stopped", failures);
        var terminal = await service.ChangeStatusAsync(new ChangeSimulatorRunStatusCommand(
            run.RunId, 4, SimulatorRunStatus.Running, "admin", "corr-terminal", null));
        Check(!terminal.IsSuccess && terminal.Code == "PRECONDITION_FAILED",
            "Stopped is terminal", failures);
        Check(repositories.CommittedEvents.Select(item => item.Action)
              .SequenceEqual(["Start", "Pause", "Resume", "Stop"]),
            "accepted lifecycle transitions emit Start/Pause/Resume/Stop exactly once", failures);
        TestCount++;
        var restarted = await service.StartAsync(new StartSimulatorCommand(
            Phase6Fixtures.SourceId, "admin", "corr-new-run", null));
        Check(restarted.IsSuccess && restarted.RunId != run.RunId,
            "a new Start after terminal Stop creates a new Run identity", failures);
        var restartedPoints = await repositories.ListPointStatesAsync(restarted.RunId!.Value);
        Check(restartedPoints.Count == 2 &&
              restartedPoints.All(point => point.NextSourceSequence == 0),
            "new Run restarts every pinned Point at source sequence zero", failures);

        TestCount++;
        var recoveryRepository = new FakeAcquisitionRunRepositories();
        recoveryRepository.Seed(
            Phase6Fixtures.Run(Guid.Parse("10000000-0000-4000-8000-000000000001"),
                SimulatorRunStatus.Running),
            Phase6Fixtures.Point(Guid.Parse("10000000-0000-4000-8000-000000000001")));
        recoveryRepository.Seed(
            Phase6Fixtures.Run(Guid.Parse("20000000-0000-4000-8000-000000000002"),
                SimulatorRunStatus.Paused),
            Phase6Fixtures.Point(Guid.Parse("20000000-0000-4000-8000-000000000002")));
        recoveryRepository.Seed(
            Phase6Fixtures.Run(Guid.Parse("30000000-0000-4000-8000-000000000003"),
                SimulatorRunStatus.Stopped),
            Phase6Fixtures.Point(Guid.Parse("30000000-0000-4000-8000-000000000003")));
        var recovery = new SimulatorRunCommandService(
            callers, snapshots, recoveryRepository, recoveryRepository, recoveryRepository,
            new DeterministicGenerator(), clock);
        var recovered = await recovery.RecoverRunningAsync();
        Check(recovered.Count == 1 && recovered[0].Status == SimulatorRunStatus.Running,
            "restart recovers only persisted Running Runs", failures);

        await CompleteStartPrerequisiteMatrixAsync(clock, failures);
    }

    private static async Task CompleteStartPrerequisiteMatrixAsync(
        FakeUtcClock clock,
        List<string> failures)
    {
        var valid = Phase6Fixtures.StartSnapshot();
        var point = valid.Points[0];
        var admin = Phase6Fixtures.Administrator;
        var rejected = new List<(
            string Name,
            SimulatorStartSnapshot Snapshot,
            RunCallerSnapshot Caller,
            string Code)>
        {
            ("algorithm version zero", valid with { AlgorithmVersion = 0 }, admin, "CONFIGURATION_INVALID"),
            ("algorithm version two", valid with { AlgorithmVersion = 2 }, admin, "CONFIGURATION_INVALID"),
            ("unknown algorithm", valid with { AlgorithmId = "IUMP-DETERMINISTIC-V2" }, admin, "CONFIGURATION_INVALID"),
            ("unknown scenario", valid with { Scenario = (SimulatorScenario)999 }, admin, "CONFIGURATION_INVALID"),
            ("empty source identity", valid with { SourceId = Guid.Empty }, admin, "CONFIGURATION_INVALID"),
            ("empty configuration identity", valid with { ConfigurationId = Guid.Empty }, admin, "CONFIGURATION_INVALID"),
            ("empty Point identity", WithPoint(valid, point with { PointId = Guid.Empty }), admin, "CONFIGURATION_INVALID"),
            ("empty Mapping identity", WithPoint(valid, point with { MappingId = Guid.Empty }), admin, "CONFIGURATION_INVALID"),
            ("empty Asset identity", WithPoint(valid, point with { AssetId = Guid.Empty }), admin, "CONFIGURATION_INVALID"),
            ("empty Metric identity", WithPoint(valid, point with { MetricId = Guid.Empty }), admin, "CONFIGURATION_INVALID"),
            ("empty Unit identity", WithPoint(valid, point with { UnitId = Guid.Empty }), admin, "CONFIGURATION_INVALID"),
            ("blank Site identity", WithPoint(valid, point with { SiteId = " " }), admin, "CONFIGURATION_INVALID"),
            ("blank Area identity", WithPoint(valid, point with { AreaId = " " }), admin, "CONFIGURATION_INVALID"),
            ("blank Unit code", WithPoint(valid, point with { UnitCode = " " }), admin, "CONFIGURATION_INVALID"),
            ("duplicate Point", valid with
            {
                Points =
                [
                    point,
                    point with { MappingId = Guid.Parse("10000000-0000-4000-8000-000000000001") }
                ]
            }, admin, "CONFIGURATION_INVALID"),
            ("duplicate Mapping", valid with
            {
                Points =
                [
                    point,
                    point with { PointId = Guid.Parse("10000000-0000-4000-8000-000000000002") }
                ]
            }, admin, "CONFIGURATION_INVALID"),
            ("Draft Source", valid with { SourceStatus = "Draft" }, admin, "SOURCE_NOT_ACTIVE"),
            ("Suspended Source", valid with { SourceStatus = "Suspended" }, admin, "SOURCE_NOT_ACTIVE"),
            ("Decommissioned Source", valid with { SourceStatus = "Decommissioned" }, admin, "SOURCE_NOT_ACTIVE"),
            ("zero Mapping", valid with { Points = [] }, admin, "MAPPING_MISSING"),
            ("inactive Mapping", WithPoint(valid, point with { MappingStatus = "Inactive" }), admin, "MAPPING_NOT_ACTIVE"),
            ("future Mapping", WithPoint(valid, point with { EffectiveFromUtc = clock.UtcNow.AddSeconds(1) }), admin, "MAPPING_NOT_ACTIVE"),
            ("expired Mapping", WithPoint(valid, point with { EffectiveToUtc = clock.UtcNow }), admin, "MAPPING_NOT_ACTIVE"),
            ("Draft Point", WithPoint(valid, point with { PointStatus = "Draft" }), admin, "POINT_NOT_ACTIVE"),
            ("Inactive Point", WithPoint(valid, point with { PointStatus = "Inactive" }), admin, "POINT_NOT_ACTIVE"),
            ("inactive Site", WithPoint(valid, point with { SiteStatus = "Inactive" }), admin, "ANCESTOR_NOT_ACTIVE"),
            ("inactive Area", WithPoint(valid, point with { AreaStatus = "Inactive" }), admin, "ANCESTOR_NOT_ACTIVE"),
            ("inactive Asset", WithPoint(valid, point with { AssetStatus = "Inactive" }), admin, "ANCESTOR_NOT_ACTIVE"),
            ("Source version zero", valid with { SourceVersion = 0 }, admin, "CONFIGURATION_INVALID"),
            ("Configuration version zero", valid with { ConfigurationVersion = 0 }, admin, "CONFIGURATION_INVALID"),
            ("Point version zero", WithPoint(valid, point with { PointVersion = 0 }), admin, "CONFIGURATION_INVALID"),
            ("Site version zero", WithPoint(valid, point with { SiteVersion = 0 }), admin, "CONFIGURATION_INVALID"),
            ("Area version zero", WithPoint(valid, point with { AreaVersion = 0 }), admin, "CONFIGURATION_INVALID"),
            ("Asset version zero", WithPoint(valid, point with { AssetVersion = 0 }), admin, "CONFIGURATION_INVALID"),
            ("Mapping version zero", WithPoint(valid, point with { MappingVersion = 0 }), admin, "CONFIGURATION_INVALID"),
            ("Operator denied", valid, new("operator", "operator", true, ["Operator"], ["site-a"]), "FORBIDDEN"),
            ("Manager denied", valid, new("manager", "manager", true, ["Manager"], ["site-a"]), "FORBIDDEN"),
            ("Viewer denied", valid, new("viewer", "viewer", true, ["Viewer"], ["site-a"]), "FORBIDDEN"),
            ("inactive caller denied", valid, admin with { IsActive = false }, "FORBIDDEN"),
            ("one invalid Point is atomic", Phase6Fixtures.StartSnapshot(twoPoints: true) with
            {
                Points =
                [
                    Phase6Fixtures.StartSnapshot(twoPoints: true).Points[0],
                    Phase6Fixtures.StartSnapshot(twoPoints: true).Points[1] with
                    {
                        PointStatus = "Inactive"
                    }
                ]
            }, admin, "POINT_NOT_ACTIVE")
        };

        foreach (var item in rejected)
        {
            TestCount++;
            var callers = new FakeRunCallerSnapshotProvider();
            callers.Callers[item.Caller.UserId] = item.Caller;
            var snapshots = new FakeSimulatorStartSnapshotProvider { Snapshot = item.Snapshot };
            var repositories = new FakeAcquisitionRunRepositories();
            var generator = new CountingSimulatorValueGenerator(new DeterministicGenerator());
            var service = new SimulatorRunCommandService(
                callers, snapshots, repositories, repositories, repositories, generator, clock);
            var result = await service.StartAsync(new StartSimulatorCommand(
                item.Snapshot.SourceId, item.Caller.UserId, $"corr-{item.Name}", null));
            Check(!result.IsSuccess && result.Code == item.Code,
                $"{item.Name} returns {item.Code}", failures);
            Check(await repositories.GetCurrentBySourceAsync(item.Snapshot.SourceId) is null &&
                  (result.RunId is null) &&
                  repositories.CommittedPointCount == 0 &&
                  repositories.CommittedEvents.Count == 0 &&
                  !repositories.IsTransactionActive,
                $"{item.Name} leaves no Run, Run-Point, event, or partial transaction state",
                failures);
            Check(generator.InitializeCount == 0,
                $"{item.Name} is rejected before PRNG initialization", failures);
        }

        foreach (var success in new[]
        {
            (
                "one-Site Engineer",
                valid,
                new RunCallerSnapshot("engineer-one", "engineer-one", true,
                    ["Engineer"], ["site-a"])),
            (
                "multi-Site Engineer",
                Phase6Fixtures.StartSnapshot(twoPoints: true),
                new RunCallerSnapshot("engineer-all", "engineer-all", true,
                    ["Engineer"], ["site-a", "site-b"]))
        })
        {
            TestCount++;
            var callers = new FakeRunCallerSnapshotProvider();
            callers.Callers[success.Item3.UserId] = success.Item3;
            var snapshots = new FakeSimulatorStartSnapshotProvider { Snapshot = success.Item2 };
            var repositories = new FakeAcquisitionRunRepositories();
            var service = new SimulatorRunCommandService(
                callers, snapshots, repositories, repositories, repositories,
                new DeterministicGenerator(), clock);
            var first = await service.StartAsync(new StartSimulatorCommand(
                success.Item2.SourceId, success.Item3.UserId, "corr-success", null));
            var eventCount = repositories.CommittedEvents.Count;
            var second = await service.StartAsync(new StartSimulatorCommand(
                success.Item2.SourceId, success.Item3.UserId, "corr-repeat", null));
            Check(first.IsSuccess && second.IsSuccess && first.RunId == second.RunId &&
                  first.Version == second.Version,
                $"{success.Item1} succeeds and repeated Running Start returns the stable Run",
                failures);
            Check(repositories.CommittedEvents.Count == eventCount && eventCount == 1,
                $"{success.Item1} repeated Running Start emits no new event", failures);
        }

        TestCount++;
        var pausedRunId = Guid.Parse("40000000-0000-4000-8000-000000000001");
        var pausedRepository = new FakeAcquisitionRunRepositories();
        pausedRepository.Seed(
            Phase6Fixtures.Run(pausedRunId, SimulatorRunStatus.Paused),
            Phase6Fixtures.Point(pausedRunId));
        var pausedCallers = new FakeRunCallerSnapshotProvider();
        pausedCallers.Callers["admin"] = admin;
        var pausedService = new SimulatorRunCommandService(
            pausedCallers,
            new FakeSimulatorStartSnapshotProvider { Snapshot = valid },
            pausedRepository,
            pausedRepository,
            pausedRepository,
            new DeterministicGenerator(),
            clock);
        var stopped = await pausedService.ChangeStatusAsync(
            new ChangeSimulatorRunStatusCommand(
                pausedRunId, 1, SimulatorRunStatus.Stopped, "admin",
                "corr-paused-stop", null));
        Check(stopped.IsSuccess &&
              (await pausedRepository.GetAsync(pausedRunId))?.Status == SimulatorRunStatus.Stopped,
            "Paused transitions directly to Stopped", failures);
    }

    private static SimulatorStartSnapshot WithPoint(
        SimulatorStartSnapshot snapshot,
        SimulatorStartPointSnapshot point) =>
        snapshot with { Points = [point] };

    private static void Check(bool condition, string message, List<string> failures)
    {
        CheckCount++;
        if (!condition) failures.Add($"T110: {message}.");
    }
}

public static class Phase6Fixtures
{
    public static readonly DateTime Now = new(2026, 7, 28, 3, 0, 0, DateTimeKind.Utc);
    public static readonly Guid SourceId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    public static readonly Guid ConfigurationId = Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");
    public static readonly Guid PointId = Guid.Parse("11111111-2222-4333-8444-555555555555");
    public static readonly Guid MappingId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
    public static readonly RunCallerSnapshot Administrator =
        new("admin", "global-admin", true, ["Administrator"], []);

    public static SimulatorStartSnapshot StartSnapshot(bool twoPoints = false)
    {
        var points = new List<SimulatorStartPointSnapshot>
        {
            StartPoint(PointId, MappingId, "site-a")
        };
        if (twoPoints)
        {
            points.Add(StartPoint(
                Guid.Parse("01111111-2222-4333-8444-555555555555"),
                Guid.Parse("0ccccccc-cccc-4ccc-8ccc-cccccccccccc"), "site-b"));
        }
        return new SimulatorStartSnapshot(
            SourceId, "Simulator", "Active", 3, ConfigurationId, 7, 60, 10, 20, 42,
            SimulatorScenario.Normal, SimulatorConfigurationConstants.AlgorithmId,
            SimulatorConfigurationConstants.AlgorithmVersion, points);
    }

    public static SimulatorStartPointSnapshot StartPoint(Guid pointId, Guid mappingId, string siteId) =>
        new(pointId, 5, "Active", siteId, 2, "Active", $"area-{siteId}", 2, "Active",
            Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd"), 2, "Active",
            mappingId, 4, "Active", Now.AddMinutes(-1), null,
            Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee"),
            Guid.Parse("ffffffff-ffff-4fff-8fff-ffffffffffff"), "kW");

    public static SimulatorRun Run(Guid runId, SimulatorRunStatus status = SimulatorRunStatus.Running,
        long version = 1, long generated = 0, long accepted = 0, long rejected = 0) =>
        new(runId, SourceId, 3, ConfigurationId, 7,
            SimulatorConfigurationConstants.AlgorithmId,
            SimulatorConfigurationConstants.AlgorithmVersion, status, version,
            generated, accepted, rejected, null, null, Now, Now,
            status == SimulatorRunStatus.Paused ? Now : null, null,
            status == SimulatorRunStatus.Stopped ? Now : null,
            "admin", "global-admin", "corr-run", "cause-run");

    public static SimulatorRunPointState Point(Guid runId, DateTime? due = null,
        long sequence = 0, byte[]? state = null) =>
        new(runId, PointId, 5, MappingId, 4,
            Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee"),
            Guid.Parse("ffffffff-ffff-4fff-8fff-ffffffffffff"), "kW", 3, sequence,
            state ?? new DeterministicGenerator().Initialize(42, PointId, ConfigurationId, 7, 1),
            due ?? Now, "site-a", "area-site-a", null, null, 0, null, 1);

    public static SimulatorConfigurationVersion Configuration(long version = 7) =>
        new(ConfigurationId, version, 60, 10, 20, 42, SimulatorScenario.Normal,
            SimulatorConfigurationConstants.AlgorithmId,
            SimulatorConfigurationConstants.AlgorithmVersion, "admin", "global-admin", Now,
            "corr-config", "cause-config");

    public static SimulatorProductionPayload Payload(Guid runId, long sequence = 0) =>
        new(Guid.Parse("e118cea2-3d28-5dd4-9726-b3d7d4425ea4"), SourceId, runId,
            PointId, MappingId, 4, sequence, SimulatorConfigurationConstants.AlgorithmId,
            SimulatorConfigurationConstants.AlgorithmVersion, ConfigurationId, 7, Now,
            12.3456, "kW", "IUMP.Worker.Simulator", "corr-payload", "lineage-payload");

    public static SimulatorProductionAttempt Pending(Guid runId, long sequence = 0) =>
        new(runId, PointId, sequence, Payload(runId, sequence),
            SimulatorProductionAttemptStatus.Pending, null, null, null, null, null, Now, null, 1);
}
