using IUMP.Modules.Telemetry.Contracts;
using IUMP.Modules.Telemetry.Domain;

namespace IUMP.Tests.Integration.Telemetry;

public enum TelemetryRepositoryFailureMode
{
    None,
    TerminalInsert,
    RawInsert,
    Latest,
    Outbox,
    Commit
}

public sealed record TelemetryRepositoryContractFixture(
    ITelemetryIngestionRepository Repository,
    ILatestProjectionRepository Latest,
    IMeasurementAcceptedEventWriter Events,
    ITelemetryFlowUnitOfWork UnitOfWork);

public interface ITelemetryRepositoryTestProviderFactory
{
    TelemetryRepositoryContractFixture Create(
        TelemetryRepositoryFailureMode failure = TelemetryRepositoryFailureMode.None);
}

public sealed class TelemetryIngestionRepositoryContractRunner
{
    public int ScenarioCount { get; private set; }
    public int AssertionCount { get; private set; }
    public List<string> Failures { get; } = [];

    public async Task RunAllAsync(ITelemetryRepositoryTestProviderFactory factory)
    {
        ScenarioCount = 0;
        AssertionCount = 0;
        Failures.Clear();
        await ScenarioAsync("Accepted identity+raw atomicity", async () =>
        {
            var fixture = factory.Create();
            var data = Data();
            await using var tx = await fixture.UnitOfWork.BeginRepeatableReadAsync();
            await fixture.Repository.StageTerminalAsync(data.Terminal, tx);
            await fixture.Repository.StageRawAsync(data.Raw, tx);
            await tx.CommitAsync();
            Check((await fixture.Repository.ListCommittedTerminalsAsync()).Count == 1, "Accepted identity");
            Check((await fixture.Repository.ListCommittedRawAsync()).Count == 1, "Accepted raw");
        });
        await ScenarioAsync("Rejected identity without raw", async () =>
        {
            var fixture = factory.Create();
            var data = Data(rejected: true);
            await using var tx = await fixture.UnitOfWork.BeginRepeatableReadAsync();
            await fixture.Repository.StageTerminalAsync(data.Terminal, tx);
            await tx.CommitAsync();
            Check((await fixture.Repository.ListCommittedTerminalsAsync()).Count == 1, "Rejected identity");
            Check((await fixture.Repository.ListCommittedRawAsync()).Count == 0, "Rejected no raw");
        });
        await ScenarioAsync("terminal API is immutable", () =>
        {
            var mutators = typeof(ITelemetryIngestionRepository).GetMethods()
                .Where(method => method.Name.Contains("Update", StringComparison.Ordinal) ||
                                 method.Name.Contains("Delete", StringComparison.Ordinal));
            Check(!mutators.Any(), "terminal mutator absent");
            return Task.CompletedTask;
        });
        await ScenarioAsync("raw API is immutable", () =>
        {
            var mutators = typeof(ITelemetryIngestionRepository).GetMethods()
                .Where(method => method.Name.Contains("UpdateRaw", StringComparison.Ordinal) ||
                                 method.Name.Contains("DeleteRaw", StringComparison.Ordinal));
            Check(!mutators.Any(), "raw mutator absent");
            return Task.CompletedTask;
        });
        await ScenarioAsync("exact Accepted replay", () =>
        {
            var data = Data();
            var result = TelemetryTerminalDecision.FromExisting(
                data.Terminal, data.Terminal.RequestFingerprint, "retry");
            Check(result.Disposition == TelemetryDisposition.Duplicate, "Accepted Duplicate");
            Check(TerminalEqual(result.OriginalResult, data.Terminal), "Accepted exact result");
            return Task.CompletedTask;
        });
        await ScenarioAsync("exact Rejected replay", () =>
        {
            var data = Data(rejected: true);
            var result = TelemetryTerminalDecision.FromExisting(
                data.Terminal, data.Terminal.RequestFingerprint, "retry");
            Check(result.Disposition == TelemetryDisposition.Duplicate, "Rejected Duplicate");
            Check(TerminalEqual(result.OriginalResult, data.Terminal), "Rejected exact result");
            return Task.CompletedTask;
        });
        await ScenarioAsync("fingerprint conflict", () =>
        {
            var data = Data();
            var result = TelemetryTerminalDecision.FromExisting(
                data.Terminal, new byte[32], "retry");
            Check(result.ErrorCode == "IDEMPOTENCY_CONFLICT", "conflict");
            return Task.CompletedTask;
        });
        await ScenarioAsync("unique Run Point sequence", async () =>
        {
            var fixture = factory.Create();
            var data = Data();
            await using var tx = await fixture.UnitOfWork.BeginRepeatableReadAsync();
            await fixture.Repository.StageTerminalAsync(data.Terminal, tx);
            await fixture.Repository.StageRawAsync(data.Raw, tx);
            await tx.CommitAsync();
            var stored = await fixture.Repository.GetTerminalBySlotAsync(
                data.Terminal.SimulatorRunId, data.Terminal.PointId,
                data.Terminal.SourceSequence);
            Check(stored?.MeasurementId == data.Terminal.MeasurementId, "slot lookup");
        });
        await ScenarioAsync("unique Run Point sequence rejects another identity", async () =>
        {
            var fixture = factory.Create();
            var winner = Data();
            var loser = Data(mappingOverride: Guid.Parse("66666666-6666-4666-8666-666666666666"));
            await using (var tx = await fixture.UnitOfWork.BeginRepeatableReadAsync())
            {
                await fixture.Repository.StageTerminalAsync(winner.Terminal, tx);
                await fixture.Repository.StageRawAsync(winner.Raw, tx);
                await tx.CommitAsync();
            }
            await using var loserTx = await fixture.UnitOfWork.BeginRepeatableReadAsync();
            try
            {
                await fixture.Repository.StageTerminalAsync(loser.Terminal, loserTx);
                Failures.Add("slot collision did not fail");
            }
            catch (TelemetryUniqueRaceException)
            {
                await loserTx.RollbackAsync();
            }
            var stored = await fixture.Repository.GetTerminalBySlotAsync(
                winner.Terminal.SimulatorRunId, winner.Terminal.PointId,
                winner.Terminal.SourceSequence);
            Check(stored?.MeasurementId == winner.Terminal.MeasurementId, "slot winner stable");
        });
        await ScenarioAsync("matching race winner reloads Duplicate", async () =>
        {
            var fixture = factory.Create();
            var data = Data();
            await using (var winnerTx = await fixture.UnitOfWork.BeginRepeatableReadAsync())
            {
                await fixture.Repository.StageTerminalAsync(data.Terminal, winnerTx);
                await fixture.Repository.StageRawAsync(data.Raw, winnerTx);
                await winnerTx.CommitAsync();
            }
            await using var loserTx = await fixture.UnitOfWork.BeginRepeatableReadAsync();
            try
            {
                await fixture.Repository.StageTerminalAsync(data.Terminal, loserTx);
                Failures.Add("matching race did not fail");
            }
            catch (TelemetryUniqueRaceException)
            {
                await loserTx.RollbackAsync();
            }
            var winner = await fixture.Repository.GetTerminalAsync(data.Terminal.MeasurementId);
            var replay = TelemetryTerminalDecision.FromExisting(
                winner!, data.Terminal.RequestFingerprint, "retry");
            Check(replay.Disposition == TelemetryDisposition.Duplicate, "matching race Duplicate");
        });
        await ScenarioAsync("conflicting race winner reloads conflict", async () =>
        {
            var fixture = factory.Create();
            var data = Data();
            await using (var winnerTx = await fixture.UnitOfWork.BeginRepeatableReadAsync())
            {
                await fixture.Repository.StageTerminalAsync(data.Terminal, winnerTx);
                await fixture.Repository.StageRawAsync(data.Raw, winnerTx);
                await winnerTx.CommitAsync();
            }
            var winner = await fixture.Repository.GetTerminalAsync(data.Terminal.MeasurementId);
            var replay = TelemetryTerminalDecision.FromExisting(
                winner!, new byte[32], "retry");
            Check(replay.ErrorCode == "IDEMPOTENCY_CONFLICT", "conflicting race conflict");
        });
        await ScenarioAsync("Accepted without raw cannot commit", async () =>
        {
            var fixture = factory.Create();
            var data = Data();
            await using var tx = await fixture.UnitOfWork.BeginRepeatableReadAsync();
            await fixture.Repository.StageTerminalAsync(data.Terminal, tx);
            try
            {
                await tx.CommitAsync();
                Failures.Add("Accepted without raw committed");
            }
            catch (InvalidOperationException)
            {
                await tx.RollbackAsync();
            }
            Check((await fixture.Repository.ListCommittedTerminalsAsync()).Count == 0,
                "invalid Accepted rollback");
        });
        await ScenarioAsync("Rejected forbids raw", async () =>
        {
            var fixture = factory.Create();
            var rejected = Data(rejected: true);
            await using var tx = await fixture.UnitOfWork.BeginRepeatableReadAsync();
            await fixture.Repository.StageTerminalAsync(rejected.Terminal, tx);
            try
            {
                await fixture.Repository.StageRawAsync(rejected.Raw, tx);
                Failures.Add("Rejected raw was accepted");
            }
            catch (InvalidOperationException)
            {
                await tx.RollbackAsync();
            }
            Check((await fixture.Repository.ListCommittedRawAsync()).Count == 0,
                "Rejected raw absent");
        });
        foreach (var item in new[]
                 {
                     (TelemetryRepositoryFailureMode.RawInsert, "raw insert rollback"),
                     (TelemetryRepositoryFailureMode.Latest, "Latest rollback"),
                     (TelemetryRepositoryFailureMode.Outbox, "outbox rollback"),
                     (TelemetryRepositoryFailureMode.Commit, "commit rollback")
                 })
            await ScenarioAsync(item.Item2, async () =>
            {
                var fixture = factory.Create(item.Item1);
                var data = Data();
                await using var tx = await fixture.UnitOfWork.BeginRepeatableReadAsync();
                try
                {
                    await fixture.Repository.StageTerminalAsync(data.Terminal, tx);
                    await fixture.Repository.StageRawAsync(data.Raw, tx);
                    var advance = await fixture.Latest.EvaluateAdvanceAsync(data.Latest, tx);
                    await fixture.Latest.StageAdvanceAsync(data.Latest, advance, tx);
                    await fixture.Events.StageAsync(data.Event, tx);
                    await tx.CommitAsync();
                    Failures.Add($"{item.Item2}: expected failure");
                }
                catch (InvalidOperationException)
                {
                    await tx.RollbackAsync();
                }
                Check((await fixture.Repository.ListCommittedTerminalsAsync()).Count == 0,
                    $"{item.Item2} terminal");
                Check((await fixture.Repository.ListCommittedRawAsync()).Count == 0,
                    $"{item.Item2} raw");
                Check((await fixture.Events.ListCommittedAsync()).Count == 0,
                    $"{item.Item2} event");
            });
        await ScenarioAsync("registry/raw consistency", async () =>
        {
            var fixture = factory.Create();
            var data = Data();
            await using var tx = await fixture.UnitOfWork.BeginRepeatableReadAsync();
            await fixture.Repository.StageTerminalAsync(data.Terminal, tx);
            await fixture.Repository.StageRawAsync(data.Raw, tx);
            await tx.CommitAsync();
            var terminal = (await fixture.Repository.ListCommittedTerminalsAsync()).Single();
            var raw = (await fixture.Repository.ListCommittedRawAsync()).Single();
            Check(terminal.MeasurementPersisted &&
                  terminal.PersistedMeasurementId == raw.MeasurementId, "consistent identity");
        });
        await ScenarioAsync("stable deterministic reads", async () =>
        {
            var fixture = factory.Create();
            var first = Data();
            var second = Data(sequence: 2);
            await using var tx = await fixture.UnitOfWork.BeginRepeatableReadAsync();
            await fixture.Repository.StageTerminalAsync(second.Terminal, tx);
            await fixture.Repository.StageRawAsync(second.Raw, tx);
            await fixture.Repository.StageTerminalAsync(first.Terminal, tx);
            await fixture.Repository.StageRawAsync(first.Raw, tx);
            await tx.CommitAsync();
            var one = await fixture.Repository.ListCommittedTerminalsAsync();
            var two = await fixture.Repository.ListCommittedTerminalsAsync();
            Check(one.Select(value => value.MeasurementId)
                .SequenceEqual(two.Select(value => value.MeasurementId)), "stable ordering");
        });
        await ScenarioAsync("no independent commit surface", () =>
        {
            Check(!typeof(ITelemetryIngestionRepository).GetMethods()
                .Any(method => method.Name is "Commit" or "CommitAsync"),
                "repository has no commit");
            return Task.CompletedTask;
        });
    }

    private async Task ScenarioAsync(string name, Func<Task> scenario)
    {
        ScenarioCount++;
        try { await scenario(); }
        catch (Exception ex) { Failures.Add($"{name}: {ex.GetType().Name}: {ex.Message}"); }
    }

    private void Check(bool condition, string message)
    {
        AssertionCount++;
        if (!condition) Failures.Add(message);
    }

    private static bool TerminalEqual(
        TelemetryTerminalResult? left, TelemetryTerminalResult? right) =>
        left is not null && right is not null &&
        left with { RequestFingerprint = Array.Empty<byte>() } ==
        right with { RequestFingerprint = Array.Empty<byte>() } &&
        left.RequestFingerprint.SequenceEqual(right.RequestFingerprint);

    private static ContractData Data(
        bool rejected = false,
        long sequence = 1,
        Guid? mappingOverride = null)
    {
        var source = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var run = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var point = Guid.Parse("33333333-3333-4333-8333-333333333333");
        var mapping = mappingOverride ??
            Guid.Parse("44444444-4444-4444-8444-444444444444");
        var configuration = Guid.Parse("55555555-5555-4555-8555-555555555555");
        var id = MeasurementIdentityVerifier.Create(source, run, point, mapping, sequence, 1);
        var request = new TelemetryMeasurementRequest(
            id.ToString("D"), source, run, point, mapping, 1, sequence,
            "IUMP-DETERMINISTIC-V1", 1, configuration, 1,
            new DateTime(2026, 7, 28, 6, 0, 0, DateTimeKind.Utc),
            12.5, "kW", "IUMP.Acquisition.Simulator.v1", "correlation", "lineage");
        var fingerprint = TelemetryRequestFingerprintV1.Compute(request);
        var terminal = new TelemetryTerminalResult(
            id, source, run, point, mapping, 1, sequence, request.AlgorithmId, 1,
            configuration, 1,
            rejected ? TelemetryFinalClassification.Rejected : TelemetryFinalClassification.Accepted,
            !rejected, rejected ? null : id,
            rejected ? null : MeasurementQuality.Good, null,
            rejected ? "POINT_INACTIVE" : null, rejected ? null : true,
            request.SourceTimestampUtc, request.CorrelationId, request.LineageId, fingerprint);
        var raw = new RawMeasurement(
            id, source, run, point, mapping, 1, sequence, request.SourceTimestampUtc,
            request.SourceTimestampUtc, request.SourceTimestampUtc, 12.5, "kW",
            MeasurementQuality.Good, null, request.CorrelationId, request.LineageId);
        var latest = new LatestProjectionCandidate(
            id, point, request.SourceTimestampUtc, sequence,
            request.SourceTimestampUtc, MeasurementQuality.Good);
        var ownerEvent = new TelemetryOwnerEvent(
            Guid.NewGuid(), "MeasurementAccepted.v1", 1, "IUMP.Telemetry",
            "Measurement", id, 1, "IUMP.Telemetry", "trusted-simulator",
            "Accepted", "Measurement accepted.", request.SourceTimestampUtc,
            request.CorrelationId, null, "site", "area",
            new Dictionary<string, object?>(), new Dictionary<string, object?>());
        return new ContractData(terminal, raw, latest, ownerEvent);
    }

    private sealed record ContractData(
        TelemetryTerminalResult Terminal,
        RawMeasurement Raw,
        LatestProjectionCandidate Latest,
        TelemetryOwnerEvent Event);
}
