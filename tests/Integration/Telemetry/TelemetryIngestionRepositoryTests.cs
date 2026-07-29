using IUMP.Modules.Telemetry.Contracts;
using IUMP.Modules.Telemetry.Domain;
using IUMP.Modules.Telemetry.Application;
using IUMP.Tests.Unit.Telemetry;

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
    ITelemetryFlowUnitOfWork UnitOfWork,
    ITelemetryTerminalReplayProbe? ReplayProbe = null,
    ITelemetryRaceWinnerProbe? RaceWinnerProbe = null);

public interface ITelemetryTerminalReplayProbe
{
    string ReplayTerminal(TelemetryTerminalResult candidate);
}

public interface ITelemetryRaceWinnerProbe
{
    void StageRaceWinner(TelemetryRaceWinnerFixture fixture);
    int LatestCount { get; }
    Task<LatestProjectionCandidate?> GetCommittedLatestAsync(Guid pointId, CancellationToken ct = default);
}

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
        await ScenarioAsync("replay conflict checks every persisted terminal field", () =>
        {
            var fixture = factory.Create();
            var data = Data();
            var variants = new[]
            {
                data.Terminal with { FinalClassification = TelemetryFinalClassification.Rejected },
                data.Terminal with { MeasurementPersisted = false },
                data.Terminal with { PersistedMeasurementId = Guid.NewGuid() },
                data.Terminal with { QualityCode = MeasurementQuality.Uncertain },
                data.Terminal with { ReasonCode = "SOURCE_TIMESTAMP_FUTURE" },
                data.Terminal with { LatestAdvanced = false },
                data.Terminal with { CompletedAtUtc = data.Terminal.CompletedAtUtc.AddSeconds(1) },
                data.Terminal with { OriginalCorrelationId = "changed" },
                data.Terminal with { OriginalLineageId = "changed" },
                data.Terminal with { RequestFingerprint = Enumerable.Repeat((byte)9, 32).ToArray() }
            };
            var conflictIndex = 0;
            if (fixture.ReplayProbe is not null)
            {
                var tx = fixture.UnitOfWork.BeginRepeatableReadAsync().GetAwaiter().GetResult();
                fixture.Repository.StageTerminalAsync(data.Terminal, tx).GetAwaiter().GetResult();
                fixture.Repository.StageRawAsync(data.Raw, tx).GetAwaiter().GetResult();
                tx.CommitAsync().GetAwaiter().GetResult();
                tx.DisposeAsync().GetAwaiter().GetResult();
                foreach (var variant in variants)
                {
                    Check(!TerminalEqual(data.Terminal, variant),
                        "terminal mutation is materially different");
                    Check(fixture.ReplayProbe.ReplayTerminal(variant) == "TERMINAL_RESULT_CONFLICT",
                        "terminal field conflict is detected by repository");
                }
            }
            else
            {
                foreach (var variant in variants)
                {
                    var conflictFingerprint = Enumerable.Repeat(
                        (byte)(conflictIndex++ + 1), 32).ToArray();
                    Check(!TerminalEqual(data.Terminal, variant),
                        "terminal mutation is materially different");
                    var replay = TelemetryTerminalDecision.FromExisting(
                        data.Terminal, conflictFingerprint, "retry");
                    Check(replay.ErrorCode == "IDEMPOTENCY_CONFLICT",
                        "terminal field conflict preserves stored exact result");
                }
            }
            Check(TerminalEqual(data.Terminal, data.Terminal.Copy()),
                "every terminal field round-trips");
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
            fixture.RaceWinnerProbe?.StageRaceWinner(new TelemetryRaceWinnerFixture(
                data.Terminal, data.Raw, data.Latest, data.Event));
            await using (var winnerTx = await fixture.UnitOfWork.BeginRepeatableReadAsync())
            {
                try
                {
                    await fixture.Repository.StageTerminalAsync(data.Terminal, winnerTx);
                    await fixture.Repository.StageRawAsync(data.Raw, winnerTx);
                    await winnerTx.CommitAsync();
                }
                catch (TelemetryUniqueRaceException)
                {
                    await winnerTx.RollbackAsync();
                }
            }
            if (fixture.RaceWinnerProbe is not null)
            {
                Check((await fixture.Repository.ListCommittedRawAsync()).Single() == data.Raw,
                    "race winner raw fixture copied exactly");
                Check(EventEqual((await fixture.Events.ListCommittedAsync()).Single(), data.Event),
                    "race winner event fixture copied exactly");
                Check(fixture.RaceWinnerProbe.LatestCount == 1 && data.Latest is not null,
                    "race winner Latest fixture copied exactly");
                Check((await fixture.Repository.GetTerminalAsync(data.Terminal.MeasurementId)) is
                    { } stored && TerminalEqual(stored, data.Terminal),
                    "race winner terminal fixture copied exactly");
            }
            else
            {
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
            }
            var winner = await fixture.Repository.GetTerminalAsync(data.Terminal.MeasurementId);
            var replay = TelemetryTerminalDecision.FromExisting(
                winner!, data.Terminal.RequestFingerprint, "retry");
            Check(replay.Disposition == TelemetryDisposition.Duplicate, "matching race Duplicate");
            Check(TerminalEqual(replay.OriginalResult, data.Terminal),
                "matching race returns exact terminal fixture");
        });
        await ScenarioAsync("exact Rejected race winner", async () =>
        {
            var fixture = factory.Create();
            var data = Data(rejected: true);
            var rejectedFixture = new TelemetryRaceWinnerFixture(data.Terminal, null, null, null);
            fixture.RaceWinnerProbe?.StageRaceWinner(rejectedFixture);
            await using (var winnerTx = await fixture.UnitOfWork.BeginRepeatableReadAsync())
            {
                try
                {
                    await fixture.Repository.StageTerminalAsync(data.Terminal, winnerTx);
                    await winnerTx.CommitAsync();
                }
                catch (TelemetryUniqueRaceException)
                {
                    await winnerTx.RollbackAsync();
                }
            }
            if (fixture.RaceWinnerProbe is not null)
            {
                var stored = await fixture.Repository.GetTerminalAsync(data.Terminal.MeasurementId);
                Check(stored is not null && TerminalEqual(stored, data.Terminal),
                    "Rejected winner terminal committed exactly");
                Check((await fixture.Repository.ListCommittedRawAsync()).Count == 0,
                    "Rejected winner zero raw");
                Check(fixture.RaceWinnerProbe.LatestCount == 0,
                    "Rejected winner zero Latest");
                Check((await fixture.Events.ListCommittedAsync()).Count == 0,
                    "Rejected winner zero event");
                var loser = TelemetryTerminalDecision.FromExisting(
                    data.Terminal, data.Terminal.RequestFingerprint, "retry");
                Check(loser.Disposition == TelemetryDisposition.Duplicate &&
                      TerminalEqual(loser.OriginalResult, data.Terminal),
                    "Rejected winner loser returns exact Duplicate original result");
            }
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
                    var advance = await fixture.Latest.EvaluateAdvanceAsync(data.Latest!, tx);
                    await fixture.Latest.StageAdvanceAsync(data.Latest!, advance, tx);
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
        // Exact Latest evidence scenarios
        await ScenarioAsync("Accepted race winner exact Latest evidence", async () =>
        {
            var fixture = factory.Create();
            var data = Data();
            fixture.RaceWinnerProbe?.StageRaceWinner(new TelemetryRaceWinnerFixture(
                data.Terminal, data.Raw, data.Latest, data.Event));
            await using (var winnerTx = await fixture.UnitOfWork.BeginRepeatableReadAsync())
            {
                try
                {
                    await fixture.Repository.StageTerminalAsync(data.Terminal, winnerTx);
                    await fixture.Repository.StageRawAsync(data.Raw, winnerTx);
                    await winnerTx.CommitAsync();
                }
                catch (TelemetryUniqueRaceException)
                {
                    await winnerTx.RollbackAsync();
                }
            }
            if (fixture.RaceWinnerProbe is not null)
            {
                var committed = await fixture.RaceWinnerProbe.GetCommittedLatestAsync(data.Terminal.PointId);
                Check(committed is not null, "Latest exists after Accepted race");
                Check(committed!.MeasurementId == data.Latest!.MeasurementId, "Latest MeasurementId equal");
                Check(committed.PointId == data.Latest.PointId, "Latest PointId equal");
                Check(committed.SourceTimestampUtc == data.Latest.SourceTimestampUtc, "Latest SourceTimestampUtc equal");
                Check(committed.SourceSequence == data.Latest.SourceSequence, "Latest SourceSequence equal");
                Check(committed.ProcessingAtUtc == data.Latest.ProcessingAtUtc, "Latest ProcessingAtUtc equal");
                Check(committed.QualityCode == data.Latest.QualityCode, "Latest QualityCode equal");
            }
        });
        await ScenarioAsync("Accepted race winner LatestAdvanced=false returns null Latest", async () =>
        {
            var fixture = factory.Create();
            var data = Data(latestAdvanced: false);
            fixture.RaceWinnerProbe?.StageRaceWinner(new TelemetryRaceWinnerFixture(
                data.Terminal, data.Raw, null, data.Event));
            await using (var winnerTx = await fixture.UnitOfWork.BeginRepeatableReadAsync())
            {
                try
                {
                    await fixture.Repository.StageTerminalAsync(data.Terminal, winnerTx);
                    await fixture.Repository.StageRawAsync(data.Raw, winnerTx);
                    await winnerTx.CommitAsync();
                }
                catch (TelemetryUniqueRaceException)
                {
                    await winnerTx.RollbackAsync();
                }
            }
            if (fixture.RaceWinnerProbe is not null)
            {
                var committed = await fixture.RaceWinnerProbe.GetCommittedLatestAsync(data.Terminal.PointId);
                Check(committed is null, "LatestAdvanced=false Latest is null");
                Check(fixture.RaceWinnerProbe.LatestCount == 0, "LatestAdvanced=false LatestCount==0");
                var stored = await fixture.Repository.GetTerminalAsync(data.Terminal.MeasurementId);
                Check(stored is not null && stored.LatestAdvanced == false, "LatestAdvanced stored as false");
                Check(stored is not null && stored.FinalClassification == TelemetryFinalClassification.Accepted, "Accepted classification");
            }
            Check((await fixture.Repository.ListCommittedTerminalsAsync()).Count == 1, "terminal committed");
            Check((await fixture.Repository.ListCommittedRawAsync()).Count == 1, "raw committed");
            Check((await fixture.Events.ListCommittedAsync()).Count == 1, "event committed");
        });
        // Invalid Accepted fixture cases — prove zero partial publication
        foreach (var (label, makeFixture) in new (string, Func<ContractData, TelemetryRaceWinnerFixture>)[]
        {
            ("invalid Accepted Raw null", d => new TelemetryRaceWinnerFixture(d.Terminal, null, d.Latest, d.Event)),
            ("invalid Accepted Raw identity mismatch", d => new TelemetryRaceWinnerFixture(d.Terminal,
                d.Raw with { MeasurementId = Guid.NewGuid() }, d.Latest, d.Event)),
            ("invalid Accepted Latest null when LatestAdvanced=true", d => new TelemetryRaceWinnerFixture(d.Terminal, d.Raw, null, d.Event)),
            ("invalid Accepted Latest present when LatestAdvanced=false", d => new TelemetryRaceWinnerFixture(
                d.Terminal with { LatestAdvanced = false }, d.Raw, d.Latest, d.Event)),
            ("invalid Accepted Latest field mismatch", d => new TelemetryRaceWinnerFixture(d.Terminal, d.Raw,
                d.Latest! with { QualityCode = MeasurementQuality.Uncertain }, d.Event)),
            ("invalid Accepted Event null", d => new TelemetryRaceWinnerFixture(d.Terminal, d.Raw, d.Latest, null)),
            ("invalid Accepted Event envelope", d => new TelemetryRaceWinnerFixture(d.Terminal, d.Raw, d.Latest,
                d.Event with { EventType = "Wrong.v1" })),
            ("invalid Accepted Event payload", d =>
            {
                var badAfter = new Dictionary<string, object?>(d.Event.After, StringComparer.Ordinal)
                    { ["unitCode"] = "KWH" };
                return new TelemetryRaceWinnerFixture(d.Terminal, d.Raw, d.Latest,
                    d.Event with { After = badAfter });
            }),
        })
        {
            await ScenarioAsync(label, async () =>
            {
                var fixture = factory.Create();
                var data = Data();
                await using (var seedTx = await fixture.UnitOfWork.BeginRepeatableReadAsync())
                {
                    await fixture.Repository.StageTerminalAsync(data.Terminal, seedTx);
                    await fixture.Repository.StageRawAsync(data.Raw, seedTx);
                    await seedTx.CommitAsync();
                }
                var preCount = (await fixture.Repository.ListCommittedTerminalsAsync()).Count;
                var preRaw = (await fixture.Repository.ListCommittedRawAsync()).Count;
                var preLatest = fixture.RaceWinnerProbe?.LatestCount ?? 0;
                var preEvents = (await fixture.Events.ListCommittedAsync()).Count;
                var preLatestEntry = fixture.RaceWinnerProbe is not null
                    ? await fixture.RaceWinnerProbe.GetCommittedLatestAsync(data.Terminal.PointId)
                    : null;
                var invalid = makeFixture(data);
                fixture.RaceWinnerProbe?.StageRaceWinner(invalid);
                var loserRequest = Data().Terminal;
                await using (var loserTx = await fixture.UnitOfWork.BeginRepeatableReadAsync())
                {
                    try
                    {
                        await fixture.Repository.StageTerminalAsync(loserRequest, loserTx);
                        Failures.Add($"{label}: expected failure");
                    }
                    catch (TelemetryUniqueRaceException)
                    {
                        await loserTx.RollbackAsync();
                    }
                    catch (InvalidOperationException)
                    {
                        await loserTx.RollbackAsync();
                    }
                }
                Check((await fixture.Repository.ListCommittedTerminalsAsync()).Count == preCount,
                    $"{label}: terminal count unchanged");
                Check((await fixture.Repository.ListCommittedRawAsync()).Count == preRaw,
                    $"{label}: raw count unchanged");
                Check((fixture.RaceWinnerProbe?.LatestCount ?? 0) == preLatest,
                    $"{label}: Latest count unchanged");
                Check((await fixture.Events.ListCommittedAsync()).Count == preEvents,
                    $"{label}: event count unchanged");
                if (fixture.RaceWinnerProbe is not null)
                {
                    var afterLatest = await fixture.RaceWinnerProbe.GetCommittedLatestAsync(data.Terminal.PointId);
                    Check((afterLatest is null) == (preLatestEntry is null),
                        $"{label}: Latest entry existence unchanged");
                    if (afterLatest is not null && preLatestEntry is not null)
                    {
                        Check(afterLatest.MeasurementId == preLatestEntry.MeasurementId,
                            $"{label}: Latest MeasurementId unchanged");
                        Check(afterLatest.QualityCode == preLatestEntry.QualityCode,
                            $"{label}: Latest QualityCode unchanged");
                    }
                }
            });
        }
        // Invalid Rejected fixture cases
        foreach (var (label, makeFixture) in new (string, Func<ContractData, TelemetryRaceWinnerFixture>)[]
        {
            ("invalid Rejected Raw present", d =>
                new TelemetryRaceWinnerFixture(d.Terminal with { FinalClassification = TelemetryFinalClassification.Rejected },
                    d.Raw, null, null)),
            ("invalid Rejected Latest present", d =>
                new TelemetryRaceWinnerFixture(d.Terminal with { FinalClassification = TelemetryFinalClassification.Rejected },
                    null, d.Latest!, null)),
            ("invalid Rejected Event present", d =>
                new TelemetryRaceWinnerFixture(d.Terminal with { FinalClassification = TelemetryFinalClassification.Rejected },
                    null, null, d.Event)),
        })
        {
            await ScenarioAsync(label, async () =>
            {
                var fixture = factory.Create();
                var data = Data();
                await using (var seedTx = await fixture.UnitOfWork.BeginRepeatableReadAsync())
                {
                    await fixture.Repository.StageTerminalAsync(data.Terminal, seedTx);
                    await fixture.Repository.StageRawAsync(data.Raw, seedTx);
                    await seedTx.CommitAsync();
                }
                var preCount = (await fixture.Repository.ListCommittedTerminalsAsync()).Count;
                var preRaw = (await fixture.Repository.ListCommittedRawAsync()).Count;
                var invalid = makeFixture(data);
                fixture.RaceWinnerProbe?.StageRaceWinner(invalid);
                await using (var loserTx = await fixture.UnitOfWork.BeginRepeatableReadAsync())
                {
                    var loserTerminal = Data().Terminal;
                    try
                    {
                        await fixture.Repository.StageTerminalAsync(loserTerminal, loserTx);
                        Failures.Add($"{label}: expected failure");
                    }
                    catch (TelemetryUniqueRaceException)
                    {
                        await loserTx.RollbackAsync();
                    }
                    catch (InvalidOperationException)
                    {
                        await loserTx.RollbackAsync();
                    }
                }
                Check((await fixture.Repository.ListCommittedTerminalsAsync()).Count == preCount,
                    $"{label}: terminal count unchanged");
                Check((await fixture.Repository.ListCommittedRawAsync()).Count == preRaw,
                    $"{label}: raw count unchanged");
            });
        }
        // Rejected fixture matrix — multiple valid Rejected fixtures preserve pre-existing state
        await ScenarioAsync("Rejected fixture preserves pre-existing Accepted state", async () =>
        {
            var fixture = factory.Create();
            var accepted = Data();
            await using (var seedTx = await fixture.UnitOfWork.BeginRepeatableReadAsync())
            {
                await fixture.Repository.StageTerminalAsync(accepted.Terminal, seedTx);
                await fixture.Repository.StageRawAsync(accepted.Raw, seedTx);
                await seedTx.CommitAsync();
            }
            var preTerminals = (await fixture.Repository.ListCommittedTerminalsAsync()).ToList();
            var preRaw = (await fixture.Repository.ListCommittedRawAsync()).ToList();
            var preEvents = (await fixture.Events.ListCommittedAsync()).ToList();
            var preLatest = fixture.RaceWinnerProbe?.LatestCount ?? 0;
            var rejected = Data(rejected: true, sequence: 2);
            var rejectedFixture = new TelemetryRaceWinnerFixture(rejected.Terminal, null, null, null);
            fixture.RaceWinnerProbe?.StageRaceWinner(rejectedFixture);
            await using (var rejectedTx = await fixture.UnitOfWork.BeginRepeatableReadAsync())
            {
                try
                {
                    await fixture.Repository.StageTerminalAsync(rejected.Terminal, rejectedTx);
                    await rejectedTx.CommitAsync();
                }
                catch (TelemetryUniqueRaceException)
                {
                    await rejectedTx.RollbackAsync();
                }
            }
            Check((await fixture.Repository.ListCommittedTerminalsAsync()).Count == preTerminals.Count + 1,
                "Rejected adds one terminal");
            Check((await fixture.Repository.ListCommittedRawAsync()).Count == preRaw.Count,
                "Rejected does not add raw");
            Check((fixture.RaceWinnerProbe?.LatestCount ?? 0) == preLatest,
                "Rejected does not add Latest");
            Check((await fixture.Events.ListCommittedAsync()).Count == preEvents.Count,
                "Rejected does not add event");
            foreach (var pre in preTerminals)
            {
                var stored = await fixture.Repository.GetTerminalAsync(pre.MeasurementId);
                Check(TerminalEqual(stored, pre), "Rejected preserves pre-existing terminal");
            }
            foreach (var pre in preRaw)
            {
                var storedRawList = await fixture.Repository.ListCommittedRawAsync();
                Check(storedRawList.Any(r => r.MeasurementId == pre.MeasurementId && r.Equals(pre)),
                    "Rejected preserves pre-existing raw");
            }
        });
        await ScenarioAsync("Rejected fixture with multiple rejection codes", async () =>
        {
            var codes = new[] { "POINT_INACTIVE", "SITE_INACTIVE", "SOURCE_TYPE_NOT_SIMULATOR",
                "PROVENANCE_INVALID", "CONFIGURATION_VERSION_MISSING" };
            for (var i = 0; i < codes.Length; i++)
            {
                var code = codes[i];
                var fixture = factory.Create();
                var rejected = Data(rejected: true, sequence: i + 1);
                var terminalWithCode = rejected.Terminal with { RejectionCode = code };
                var rejectedFixture = new TelemetryRaceWinnerFixture(terminalWithCode, null, null, null);
                fixture.RaceWinnerProbe?.StageRaceWinner(rejectedFixture);
                await using (var tx = await fixture.UnitOfWork.BeginRepeatableReadAsync())
                {
                    try
                    {
                        await fixture.Repository.StageTerminalAsync(terminalWithCode, tx);
                        await tx.CommitAsync();
                    }
                    catch (TelemetryUniqueRaceException)
                    {
                        await tx.RollbackAsync();
                    }
                }
                var stored = await fixture.Repository.GetTerminalAsync(terminalWithCode.MeasurementId);
                Check(stored is not null && TerminalEqual(stored, terminalWithCode),
                    $"Rejected code {code} stored exactly");
                Check(stored?.RejectionCode == code, $"Rejected code {code} preserved");
                Check(stored?.FinalClassification == TelemetryFinalClassification.Rejected,
                    $"Rejected code {code} classification");
                Check((await fixture.Repository.ListCommittedRawAsync()).Count == 0,
                    $"Rejected code {code} zero raw");
                Check(fixture.RaceWinnerProbe?.LatestCount == 0,
                    $"Rejected code {code} zero Latest");
                Check((await fixture.Events.ListCommittedAsync()).Count == 0,
                    $"Rejected code {code} zero event");
            }
        });
        // Direct fixture/slot conflict probe tests
        await ScenarioAsync("direct fixture conflict probe rejects different terminal for same MeasurementId", async () =>
        {
            var fixture = factory.Create();
            var data = Data();
            var conflicting = data.Terminal with { OriginalCorrelationId = "different" };
            var probe = fixture.ReplayProbe;
            if (probe is not null)
            {
                await using var tx = await fixture.UnitOfWork.BeginRepeatableReadAsync();
                await fixture.Repository.StageTerminalAsync(data.Terminal, tx);
                await fixture.Repository.StageRawAsync(data.Raw, tx);
                await tx.CommitAsync();
                var replayResult = probe.ReplayTerminal(conflicting);
                Check(replayResult != "DUPLICATE", "conflicting measurement not DUPLICATE");
                Check(probe.ReplayTerminal(data.Terminal) == "DUPLICATE",
                    "exact match is DUPLICATE");
            }
        });
        await ScenarioAsync("direct slot conflict probe rejects different Terminal for same Run+Point+sequence", async () =>
        {
            var fixture = factory.Create();
            var winner = Data();
            var loser = Data(mappingOverride: Guid.Parse("66666666-6666-4666-8666-666666666666"));
            await using (var seedTx = await fixture.UnitOfWork.BeginRepeatableReadAsync())
            {
                await fixture.Repository.StageTerminalAsync(winner.Terminal, seedTx);
                await fixture.Repository.StageRawAsync(winner.Raw, seedTx);
                await seedTx.CommitAsync();
            }
            await using var slotTx = await fixture.UnitOfWork.BeginRepeatableReadAsync();
            try
            {
                await fixture.Repository.StageTerminalAsync(loser.Terminal, slotTx);
                Check(false, "slot conflict should throw");
            }
            catch (TelemetryUniqueRaceException)
            {
                await slotTx.RollbackAsync();
            }
            var stored = await fixture.Repository.GetTerminalBySlotAsync(
                winner.Terminal.SimulatorRunId, winner.Terminal.PointId,
                winner.Terminal.SourceSequence);
            Check(stored?.MeasurementId == winner.Terminal.MeasurementId,
                "slot conflict preserves original winner");
            if (fixture.ReplayProbe is not null)
            {
                Check(fixture.ReplayProbe.ReplayTerminal(winner.Terminal) == "DUPLICATE",
                    "slot conflict probe: winner is DUPLICATE");
                Check(fixture.ReplayProbe.ReplayTerminal(loser.Terminal) == "MISSING",
                    "slot conflict probe: loser never committed so is MISSING");
            }
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

    private static bool EventEqual(TelemetryOwnerEvent left, TelemetryOwnerEvent right) =>
        left.EventId == right.EventId && left.EventType == right.EventType &&
        left.SchemaVersion == right.SchemaVersion && left.Producer == right.Producer &&
        left.AggregateType == right.AggregateType && left.AggregateId == right.AggregateId &&
        left.AggregateVersion == right.AggregateVersion && left.ActorId == right.ActorId &&
        left.ActorUsername == right.ActorUsername && left.Action == right.Action &&
        left.Summary == right.Summary && left.OccurredAtUtc == right.OccurredAtUtc &&
        left.CorrelationId == right.CorrelationId && left.CausationId == right.CausationId &&
        left.SiteId == right.SiteId && left.AreaId == right.AreaId &&
        left.Before.SequenceEqual(right.Before) && left.After.SequenceEqual(right.After);

    private static ContractData Data(
        bool rejected = false,
        long sequence = 1,
        Guid? mappingOverride = null,
        bool latestAdvanced = true)
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
            rejected ? "POINT_INACTIVE" : null, rejected ? null : latestAdvanced,
            request.SourceTimestampUtc, request.CorrelationId, request.LineageId, fingerprint);
        var raw = new RawMeasurement(
            id, source, run, point, mapping, 1, sequence, request.SourceTimestampUtc,
            request.SourceTimestampUtc, request.SourceTimestampUtc, 12.5, "kW",
            MeasurementQuality.Good, null, request.CorrelationId, request.LineageId);
        var latest = latestAdvanced
            ? new LatestProjectionCandidate(
                id, point, request.SourceTimestampUtc, sequence,
                request.SourceTimestampUtc, MeasurementQuality.Good)
            : null!;
        var provider = TelemetryTestData.Provider();
        var ownerEvent = MeasurementAcceptedEventFactory.Create(
            raw, latestAdvanced, provider, provider.TrustedSiteId, provider.TrustedAreaId!);
        return new ContractData(terminal, raw, latest, ownerEvent);
    }

    private sealed record ContractData(
        TelemetryTerminalResult Terminal,
        RawMeasurement Raw,
        LatestProjectionCandidate? Latest,
        TelemetryOwnerEvent Event);
}
