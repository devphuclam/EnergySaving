using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Integration.Contracts;

namespace IUMP.Tests.Unit.Organization;

public static class PointActivationTransactionTests
{
    public const int CaseCount = 12;
    public static List<string> Run()
    {
        var f = new List<string>();
        Case(f, "missing participant", MissingParticipantFails);
        Case(f, "exact invocation and shared identity", ExactOrderAndIdentity);
        Case(f, "registration closes at begin", RegisterAfterBeginFails);
        Case(f, "participant commit surface absent", IndependentCommitSurfaceAbsent);
        Case(f, "lock order enforced", LockOrderEnforced);
        Case(f, "single host prepare", SingleHostCommit);
        Case(f, "shared participant prepared once", SharedParticipantPreparedOnce);
        Case(f, "rollback reverse and prep failure", RollbackAtomic);
        Case(f, "finalization failure rollback", FinalizationFailureRollsBack);
        Case(f, "lock failure rollback", LockFailureRollsBack);
        Case(f, "cancellation", CancellationRollsBack);
        Case(f, "retry trace", RetryTrace);
        return f;
    }
    private static void Case(List<string> f, string n, Func<string?> t) { try { if (t() is { } e) f.Add($"{n}: {e}"); } catch (Exception e) { f.Add($"{n}: {e.Message}"); } }

    private static string? MissingParticipantFails()
    {
        var tx = new HostTransactionCoordinator();
        foreach (var target in HostTransactionCoordinator.RequiredTargets.Where(x => x != LockTarget.CatalogUnit)) tx.RegisterParticipant(target, new RecordingParticipant());
        try { tx.BeginAsync().GetAwaiter().GetResult(); return "BeginAsync must fail closed when a required participant is missing."; }
        catch (InvalidOperationException e) when (e.Message.Contains("MISSING_TRANSACTION_PARTICIPANT")) { return null; }
    }

    private static string? ExactOrderAndIdentity()
    {
        var tx = new HostTransactionCoordinator(); var participants = RegisterAll(tx); tx.BeginAsync().GetAwaiter().GetResult();
        for (var i = 0; i < HostTransactionCoordinator.RequiredTargets.Count; i++) tx.LockAsync(HostTransactionCoordinator.RequiredTargets[i], $"id-{i}", i + 1).GetAwaiter().GetResult();
        if (tx.LockTrace.Select(x => x.Target).SequenceEqual(HostTransactionCoordinator.RequiredTargets) == false) return "lock order mismatch.";
        if (participants.Any(p => p.TransactionIds.Any(id => id != tx.TransactionId))) return "all participants must receive the same TransactionId.";
        tx.RollbackAsync().GetAwaiter().GetResult(); return null;
    }

    private static string? RegisterAfterBeginFails()
    {
        var tx = new HostTransactionCoordinator(); RegisterAll(tx); tx.BeginAsync().GetAwaiter().GetResult();
        try { tx.RegisterParticipant(LockTarget.IamUser, new RecordingParticipant()); return "registration after BeginAsync must fail."; }
        catch (InvalidOperationException e) when (e.Message.Contains("BEFORE_BEGIN", StringComparison.Ordinal)) { return null; }
    }

    private static string? IndependentCommitSurfaceAbsent() =>
        typeof(IHostTransactionParticipant).GetMethods().Any(m => m.Name is "CommitAsync" or "RollbackAsync")
            ? "participants must not expose independent commit or rollback methods." : null;

    private static string? LockOrderEnforced()
    {
        var tx = new HostTransactionCoordinator(); RegisterAll(tx); tx.BeginAsync().GetAwaiter().GetResult();
        tx.LockAsync(LockTarget.OrganizationPoint, "point", 5).GetAwaiter().GetResult();
        try { tx.LockAsync(LockTarget.OrganizationArea, "area", 3).GetAwaiter().GetResult(); return "out-of-order lock must fail."; }
        catch (InvalidOperationException e) when (e.Message.Contains("LOCK_ORDER_VIOLATION", StringComparison.Ordinal)) { tx.RollbackAsync().GetAwaiter().GetResult(); return null; }
    }

    private static string? SingleHostCommit()
    {
        var tx = new HostTransactionCoordinator(); var participants = RegisterAll(tx); tx.BeginAsync().GetAwaiter().GetResult();
        tx.LockAsync(LockTarget.OrganizationPoint, "point", 5).GetAwaiter().GetResult(); tx.LockAsync(LockTarget.IntegrationOutbox, "outbox", 9).GetAwaiter().GetResult(); tx.CommitAsync().GetAwaiter().GetResult();
        return participants.Sum(p => p.PrepareCount) == 2 && participants.All(p => p.DiscardCount == 0) && tx.IsCompleted ? null : "host must prepare staged work once and own completion.";
    }

    private static string? SharedParticipantPreparedOnce()
    {
        var tx = new HostTransactionCoordinator(); var participant = new RecordingParticipant(); RegisterAll(tx, participant); tx.BeginAsync().GetAwaiter().GetResult();
        for (var i = 0; i < HostTransactionCoordinator.RequiredTargets.Count; i++) tx.LockAsync(HostTransactionCoordinator.RequiredTargets[i], $"id-{i}", i + 1).GetAwaiter().GetResult();
        tx.CommitAsync().GetAwaiter().GetResult();
        return participant.PrepareCount == 1 ? null : "one participant registered for several targets must prepare once.";
    }

    private static string? RollbackAtomic()
    {
        var tx = new HostTransactionCoordinator(); var participants = RegisterAll(tx); participants[^1].FailOnPrepare = true; tx.BeginAsync().GetAwaiter().GetResult();
        tx.LockAsync(LockTarget.OrganizationPoint, "point", 5).GetAwaiter().GetResult(); tx.LockAsync(LockTarget.IntegrationOutbox, "outbox", 9).GetAwaiter().GetResult();
        try { tx.CommitAsync().GetAwaiter().GetResult(); return "integration preparation failure must fail commit."; } catch { }
        return participants.Where(p => p.PrepareCount > 0).All(p => p.DiscardCount > 0) && tx.IsCompleted ? null : "one rollback must discard every staged participant.";
    }

    private static string? FinalizationFailureRollsBack()
    {
        var tx = new HostTransactionCoordinator(); var organization = new RecordingParticipant(); var integration = new RecordingParticipant { FailOnFinalize = true };
        tx.RegisterParticipant(LockTarget.OrganizationPoint, organization); tx.RegisterParticipant(LockTarget.IntegrationOutbox, integration);
        foreach (var target in new[] { LockTarget.IamUser, LockTarget.OrganizationSite, LockTarget.OrganizationArea, LockTarget.OrganizationAsset, LockTarget.CatalogMetric, LockTarget.CatalogUnit, LockTarget.CatalogMapping }) tx.RegisterParticipant(target, new RecordingParticipant());
        tx.BeginAsync().GetAwaiter().GetResult(); tx.LockAsync(LockTarget.OrganizationPoint, "point", 5).GetAwaiter().GetResult(); tx.LockAsync(LockTarget.IntegrationOutbox, "outbox", 9).GetAwaiter().GetResult();
        try { tx.CommitAsync().GetAwaiter().GetResult(); return "integration finalization failure must throw."; } catch { }
        return organization.DiscardCount > 0 && integration.DiscardCount > 0 && tx.IsCompleted ? null : "finalization failure must roll back every participant.";
    }

    private static string? LockFailureRollsBack()
    {
        var tx = new HostTransactionCoordinator(); var failing = new RecordingParticipant { FailOnAcquire = true }; RegisterAll(tx, failing); tx.BeginAsync().GetAwaiter().GetResult();
        try { tx.LockAsync(LockTarget.IamUser, "owner", 1).GetAwaiter().GetResult(); return "lock failure must throw."; }
        catch (InvalidOperationException e) when (e.Message.Contains("LOCK_FAILED", StringComparison.Ordinal)) { tx.RollbackAsync().GetAwaiter().GetResult(); return tx.IsCompleted ? null : "lock failure must complete rollback."; }
    }

    private static string? CancellationRollsBack()
    {
        var tx = new HostTransactionCoordinator(); RegisterAll(tx); tx.BeginAsync().GetAwaiter().GetResult(); using var cts = new CancellationTokenSource(); cts.Cancel();
        try { tx.LockAsync(LockTarget.IamUser, "owner", 1, cts.Token).GetAwaiter().GetResult(); return "cancelled lock must throw."; }
        catch (OperationCanceledException) { tx.RollbackAsync().GetAwaiter().GetResult(); return tx.IsCompleted ? null : "rollback must complete."; }
    }

    private static string? RetryTrace()
    {
        var clock = new FakeHostDelay(); var tx = new HostTransactionCoordinator(clock); var p = new RecordingParticipant { TransientFailures = 4 }; RegisterAll(tx, p); tx.BeginAsync().GetAwaiter().GetResult();
        try { tx.LockWithRetryAsync(LockTarget.IamUser, "owner", 1).GetAwaiter().GetResult(); return "fourth acquisition should exhaust retries."; } catch (TransientDatabaseConflictException) { }
        return p.AcquireCount == 4 && clock.Delays.SequenceEqual(new[] { 50, 150, 450 }) ? null : "retry must use 50/150/450ms after three failures.";
    }

    private static List<RecordingParticipant> RegisterAll(HostTransactionCoordinator tx, RecordingParticipant? shared = null)
    {
        var list = new List<RecordingParticipant>(); foreach (var target in HostTransactionCoordinator.RequiredTargets) { var p = shared ?? new RecordingParticipant(); tx.RegisterParticipant(target, p); if (!list.Contains(p)) list.Add(p); } return list;
    }

    private sealed class FakeHostDelay : IHostDelay { public List<int> Delays { get; } = new(); public Task DelayAsync(int milliseconds, CancellationToken ct = default) { Delays.Add(milliseconds); return Task.CompletedTask; } }
    private sealed class RecordingParticipant : IHostTransactionParticipant
    {
        public int TransientFailures { get; set; } public int AcquireCount { get; private set; } public int PrepareCount { get; private set; } public int DiscardCount { get; private set; } public List<Guid> TransactionIds { get; } = new(); public bool FailOnPrepare { get; set; } public bool FailOnAcquire { get; set; } public bool FailOnFinalize { get; set; }
        public ValueTask AcquireLockAsync(IHostTransaction transaction, LockRequest request, CancellationToken ct = default) { TransactionIds.Add(transaction.TransactionId); AcquireCount++; if (FailOnAcquire) throw new InvalidOperationException("LOCK_FAILED"); if (TransientFailures-- > 0) throw new TransientDatabaseConflictException("conflict"); return ValueTask.CompletedTask; }
        public ValueTask PrepareAsync(IHostTransaction transaction, CancellationToken ct = default) { PrepareCount++; if (FailOnPrepare) throw new InvalidOperationException("PREPARE_FAILED"); return ValueTask.CompletedTask; }
        public ValueTask FinalizeAsync(IHostTransaction transaction, CancellationToken ct = default) { if (FailOnFinalize) throw new InvalidOperationException("FINALIZE_FAILED"); return ValueTask.CompletedTask; }
        public ValueTask DiscardAsync(IHostTransaction transaction, CancellationToken ct = default) { DiscardCount++; return ValueTask.CompletedTask; }
    }
}
