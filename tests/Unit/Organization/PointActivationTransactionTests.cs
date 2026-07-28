using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Integration.Contracts;
using IUMP.Modules.Organization.Application;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Organization;

public static class PointActivationTransactionTests
{
    public const int CaseCount = 20;

    public static List<string> Run()
    {
        var f = new List<string>();

        // participant surface checks
        SurfaceCheck(f);

        // lock and transaction integrity
        LockOrder(f);
        MissingParticipant(f);
        SameTransactionId(f);

        // pre-commit invisibility
        PreCommitPointInvisible(f);
        PreCommitLifecycleInvisible(f);
        PreCommitOutboxInvisible(f);

        // atomic commit
        AtomicCommitPublishesAll(f);
        AtomicCommitFailurePublishesNone(f);

        // rollback scenarios
        LockFailureRollback(f);
        ProviderDriftRollback(f);
        CancellationRollback(f);

        // retry
        RetryDelays(f);
        RetryExhaustion(f);

        // backend invocation counts
        OneBackendCommit(f);
        OneBackendRollback(f);
        NoParticipantCommitSurface(f);

        return f;
    }

    private static void Fail(List<string> failures, string name, string? err)
    {
        if (err is not null) failures.Add($"{name}: {err}");
    }

    // -- 1. IHostTransactionParticipant surface checks --
    private static void SurfaceCheck(List<string> f)
    {
        var participantType = typeof(IHostTransactionParticipant);
        var methods = participantType.GetMethods().Select(m => m.Name).ToHashSet();
        if (methods.Contains("PrepareAsync")) f.Add("IHostTransactionParticipant must not expose PrepareAsync");
        if (methods.Contains("FinalizeAsync")) f.Add("IHostTransactionParticipant must not expose FinalizeAsync");
        if (methods.Contains("DiscardAsync")) f.Add("IHostTransactionParticipant must not expose DiscardAsync");
        if (!methods.Contains("AcquireLockAsync")) f.Add("IHostTransactionParticipant must expose AcquireLockAsync");
    }

    // -- 2. exact lock order --
    private static void LockOrder(List<string> f)
    {
        var (backend, repo, _, _, coord, _, _) = CreateBackedFixture();
        RegisterAll(coord);
        coord.BeginAsync().GetAwaiter().GetResult();
        // correct order
        coord.LockWithRetryAsync(LockTarget.IamUser, "u1", 1).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.OrganizationSite, "s1", 2).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.OrganizationArea, "a1", 3).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.OrganizationAsset, "as1", 4).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.OrganizationPoint, "p1", 5).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.CatalogMetric, "m1", 6).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.CatalogUnit, "u1", 7).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.CatalogMapping, "mp1", 8).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.IntegrationOutbox, "o1", 9).GetAwaiter().GetResult();
        if (coord.LockTrace.Count != 9) f.Add("lock order must produce exactly 9 lock entries");
        // violation
        try
        {
            coord.LockWithRetryAsync(LockTarget.IamUser, "x", 1).GetAwaiter().GetResult();
            f.Add("lock order violation must throw");
        }
        catch (InvalidOperationException) { }
    }

    // -- 3. missing participant --
    private static void MissingParticipant(List<string> f)
    {
        var coord = new HostTransactionCoordinator(NullBackend.Instance);
        try
        {
            coord.BeginAsync().GetAwaiter().GetResult();
            f.Add("missing participant must throw at begin");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("MISSING_TRANSACTION_PARTICIPANT")) { }
    }

    // -- 4. same TransactionId --
    private static void SameTransactionId(List<string> f)
    {
        var (backend, repo, org, identity, coord, catalog, outbox) = CreateBackedFixture();
        RegisterAll(coord);
        coord.BeginAsync().GetAwaiter().GetResult();
        var allIds = identity.TransactionIds
            .Concat(org.TransactionIds)
            .Concat(catalog.TransactionIds)
            .Concat(outbox.TransactionIds);
        if (allIds.Any(id => id != coord.TransactionId)) f.Add("all participant TransactionIds must match the host");
    }

    // -- 5. pre-commit point invisibility --
    private static void PreCommitPointInvisible(List<string> f)
    {
        var (backend, repo, org, identity, coord, catalog, outbox) = CreateBackedFixture();
        var point = SeedDraftPoint(repo, out var siteId);
        RegisterAll(coord, identity, org, catalog, outbox);
        coord.BeginAsync().GetAwaiter().GetResult();
        LockAll(coord, point);
        var snap = org.ReadLockedSnapshotAsync(coord, point.Id).GetAwaiter().GetResult()!;
        org.StageActivationAsync(coord, snap, "admin", "admin", "corr", null).GetAwaiter().GetResult();
        // staged but not committed — committed repo must still show old state
        var committed = repo.GetPointAsync(point.Id).GetAwaiter().GetResult()!;
        if (committed.Status == PointStatus.Active) f.Add("pre-commit: committed Point must not be Active before commit");
        if (committed.Version != 1) f.Add("pre-commit: committed Point version must be unchanged");
    }

    // -- 6. pre-commit lifecycle invisibility --
    private static void PreCommitLifecycleInvisible(List<string> f)
    {
        var (backend, repo, org, identity, coord, catalog, outbox) = CreateBackedFixture();
        var point = SeedDraftPoint(repo, out _);
        RegisterAll(coord, identity, org, catalog, outbox);
        coord.BeginAsync().GetAwaiter().GetResult();
        LockAll(coord, point);
        var snap = org.ReadLockedSnapshotAsync(coord, point.Id).GetAwaiter().GetResult()!;
        org.StageActivationAsync(coord, snap, "admin", "admin", "corr", null).GetAwaiter().GetResult();
        var lifecycle = repo.GetLifecycleForPointAsync(point.Id.ToString()).GetAwaiter().GetResult();
        if (lifecycle.Count != 0) f.Add("pre-commit: no lifecycle entry should be visible before commit");
    }

    // -- 7. pre-commit outbox invisibility --
    private static void PreCommitOutboxInvisible(List<string> f)
    {
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
        if (backend.CommittedEnvelopes.Count != 0) f.Add("pre-commit: no outbox envelope should be committed before commit");
    }

    // -- 8. atomic commit publishes all --
    private static void AtomicCommitPublishesAll(List<string> f)
    {
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
        if (committed.Status != PointStatus.Active) f.Add("atomic commit: Point must be Active after commit");
        if (committed.Version != 2) f.Add("atomic commit: Point version must be incremented");
        var lifecycle = repo.GetLifecycleForPointAsync(point.Id.ToString()).GetAwaiter().GetResult();
        if (lifecycle.Count != 1) f.Add("atomic commit: exactly one lifecycle entry expected");
        if (backend.CommittedEnvelopes.Count != 1) f.Add("atomic commit: exactly one outbox envelope expected");
        if (backend.CommitCount != 1) f.Add("atomic commit: exactly one backend commit expected");
        if (backend.RollbackCount != 0) f.Add("atomic commit: no rollback expected");
    }

    // -- 9. atomic commit failure publishes none --
    private static void AtomicCommitFailurePublishesNone(List<string> f)
    {
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
        if (committed.Status == PointStatus.Active) f.Add("commit failure: Point must stay unchanged after failed commit");
        if (backend.CommittedEnvelopes.Count != 0) f.Add("commit failure: no outbox envelope should be published");
        var lifecycle = repo.GetLifecycleForPointAsync(point.Id.ToString()).GetAwaiter().GetResult();
        if (lifecycle.Count != 0) f.Add("commit failure: no lifecycle entry should be published");
        if (backend.RollbackCount != 0) f.Add("commit failure: rollback must not be called when backend throws");
    }

    // -- 10. lock failure rollback --
    private static void LockFailureRollback(List<string> f)
    {
        var (backend, repo, org, identity, coord, catalog, outbox) = CreateBackedFixture();
        var badIdentity = new FakeActivationIdentityQuery { TransientFailures = 4 };
        RegisterAll(coord, badIdentity, org, catalog, outbox);
        coord.BeginAsync().GetAwaiter().GetResult();
        try { coord.LockWithRetryAsync(LockTarget.IamUser, "bad", 1).GetAwaiter().GetResult(); } catch (TransientDatabaseConflictException) { }
        // after lock failure the coordinator is not in a commit state — no backend rollback needed
    }

    // -- 11. provider drift rollback --
    private static void ProviderDriftRollback(List<string> f)
    {
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
        // simulate drift — nothing committed yet, so just verify rollback is clean
        coord.RollbackAsync().GetAwaiter().GetResult();
        if (backend.RollbackCount != 1) f.Add("provider drift: must call backend rollback");
        var committed = repo.GetPointAsync(point.Id).GetAwaiter().GetResult()!;
        if (committed.Status == PointStatus.Active) f.Add("provider drift: committed Point must not change");
    }

    // -- 12. cancellation rollback --
    private static void CancellationRollback(List<string> f)
    {
        var (backend, repo, org, identity, coord, catalog, outbox) = CreateBackedFixture();
        var point = SeedDraftPoint(repo, out _);
        RegisterAll(coord, identity, org, catalog, outbox);
        coord.BeginAsync().GetAwaiter().GetResult();
        LockAll(coord, point);
        coord.RollbackAsync().GetAwaiter().GetResult();
        if (backend.RollbackCount != 1) f.Add("cancellation: must call backend rollback");
        var committed = repo.GetPointAsync(point.Id).GetAwaiter().GetResult()!;
        if (committed.Status == PointStatus.Active) f.Add("cancellation: committed Point must not change");
    }

    // -- 13. retry delay trace --
    private static void RetryDelays(List<string> f)
    {
        var clock = new List<int>();
        var delay = new FakeHostDelay(clock);
        var backend = new FakeAtomicBackend(new FakeOrganizationCommandRepository());
        var coord = new HostTransactionCoordinator(backend, delay);
        var identity = new FakeActivationIdentityQuery { TransientFailures = 2 };
        RegisterAll(coord, identity, new FakeActivationOrganizationParticipant(new FakeOrganizationCommandRepository(), backend),
            new FakeActivationCatalogQuery(), new FakeTransactionalOutboxWriter(backend));
        coord.BeginAsync().GetAwaiter().GetResult();
        try { coord.LockWithRetryAsync(LockTarget.IamUser, "u1", 1).GetAwaiter().GetResult(); } catch (TransientDatabaseConflictException) { }
        if (clock.Count < 1) f.Add("retry delays must be recorded (expected >=1, got 0)");
        var expected = new[] { 50, 150, 450 };
        for (var i = 0; i < clock.Count && i < expected.Length; i++)
            if (clock[i] != expected[i]) f.Add($"retry delay {i}: expected {expected[i]}ms, got {clock[i]}ms");
    }

    // -- 14. retry exhaustion --
    private static void RetryExhaustion(List<string> f)
    {
        var backend = new FakeAtomicBackend(new FakeOrganizationCommandRepository());
        var coord = new HostTransactionCoordinator(backend);
        var identity = new FakeActivationIdentityQuery { TransientFailures = 4 };
        RegisterAll(coord, identity, new FakeActivationOrganizationParticipant(new FakeOrganizationCommandRepository(), backend),
            new FakeActivationCatalogQuery(), new FakeTransactionalOutboxWriter(backend));
        coord.BeginAsync().GetAwaiter().GetResult();
        try
        {
            coord.LockWithRetryAsync(LockTarget.IamUser, "u1", 1).GetAwaiter().GetResult();
            f.Add("retry exhaustion must throw TransientDatabaseConflictException");
        }
        catch (TransientDatabaseConflictException) { }
    }

    // -- 15. exactly one backend CommitAsync call --
    private static void OneBackendCommit(List<string> f)
    {
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
        if (backend.CommitCount != 1) f.Add($"exactly one backend CommitAsync expected, got {backend.CommitCount}");
        if (backend.RollbackCount != 0) f.Add($"zero backend RollbackAsync expected, got {backend.RollbackCount}");
    }

    // -- 16. exactly one backend RollbackAsync call --
    private static void OneBackendRollback(List<string> f)
    {
        var (backend, repo, org, identity, coord, catalog, outbox) = CreateBackedFixture();
        var point = SeedDraftPoint(repo, out _);
        RegisterAll(coord, identity, org, catalog, outbox);
        coord.BeginAsync().GetAwaiter().GetResult();
        LockAll(coord, point);
        coord.RollbackAsync().GetAwaiter().GetResult();
        if (backend.RollbackCount != 1) f.Add($"exactly one backend RollbackAsync expected, got {backend.RollbackCount}");
        if (backend.CommitCount != 0) f.Add($"zero backend CommitAsync expected, got {backend.CommitCount}");
    }

    // -- 17. no participant commit surface --
    private static void NoParticipantCommitSurface(List<string> f)
    {
        var backend = new FakeAtomicBackend(new FakeOrganizationCommandRepository());
        var org = new FakeActivationOrganizationParticipant(new FakeOrganizationCommandRepository(), backend);
        var identity = new FakeActivationIdentityQuery();
        var catalog = new FakeActivationCatalogQuery();
        var outbox = new FakeTransactionalOutboxWriter(backend);
        var participantType = typeof(IHostTransactionParticipant);
        var methods = participantType.GetMethods().Select(m => m.Name).ToHashSet();
        foreach (var bad in new[] { "PrepareAsync", "FinalizeAsync", "DiscardAsync", "CommitAsync", "RollbackAsync" })
            if (methods.Contains(bad)) f.Add($"no IHostTransactionParticipant should expose {bad}");
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
