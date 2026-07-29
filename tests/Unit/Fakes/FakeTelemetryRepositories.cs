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
    ITelemetryFlowUnitOfWork,
    ITelemetryTerminalReplayProbe,
    ITelemetryRaceWinnerProbe
{
    private readonly Dictionary<Guid, TelemetryTerminalResult> _terminals = [];
    private readonly Dictionary<Guid, RawMeasurement> _raw = [];
    private readonly List<TelemetryOwnerEvent> _events = [];
    private readonly Dictionary<Guid, LatestProjectionCandidate> _latest = [];

    public TelemetryFakeFailure Failure { get; set; }
    public bool LatestAdvanceResult { get; set; } = true;
    public TelemetryRaceWinnerFixture? RaceWinnerFixtureOnStage { get; set; }
    public IReadOnlyList<TelemetryFlowLock> LastLockTrace { get; private set; } = [];

    public void StageRaceWinner(TelemetryRaceWinnerFixture fixture) =>
        RaceWinnerFixtureOnStage = fixture;

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
        if (RaceWinnerFixtureOnStage is { } w)
        {
            PublishRaceWinner(w);
            RaceWinnerFixtureOnStage = null;
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

    public string ReplayTerminal(TelemetryTerminalResult candidate)
    {
        if (!_terminals.TryGetValue(candidate.MeasurementId, out var stored))
            return "MISSING";
        var exact = stored with { RequestFingerprint = Array.Empty<byte>() } ==
                    candidate with { RequestFingerprint = Array.Empty<byte>() } &&
                    stored.RequestFingerprint.SequenceEqual(candidate.RequestFingerprint);
        return exact ? "DUPLICATE" : "TERMINAL_RESULT_CONFLICT";
    }

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

    private void PublishRaceWinner(TelemetryRaceWinnerFixture fixture)
    {
        var winner = fixture.Terminal;
        TelemetryTerminalResultValidator.EnsureValid(winner);
        _terminals[winner.MeasurementId] = winner.Copy();
        if (winner.FinalClassification == TelemetryFinalClassification.Rejected)
        {
            if (fixture.Raw is not null || fixture.Latest is not null || fixture.Event is not null)
                throw new InvalidOperationException("RACE_WINNER_FIXTURE_INVALID");
            return;
        }

        if (fixture.Raw is null || fixture.Event is null ||
            ((winner.LatestAdvanced == true) != (fixture.Latest is not null)) ||
            !RawMatchesWinner(fixture.Raw, winner) ||
            (fixture.Latest is not null && !LatestMatchesWinner(fixture.Latest, fixture.Raw, winner)) ||
            !EventMatchesWinner(fixture.Event, fixture.Raw, winner))
            throw new InvalidOperationException("RACE_WINNER_FIXTURE_INVALID");
        _raw[winner.MeasurementId] = fixture.Raw with { };
        if (fixture.Latest is not null)
            _latest[winner.PointId] = fixture.Latest with { };
        _events.Add(fixture.Event with
        {
            Before = new Dictionary<string, object?>(fixture.Event.Before, StringComparer.Ordinal),
            After = new Dictionary<string, object?>(fixture.Event.After, StringComparer.Ordinal)
        });
    }

    private static bool RawMatchesWinner(RawMeasurement raw, TelemetryTerminalResult winner) =>
        raw.MeasurementId == winner.MeasurementId && raw.SourceId == winner.SourceId &&
        raw.SimulatorRunId == winner.SimulatorRunId && raw.PointId == winner.PointId &&
        raw.MappingId == winner.MappingId && raw.MappingVersion == winner.MappingVersion &&
        raw.SourceSequence == winner.SourceSequence &&
        raw.SourceTimestampUtc.Kind == DateTimeKind.Utc &&
        raw.ReceivedAtUtc.Kind == DateTimeKind.Utc &&
        raw.ProcessingAtUtc.Kind == DateTimeKind.Utc &&
        !double.IsNaN(raw.NumericValue) && !double.IsInfinity(raw.NumericValue) &&
        !string.IsNullOrWhiteSpace(raw.UnitCode) && raw.QualityCode == winner.QualityCode &&
        raw.ReasonCode == winner.ReasonCode && raw.CorrelationId == winner.OriginalCorrelationId &&
        raw.LineageId == winner.OriginalLineageId;

    private static bool LatestMatchesWinner(
        LatestProjectionCandidate latest, RawMeasurement raw, TelemetryTerminalResult winner) =>
        latest.MeasurementId == winner.MeasurementId && latest.PointId == winner.PointId &&
        latest.SourceTimestampUtc == raw.SourceTimestampUtc &&
        latest.SourceSequence == winner.SourceSequence &&
        latest.ProcessingAtUtc == raw.ProcessingAtUtc && latest.QualityCode == winner.QualityCode;

    private static bool EventMatchesWinner(
        TelemetryOwnerEvent ownerEvent, RawMeasurement raw, TelemetryTerminalResult winner)
    {
        var expected = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["measurementId"] = raw.MeasurementId.ToString("D"),
            ["sourceId"] = raw.SourceId.ToString("D"),
            ["simulatorRunId"] = raw.SimulatorRunId.ToString("D"),
            ["pointId"] = raw.PointId.ToString("D"),
            ["mappingId"] = raw.MappingId.ToString("D"),
            ["mappingVersion"] = raw.MappingVersion,
            ["sourceSequence"] = raw.SourceSequence,
            ["sourceTimestampUtc"] = raw.SourceTimestampUtc,
            ["receivedAtUtc"] = raw.ReceivedAtUtc,
            ["processingAtUtc"] = raw.ProcessingAtUtc,
            ["numericValue"] = raw.NumericValue,
            ["unitCode"] = raw.UnitCode,
            ["qualityCode"] = raw.QualityCode.ToString(),
            ["reasonCode"] = raw.ReasonCode,
            ["latestAdvanced"] = winner.LatestAdvanced == true,
            ["correlationId"] = raw.CorrelationId,
            ["lineageId"] = raw.LineageId
        };
        return ownerEvent.EventType == "MeasurementAccepted.v1" &&
            ownerEvent.EventId != Guid.Empty && ownerEvent.SchemaVersion == 1 &&
            ownerEvent.Producer == "IUMP.Telemetry" && ownerEvent.AggregateType == "Measurement" &&
            ownerEvent.AggregateId == winner.MeasurementId && ownerEvent.AggregateVersion == 1 &&
            ownerEvent.ActorId == "IUMP.Telemetry" &&
            ownerEvent.ActorUsername == "trusted-simulator" &&
            ownerEvent.Action == "Accepted" && ownerEvent.Summary == "Measurement accepted." &&
            !string.IsNullOrWhiteSpace(ownerEvent.SiteId) &&
            (ownerEvent.AreaId is null || ownerEvent.AreaId.Length > 0) &&
            ownerEvent.CausationId is null &&
            ownerEvent.OccurredAtUtc == raw.ProcessingAtUtc &&
            ownerEvent.CorrelationId == raw.CorrelationId && ownerEvent.Before.Count == 0 &&
            ownerEvent.After.Count == expected.Count &&
            expected.All(pair => ownerEvent.After.TryGetValue(pair.Key, out var actual) &&
                                 Equals(actual, pair.Value));
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
                 target >= TelemetryFlowLockTarget.OrganizationSite &&
                 target <= TelemetryFlowLockTarget.OrganizationPoint) ||
                (_owner.Failure == TelemetryFakeFailure.CatalogLock &&
                 target >= TelemetryFlowLockTarget.CatalogSource &&
                 target <= TelemetryFlowLockTarget.CatalogUnit))
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
    public TelemetryProviderSnapshot? CurrentSnapshot { get; set; }
    public int Reads { get; private set; }
    public int Rechecks { get; private set; }

    public Task<TelemetryProviderSnapshot?> GetAsync(
        TelemetryMeasurementRequest request, DateTime receivedAtUtc,
        CancellationToken ct = default)
    {
        Reads++;
        return Task.FromResult(Snapshot);
    }

    public Task<TelemetryProviderRecheckResult> RecheckAsync(
        TelemetryProviderSnapshot snapshot, ITelemetryFlowTransaction transaction,
        CancellationToken ct = default)
    {
        Rechecks++;
        var current = CurrentSnapshot ?? Snapshot;
        return Task.FromResult(current is null
            ? TelemetryProviderRecheckResult.Compare(
                snapshot, snapshot with { PointExists = false })
            : TelemetryProviderRecheckResult.Compare(snapshot, current));
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
        return new TelemetryRepositoryContractFixture(
            store, store, store, store, store, store);
    }
}
