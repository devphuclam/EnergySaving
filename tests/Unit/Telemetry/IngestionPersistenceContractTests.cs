using IUMP.Modules.Telemetry.Contracts;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Telemetry;

public static class IngestionPersistenceContractTests
{
    public static int TestCount { get; private set; }
    public static int CheckCount { get; private set; }

    public static List<string> Run()
    {
        TestCount = 0;
        CheckCount = 0;
        var failures = new List<string>();
        Case("Accepted commits registry raw Latest event atomically", failures, () =>
        {
            var system = IngestionOrchestrationTests.Create();
            var result = IngestionOrchestrationTests.Execute(system, TelemetryTestData.Request());
            Check(result.Disposition == TelemetryDisposition.Accepted, "Accepted disposition", failures);
            Check(system.Store.ListCommittedTerminalsAsync().Result.Count == 1, "one registry", failures);
            Check(system.Store.ListCommittedRawAsync().Result.Count == 1, "one raw", failures);
            Check(system.Store.LatestCount == 1, "one Latest", failures);
            Check(system.Store.ListCommittedAsync().Result.Count == 1, "one event", failures);
            Check(system.Store.LastLockTrace.Select(item => item.Target).SequenceEqual(
                 [
                     TelemetryFlowLockTarget.OrganizationSite,
                     TelemetryFlowLockTarget.OrganizationArea,
                     TelemetryFlowLockTarget.OrganizationAsset,
                     TelemetryFlowLockTarget.OrganizationPoint,
                     TelemetryFlowLockTarget.CatalogSource,
                     TelemetryFlowLockTarget.CatalogMapping,
                     TelemetryFlowLockTarget.CatalogMetric,
                     TelemetryFlowLockTarget.CatalogUnit,
                     TelemetryFlowLockTarget.CatalogCompatibility,
                     TelemetryFlowLockTarget.TelemetryIdentityRawLatest,
                     TelemetryFlowLockTarget.IntegrationOutbox
                 ]), "exact lock order", failures);
        });
        Case("Rejected commits registry only", failures, () =>
        {
            var system = IngestionOrchestrationTests.Create();
            system.Providers.Snapshot = TelemetryTestData.Provider() with { PointActive = false };
            var result = IngestionOrchestrationTests.Execute(system, TelemetryTestData.Request());
            Check(result.Disposition == TelemetryDisposition.Rejected, "Rejected disposition", failures);
            Check(system.Store.ListCommittedTerminalsAsync().Result.Count == 1, "Rejected registry", failures);
            Check(system.Store.ListCommittedRawAsync().Result.Count == 0, "Rejected no raw", failures);
            Check(system.Store.LatestCount == 0, "Rejected no Latest", failures);
            Check(system.Store.ListCommittedAsync().Result.Count == 0, "Rejected no event", failures);
        });
        Case("pre-commit changes are invisible", failures, () =>
        {
            var store = new FakeTelemetryRepositories();
            var request = TelemetryTestData.Request();
            var terminal = TelemetryTestData.Terminal(request, TelemetryFinalClassification.Accepted);
            var raw = new RawMeasurement(
                terminal.MeasurementId, request.SourceId, request.SimulatorRunId,
                request.PointId, request.MappingId, request.MappingVersion,
                request.SourceSequence, request.SourceTimestampUtc,
                TelemetryTestData.Now, TelemetryTestData.Now, request.NumericValue,
                request.UnitCode, MeasurementQuality.Good, null,
                request.CorrelationId, request.LineageId);
            var tx = store.BeginRepeatableReadAsync().Result;
            store.StageTerminalAsync(terminal, tx).GetAwaiter().GetResult();
            store.StageRawAsync(raw, tx).GetAwaiter().GetResult();
            Check(store.ListCommittedTerminalsAsync().Result.Count == 0, "precommit invisible", failures);
            Check(store.ListCommittedRawAsync().Result.Count == 0, "precommit raw invisible", failures);
            tx.CommitAsync().GetAwaiter().GetResult();
            Check(store.ListCommittedTerminalsAsync().Result.Count == 1, "postcommit visible", failures);
            Check(store.ListCommittedRawAsync().Result.Count == 1, "postcommit raw visible", failures);
            tx.DisposeAsync().GetAwaiter().GetResult();
        });
        Case("every stage failure rolls back all local state", failures, () =>
        {
            foreach (var point in new[]
                     {
                         TelemetryFakeFailure.OrganizationLock,
                         TelemetryFakeFailure.CatalogLock,
                         TelemetryFakeFailure.TerminalInsert,
                         TelemetryFakeFailure.RawInsert,
                         TelemetryFakeFailure.Latest,
                         TelemetryFakeFailure.Outbox,
                         TelemetryFakeFailure.Commit
                     })
            {
                var system = IngestionOrchestrationTests.Create();
                system.Store.Failure = point;
                try
                {
                    IngestionOrchestrationTests.Execute(system, TelemetryTestData.Request());
                    failures.Add($"{point} did not fail");
                }
                catch (InvalidOperationException) { CheckCount++; }
                Check(system.Store.ListCommittedTerminalsAsync().Result.Count == 0, $"{point} registry rollback", failures);
                Check(system.Store.ListCommittedRawAsync().Result.Count == 0, $"{point} raw rollback", failures);
                Check(system.Store.LatestCount == 0, $"{point} latest rollback", failures);
                Check(system.Store.ListCommittedAsync().Result.Count == 0, $"{point} event rollback", failures);
            }
        });
        Case("matching unique-race winner returns Duplicate", failures, () =>
        {
            var system = IngestionOrchestrationTests.Create();
            var request = TelemetryTestData.Request();
            var winnerTerminal = TelemetryTestData.Terminal(
                request, TelemetryFinalClassification.Accepted);
            var winnerFixture = TelemetryTestData.RaceFixture(request, winnerTerminal);
            system.Store.RaceWinnerFixtureOnStage =
                winnerFixture;
            var result = IngestionOrchestrationTests.Execute(system, request);
            Check(result.Disposition == TelemetryDisposition.Duplicate, "race duplicate", failures);
            Check(system.Store.ListCommittedTerminalsAsync().Result.Count == 1, "winner only", failures);
            Check(system.Store.ListCommittedRawAsync().Result.Count == 1, "winner raw only", failures);
            Check(system.Store.LatestCount == 1, "winner Latest only", failures);
            Check(system.Store.ListCommittedAsync().Result.Count == 1, "winner event only", failures);
            var raw = system.Store.ListCommittedRawAsync().Result[0];
            Check(raw == winnerFixture.Raw, "winner raw is copied exactly", failures);
            var ev = system.Store.ListCommittedAsync().Result[0];
            Check(EventEqual(ev, winnerFixture.Event!), "winner event is copied exactly", failures);
            Check(system.Store.LatestCount == (winnerFixture.Latest is null ? 0 : 1),
                "winner Latest fixture is copied exactly", failures);
            Check(MeasurementIdentityRegistryTests.TerminalEqual(
                result.OriginalResult, winnerTerminal), "exact stored winner terminal", failures);
        });
        Case("conflicting unique-race winner returns conflict", failures, () =>
        {
            var system = IngestionOrchestrationTests.Create();
            var request = TelemetryTestData.Request();
            var winnerRequest = request with { NumericValue = 44 };
            var winnerTerminal = TelemetryTestData.Terminal(
                winnerRequest, TelemetryFinalClassification.Accepted);
            var winnerFixture = TelemetryTestData.RaceFixture(winnerRequest, winnerTerminal);
            system.Store.RaceWinnerFixtureOnStage =
                winnerFixture;
            var result = IngestionOrchestrationTests.Execute(system, request);
            Check(result.ErrorCode == "IDEMPOTENCY_CONFLICT", "race conflict", failures);
            Check(system.Store.ListCommittedRawAsync().Result.Count == 1, "conflict winner raw only", failures);
            Check(system.Store.ListCommittedAsync().Result.Count == 1, "conflict winner event only", failures);
            var raw = system.Store.ListCommittedRawAsync().Result[0];
            Check(raw == winnerFixture.Raw, "conflict winner raw is copied exactly", failures);
            Check(EventEqual(system.Store.ListCommittedAsync().Result.Single(), winnerFixture.Event!),
                "conflict winner event is copied exactly", failures);
        });
        Case("different-ID slot-race winner returns slot conflict", failures, () =>
        {
            var system = IngestionOrchestrationTests.Create();
            var winnerRequest = TelemetryTestData.Request();
            var winnerTerminal = TelemetryTestData.Terminal(
                winnerRequest, TelemetryFinalClassification.Accepted);
            var winnerFixture = TelemetryTestData.RaceFixture(winnerRequest, winnerTerminal);
            system.Store.RaceWinnerFixtureOnStage =
                winnerFixture;
            var mapping = Guid.Parse("66666666-6666-4666-8666-666666666666");
            var loserRequest = IngestionOrchestrationTests.WithIdentity(
                winnerRequest with { MappingId = mapping });
            system.Providers.Snapshot =
                TelemetryTestData.Provider() with { MappingId = mapping };
            var result = IngestionOrchestrationTests.Execute(system, loserRequest);
            Check(result.ErrorCode == "MEASUREMENT_SLOT_CONFLICT", "slot race conflict", failures);
            Check(system.Store.ListCommittedTerminalsAsync().Result.Count == 1, "slot winner only", failures);
            Check(MeasurementIdentityRegistryTests.TerminalEqual(
                system.Store.ListCommittedTerminalsAsync().Result[0], winnerTerminal),
                "slot race stores winner exactly", failures);
        });
        Case("rejected race winner has no synthesized raw/latest/event", failures, () =>
        {
            var system = IngestionOrchestrationTests.Create();
            var request = TelemetryTestData.Request();
            var rejected = TelemetryTestData.Terminal(request, TelemetryFinalClassification.Rejected);
            system.Store.RaceWinnerFixtureOnStage = TelemetryTestData.RaceFixture(request, rejected);
            var result = IngestionOrchestrationTests.Execute(system, request);
            Check(result.Disposition == TelemetryDisposition.Duplicate &&
                  result.OriginalResult?.FinalClassification == TelemetryFinalClassification.Rejected,
                "rejected race winner duplicate", failures);
            Check(system.Store.ListCommittedRawAsync().Result.Count == 0 &&
                  system.Store.LatestCount == 0 &&
                  system.Store.ListCommittedAsync().Result.Count == 0,
                "rejected winner has no dependent artifacts", failures);
        });
        // Invalid Accepted fixture cases — each proves zero partial publication
        foreach (var (name, makeFixture) in new (string, Func<TelemetryMeasurementRequest, TelemetryTerminalResult, TelemetryRaceWinnerFixture>)[]
        {
            ("Accepted fixture with null Raw", (req, term) =>
            {
                var f = TelemetryTestData.RaceFixture(req, term);
                return new TelemetryRaceWinnerFixture(f.Terminal, null, f.Latest, f.Event);
            }),
            ("Accepted fixture with Raw identity mismatch", (req, term) =>
            {
                var f = TelemetryTestData.RaceFixture(req, term);
                return new TelemetryRaceWinnerFixture(f.Terminal,
                    f.Raw! with { MeasurementId = Guid.NewGuid() }, f.Latest, f.Event);
            }),
            ("Accepted fixture with Latest missing when LatestAdvanced=true", (req, term) =>
            {
                var f = TelemetryTestData.RaceFixture(req, term);
                return new TelemetryRaceWinnerFixture(f.Terminal, f.Raw, null, f.Event);
            }),
            ("Accepted fixture with Latest present when LatestAdvanced=false", (req, term) =>
            {
                var f = TelemetryTestData.RaceFixture(req, term);
                return new TelemetryRaceWinnerFixture(
                    f.Terminal with { LatestAdvanced = false },
                    f.Raw, f.Latest, f.Event);
            }),
            ("Accepted fixture with Latest field mismatch", (req, term) =>
            {
                var f = TelemetryTestData.RaceFixture(req, term);
                return new TelemetryRaceWinnerFixture(f.Terminal, f.Raw,
                    f.Latest! with { QualityCode = MeasurementQuality.Uncertain }, f.Event);
            }),
            ("Accepted fixture with null Event", (req, term) =>
            {
                var f = TelemetryTestData.RaceFixture(req, term);
                return new TelemetryRaceWinnerFixture(f.Terminal, f.Raw, f.Latest, null);
            }),
            ("Accepted fixture with Event envelope mismatch", (req, term) =>
            {
                var f = TelemetryTestData.RaceFixture(req, term);
                return new TelemetryRaceWinnerFixture(f.Terminal, f.Raw, f.Latest,
                    f.Event! with { EventType = "Wrong.v1" });
            }),
            ("Accepted fixture with Event payload mismatch", (req, term) =>
            {
                var f = TelemetryTestData.RaceFixture(req, term);
                var badAfter = new Dictionary<string, object?>(f.Event!.After, StringComparer.Ordinal)
                    { ["unitCode"] = "KWH" };
                return new TelemetryRaceWinnerFixture(f.Terminal, f.Raw, f.Latest,
                    f.Event with { After = badAfter });
            }),
        })
        {
            Case($"invalid Accepted fixture: {name}", failures, () =>
            {
                var system = IngestionOrchestrationTests.Create();
                var request = TelemetryTestData.Request();
                var terminal = TelemetryTestData.Terminal(request, TelemetryFinalClassification.Accepted);
                Check(system.Store.ListCommittedTerminalsAsync().Result.Count == 0, "before: zero terminals", failures);
                Check(system.Store.ListCommittedRawAsync().Result.Count == 0, "before: zero raw", failures);
                Check(system.Store.LatestCount == 0, "before: zero Latest", failures);
                Check(system.Store.ListCommittedAsync().Result.Count == 0, "before: zero events", failures);
                var invalidFixture = makeFixture(request, terminal);
                system.Store.RaceWinnerFixtureOnStage = invalidFixture;
                try
                {
                    IngestionOrchestrationTests.Execute(system, request);
                    failures.Add($"{name}: expected failure");
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("RACE_WINNER_FIXTURE_INVALID"))
                {
                    CheckCount++;
                }
                Check(system.Store.ListCommittedTerminalsAsync().Result.Count == 0, $"{name}: terminal count unchanged", failures);
                Check(system.Store.ListCommittedRawAsync().Result.Count == 0, $"{name}: raw count unchanged", failures);
                Check(system.Store.LatestCount == 0, $"{name}: Latest unchanged", failures);
                Check(system.Store.ListCommittedAsync().Result.Count == 0, $"{name}: event count unchanged", failures);
            });
        }
        // Invalid Rejected fixture cases
        foreach (var (label, makeFixture) in new (string, Func<TelemetryMeasurementRequest, TelemetryTerminalResult, TelemetryRaceWinnerFixture>)[]
        {
            ("Rejected fixture with Raw present", (req, term) =>
                new TelemetryRaceWinnerFixture(term,
                    TelemetryTestData.RaceFixture(req, TelemetryTestData.Terminal(req, TelemetryFinalClassification.Accepted)).Raw,
                    null, null)),
            ("Rejected fixture with Latest present", (req, term) =>
                new TelemetryRaceWinnerFixture(term, null,
                    TelemetryTestData.RaceFixture(req, TelemetryTestData.Terminal(req, TelemetryFinalClassification.Accepted)).Latest,
                    null)),
            ("Rejected fixture with Event present", (req, term) =>
                new TelemetryRaceWinnerFixture(term, null, null,
                    TelemetryTestData.RaceFixture(req, TelemetryTestData.Terminal(req, TelemetryFinalClassification.Accepted)).Event)),
        })
        {
            Case($"invalid Rejected fixture: {label}", failures, () =>
            {
                var system = IngestionOrchestrationTests.Create();
                var request = TelemetryTestData.Request();
                var terminal = TelemetryTestData.Terminal(request, TelemetryFinalClassification.Rejected);
                Check(system.Store.ListCommittedTerminalsAsync().Result.Count == 0, "before: zero terminals", failures);
                var invalidFixture = makeFixture(request, terminal);
                system.Store.RaceWinnerFixtureOnStage = invalidFixture;
                try
                {
                    IngestionOrchestrationTests.Execute(system, request);
                    failures.Add($"{label}: expected failure");
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("RACE_WINNER_FIXTURE_INVALID"))
                {
                    CheckCount++;
                }
                Check(system.Store.ListCommittedTerminalsAsync().Result.Count == 0, $"{label}: terminal count unchanged", failures);
                Check(system.Store.ListCommittedRawAsync().Result.Count == 0, $"{label}: raw count unchanged", failures);
                Check(system.Store.LatestCount == 0, $"{label}: Latest unchanged", failures);
                Check(system.Store.ListCommittedAsync().Result.Count == 0, $"{label}: event count unchanged", failures);
            });
        }
        Case("Bad bypasses Latest seam and still persists raw", failures, () =>
        {
            var system = IngestionOrchestrationTests.Create();
            system.Store.Failure = TelemetryFakeFailure.Latest;
            var result = IngestionOrchestrationTests.Execute(
                system, TelemetryTestData.Request() with { NumericValue = 101 });
            Check(result.Disposition == TelemetryDisposition.Accepted, "Bad accepted", failures);
            Check(result.OriginalResult?.LatestAdvanced == false, "Bad false", failures);
            Check(system.Store.ListCommittedRawAsync().Result.Count == 1, "Bad raw", failures);
        });
        // Deep immutability: fingerprint mutation isolation
        Case("CommittedState terminal fingerprint mutation isolation", failures, () =>
        {
            var system = IngestionOrchestrationTests.Create();
            IngestionOrchestrationTests.Execute(system, TelemetryTestData.Request());
            var snapshot = system.Store.CommittedState;
            var terminal = snapshot.Terminals.Values.First();
            var original = terminal.RequestFingerprint.ToArray();
            terminal.RequestFingerprint[0] = 0xFF;
            var reread = system.Store.GetTerminalAsync(terminal.MeasurementId).Result;
            Check(reread!.RequestFingerprint.SequenceEqual(original), "internal fingerprint unchanged", failures);
        });
        // Deep immutability: event After dictionary isolation
        Case("event After dictionary mutation isolation", failures, () =>
        {
            var system = IngestionOrchestrationTests.Create();
            IngestionOrchestrationTests.Execute(system, TelemetryTestData.Request());
            var events = system.Store.ListCommittedAsync().Result;
            var after = events[0].After;
            var count = after.Count;
            var dict = new Dictionary<string, object?>(after, StringComparer.Ordinal) { ["injected"] = "malicious" };
            var reread = system.Store.ListCommittedAsync().Result;
            Check(!reread[0].After.ContainsKey("injected"), "event After dict immutable", failures);
            Check(reread[0].After.Count == count, "event After count unchanged", failures);
        });
        // Direct PublishRaceWinner existing-state: exact Accepted fixture repeated is no-op
        Case("direct PublishRaceWinner exact Accepted fixture repeated is no-op", failures, () =>
        {
            var store = new FakeTelemetryRepositories();
            var request = TelemetryTestData.Request();
            var terminal = TelemetryTestData.Terminal(request, TelemetryFinalClassification.Accepted);
            var fixture = TelemetryTestData.RaceFixture(request, terminal);
            store.RaceWinnerFixtureOnStage = fixture;
            var seedTx = store.BeginRepeatableReadAsync().GetAwaiter().GetResult();
            try { store.StageTerminalAsync(terminal, seedTx).GetAwaiter().GetResult(); } catch (TelemetryUniqueRaceException) { }
            seedTx.DisposeAsync().GetAwaiter().GetResult();
            var preTerminals = store.ListCommittedTerminalsAsync().Result;
            var preRaw = store.ListCommittedRawAsync().Result;
            var preLatestCount = store.LatestCount;
            var preEvents = store.ListCommittedAsync().Result;
            store.RaceWinnerFixtureOnStage = fixture;
            var repeatTx = store.BeginRepeatableReadAsync().GetAwaiter().GetResult();
            try { store.StageTerminalAsync(terminal, repeatTx).GetAwaiter().GetResult(); } catch (TelemetryUniqueRaceException) { }
            repeatTx.DisposeAsync().GetAwaiter().GetResult();
            Check(store.ListCommittedTerminalsAsync().Result.Count == preTerminals.Count, "no-op terminal count unchanged", failures);
            Check(MeasurementIdentityRegistryTests.TerminalEqual(
                store.ListCommittedTerminalsAsync().Result[0], preTerminals[0]), "no-op terminal unchanged", failures);
            Check(store.ListCommittedRawAsync().Result.Count == preRaw.Count, "no-op raw count unchanged", failures);
            Check(store.LatestCount == preLatestCount, "no-op Latest count unchanged", failures);
            Check(store.ListCommittedAsync().Result.Count == preEvents.Count, "no-op event count unchanged", failures);
            Check(EventEqual(store.ListCommittedAsync().Result[0], preEvents[0]), "no-op event unchanged", failures);
        });
        // Direct PublishRaceWinner existing-state: exact Rejected fixture repeated is no-op
        Case("direct PublishRaceWinner exact Rejected fixture repeated is no-op", failures, () =>
        {
            var store = new FakeTelemetryRepositories();
            var request = TelemetryTestData.Request();
            var terminal = TelemetryTestData.Terminal(request, TelemetryFinalClassification.Rejected);
            var fixture = TelemetryTestData.RaceFixture(request, terminal);
            store.RaceWinnerFixtureOnStage = fixture;
            var seedTx = store.BeginRepeatableReadAsync().GetAwaiter().GetResult();
            try { store.StageTerminalAsync(terminal, seedTx).GetAwaiter().GetResult(); } catch (TelemetryUniqueRaceException) { }
            seedTx.DisposeAsync().GetAwaiter().GetResult();
            var preTerminals = store.ListCommittedTerminalsAsync().Result;
            store.RaceWinnerFixtureOnStage = fixture;
            var repeatTx = store.BeginRepeatableReadAsync().GetAwaiter().GetResult();
            try { store.StageTerminalAsync(terminal, repeatTx).GetAwaiter().GetResult(); } catch (TelemetryUniqueRaceException) { }
            Check(store.ListCommittedTerminalsAsync().Result.Count == preTerminals.Count, "rejected no-op terminal count", failures);
            Check(MeasurementIdentityRegistryTests.TerminalEqual(
                store.ListCommittedTerminalsAsync().Result[0], preTerminals[0]), "rejected no-op terminal unchanged", failures);
            Check(store.ListCommittedRawAsync().Result.Count == 0, "rejected no-op raw=0", failures);
            Check(store.LatestCount == 0, "rejected no-op Latest=0", failures);
            Check(store.ListCommittedAsync().Result.Count == 0, "rejected no-op event=0", failures);
        });
        // Direct PublishRaceWinner existing-state: changed EventId → conflict
        Case("direct PublishRaceWinner changed EventId → RACE_WINNER_FIXTURE_CONFLICT", failures, () =>
        {
            var store = new FakeTelemetryRepositories();
            var request = TelemetryTestData.Request();
            var terminal = TelemetryTestData.Terminal(request, TelemetryFinalClassification.Accepted);
            var fixture = TelemetryTestData.RaceFixture(request, terminal);
            store.RaceWinnerFixtureOnStage = fixture;
            var seedTx = store.BeginRepeatableReadAsync().GetAwaiter().GetResult();
            try { store.StageTerminalAsync(terminal, seedTx).GetAwaiter().GetResult(); } catch (TelemetryUniqueRaceException) { }
            var changedFixture = fixture with { Event = fixture.Event! with { EventId = Guid.NewGuid() } };
            store.RaceWinnerFixtureOnStage = changedFixture;
            var conflictTx = store.BeginRepeatableReadAsync().GetAwaiter().GetResult();
            try { store.StageTerminalAsync(terminal, conflictTx).GetAwaiter().GetResult(); failures.Add("changed EventId should conflict"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("RACE_WINNER_FIXTURE_CONFLICT")) { CheckCount++; }
            Check(store.ListCommittedTerminalsAsync().Result.Count == 1, "conflict terminal count=1", failures);
            Check(store.ListCommittedRawAsync().Result.Count == 1, "conflict raw count=1", failures);
        });

        // Direct PublishRaceWinner existing-state: changed fingerprint → conflict
        Case("direct PublishRaceWinner changed fingerprint → RACE_WINNER_FIXTURE_CONFLICT", failures, () =>
        {
            var store = new FakeTelemetryRepositories();
            var request = TelemetryTestData.Request();
            var terminal = TelemetryTestData.Terminal(request, TelemetryFinalClassification.Accepted);
            var fixture = TelemetryTestData.RaceFixture(request, terminal);
            store.RaceWinnerFixtureOnStage = fixture;
            var seedTx = store.BeginRepeatableReadAsync().GetAwaiter().GetResult();
            try { store.StageTerminalAsync(terminal, seedTx).GetAwaiter().GetResult(); } catch (TelemetryUniqueRaceException) { }
            var changedFixture = fixture with { Terminal = fixture.Terminal with { RequestFingerprint = new byte[32] } };
            store.RaceWinnerFixtureOnStage = changedFixture;
            var conflictTx = store.BeginRepeatableReadAsync().GetAwaiter().GetResult();
            try { store.StageTerminalAsync(terminal, conflictTx).GetAwaiter().GetResult(); failures.Add("changed fingerprint should conflict"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("RACE_WINNER_FIXTURE_CONFLICT")) { CheckCount++; }
        });
        // Direct PublishRaceWinner existing-state: changed trusted Site → conflict
        Case("direct PublishRaceWinner changed trusted Site → RACE_WINNER_FIXTURE_CONFLICT", failures, () =>
        {
            var store = new FakeTelemetryRepositories();
            var request = TelemetryTestData.Request();
            var terminal = TelemetryTestData.Terminal(request, TelemetryFinalClassification.Accepted);
            var fixture = TelemetryTestData.RaceFixture(request, terminal);
            store.RaceWinnerFixtureOnStage = fixture;
            var seedTx = store.BeginRepeatableReadAsync().GetAwaiter().GetResult();
            try { store.StageTerminalAsync(terminal, seedTx).GetAwaiter().GetResult(); } catch (TelemetryUniqueRaceException) { }
            var changedFixture = fixture with { Event = fixture.Event! with { SiteId = "other-site" } };
            store.RaceWinnerFixtureOnStage = changedFixture;
            var conflictTx = store.BeginRepeatableReadAsync().GetAwaiter().GetResult();
            try { store.StageTerminalAsync(terminal, conflictTx).GetAwaiter().GetResult(); failures.Add("changed Site should conflict"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("RACE_WINNER_FIXTURE_CONFLICT")) { CheckCount++; }
        });
        // Direct race-winner slot conflict: different MeasurementId, same Run+Point+sequence
        Case("direct PublishRaceWinner slot conflict → RACE_WINNER_SLOT_CONFLICT", failures, () =>
        {
            var store = new FakeTelemetryRepositories();
            var requestA = TelemetryTestData.Request();
            var terminalA = TelemetryTestData.Terminal(requestA, TelemetryFinalClassification.Accepted);
            var fixtureA = TelemetryTestData.RaceFixture(requestA, terminalA);
            store.RaceWinnerFixtureOnStage = fixtureA;
            var seedTx = store.BeginRepeatableReadAsync().GetAwaiter().GetResult();
            try { store.StageTerminalAsync(terminalA, seedTx).GetAwaiter().GetResult(); } catch (TelemetryUniqueRaceException) { }
            var mappingB = Guid.Parse("66666666-6666-4666-8666-666666666666");
            var requestB = IngestionOrchestrationTests.WithIdentity(
                TelemetryTestData.Request() with { MappingId = mappingB });
            var terminalB = TelemetryTestData.Terminal(requestB, TelemetryFinalClassification.Accepted);
            var fixtureB = TelemetryTestData.RaceFixture(requestB, terminalB);
            store.RaceWinnerFixtureOnStage = fixtureB;
            var slotTx = store.BeginRepeatableReadAsync().GetAwaiter().GetResult();
            try { store.StageTerminalAsync(terminalB, slotTx).GetAwaiter().GetResult(); failures.Add("slot conflict should throw"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("RACE_WINNER_SLOT_CONFLICT")) { CheckCount++; }
            Check(store.ListCommittedTerminalsAsync().Result.Count == 1, "slot original winner count=1", failures);
            Check(MeasurementIdentityRegistryTests.TerminalEqual(
                store.ListCommittedTerminalsAsync().Result[0], terminalA), "slot original winner unchanged", failures);
        });
        // Valid Rejected invalid-fixture matrix: Rejected terminal using Data(rejected: true)
        foreach (var (label, makeFixture) in new (string, Func<TelemetryMeasurementRequest, TelemetryTerminalResult, TelemetryRaceWinnerFixture>)[]
        {
            ("Rejected with Raw uses Data(rejected:true) + attaches Raw", (req, term) =>
                new TelemetryRaceWinnerFixture(term,
                    TelemetryTestData.RaceFixture(req, TelemetryTestData.Terminal(req, TelemetryFinalClassification.Accepted)).Raw,
                    null, null)),
            ("Rejected with Latest uses Data(rejected:true) + attaches Latest", (req, term) =>
                new TelemetryRaceWinnerFixture(term, null,
                    TelemetryTestData.RaceFixture(req, TelemetryTestData.Terminal(req, TelemetryFinalClassification.Accepted)).Latest,
                    null)),
            ("Rejected with Event uses Data(rejected:true) + attaches Event", (req, term) =>
                new TelemetryRaceWinnerFixture(term, null, null,
                    TelemetryTestData.RaceFixture(req, TelemetryTestData.Terminal(req, TelemetryFinalClassification.Accepted)).Event)),
        })
        {
            Case($"valid Rejected invalid fixture: {label}", failures, () =>
            {
                var system = IngestionOrchestrationTests.Create();
                var request = TelemetryTestData.Request();
                var terminal = TelemetryTestData.Terminal(request, TelemetryFinalClassification.Rejected);
                Check(system.Store.ListCommittedTerminalsAsync().Result.Count == 0, "before: zero terminals", failures);
                Check(terminal.FinalClassification == TelemetryFinalClassification.Rejected, "terminal is Rejected", failures);
                Check(terminal.MeasurementPersisted == false, "Rejected MeasurementPersisted=false", failures);
                Check(terminal.PersistedMeasurementId is null, "Rejected PersistedMeasurementId null", failures);
                Check(terminal.LatestAdvanced is null, "Rejected LatestAdvanced null", failures);
                Check(terminal.RejectionCode is not null, "Rejected has RejectionCode", failures);
                var invalidFixture = makeFixture(request, terminal);
                system.Store.RaceWinnerFixtureOnStage = invalidFixture;
                try
                {
                    IngestionOrchestrationTests.Execute(system, request);
                    failures.Add($"{label}: expected failure");
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("RACE_WINNER_FIXTURE_INVALID"))
                {
                    CheckCount++;
                }
                Check(system.Store.ListCommittedTerminalsAsync().Result.Count == 0, $"{label}: terminal count unchanged", failures);
                Check(system.Store.ListCommittedRawAsync().Result.Count == 0, $"{label}: raw count unchanged", failures);
                Check(system.Store.LatestCount == 0, $"{label}: Latest unchanged", failures);
                Check(system.Store.ListCommittedAsync().Result.Count == 0, $"{label}: event count unchanged", failures);
            });
        }
        // Complete pre-existing state preservation: all fields compared after conflict
        Case("pre-existing state preserved after fixture conflict", failures, () =>
        {
            var store = new FakeTelemetryRepositories();
            var request = TelemetryTestData.Request();
            var terminal = TelemetryTestData.Terminal(request, TelemetryFinalClassification.Accepted);
            var fixture = TelemetryTestData.RaceFixture(request, terminal);
            store.RaceWinnerFixtureOnStage = fixture;
            var seedTx = store.BeginRepeatableReadAsync().GetAwaiter().GetResult();
            try { store.StageTerminalAsync(terminal, seedTx).GetAwaiter().GetResult(); } catch (TelemetryUniqueRaceException) { }
            var preTerminal = store.ListCommittedTerminalsAsync().Result[0];
            var preFingerprint = preTerminal.RequestFingerprint.ToArray();
            var preRaw = store.ListCommittedRawAsync().Result[0];
            var preEvents = store.ListCommittedAsync().Result[0];
            var preLatest = store.LatestCount > 0 ? store.GetCommittedLatestAsync(terminal.PointId).Result : null;
            var changedFixture = fixture with { Event = fixture.Event! with { EventId = Guid.NewGuid() } };
            store.RaceWinnerFixtureOnStage = changedFixture;
            var conflictTx = store.BeginRepeatableReadAsync().GetAwaiter().GetResult();
            try { store.StageTerminalAsync(terminal, conflictTx).GetAwaiter().GetResult(); } catch (InvalidOperationException) { }
            var postTerminal = store.ListCommittedTerminalsAsync().Result[0];
            Check(preTerminal.MeasurementId == postTerminal.MeasurementId, "preserved MeasurementId", failures);
            Check(preTerminal.FinalClassification == postTerminal.FinalClassification, "preserved FinalClassification", failures);
            Check(preTerminal.MeasurementPersisted == postTerminal.MeasurementPersisted, "preserved MeasurementPersisted", failures);
            Check(preTerminal.SourceId == postTerminal.SourceId, "preserved SourceId", failures);
            Check(preTerminal.PointId == postTerminal.PointId, "preserved PointId", failures);
            Check(preTerminal.CompletedAtUtc == postTerminal.CompletedAtUtc, "preserved CompletedAtUtc", failures);
            Check(preTerminal.OriginalCorrelationId == postTerminal.OriginalCorrelationId, "preserved CorrelationId", failures);
            Check(preFingerprint.SequenceEqual(postTerminal.RequestFingerprint), "preserved fingerprint", failures);
            var postRaw = store.ListCommittedRawAsync().Result[0];
            Check(preRaw.MeasurementId == postRaw.MeasurementId && preRaw.NumericValue == postRaw.NumericValue, "preserved Raw", failures);
            Check(EventEqual(preEvents, store.ListCommittedAsync().Result[0]), "preserved event all fields", failures);
            var postLatest = store.LatestCount > 0 ? store.GetCommittedLatestAsync(terminal.PointId).Result : null;
            Check((preLatest is null) == (postLatest is null), "preserved Latest existence", failures);
        });
        // Commit-time concurrency: same Measurement ID, two transactions
        Case("commit-time same Measurement ID race", failures, () =>
        {
            var store = new FakeTelemetryRepositories();
            var request = TelemetryTestData.Request();
            var terminal = TelemetryTestData.Terminal(request, TelemetryFinalClassification.Accepted);
            var raw = new RawMeasurement(terminal.MeasurementId, terminal.SourceId, terminal.SimulatorRunId,
                terminal.PointId, terminal.MappingId, terminal.MappingVersion, terminal.SourceSequence,
                TelemetryTestData.Now, TelemetryTestData.Now, TelemetryTestData.Now,
                12.5, "kW", MeasurementQuality.Good, null, request.CorrelationId, request.LineageId);
            var txA = store.BeginRepeatableReadAsync().GetAwaiter().GetResult();
            store.StageTerminalAsync(terminal, txA).GetAwaiter().GetResult();
            store.StageRawAsync(raw, txA).GetAwaiter().GetResult();
            var txB = store.BeginRepeatableReadAsync().GetAwaiter().GetResult();
            store.StageTerminalAsync(terminal, txB).GetAwaiter().GetResult();
            store.StageRawAsync(raw, txB).GetAwaiter().GetResult();
            txA.CommitAsync().GetAwaiter().GetResult();
            try
            {
                txB.CommitAsync().GetAwaiter().GetResult();
                failures.Add("same-ID commit B should throw TelemetryUniqueRaceException");
            }
            catch (TelemetryUniqueRaceException)
            {
                txB.RollbackAsync().GetAwaiter().GetResult();
            }
            Check(store.ListCommittedTerminalsAsync().Result.Count == 1, "same-ID race: one terminal", failures);
            Check(store.ListCommittedRawAsync().Result.Count == 1, "same-ID race: one raw", failures);
        });
        // Commit-time concurrency: same slot, different Measurement IDs
        Case("commit-time same slot race", failures, () =>
        {
            var store = new FakeTelemetryRepositories();
            var requestA = TelemetryTestData.Request();
            var terminalA = TelemetryTestData.Terminal(requestA, TelemetryFinalClassification.Accepted);
            var rawA = new RawMeasurement(terminalA.MeasurementId, terminalA.SourceId, terminalA.SimulatorRunId,
                terminalA.PointId, terminalA.MappingId, terminalA.MappingVersion, terminalA.SourceSequence,
                TelemetryTestData.Now, TelemetryTestData.Now, TelemetryTestData.Now,
                12.5, "kW", MeasurementQuality.Good, null, requestA.CorrelationId, requestA.LineageId);
            var mappingB = Guid.Parse("66666666-6666-4666-8666-666666666666");
            var requestB = IngestionOrchestrationTests.WithIdentity(
                TelemetryTestData.Request() with { MappingId = mappingB });
            var terminalB = TelemetryTestData.Terminal(requestB, TelemetryFinalClassification.Accepted);
            var rawB = new RawMeasurement(terminalB.MeasurementId, terminalB.SourceId, terminalB.SimulatorRunId,
                terminalB.PointId, terminalB.MappingId, terminalB.MappingVersion, terminalB.SourceSequence,
                TelemetryTestData.Now, TelemetryTestData.Now, TelemetryTestData.Now,
                12.5, "kW", MeasurementQuality.Good, null, requestB.CorrelationId, requestB.LineageId);
            var txA = store.BeginRepeatableReadAsync().GetAwaiter().GetResult();
            store.StageTerminalAsync(terminalA, txA).GetAwaiter().GetResult();
            store.StageRawAsync(rawA, txA).GetAwaiter().GetResult();
            var txB = store.BeginRepeatableReadAsync().GetAwaiter().GetResult();
            store.StageTerminalAsync(terminalB, txB).GetAwaiter().GetResult();
            store.StageRawAsync(rawB, txB).GetAwaiter().GetResult();
            txA.CommitAsync().GetAwaiter().GetResult();
            try
            {
                txB.CommitAsync().GetAwaiter().GetResult();
                failures.Add("same-slot commit B should throw TelemetryUniqueRaceException");
            }
            catch (TelemetryUniqueRaceException)
            {
                txB.RollbackAsync().GetAwaiter().GetResult();
            }
            Check(store.ListCommittedTerminalsAsync().Result.Count == 1, "same-slot race: one terminal", failures);
            Check(MeasurementIdentityRegistryTests.TerminalEqual(
                store.ListCommittedTerminalsAsync().Result[0], terminalA), "same-slot race: A wins", failures);
            Check(store.ListCommittedRawAsync().Result.Count == 1, "same-slot race: one raw", failures);
        });
        // Commit-time concurrency: independent slots, no lost update
        Case("commit-time independent slots no lost update", failures, () =>
        {
            var store = new FakeTelemetryRepositories();
            var requestA = TelemetryTestData.Request();
            var terminalA = TelemetryTestData.Terminal(requestA, TelemetryFinalClassification.Accepted);
            var rawA = new RawMeasurement(terminalA.MeasurementId, terminalA.SourceId, terminalA.SimulatorRunId,
                terminalA.PointId, terminalA.MappingId, terminalA.MappingVersion, 1,
                TelemetryTestData.Now, TelemetryTestData.Now, TelemetryTestData.Now,
                12.5, "kW", MeasurementQuality.Good, null, requestA.CorrelationId, requestA.LineageId);
            var requestB = IngestionOrchestrationTests.WithIdentity(
                TelemetryTestData.Request() with { SourceSequence = 2 });
            var terminalB = TelemetryTestData.Terminal(requestB, TelemetryFinalClassification.Accepted);
            var rawB = new RawMeasurement(terminalB.MeasurementId, terminalB.SourceId, terminalB.SimulatorRunId,
                terminalB.PointId, terminalB.MappingId, terminalB.MappingVersion, 2,
                TelemetryTestData.Now, TelemetryTestData.Now, TelemetryTestData.Now,
                12.5, "kW", MeasurementQuality.Good, null, requestB.CorrelationId, requestB.LineageId);
            var txA = store.BeginRepeatableReadAsync().GetAwaiter().GetResult();
            store.StageTerminalAsync(terminalA, txA).GetAwaiter().GetResult();
            store.StageRawAsync(rawA, txA).GetAwaiter().GetResult();
            var txB = store.BeginRepeatableReadAsync().GetAwaiter().GetResult();
            store.StageTerminalAsync(terminalB, txB).GetAwaiter().GetResult();
            store.StageRawAsync(rawB, txB).GetAwaiter().GetResult();
            txA.CommitAsync().GetAwaiter().GetResult();
            txB.CommitAsync().GetAwaiter().GetResult();
            Check(store.ListCommittedTerminalsAsync().Result.Count == 2, "independent: two terminals", failures);
            Check(store.ListCommittedRawAsync().Result.Count == 2, "independent: two raw", failures);
        });
        return failures;
    }

    private static void Case(string name, List<string> failures, Action action)
    {
        TestCount++;
        try { action(); }
        catch (Exception ex) { failures.Add($"{name}: {ex.GetType().Name}: {ex.Message}"); }
    }

    private static void Check(bool condition, string message, List<string> failures)
    {
        CheckCount++;
        if (!condition) failures.Add(message);
    }

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
}
