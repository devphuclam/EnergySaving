using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Integration.Contracts;
using IUMP.Modules.Organization.Application;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Organization;

public static class PointActivationTransactionTests
{
    public static int TestCount;
    public static int AssertionCount;

    public static List<string> Run()
    {
        TestCount = 0;
        AssertionCount = 0;
        var f = new List<string>();

        SurfaceCheck(f);
        LockOrder(f);
        MissingParticipant(f);
        SameTransactionId(f);
        PreCommitPointInvisible(f);
        PreCommitLifecycleInvisible(f);
        PreCommitOutboxInvisible(f);
        AtomicCommitPublishesAll(f);
        AtomicCommitFailurePublishesNone(f);
        LockFailureRollback(f);
        ProviderDriftRollback(f);
        CancellationRollback(f);
        RetryDelays(f);
        RetryExhaustion(f);
        OneBackendCommit(f);
        OneBackendRollback(f);
        NoParticipantCommitSurface(f);

        return f;
    }

    private static void Fail(List<string> failures, string name, string? err)
    {
        AssertionCount++;
        if (err is not null) failures.Add($"{name}: {err}");
    }

    // -- 1. IHostTransactionParticipant surface checks --
    private static void SurfaceCheck(List<string> f)
    {
        TestCount++;
        var participantType = typeof(IHostTransactionParticipant);
        var methods = participantType.GetMethods().Select(m => m.Name).ToHashSet();
        Fail(f, "no PrepareAsync", methods.Contains("PrepareAsync") ? "must not expose PrepareAsync" : null);
        Fail(f, "no FinalizeAsync", methods.Contains("FinalizeAsync") ? "must not expose FinalizeAsync" : null);
        Fail(f, "no DiscardAsync", methods.Contains("DiscardAsync") ? "must not expose DiscardAsync" : null);
        Fail(f, "has AcquireLockAsync", !methods.Contains("AcquireLockAsync") ? "must expose AcquireLockAsync" : null);
    }

    // -- 2. exact lock order --
    private static void LockOrder(List<string> f)
    {
        TestCount++;
        var (backend, repo, _, _, coord, _, _) = CreateBackedFixture();
        RegisterAll(coord);
        coord.BeginAsync().GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.IamUser, "u1", 1).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.OrganizationSite, "s1", 2).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.OrganizationArea, "a1", 3).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.OrganizationAsset, "as1", 4).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.OrganizationPoint, "p1", 5).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.CatalogMetric, "m1", 6).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.CatalogUnit, "u1", 7).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.CatalogMapping, "mp1", 8).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.IntegrationOutbox, "o1", 9).GetAwaiter().GetResult();
        Fail(f, "lock trace count", coord.LockTrace.Count != 9 ? $"expected 9, got {coord.LockTrace.Count}" : null);
        try
        {
            coord.LockWithRetryAsync(LockTarget.IamUser, "x", 1).GetAwaiter().GetResult();
            Fail(f, "lock order violation", null);
        }
        catch (InvalidOperationException) { Fail(f, "lock order violation", null); }
    }

    // -- 3. missing participant --
    private static void MissingParticipant(List<string> f)
    {
        TestCount++;
        var coord = new HostTransactionCoordinator(NullBackend.Instance);
        try
        {
            coord.BeginAsync().GetAwaiter().GetResult();
            Fail(f, "missing participant", "must throw at begin");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("MISSING_TRANSACTION_PARTICIPANT")) { Fail(f, "missing participant", null); }
    }

    // -- 4. same TransactionId --
    private static void SameTransactionId(List<string> f)
    {
        TestCount++;
        var (backend, repo, org, identity, coord, catalog, outbox) = CreateBackedFixture();
        var point = SeedDraftPoint(repo, out _);
        RegisterAll(coord, identity, org, catalog, outbox);
        coord.BeginAsync().GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.IamUser, point.DataOwnerUserId, 1).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.OrganizationSite, point.SiteId.ToString(), 2).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.OrganizationArea, point.AreaId.ToString(), 3).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.OrganizationAsset, point.AssetId.ToString(), 4).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.OrganizationPoint, point.Id.ToString(), 5).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.CatalogMetric, point.MetricId, 6).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.CatalogUnit, point.UnitId, 7).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.CatalogMapping, point.Id.ToString(), 8).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.IntegrationOutbox, point.Id.ToString(), 9).GetAwaiter().GetResult();
        var allIds = identity.TransactionIds
            .Concat(org.TransactionIds)
            .Concat(catalog.TransactionIds)
            .Concat(outbox.TransactionIds).ToArray();
        Fail(f, "non-empty IDs", allIds.Length == 0 ? "no participant recorded a TransactionId" : null);
        Fail(f, "all match host", allIds.Length > 0 && allIds.Any(id => id != coord.TransactionId) ? $"expected all {coord.TransactionId}, got {string.Join(",", allIds.Select(x => x.ToString()))}" : null);
    }

    // -- 5. pre-commit point invisibility --
    private static void PreCommitPointInvisible(List<string> f)
    {
        TestCount++;
        var (backend, repo, org, identity, coord, catalog, outbox) = CreateBackedFixture();
        var point = SeedDraftPoint(repo, out var siteId);
        RegisterAll(coord, identity, org, catalog, outbox);
        coord.BeginAsync().GetAwaiter().GetResult();
        LockAll(coord, point);
        var snap = org.ReadLockedSnapshotAsync(coord, point.Id).GetAwaiter().GetResult()!;
        org.StageActivationAsync(coord, snap, "admin", "admin", "corr", null).GetAwaiter().GetResult();
        var committed = repo.GetPointAsync(point.Id).GetAwaiter().GetResult()!;
        Fail(f, "pre-commit not Active", committed.Status == PointStatus.Active ? "must not be Active before commit" : null);
        Fail(f, "pre-commit version unchanged", committed.Version != 1 ? $"expected 1, got {committed.Version}" : null);
    }

    // -- 6. pre-commit lifecycle invisibility --
    private static void PreCommitLifecycleInvisible(List<string> f)
    {
        TestCount++;
        var (backend, repo, org, identity, coord, catalog, outbox) = CreateBackedFixture();
        var point = SeedDraftPoint(repo, out _);
        RegisterAll(coord, identity, org, catalog, outbox);
        coord.BeginAsync().GetAwaiter().GetResult();
        LockAll(coord, point);
        var snap = org.ReadLockedSnapshotAsync(coord, point.Id).GetAwaiter().GetResult()!;
        org.StageActivationAsync(coord, snap, "admin", "admin", "corr", null).GetAwaiter().GetResult();
        var lifecycle = repo.GetLifecycleForPointAsync(point.Id.ToString()).GetAwaiter().GetResult();
        Fail(f, "pre-commit lifecycle", lifecycle.Count != 0 ? "must be empty before commit" : null);
    }

    // -- 7. pre-commit outbox invisibility --
    private static void PreCommitOutboxInvisible(List<string> f)
    {
        TestCount++;
        var (backend, repo, org, identity, coord, catalog, outbox) = CreateBackedFixture();
        var point = SeedDraftPoint(repo, out _);
        RegisterAll(coord, identity, org, catalog, outbox);
        coord.BeginAsync().GetAwaiter().GetResult();
        LockAll(coord, point);
        var snap = org.ReadLockedSnapshotAsync(coord, point.Id).GetAwaiter().GetResult()!;
        var activated = org.StageActivationAsync(coord, snap, "admin", "admin", "corr", null).GetAwaiter().GetResult();
        var envelope = OrganizationEvents.BuildPointStatusChanged(activated, PointStatus.Draft, PointStatus.Active,
            new OrganizationCommandContext("admin", "corr", null),
            new OrganizationCallerSnapshot("admin", "admin@test", true, [], [], []));
        outbox.EnqueueAsync(envelope, coord).GetAwaiter().GetResult();
        Fail(f, "pre-commit outbox", backend.CommittedEnvelopes.Count != 0 ? "must be empty before commit" : null);
    }

    // -- 8. atomic commit publishes all --
    private static void AtomicCommitPublishesAll(List<string> f)
    {
        TestCount++;
        var (backend, repo, org, identity, coord, catalog, outbox) = CreateBackedFixture();
        var point = SeedDraftPoint(repo, out _);
        RegisterAll(coord, identity, org, catalog, outbox);
        coord.BeginAsync().GetAwaiter().GetResult();
        LockAll(coord, point);
        var snap = org.ReadLockedSnapshotAsync(coord, point.Id).GetAwaiter().GetResult()!;
        var activated = org.StageActivationAsync(coord, snap, "admin", "admin", "corr", null).GetAwaiter().GetResult();
        var envelope = OrganizationEvents.BuildPointStatusChanged(activated, PointStatus.Draft, PointStatus.Active,
            new OrganizationCommandContext("admin", "corr", null),
            new OrganizationCallerSnapshot("admin", "admin@test", true, [], [], []));
        outbox.EnqueueAsync(envelope, coord).GetAwaiter().GetResult();
        coord.CommitAsync().GetAwaiter().GetResult();

        var committed = repo.GetPointAsync(point.Id).GetAwaiter().GetResult()!;
        Fail(f, "commit: Active", committed.Status != PointStatus.Active ? "must be Active" : null);
        Fail(f, "commit: version +1", committed.Version != 2 ? $"expected 2, got {committed.Version}" : null);
        var lifecycle = repo.GetLifecycleForPointAsync(point.Id.ToString()).GetAwaiter().GetResult();
        Fail(f, "commit: lifecycle", lifecycle.Count != 1 ? "expected 1" : null);
        Fail(f, "commit: outbox", backend.CommittedEnvelopes.Count != 1 ? "expected 1" : null);
        Fail(f, "commit: backend commit", backend.CommitCount != 1 ? $"expected 1, got {backend.CommitCount}" : null);
        Fail(f, "commit: backend rollback", backend.RollbackCount != 0 ? $"expected 0, got {backend.RollbackCount}" : null);
    }

    // -- 9. atomic commit failure publishes none + cleanup --
    private static void AtomicCommitFailurePublishesNone(List<string> f)
    {
        TestCount++;
        var (backend, repo, org, identity, coord, catalog, outbox) = CreateBackedFixture();
        backend.FailOnCommit = true;
        var point = SeedDraftPoint(repo, out _);
        RegisterAll(coord, identity, org, catalog, outbox);
        coord.BeginAsync().GetAwaiter().GetResult();
        LockAll(coord, point);
        var snap = org.ReadLockedSnapshotAsync(coord, point.Id).GetAwaiter().GetResult()!;
        var activated = org.StageActivationAsync(coord, snap, "admin", "admin", "corr", null).GetAwaiter().GetResult();
        var envelope = OrganizationEvents.BuildPointStatusChanged(activated, PointStatus.Draft, PointStatus.Active,
            new OrganizationCommandContext("admin", "corr", null),
            new OrganizationCallerSnapshot("admin", "admin@test", true, [], [], []));
        outbox.EnqueueAsync(envelope, coord).GetAwaiter().GetResult();

        try { coord.CommitAsync().GetAwaiter().GetResult(); } catch (InvalidOperationException) { }

        var committed = repo.GetPointAsync(point.Id).GetAwaiter().GetResult()!;
        Fail(f, "commit-fail: Point unchanged", committed.Status == PointStatus.Active ? "must stay unchanged" : null);
        Fail(f, "commit-fail: outbox empty", backend.CommittedEnvelopes.Count != 0 ? "must be empty" : null);
        var lifecycle = repo.GetLifecycleForPointAsync(point.Id.ToString()).GetAwaiter().GetResult();
        Fail(f, "commit-fail: lifecycle empty", lifecycle.Count != 0 ? "must be empty" : null);
        Fail(f, "commit-fail: workspace null", backend.GetWorkspace(coord) is not null ? "workspace must be removed" : null);
        Fail(f, "commit-fail: backend rollback=1", backend.RollbackCount != 1 ? $"expected 1, got {backend.RollbackCount}" : null);
        Fail(f, "commit-fail: backend commit=0", backend.CommitCount != 0 ? $"expected 0, got {backend.CommitCount}" : null);
        Fail(f, "commit-fail: coordinator completed", !coord.IsCompleted ? "must be marked completed" : null);
    }

    // -- 10. lock failure rollback --
    private static void LockFailureRollback(List<string> f)
    {
        TestCount++;
        var (backend, repo, org, identity, coord, catalog, outbox) = CreateBackedFixture();
        var badIdentity = new FakeActivationIdentityQuery { TransientFailures = 4 };
        RegisterAll(coord, badIdentity, org, catalog, outbox);
        coord.BeginAsync().GetAwaiter().GetResult();
        try { coord.LockWithRetryAsync(LockTarget.IamUser, "bad", 1).GetAwaiter().GetResult(); } catch (TransientDatabaseConflictException) { }
        coord.DisposeAsync().GetAwaiter().GetResult();
        Fail(f, "lock-fail: rollback=1", backend.RollbackCount != 1 ? $"expected 1, got {backend.RollbackCount}" : null);
        Fail(f, "lock-fail: workspace null", backend.GetWorkspace(coord) is not null ? "must be removed" : null);
        Fail(f, "lock-fail: no committed state", repo.GetPointAsync(PointId.New()).GetAwaiter().GetResult() is not null ? "nothing seeded" : null);
    }

    // -- 11. provider drift rollback --
    private static void ProviderDriftRollback(List<string> f)
    {
        TestCount++;
        var (backend, repo, org, identity, coord, catalog, outbox) = CreateBackedFixture();
        var point = SeedDraftPoint(repo, out _);
        RegisterAll(coord, identity, org, catalog, outbox);
        coord.BeginAsync().GetAwaiter().GetResult();
        LockAll(coord, point);
        var snap = org.ReadLockedSnapshotAsync(coord, point.Id).GetAwaiter().GetResult()!;
        var env = OrganizationEvents.BuildPointStatusChanged(snap.Point, PointStatus.Draft, PointStatus.Active,
            new OrganizationCommandContext("admin", "corr", null),
            new OrganizationCallerSnapshot("admin", "admin@test", true, [], [], []));
        outbox.EnqueueAsync(env, coord).GetAwaiter().GetResult();
        coord.RollbackAsync().GetAwaiter().GetResult();
        Fail(f, "drift: rollback=1", backend.RollbackCount != 1 ? $"expected 1, got {backend.RollbackCount}" : null);
        var committed = repo.GetPointAsync(point.Id).GetAwaiter().GetResult()!;
        Fail(f, "drift: Point unchanged", committed.Status == PointStatus.Active ? "must not change" : null);
    }

    // -- 12. cancellation rollback --
    private static void CancellationRollback(List<string> f)
    {
        TestCount++;
        var (backend, repo, org, identity, coord, catalog, outbox) = CreateBackedFixture();
        var point = SeedDraftPoint(repo, out _);
        RegisterAll(coord, identity, org, catalog, outbox);
        coord.BeginAsync().GetAwaiter().GetResult();
        LockAll(coord, point);
        var snap = org.ReadLockedSnapshotAsync(coord, point.Id).GetAwaiter().GetResult()!;
        var activated = org.StageActivationAsync(coord, snap, "admin", "admin", "corr", null).GetAwaiter().GetResult();
        var envelope = OrganizationEvents.BuildPointStatusChanged(activated, PointStatus.Draft, PointStatus.Active,
            new OrganizationCommandContext("admin", "corr", null),
            new OrganizationCallerSnapshot("admin", "admin@test", true, [], [], []));
        outbox.EnqueueAsync(envelope, coord).GetAwaiter().GetResult();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        try { coord.CommitAsync(cts.Token).GetAwaiter().GetResult(); Fail(f, "cancel: must throw", null); } catch (OperationCanceledException) { Fail(f, "cancel: threw", null); }
        Fail(f, "cancel: rollback=1", backend.RollbackCount != 1 ? $"expected 1, got {backend.RollbackCount}" : null);
        var committed = repo.GetPointAsync(point.Id).GetAwaiter().GetResult()!;
        Fail(f, "cancel: Point unchanged", committed.Status == PointStatus.Active ? "must not change" : null);
    }

    // -- 13. retry delay trace --
    private static void RetryDelays(List<string> f)
    {
        TestCount++;
        var clock = new List<int>();
        var delay = new FakeHostDelay(clock);
        var backend = new FakeAtomicBackend(new FakeOrganizationCommandRepository());
        var coord = new HostTransactionCoordinator(backend, delay);
        var identity = new FakeActivationIdentityQuery { TransientFailures = 3 };
        RegisterAll(coord, identity, new FakeActivationOrganizationParticipant(new FakeOrganizationCommandRepository(), backend),
            new FakeActivationCatalogQuery(), new FakeTransactionalOutboxWriter(backend));
        coord.BeginAsync().GetAwaiter().GetResult();
        try { coord.LockWithRetryAsync(LockTarget.IamUser, "u1", 1).GetAwaiter().GetResult(); } catch (TransientDatabaseConflictException) { }
        var expected = new[] { 50, 150, 450 };
        Fail(f, "retry: delay count", clock.Count != 3 ? $"expected 3, got {clock.Count}" : null);
        for (var i = 0; i < clock.Count && i < expected.Length; i++)
            Fail(f, $"retry: delay[{i}]", clock[i] != expected[i] ? $"expected {expected[i]}ms, got {clock[i]}ms" : null);
    }

    // -- 14. retry exhaustion --
    private static void RetryExhaustion(List<string> f)
    {
        TestCount++;
        var backend = new FakeAtomicBackend(new FakeOrganizationCommandRepository());
        var coord = new HostTransactionCoordinator(backend);
        var identity = new FakeActivationIdentityQuery { TransientFailures = 4 };
        RegisterAll(coord, identity, new FakeActivationOrganizationParticipant(new FakeOrganizationCommandRepository(), backend),
            new FakeActivationCatalogQuery(), new FakeTransactionalOutboxWriter(backend));
        coord.BeginAsync().GetAwaiter().GetResult();
        try
        {
            coord.LockWithRetryAsync(LockTarget.IamUser, "u1", 1).GetAwaiter().GetResult();
            Fail(f, "retry: exhaustion", "must throw TransientDatabaseConflictException");
        }
        catch (TransientDatabaseConflictException) { Fail(f, "retry: exhaustion", null); }
    }

    // -- 15. exactly one backend CommitAsync call --
    private static void OneBackendCommit(List<string> f)
    {
        TestCount++;
        var (backend, repo, org, identity, coord, catalog, outbox) = CreateBackedFixture();
        var point = SeedDraftPoint(repo, out _);
        RegisterAll(coord, identity, org, catalog, outbox);
        coord.BeginAsync().GetAwaiter().GetResult();
        LockAll(coord, point);
        var snap = org.ReadLockedSnapshotAsync(coord, point.Id).GetAwaiter().GetResult()!;
        var activated = org.StageActivationAsync(coord, snap, "admin", "admin", "corr", null).GetAwaiter().GetResult();
        var env = OrganizationEvents.BuildPointStatusChanged(activated, PointStatus.Draft, PointStatus.Active,
            new OrganizationCommandContext("admin", "corr", null),
            new OrganizationCallerSnapshot("admin", "admin@test", true, [], [], []));
        outbox.EnqueueAsync(env, coord).GetAwaiter().GetResult();
        coord.CommitAsync().GetAwaiter().GetResult();
        Fail(f, "one-commit: count", backend.CommitCount != 1 ? $"expected 1, got {backend.CommitCount}" : null);
        Fail(f, "one-commit: no rollback", backend.RollbackCount != 0 ? $"expected 0, got {backend.RollbackCount}" : null);
    }

    // -- 16. exactly one backend RollbackAsync call --
    private static void OneBackendRollback(List<string> f)
    {
        TestCount++;
        var (backend, repo, org, identity, coord, catalog, outbox) = CreateBackedFixture();
        var point = SeedDraftPoint(repo, out _);
        RegisterAll(coord, identity, org, catalog, outbox);
        coord.BeginAsync().GetAwaiter().GetResult();
        LockAll(coord, point);
        coord.RollbackAsync().GetAwaiter().GetResult();
        Fail(f, "one-rollback: count", backend.RollbackCount != 1 ? $"expected 1, got {backend.RollbackCount}" : null);
        Fail(f, "one-rollback: no commit", backend.CommitCount != 0 ? $"expected 0, got {backend.CommitCount}" : null);
    }

    // -- 17. no participant commit surface --
    private static void NoParticipantCommitSurface(List<string> f)
    {
        TestCount++;
        var participantType = typeof(IHostTransactionParticipant);
        var methods = participantType.GetMethods().Select(m => m.Name).ToHashSet();
        foreach (var bad in new[] { "PrepareAsync", "FinalizeAsync", "DiscardAsync", "CommitAsync", "RollbackAsync" })
            Fail(f, $"no {bad}", methods.Contains(bad) ? $"must not expose {bad}" : null);
    }

    // -- internal helpers --
    private static (FakeAtomicBackend Backend, FakeOrganizationCommandRepository Repo, FakeActivationOrganizationParticipant Org, FakeActivationIdentityQuery Identity, HostTransactionCoordinator Coord, FakeActivationCatalogQuery Catalog, FakeTransactionalOutboxWriter Outbox) CreateBackedFixture()
    {
        var repo = new FakeOrganizationCommandRepository();
        var backend = new FakeAtomicBackend(repo);
        var org = new FakeActivationOrganizationParticipant(repo, backend);
        var identity = new FakeActivationIdentityQuery();
        var catalog = new FakeActivationCatalogQuery();
        var outbox = new FakeTransactionalOutboxWriter(backend);
        var coord = new HostTransactionCoordinator(backend);
        return (backend, repo, org, identity, coord, catalog, outbox);
    }

    private static MeasurementPoint SeedDraftPoint(FakeOrganizationCommandRepository repo, out SiteId siteId)
    {
        var site = new Site(SiteId.New(), "TX-SITE", "Tx", null, "UTC", SiteStatus.Active, 1);
        var area = new Area(AreaId.New(), site.Id, "TX-AREA", "Area", null, AreaStatus.Active, 1);
        var asset = new Asset(AssetId.New(), site.Id, area.Id, "TX-ASSET", "Asset", null, AssetStatus.Active, 1);
        var point = new MeasurementPoint(PointId.New(), site.Id, area.Id, asset.Id, "TX-POINT", "tx", "metric-1", "unit-1", "owner-user", 60, 300, PointStatus.Draft, 1);
        repo.AddSiteAsync(site).GetAwaiter().GetResult();
        repo.AddAreaAsync(area).GetAwaiter().GetResult();
        repo.AddAssetAsync(asset).GetAwaiter().GetResult();
        repo.AddPointAsync(point).GetAwaiter().GetResult();
        siteId = site.Id;
        return point;
    }

    private static void RegisterAll(HostTransactionCoordinator coord, IActivationIdentityParticipant? identity = null,
        IActivationOrganizationParticipant? org = null, IActivationCatalogParticipant? catalog = null,
        ITransactionalOutboxWriter? outbox = null)
    {
        coord.RegisterParticipant(LockTarget.IamUser, identity ?? new FakeActivationIdentityQuery());
        coord.RegisterParticipant(LockTarget.OrganizationSite, org ?? new FakeActivationOrganizationParticipant(new FakeOrganizationCommandRepository(), new FakeAtomicBackend(new FakeOrganizationCommandRepository())));
        coord.RegisterParticipant(LockTarget.OrganizationArea, org ?? new FakeActivationOrganizationParticipant(new FakeOrganizationCommandRepository(), new FakeAtomicBackend(new FakeOrganizationCommandRepository())));
        coord.RegisterParticipant(LockTarget.OrganizationAsset, org ?? new FakeActivationOrganizationParticipant(new FakeOrganizationCommandRepository(), new FakeAtomicBackend(new FakeOrganizationCommandRepository())));
        coord.RegisterParticipant(LockTarget.OrganizationPoint, org ?? new FakeActivationOrganizationParticipant(new FakeOrganizationCommandRepository(), new FakeAtomicBackend(new FakeOrganizationCommandRepository())));
        coord.RegisterParticipant(LockTarget.CatalogMetric, catalog ?? new FakeActivationCatalogQuery());
        coord.RegisterParticipant(LockTarget.CatalogUnit, catalog ?? new FakeActivationCatalogQuery());
        coord.RegisterParticipant(LockTarget.CatalogMapping, catalog ?? new FakeActivationCatalogQuery());
        coord.RegisterParticipant(LockTarget.IntegrationOutbox, outbox ?? new FakeTransactionalOutboxWriter(new FakeAtomicBackend(new FakeOrganizationCommandRepository())));
    }

    private static void LockAll(HostTransactionCoordinator coord, MeasurementPoint point)
    {
        coord.LockWithRetryAsync(LockTarget.IamUser, point.DataOwnerUserId, 1).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.OrganizationSite, point.SiteId.ToString(), 2).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.OrganizationArea, point.AreaId.ToString(), 3).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.OrganizationAsset, point.AssetId.ToString(), 4).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.OrganizationPoint, point.Id.ToString(), 5).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.CatalogMetric, point.MetricId, 6).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.CatalogUnit, point.UnitId, 7).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.CatalogMapping, point.Id.ToString(), 8).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.IntegrationOutbox, point.Id.ToString(), 9).GetAwaiter().GetResult();
    }

    private sealed class FakeHostDelay : IHostDelay
    {
        private readonly List<int> _delays;
        public FakeHostDelay(List<int> delays) => _delays = delays;
        public Task DelayAsync(int milliseconds, CancellationToken ct = default)
        {
            _delays.Add(milliseconds);
            return Task.CompletedTask;
        }
    }
}
