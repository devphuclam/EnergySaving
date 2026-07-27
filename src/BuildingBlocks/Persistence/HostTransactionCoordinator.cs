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

public interface IHostTransaction : IAsyncDisposable
{
    Guid TransactionId { get; }
    string IsolationIntent { get; }
    ValueTask CommitAsync(CancellationToken ct = default);
    ValueTask RollbackAsync(CancellationToken ct = default);
    IReadOnlyList<LockRequest> LockTrace { get; }
    bool IsCompleted { get; }
}

public interface IHostTransactionParticipant
{
    ValueTask AcquireLockAsync(IHostTransaction transaction, LockRequest request, CancellationToken ct = default);
    ValueTask CommitAsync(IHostTransaction transaction, CancellationToken ct = default);
    ValueTask RollbackAsync(IHostTransaction transaction, CancellationToken ct = default);
}

public sealed class HostTransactionCoordinator : IHostTransaction
{
    private readonly List<LockRequest> _lockTrace = new();
    private readonly Dictionary<LockTarget, IHostTransactionParticipant> _participants = new();
    private int _lastOrder;
    private bool _completed;
    private bool _disposed;

    public HostTransactionCoordinator()
    {
        foreach (var target in Enum.GetValues<LockTarget>()) _participants[target] = NoOpParticipant.Instance;
    }

    public Guid TransactionId { get; } = Guid.NewGuid();
    public string IsolationIntent => "REPEATABLE READ";
    public TimeSpan LockTimeout => TimeSpan.FromSeconds(2);
    public IReadOnlyList<LockRequest> LockTrace => _lockTrace.AsReadOnly();
    public bool IsCompleted => _completed;

    public ValueTask<IHostTransaction> BeginAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (_disposed) throw new ObjectDisposedException(nameof(HostTransactionCoordinator));
        return ValueTask.FromResult<IHostTransaction>(this);
    }

    public void RegisterParticipant(LockTarget target, IHostTransactionParticipant participant)
    {
        if (_completed) throw new InvalidOperationException("Transaction already completed.");
        _participants[target] = participant ?? throw new ArgumentNullException(nameof(participant));
    }

    public async ValueTask LockAsync(LockTarget target, string id, int expectedOrder, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (_disposed) throw new ObjectDisposedException(nameof(HostTransactionCoordinator));
        if (_completed) throw new InvalidOperationException("Transaction already completed.");
        if (expectedOrder <= _lastOrder) throw new InvalidOperationException("LOCK_ORDER_VIOLATION");
        if (!_participants.TryGetValue(target, out var participant)) throw new InvalidOperationException($"No participant registered for {target}.");
        var request = new LockRequest(target, id, expectedOrder);
        await participant.AcquireLockAsync(this, request, ct);
        _lockTrace.Add(request);
        _lastOrder = expectedOrder;
    }

    public async ValueTask LockWithRetryAsync(LockTarget target, string id, int expectedOrder, CancellationToken ct = default)
    {
        var backoffMilliseconds = new[] { 50, 150, 450 };
        for (var attempt = 0; attempt < backoffMilliseconds.Length; attempt++)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(LockTimeout);
                await LockAsync(target, id, expectedOrder, timeout.Token);
                return;
            }
            catch (TransientDatabaseConflictException) when (attempt < backoffMilliseconds.Length - 1)
            {
                await Task.Delay(backoffMilliseconds[attempt], ct);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested && attempt < backoffMilliseconds.Length - 1)
            {
                await Task.Delay(backoffMilliseconds[attempt], ct);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TransientDatabaseConflictException("TRANSIENT_DATABASE_CONFLICT");
            }
        }
        throw new TransientDatabaseConflictException("TRANSIENT_DATABASE_CONFLICT");
    }

    public async ValueTask CommitAsync(CancellationToken ct = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(HostTransactionCoordinator));
        if (_completed) return;
        try
        {
            foreach (var participant in OrderedParticipants()) await participant.CommitAsync(this, ct);
            _completed = true;
        }
        catch
        {
            await RollbackAsync(ct);
            throw;
        }
    }

    public async ValueTask RollbackAsync(CancellationToken ct = default)
    {
        if (_completed) return;
        var participants = OrderedParticipants().ToList();
        for (var index = participants.Count - 1; index >= 0; index--)
        {
            try { await participants[index].RollbackAsync(this, ct); }
            catch { /* rollback is best effort across all participants */ }
        }
        _completed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        if (!_completed) await RollbackAsync();
        _disposed = true;
    }

    private sealed class NoOpParticipant : IHostTransactionParticipant
    {
        public static readonly NoOpParticipant Instance = new();
        public ValueTask AcquireLockAsync(IHostTransaction transaction, LockRequest request, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask CommitAsync(IHostTransaction transaction, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask RollbackAsync(IHostTransaction transaction, CancellationToken ct = default) => ValueTask.CompletedTask;
    }

    private IEnumerable<IHostTransactionParticipant> OrderedParticipants()
    {
        var seen = new HashSet<IHostTransactionParticipant>();
        foreach (var target in _lockTrace.OrderBy(x => x.Order).Select(x => x.Target).Distinct())
        {
            var participant = _participants[target];
            if (seen.Add(participant)) yield return participant;
        }
    }
}
