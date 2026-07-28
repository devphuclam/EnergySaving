using IUMP.Modules.Telemetry.Contracts;
using IUMP.Modules.Telemetry.Domain;
using IUMP.Tests.Integration.Telemetry;

namespace IUMP.Tests.Unit.Fakes;

public enum TelemetryFakeFailure
{
    None,
    OrganizationLock,
    CatalogLock,
    TerminalInsert,
    RawInsert,
    Latest,
    Outbox,
    Commit
}

public sealed class FakeTelemetryRepositories :
    ITelemetryIngestionRepository,
    ILatestProjectionRepository,
    ISourceHealthRepository,
    ITelemetryQueryRepository,
    IMeasurementAcceptedEventWriter,
    ITelemetryFlowUnitOfWork
{
    private readonly Dictionary<Guid, TelemetryTerminalResult> _terminals = [];
    private readonly Dictionary<Guid, RawMeasurement> _raw = [];
    private readonly List<TelemetryOwnerEvent> _events = [];
    private readonly Dictionary<Guid, LatestProjectionCandidate> _latest = [];

    public TelemetryFakeFailure Failure { get; set; }
    public bool LatestAdvanceResult { get; set; } = true;
    public (TelemetryTerminalResult Winner, double NumericValue, string UnitCode)? RaceWinnerOnStage { get; set; }
    public IReadOnlyList<TelemetryFlowLock> LastLockTrace { get; private set; } = [];

    public ValueTask<ITelemetryFlowTransaction> BeginRepeatableReadAsync(
        CancellationToken ct = default) =>
        ValueTask.FromResult<ITelemetryFlowTransaction>(new Transaction(this));

    public Task<TelemetryTerminalResult?> GetTerminalAsync(
        Guid measurementId, CancellationToken ct = default) =>
        Task.FromResult(_terminals.TryGetValue(measurementId, out var value) ? value.Copy() : null);

    public Task<TelemetryTerminalResult?> GetTerminalBySlotAsync(
        Guid runId, Guid pointId, long sourceSequence, CancellationToken ct = default)
    {
        var value = _terminals.Values.FirstOrDefault(candidate =>
            candidate.SimulatorRunId == runId && candidate.PointId == pointId &&
            candidate.SourceSequence == sourceSequence);
        return Task.FromResult(value?.Copy());
    }

    public Task<TelemetryTerminalResult?> RecheckTerminalAsync(
        Guid measurementId, ITelemetryFlowTransaction transaction,
        CancellationToken ct = default) => GetTerminalAsync(measurementId, ct);

    public Task StageTerminalAsync(TelemetryTerminalResult result,
        ITelemetryFlowTransaction transaction, CancellationToken ct = default)
    {
        var tx = Require(transaction);
        ThrowIf(TelemetryFakeFailure.TerminalInsert);
        TelemetryTerminalResultValidator.EnsureValid(result);
        if (RaceWinnerOnStage is { } w)
        {
            PublishRaceWinner(w.Winner, w.NumericValue, w.UnitCode);
            RaceWinnerOnStage = null;
            throw new TelemetryUniqueRaceException();
        }
        if (_terminals.ContainsKey(result.MeasurementId) ||
            tx.Terminals.Any(item => item.MeasurementId == result.MeasurementId) ||
            _terminals.Values.Any(item =>
                item.SimulatorRunId == result.SimulatorRunId &&
                item.PointId == result.PointId &&
                item.SourceSequence == result.SourceSequence) ||
            tx.Terminals.Any(item =>
                item.SimulatorRunId == result.SimulatorRunId &&
                item.PointId == result.PointId &&
                item.SourceSequence == result.SourceSequence))
            throw new TelemetryUniqueRaceException();
        tx.Terminals.Add(result.Copy());
        return Task.CompletedTask;
    }

    public Task StageRawAsync(RawMeasurement measurement,
        ITelemetryFlowTransaction transaction, CancellationToken ct = default)
    {
        ThrowIf(TelemetryFakeFailure.RawInsert);
        if (!double.IsFinite(measurement.NumericValue) ||
            measurement.SourceTimestampUtc.Kind != DateTimeKind.Utc ||
            measurement.ReceivedAtUtc.Kind != DateTimeKind.Utc ||
            measurement.ProcessingAtUtc.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("RAW_MEASUREMENT_INVALID");
        var tx = Require(transaction);
        var terminal = tx.Terminals.Concat(_terminals.Values)
            .FirstOrDefault(item => item.MeasurementId == measurement.MeasurementId);
        if (terminal is null ||
            terminal.FinalClassification != TelemetryFinalClassification.Accepted ||
            terminal.SourceId != measurement.SourceId ||
            terminal.SimulatorRunId != measurement.SimulatorRunId ||
            terminal.PointId != measurement.PointId ||
            terminal.MappingId != measurement.MappingId ||
            terminal.MappingVersion != measurement.MappingVersion ||
            terminal.SourceSequence != measurement.SourceSequence ||
            terminal.QualityCode != measurement.QualityCode ||
            terminal.ReasonCode != measurement.ReasonCode)
            throw new InvalidOperationException("RAW_REQUIRES_MATCHING_ACCEPTED_TERMINAL");
        tx.Raw.Add(measurement with { });
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TelemetryTerminalResult>> ListCommittedTerminalsAsync(
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TelemetryTerminalResult>>(
            _terminals.Values.OrderBy(value => value.MeasurementId)
                .Select(value => value.Copy()).ToList());

    public Task<IReadOnlyList<RawMeasurement>> ListCommittedRawAsync(
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<RawMeasurement>>(
            _raw.Values.OrderBy(value => value.MeasurementId)
                .Select(value => value with { }).ToList());

    public Task<bool> EvaluateAdvanceAsync(
        LatestProjectionCandidate candidate,
        ITelemetryFlowTransaction transaction,
        CancellationToken ct = default)
    {
        if (candidate.QualityCode == MeasurementQuality.Bad)
            return Task.FromResult(false);
        return Task.FromResult(LatestAdvanceResult);
    }

    public Task StageAdvanceAsync(
        LatestProjectionCandidate candidate,
        bool latestAdvanced,
        ITelemetryFlowTransaction transaction,
        CancellationToken ct = default)
    {
        if (candidate.QualityCode == MeasurementQuality.Bad)
            throw new InvalidOperationException("BAD_LATEST_FORBIDDEN");
        ThrowIf(TelemetryFakeFailure.Latest);
        if (latestAdvanced) Require(transaction).Latest.Add(candidate);
        return Task.CompletedTask;
    }

    public ValueTask StageAsync(TelemetryOwnerEvent ownerEvent,
        ITelemetryFlowTransaction transaction, CancellationToken ct = default)
    {
        ThrowIf(TelemetryFakeFailure.Outbox);
        Require(transaction).Events.Add(ownerEvent with
        {
            Before = new Dictionary<string, object?>(ownerEvent.Before, StringComparer.Ordinal),
            After = new Dictionary<string, object?>(ownerEvent.After, StringComparer.Ordinal)
        });
        return ValueTask.CompletedTask;
    }

    public Task<IReadOnlyList<TelemetryOwnerEvent>> ListCommittedAsync(
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TelemetryOwnerEvent>>(_events.ToList());

    public Task<RawMeasurement?> GetMeasurementAsync(
        Guid measurementId, CancellationToken ct = default) =>
        Task.FromResult(_raw.TryGetValue(measurementId, out var value) ? value with { } : null);

    public Task<SourceHealthSnapshot?> GetSourceHealthAsync(
        Guid pointId, CancellationToken ct = default) =>
        Task.FromResult<SourceHealthSnapshot?>(null);

    public int LatestCount => _latest.Count;

    private Transaction Require(ITelemetryFlowTransaction transaction) =>
        transaction as Transaction ?? throw new InvalidOperationException("FOREIGN_TRANSACTION");

    private void ThrowIf(TelemetryFakeFailure point)
    {
        if (Failure == point) throw new InvalidOperationException($"INJECTED_{point}");
    }

    private void PublishRaceWinner(TelemetryTerminalResult winner,
        double numericValue, string unitCode)
    {
        TelemetryTerminalResultValidator.EnsureValid(winner);
        _terminals[winner.MeasurementId] = winner.Copy();
        if (winner.FinalClassification != TelemetryFinalClassification.Accepted) return;
        var sourceTs = winner.CompletedAtUtc.AddSeconds(-2);
        var receivedAt = winner.CompletedAtUtc.AddSeconds(-1);
        var processingAt = winner.CompletedAtUtc;
        var raw = new RawMeasurement(
            winner.MeasurementId, winner.SourceId, winner.SimulatorRunId, winner.PointId,
            winner.MappingId, winner.MappingVersion, winner.SourceSequence,
            sourceTs, receivedAt, processingAt,
            numericValue, unitCode,
            winner.QualityCode!.Value, winner.ReasonCode,
            winner.OriginalCorrelationId, winner.OriginalLineageId);
        _raw[winner.MeasurementId] = raw;
        if (winner.LatestAdvanced == true)
            _latest[winner.PointId] = new LatestProjectionCandidate(
                winner.MeasurementId, winner.PointId, sourceTs,
                winner.SourceSequence, processingAt, winner.QualityCode.Value);
        var after = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["measurementId"] = winner.MeasurementId.ToString("D"),
            ["sourceId"] = winner.SourceId.ToString("D"),
            ["simulatorRunId"] = winner.SimulatorRunId.ToString("D"),
            ["pointId"] = winner.PointId.ToString("D"),
            ["mappingId"] = winner.MappingId.ToString("D"),
            ["mappingVersion"] = winner.MappingVersion,
            ["sourceSequence"] = winner.SourceSequence,
            ["sourceTimestampUtc"] = sourceTs,
            ["receivedAtUtc"] = receivedAt,
            ["processingAtUtc"] = processingAt,
            ["numericValue"] = numericValue,
            ["unitCode"] = unitCode,
            ["qualityCode"] = winner.QualityCode?.ToString() ?? "",
            ["latestAdvanced"] = winner.LatestAdvanced ?? false,
            ["correlationId"] = winner.OriginalCorrelationId,
            ["lineageId"] = winner.OriginalLineageId
        };
        _events.Add(new TelemetryOwnerEvent(
            Guid.NewGuid(), "MeasurementAccepted.v1", 1, "IUMP.Telemetry", "Measurement",
            winner.MeasurementId, 1, "IUMP.Telemetry", "trusted-simulator",
            "Accepted", "Measurement accepted.", processingAt,
            winner.OriginalCorrelationId, null, "site-1", "area-1",
            new Dictionary<string, object?>(), after));
    }

    private sealed class Transaction : ITelemetryFlowTransaction
    {
        private readonly FakeTelemetryRepositories _owner;
        private readonly List<TelemetryFlowLock> _locks = [];
        public List<TelemetryTerminalResult> Terminals { get; } = [];
        public List<RawMeasurement> Raw { get; } = [];
        public List<LatestProjectionCandidate> Latest { get; } = [];
        public List<TelemetryOwnerEvent> Events { get; } = [];
        public Guid TransactionId { get; } = Guid.NewGuid();
        public string IsolationIntent => "REPEATABLE READ";
        public bool IsCompleted { get; private set; }
        public IReadOnlyList<TelemetryFlowLock> LockTrace => _locks;

        public Transaction(FakeTelemetryRepositories owner) => _owner = owner;

        public ValueTask AcquireLockAsync(TelemetryFlowLockTarget target, string key,
            CancellationToken ct = default)
        {
            if ((_owner.Failure == TelemetryFakeFailure.OrganizationLock &&
                 target == TelemetryFlowLockTarget.OrganizationPoint) ||
                (_owner.Failure == TelemetryFakeFailure.CatalogLock &&
                 target == TelemetryFlowLockTarget.CatalogSourceMappingMetricUnit))
                throw new InvalidOperationException($"INJECTED_{target}");
            if (_locks.Count > 0 && target < _locks[^1].Target)
                throw new InvalidOperationException("LOCK_ORDER_VIOLATION");
            _locks.Add(new TelemetryFlowLock(target, key));
            return ValueTask.CompletedTask;
        }

        public ValueTask CommitAsync(CancellationToken ct = default)
        {
            _owner.ThrowIf(TelemetryFakeFailure.Commit);
            if (IsCompleted) throw new InvalidOperationException("TRANSACTION_COMPLETED");
            foreach (var terminal in Terminals)
            {
                var rawCount = Raw.Count(item => item.MeasurementId == terminal.MeasurementId) +
                    (_owner._raw.ContainsKey(terminal.MeasurementId) ? 1 : 0);
                if (terminal.FinalClassification == TelemetryFinalClassification.Accepted &&
                    rawCount != 1)
                    throw new InvalidOperationException("ACCEPTED_REQUIRES_RAW");
                if (terminal.FinalClassification == TelemetryFinalClassification.Rejected &&
                    rawCount != 0)
                    throw new InvalidOperationException("REJECTED_FORBIDS_RAW");
            }
            foreach (var terminal in Terminals)
                _owner._terminals.Add(terminal.MeasurementId, terminal.Copy());
            foreach (var measurement in Raw)
                _owner._raw.Add(measurement.MeasurementId, measurement with { });
            foreach (var latest in Latest)
                _owner._latest[latest.PointId] = latest;
            _owner._events.AddRange(Events);
            _owner.LastLockTrace = _locks.ToList();
            IsCompleted = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask RollbackAsync(CancellationToken ct = default)
        {
            if (!IsCompleted)
            {
                Terminals.Clear();
                Raw.Clear();
                Latest.Clear();
                Events.Clear();
                _owner.LastLockTrace = _locks.ToList();
                IsCompleted = true;
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => IsCompleted
            ? ValueTask.CompletedTask
            : RollbackAsync();
    }
}

public sealed class FakeTelemetryProviderQuery : ITelemetryProviderSnapshotQuery
{
    public TelemetryProviderSnapshot? Snapshot { get; set; }
    public bool RecheckResult { get; set; } = true;
    public int Reads { get; private set; }
    public int Rechecks { get; private set; }

    public Task<TelemetryProviderSnapshot?> GetAsync(
        TelemetryMeasurementRequest request, DateTime receivedAtUtc,
        CancellationToken ct = default)
    {
        Reads++;
        return Task.FromResult(Snapshot);
    }

    public Task<bool> RecheckAsync(
        TelemetryProviderSnapshot snapshot, ITelemetryFlowTransaction transaction,
        CancellationToken ct = default)
    {
        Rechecks++;
        return Task.FromResult(RecheckResult);
    }
}

public sealed class FakeImmutableConfigurationQuery : IImmutableSimulatorConfigurationQuery
{
    public ImmutableConfigurationSnapshot? Snapshot { get; set; }

    public Task<ImmutableConfigurationSnapshot?> GetVersionAsync(
        Guid configurationId, long configurationVersion, CancellationToken ct = default) =>
        Task.FromResult(Snapshot is { } value &&
                        value.ConfigurationId == configurationId &&
                        value.ConfigurationVersion == configurationVersion
            ? value
            : null);
}

public sealed class FakeTelemetryClock : ITelemetryUtcClock
{
    public DateTime UtcNow { get; set; }
}

public sealed class FakeTelemetryRepositoryTestProviderFactory :
    ITelemetryRepositoryTestProviderFactory
{
    public TelemetryRepositoryContractFixture Create(
        TelemetryRepositoryFailureMode failure = TelemetryRepositoryFailureMode.None)
    {
        var store = new FakeTelemetryRepositories
        {
            Failure = failure switch
            {
                TelemetryRepositoryFailureMode.TerminalInsert => TelemetryFakeFailure.TerminalInsert,
                TelemetryRepositoryFailureMode.RawInsert => TelemetryFakeFailure.RawInsert,
                TelemetryRepositoryFailureMode.Latest => TelemetryFakeFailure.Latest,
                TelemetryRepositoryFailureMode.Outbox => TelemetryFakeFailure.Outbox,
                TelemetryRepositoryFailureMode.Commit => TelemetryFakeFailure.Commit,
                _ => TelemetryFakeFailure.None
            }
        };
        return new TelemetryRepositoryContractFixture(store, store, store, store);
    }
}
