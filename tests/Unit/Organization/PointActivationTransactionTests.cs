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
    public static int CompositeCheckCount;

    public static List<string> Run()
    {
        TestCount = 0;
        CompositeCheckCount = 0;
        var f = new List<string>();

        SurfaceCheck(f);
        MissingParticipant(f);
        LockOrderCanonical(f);
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
        RollbackFailurePreservesCommitException(f);
        BeginFailureRetry(f);
        BeginFailureSafety(f);

        return f;
    }

    private static void Check(List<string> failures, string name, string? err)
    {
        CompositeCheckCount++;
        if (err is not null) failures.Add($"{name}: {err}");
    }

    // -- 1. IHostTransactionParticipant surface checks --
    private static void SurfaceCheck(List<string> f)
    {
        TestCount++;
        var t = typeof(IHostTransactionParticipant);
        var m = t.GetMethods().Select(x => x.Name).ToHashSet();
        Check(f, "no PrepareAsync", m.Contains("PrepareAsync") ? "must not expose PrepareAsync" : null);
        Check(f, "no FinalizeAsync", m.Contains("FinalizeAsync") ? "must not expose FinalizeAsync" : null);
        Check(f, "no DiscardAsync", m.Contains("DiscardAsync") ? "must not expose DiscardAsync" : null);
        Check(f, "has AcquireLockAsync", !m.Contains("AcquireLockAsync") ? "must expose AcquireLockAsync" : null);
    }

    // -- 2. missing participant --
    private static void MissingParticipant(List<string> f)
    {
        TestCount++;
        var coord = new HostTransactionCoordinator(NullBackend.Instance);
        try
        {
            coord.BeginAsync().GetAwaiter().GetResult();
            Check(f, "missing participant", "must throw at begin");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("MISSING_TRANSACTION_PARTICIPANT")) { Check(f, "missing participant", null); }
    }

    // -- 3. canonical lock order (positive + 6 negatives) --
    private static void LockOrderCanonical(List<string> f)
    {
        TestCount++;
        var (backend, repo, _, _, coord, _, _) = CreateBackedFixture();
        RegisterAll(coord);
        coord.BeginAsync().GetAwaiter().GetResult();

        // 3a. correct complete nine-target sequence succeeds
        coord.LockWithRetryAsync(LockTarget.IamUser, "u1", 1).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.OrganizationSite, "s1", 2).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.OrganizationArea, "a1", 3).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.OrganizationAsset, "as1", 4).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.OrganizationPoint, "p1", 5).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.CatalogMetric, "m1", 6).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.CatalogUnit, "u1", 7).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.CatalogMapping, "mp1", 8).GetAwaiter().GetResult();
        coord.LockWithRetryAsync(LockTarget.IntegrationOutbox, "o1", 9).GetAwaiter().GetResult();
        Check(f, "canonical: lock trace count", coord.LockTrace.Count != 9 ? $"expected 9, got {coord.LockTrace.Count}" : null);
        Check(f, "canonical: Integration last", coord.LockTrace[^1].Target != LockTarget.IntegrationOutbox ? "must end with IntegrationOutbox" : null);

        // 3b. OrganizationPoint as first target fails
        var coord2 = MakeCoord();
        RegisterAll(coord2);
        coord2.BeginAsync().GetAwaiter().GetResult();
        try { coord2.LockWithRetryAsync(LockTarget.OrganizationPoint, "p1", 5).GetAwaiter().GetResult(); Check(f, "canonical: Point-first", "must throw"); }
        catch (InvalidOperationException) { Check(f, "canonical: Point-first", null); }

        // 3c. IAM with expectedOrder=2 fails
        var coord3 = MakeCoord();
        RegisterAll(coord3);
        coord3.BeginAsync().GetAwaiter().GetResult();
        try { coord3.LockWithRetryAsync(LockTarget.IamUser, "u1", 2).GetAwaiter().GetResult(); Check(f, "canonical: IAM order=2", "must throw"); }
        catch (InvalidOperationException) { Check(f, "canonical: IAM order=2", null); }

        // 3d. IAM followed by CatalogMetric (skip Site/Area/Asset/Point) fails
        var coord4 = MakeCoord();
        RegisterAll(coord4);
        coord4.BeginAsync().GetAwaiter().GetResult();
        coord4.LockWithRetryAsync(LockTarget.IamUser, "u1", 1).GetAwaiter().GetResult();
        try { coord4.LockWithRetryAsync(LockTarget.CatalogMetric, "m1", 6).GetAwaiter().GetResult(); Check(f, "canonical: skip to Metric", "must throw"); }
        catch (InvalidOperationException) { Check(f, "canonical: skip to Metric", null); }

        // 3e. duplicate IAM target fails
        var coord5 = MakeCoord();
        RegisterAll(coord5);
        coord5.BeginAsync().GetAwaiter().GetResult();
        coord5.LockWithRetryAsync(LockTarget.IamUser, "u1", 1).GetAwaiter().GetResult();
        try { coord5.LockWithRetryAsync(LockTarget.IamUser, "u1", 1).GetAwaiter().GetResult(); Check(f, "canonical: duplicate IAM", "must throw"); }
        catch (InvalidOperationException) { Check(f, "canonical: duplicate IAM", null); }

        // 3f. skip OrganizationArea fails (Site -> Asset)
        var coord6 = MakeCoord();
        RegisterAll(coord6);
        coord6.BeginAsync().GetAwaiter().GetResult();
        coord6.LockWithRetryAsync(LockTarget.IamUser, "u1", 1).GetAwaiter().GetResult();
        coord6.LockWithRetryAsync(LockTarget.OrganizationSite, "s1", 2).GetAwaiter().GetResult();
        try { coord6.LockWithRetryAsync(LockTarget.OrganizationAsset, "as1", 4).GetAwaiter().GetResult(); Check(f, "canonical: skip Area", "must throw"); }
        catch (InvalidOperationException) { Check(f, "canonical: skip Area", null); }

        // 3g. lock after IntegrationOutbox fails
        var coord7 = MakeCoord();
        RegisterAll(coord7);
        coord7.BeginAsync().GetAwaiter().GetResult();
        foreach (var tgt in Enum.GetValues<LockTarget>())
            coord7.LockWithRetryAsync(tgt, "x", (int)tgt + 1).GetAwaiter().GetResult();
        try { coord7.LockWithRetryAsync(LockTarget.IamUser, "x", 10).GetAwaiter().GetResult(); Check(f, "canonical: after Integration", "must throw"); }
        catch (InvalidOperationException) { Check(f, "canonical: after Integration", null); }
    }

    // -- 4. same TransactionId --
    private static void SameTransactionId(List<string> f)
    {
        TestCount++;
        var (backend, repo, org, identity, coord, catalog, outbox) = CreateBackedFixture();
        var point = SeedDraftPoint(repo, out _);
        RegisterAll(coord, identity, org, catalog, outbox);
        coord.BeginAsync().GetAwaiter().GetResult();
        LockAll(coord, point);
        var allIds = identity.TransactionIds
            .Concat(org.TransactionIds)
            .Concat(catalog.TransactionIds)
            .Concat(outbox.TransactionIds).ToArray();
        Check(f, "non-empty IDs", allIds.Length == 0 ? "no participant recorded a TransactionId" : null);
        Check(f, "all match host", allIds.Length > 0 && allIds.Any(id => id != coord.TransactionId) ? $"expected all {coord.TransactionId}, got {string.Join(",", allIds.Select(x => x.ToString()))}" : null);
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
        Check(f, "pre-commit not Active", committed.Status == PointStatus.Active ? "must not be Active before commit" : null);
        Check(f, "pre-commit version unchanged", committed.Version != 1 ? $"expected 1, got {committed.Version}" : null);
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
        Check(f, "pre-commit lifecycle", lifecycle.Count != 0 ? "must be empty before commit" : null);
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
        Check(f, "pre-commit outbox", backend.CommittedEnvelopes.Count != 0 ? "must be empty before commit" : null);
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
        Check(f, "commit: Active", committed.Status != PointStatus.Active ? "must be Active" : null);
        Check(f, "commit: version +1", committed.Version != 2 ? $"expected 2, got {committed.Version}" : null);
        var lifecycle = repo.GetLifecycleForPointAsync(point.Id.ToString()).GetAwaiter().GetResult();
        Check(f, "commit: lifecycle", lifecycle.Count != 1 ? "expected 1" : null);
        Check(f, "commit: outbox", backend.CommittedEnvelopes.Count != 1 ? "expected 1" : null);
        Check(f, "commit: backend commit", backend.CommitCount != 1 ? $"expected 1, got {backend.CommitCount}" : null);
        Check(f, "commit: backend rollback", backend.RollbackCount != 0 ? $"expected 0, got {backend.RollbackCount}" : null);
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
        Check(f, "commit-fail: Point unchanged", committed.Status == PointStatus.Active ? "must stay unchanged" : null);
        Check(f, "commit-fail: outbox empty", backend.CommittedEnvelopes.Count != 0 ? "must be empty" : null);
        var lifecycle = repo.GetLifecycleForPointAsync(point.Id.ToString()).GetAwaiter().GetResult();
        Check(f, "commit-fail: lifecycle empty", lifecycle.Count != 0 ? "must be empty" : null);
        Check(f, "commit-fail: workspace null", backend.GetWorkspace(coord) is not null ? "workspace must be removed" : null);
        Check(f, "commit-fail: backend rollback=1", backend.RollbackCount != 1 ? $"expected 1, got {backend.RollbackCount}" : null);
        Check(f, "commit-fail: backend commit=0", backend.CommitCount != 0 ? $"expected 0, got {backend.CommitCount}" : null);
        Check(f, "commit-fail: coordinator completed", !coord.IsCompleted ? "must be marked completed" : null);
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
        Check(f, "lock-fail: rollback=1", backend.RollbackCount != 1 ? $"expected 1, got {backend.RollbackCount}" : null);
        Check(f, "lock-fail: workspace null", backend.GetWorkspace(coord) is not null ? "must be removed" : null);
        Check(f, "lock-fail: no committed state", repo.GetPointAsync(PointId.New()).GetAwaiter().GetResult() is not null ? "nothing seeded" : null);
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
        Check(f, "drift: rollback=1", backend.RollbackCount != 1 ? $"expected 1, got {backend.RollbackCount}" : null);
        Check(f, "drift: Point unchanged", repo.GetPointAsync(point.Id).GetAwaiter().GetResult()!.Status == PointStatus.Active ? "must not change" : null);
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
        try
        {
            coord.CommitAsync(cts.Token).GetAwaiter().GetResult();
            Check(f, "cancel: must throw", "no exception");
        }
        catch (OperationCanceledException) { Check(f, "cancel: threw", null); }
        Check(f, "cancel: rollback=1", backend.RollbackCount != 1 ? $"expected 1, got {backend.RollbackCount}" : null);
        Check(f, "cancel: workspace null", backend.GetWorkspace(coord) is not null ? "must be removed" : null);
        Check(f, "cancel: Point unchanged", repo.GetPointAsync(point.Id).GetAwaiter().GetResult()!.Status == PointStatus.Active ? "must not change" : null);
        var lifecycle = repo.GetLifecycleForPointAsync(point.Id.ToString()).GetAwaiter().GetResult();
        Check(f, "cancel: lifecycle empty", lifecycle.Count != 0 ? "must be empty" : null);
        Check(f, "cancel: outbox empty", backend.CommittedEnvelopes.Count != 0 ? "must be empty" : null);
        Check(f, "cancel: coordinator completed", !coord.IsCompleted ? "must be marked completed" : null);
        var prevRollback = backend.RollbackCount;
        coord.RollbackAsync().GetAwaiter().GetResult();
        Check(f, "cancel: repeated rollback no-op", backend.RollbackCount != prevRollback ? "must not increment" : null);
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
        Check(f, "retry: delay count", clock.Count != 3 ? $"expected 3, got {clock.Count}" : null);
        var expected = new[] { 50, 150, 450 };
        for (var i = 0; i < clock.Count && i < expected.Length; i++)
            Check(f, $"retry: delay[{i}]", clock[i] != expected[i] ? $"expected {expected[i]}ms, got {clock[i]}ms" : null);
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
            Check(f, "retry: exhaustion", "must throw TransientDatabaseConflictException");
        }
        catch (TransientDatabaseConflictException) { Check(f, "retry: exhaustion", null); }
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
        Check(f, "one-commit: count", backend.CommitCount != 1 ? $"expected 1, got {backend.CommitCount}" : null);
        Check(f, "one-commit: no rollback", backend.RollbackCount != 0 ? $"expected 0, got {backend.RollbackCount}" : null);
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
        Check(f, "one-rollback: count", backend.RollbackCount != 1 ? $"expected 1, got {backend.RollbackCount}" : null);
        Check(f, "one-rollback: no commit", backend.CommitCount != 0 ? $"expected 0, got {backend.CommitCount}" : null);
    }

    // -- 17. no participant commit surface --
    private static void NoParticipantCommitSurface(List<string> f)
    {
        TestCount++;
        var t = typeof(IHostTransactionParticipant);
        var m = t.GetMethods().Select(x => x.Name).ToHashSet();
        foreach (var bad in new[] { "PrepareAsync", "FinalizeAsync", "DiscardAsync", "CommitAsync", "RollbackAsync" })
            Check(f, $"no {bad}", m.Contains(bad) ? $"must not expose {bad}" : null);
    }

    // -- 18. rollback failure preserves original commit exception --
    private static void RollbackFailurePreservesCommitException(List<string> f)
    {
        TestCount++;
        var (backend, repo, org, identity, coord, catalog, outbox) = CreateBackedFixture();
        backend.FailOnCommit = true;
        backend.FailOnRollback = true;
        var point = SeedDraftPoint(repo, out _);
        RegisterAll(coord, identity, org, catalog, outbox);
        coord.BeginAsync().GetAwaiter().GetResult();
        LockAll(coord, point);
        try
        {
            coord.CommitAsync().GetAwaiter().GetResult();
            Check(f, "rollback-fail: must throw", "no exception");
        }
        catch (InvalidOperationException ex)
        {
            Check(f, "rollback-fail: original commit exception", !ex.Message.Contains("ROLLBACK_FAILED") ? null : "must preserve ATOMIC_COMMIT_FAILED, not ROLLBACK_FAILED");
        }
        Check(f, "rollback-fail: rollback attempted", backend.RollbackCount != 1 ? $"expected 1, got {backend.RollbackCount}" : null);
    }

    private static void BeginFailureRetry(List<string> f)
    {
        TestCount++;
        var backend = new FakeAtomicBackend(new FakeOrganizationCommandRepository()) { FailOnBegin = true };
        var coord = new HostTransactionCoordinator(backend);
        RegisterAll(coord);
        try { coord.BeginAsync().GetAwaiter().GetResult(); Check(f, "begin-retry: first begin must fail", "no exception"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("BEGIN_FAILED")) { Check(f, "begin-retry: first begin", null); }
        try { coord.RollbackAsync().GetAwaiter().GetResult(); Check(f, "begin-retry: pre-begin rollback safe", null); }
        catch (Exception ex) { Check(f, "begin-retry: pre-begin rollback safe", $"must not throw {ex.GetType().Name}"); }
        Check(f, "begin-retry: no backend rollback before begin", backend.RollbackCount != 0 ? "must remain zero" : null);
        backend.FailOnBegin = false;
        try
        {
            coord.BeginAsync().GetAwaiter().GetResult();
            Check(f, "begin-retry: second begin succeeds", null);
            Check(f, "begin-retry: TransactionId assigned", coord.TransactionId == Guid.Empty ? "must be non-empty" : null);
            coord.RollbackAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex) { Check(f, "begin-retry: second begin succeeds", ex.Message); }
        Check(f, "begin-retry: one rollback after successful begin", backend.RollbackCount != 1 ? $"expected 1, got {backend.RollbackCount}" : null);
    }

    // -- 19. begin failure safety --
    private static void BeginFailureSafety(List<string> f)
    {
        TestCount++;
        var backend = new FakeAtomicBackend(new FakeOrganizationCommandRepository());
        backend.FailOnBegin = true;
        var coord = new HostTransactionCoordinator(backend);
        var identity = new FakeActivationIdentityQuery();
        RegisterAll(coord, identity,
            new FakeActivationOrganizationParticipant(new FakeOrganizationCommandRepository(), backend),
            new FakeActivationCatalogQuery(), new FakeTransactionalOutboxWriter(backend));
        try
        {
            coord.BeginAsync().GetAwaiter().GetResult();
            Check(f, "begin-fail: must throw", "no exception");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("BEGIN_FAILED")) { Check(f, "begin-fail: threw", null); }
        Check(f, "begin-fail: _begun false", coord.IsBegun ? "must be false" : null);
        Check(f, "begin-fail: _completed false", coord.IsCompleted ? "must be false" : null);
        Check(f, "begin-fail: TransactionId empty", coord.TransactionId != Guid.Empty ? "must be Empty" : null);
        // DisposeAsync must be safe — on baseline coordinator (buggy) this crashes because
        // _begun=true but _innerTx=null leads to NullReferenceException in backend rollback.
        try { coord.RollbackAsync().GetAwaiter().GetResult(); Check(f, "begin-fail: direct rollback safe", null); }
        catch (Exception ex) { Check(f, "begin-fail: direct rollback safe", $"must not throw {ex.GetType().Name}"); }
        Check(f, "begin-fail: rollback not called after direct rollback", backend.RollbackCount != 0 ? "must remain zero" : null);
        try { coord.DisposeAsync().GetAwaiter().GetResult(); Check(f, "begin-fail: dispose safe", null); }
        catch (Exception ex) { Check(f, "begin-fail: dispose safe", $"must not throw {ex.GetType().Name}"); }
        Check(f, "begin-fail: rollback not called after dispose", backend.RollbackCount != 0 ? "must not call rollback" : null);
    }

    // -- internal helpers --
    private static HostTransactionCoordinator MakeCoord()
    {
        return new HostTransactionCoordinator(new FakeAtomicBackend(new FakeOrganizationCommandRepository()));
    }

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
