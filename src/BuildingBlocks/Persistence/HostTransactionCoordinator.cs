namespace IUMP.BuildingBlocks.Persistence;

public enum LockTarget
{
    IamUser,
    OrganizationSite,
    OrganizationArea,
    OrganizationAsset,
    OrganizationPoint,
    CatalogMetric,
    CatalogUnit,
    CatalogMapping,
    IntegrationOutbox
}

public sealed record LockRequest(LockTarget Target, string Id, int Order);

public sealed class TransientDatabaseConflictException : InvalidOperationException
{
    public TransientDatabaseConflictException(string message, Exception? inner = null) : base(message, inner) { }
}

public interface IHostDelay
{
    Task DelayAsync(int milliseconds, CancellationToken ct = default);
}

public sealed class RealHostDelay : IHostDelay
{
    public Task DelayAsync(int milliseconds, CancellationToken ct = default) => Task.Delay(milliseconds, ct);
}

public sealed class HostTransactionCoordinator : IHostTransaction
{
    public static IReadOnlyList<LockTarget> RequiredTargets { get; } = Enum.GetValues<LockTarget>();

    private readonly IHostTransactionBackend _backend;
    private readonly Dictionary<LockTarget, IHostTransactionParticipant> _participants = new();
    private readonly List<LockRequest> _lockTrace = new();
    private readonly IHostDelay _delay;
    private IHostTransaction? _innerTx;
    private int _lastOrder;
    private bool _begun;
    private bool _completed;
    private bool _disposed;

    public HostTransactionCoordinator(IHostTransactionBackend backend, IHostDelay? delay = null)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _delay = delay ?? new RealHostDelay();
    }

    public Guid TransactionId => _innerTx?.TransactionId ?? Guid.Empty;
    public string IsolationIntent => "REPEATABLE READ";
    public TimeSpan LockTimeout => TimeSpan.FromSeconds(2);
    public IReadOnlyList<LockRequest> LockTrace => _lockTrace.AsReadOnly();
    public bool IsCompleted => _completed;
    public bool IsBegun => _begun;

    public void RegisterParticipant(LockTarget target, IHostTransactionParticipant participant)
    {
        if (_begun) throw new InvalidOperationException("PARTICIPANTS_MUST_BE_REGISTERED_BEFORE_BEGIN");
        _participants[target] = participant ?? throw new ArgumentNullException(nameof(participant));
    }

    public async ValueTask<IHostTransaction> BeginAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (_disposed) throw new ObjectDisposedException(nameof(HostTransactionCoordinator));
        if (_begun) throw new InvalidOperationException("TRANSACTION_ALREADY_BEGUN");
        var missing = RequiredTargets.Where(target => !_participants.ContainsKey(target)).ToArray();
        if (missing.Length > 0) throw new InvalidOperationException($"MISSING_TRANSACTION_PARTICIPANT:{string.Join(',', missing)}");
        var tx = await _backend.BeginAsync(ct);
        _innerTx = tx;
        _begun = true;
        return this;
    }

    public async ValueTask LockAsync(LockTarget target, string id, int expectedOrder, CancellationToken ct = default)
    {
        EnsureBegun();
        ct.ThrowIfCancellationRequested();
        var canonicalIndex = (int)target + 1;
        if (canonicalIndex != expectedOrder) throw new InvalidOperationException("LOCK_ORDER_VIOLATION");
        if (canonicalIndex != _lastOrder + 1) throw new InvalidOperationException("LOCK_ORDER_VIOLATION");
        if (_lockTrace.Any(l => l.Target == target)) throw new InvalidOperationException("LOCK_ORDER_VIOLATION");
        var participant = _participants[target];
        var request = new LockRequest(target, id, expectedOrder);
        await participant.AcquireLockAsync(this, request, ct);
        _lockTrace.Add(request);
        _lastOrder = expectedOrder;
    }

    public async ValueTask LockWithRetryAsync(LockTarget target, string id, int expectedOrder, CancellationToken ct = default)
    {
        var delays = new[] { 50, 150, 450 };
        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(LockTimeout);
                await LockAsync(target, id, expectedOrder, timeout.Token);
                return;
            }
            catch (TransientDatabaseConflictException) when (attempt < 3) { await _delay.DelayAsync(delays[attempt], ct); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested && attempt < 3) { await _delay.DelayAsync(delays[attempt], ct); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new TransientDatabaseConflictException("TRANSIENT_DATABASE_CONFLICT"); }
        }
        throw new TransientDatabaseConflictException("TRANSIENT_DATABASE_CONFLICT");
    }

    public async ValueTask CommitAsync(CancellationToken ct = default)
    {
        EnsureBegun();
        if (_completed) return;
        try
        {
            await _backend.CommitAsync(_innerTx!, ct);
            _completed = true;
        }
        catch
        {
            try { await _backend.RollbackAsync(_innerTx!, CancellationToken.None); }
            catch { /* swallow rollback failure — preserve original commit exception */ }
            _completed = true;
            throw;
        }
    }

    public async ValueTask RollbackAsync(CancellationToken ct = default)
    {
        if (_completed) return;
        try
        {
            await _backend.RollbackAsync(_innerTx!, ct);
        }
        finally
        {
            _completed = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        if (!_completed && _begun) await RollbackAsync();
        _disposed = true;
        if (_innerTx is not null) await _innerTx.DisposeAsync();
    }

    private void EnsureBegun()
    {
        if (!_begun) throw new InvalidOperationException("TRANSACTION_NOT_BEGUN");
        if (_disposed) throw new ObjectDisposedException(nameof(HostTransactionCoordinator));
        if (_completed) throw new InvalidOperationException("TRANSACTION_COMPLETED");
    }

    public IReadOnlyList<LockTarget> RegisteredTargets => _participants.Keys.OrderBy(x => (int)x).ToList();

    public bool HasParticipant(LockTarget target) => _participants.ContainsKey(target);
}
