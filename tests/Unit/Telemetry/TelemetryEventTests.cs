using IUMP.Modules.Telemetry.Application;
using IUMP.Modules.Telemetry.Contracts;

namespace IUMP.Tests.Unit.Telemetry;

public static class TelemetryEventTests
{
    public static int TestCount { get; private set; }
    public static int CheckCount { get; private set; }

    public static List<string> Run()
    {
        TestCount = 0;
        CheckCount = 0;
        var failures = new List<string>();
        Case("MeasurementAccepted.v1 envelope", failures, () =>
        {
            var evt = Create();
            Check(evt.EventId != Guid.Empty, "EventId", failures);
            Check(evt.EventType == "MeasurementAccepted.v1" && evt.SchemaVersion == 1, "version", failures);
            Check(evt.Producer == "IUMP.Telemetry", "producer", failures);
            Check(evt.AggregateType == "Measurement" &&
                  evt.AggregateId == Guid.Parse(TelemetryTestData.Request().MeasurementId),
                "aggregate", failures);
            Check(evt.AggregateVersion == 1, "aggregate version", failures);
            Check(evt.ActorId == "IUMP.Telemetry" && evt.ActorUsername == "trusted-simulator",
                "safe actor", failures);
            Check(evt.Action == "Accepted" && !string.IsNullOrWhiteSpace(evt.Summary), "action", failures);
            Check(evt.OccurredAtUtc.Kind == DateTimeKind.Utc, "UTC", failures);
            Check(evt.CorrelationId == "correlation-1" && evt.CausationId is null,
                "correlation/causation", failures);
            var provider = TelemetryTestData.Provider();
            Check(evt.SiteId == provider.TrustedSiteId && evt.AreaId == provider.TrustedAreaId, "trusted scope", failures);
        });
        Case("safe After allowlist", failures, () =>
        {
            var expected = new[]
            {
                "measurementId", "sourceId", "simulatorRunId", "pointId", "mappingId",
                "mappingVersion", "sourceSequence", "sourceTimestampUtc", "receivedAtUtc",
                "processingAtUtc", "numericValue", "unitCode", "qualityCode", "reasonCode",
                "latestAdvanced", "correlationId", "lineageId"
            };
            Check(Create().After.Keys.Order().SequenceEqual(expected.Order()), "exact allowlist", failures);
        });
        Case("Before is empty and sensitive fields absent", failures, () =>
        {
            var evt = Create();
            Check(evt.Before.Count == 0, "Before empty", failures);
            foreach (var forbidden in new[]
                     {
                         "requestFingerprint", "credential", "token", "cookie",
                         "connectionString", "principal", "prngState"
                     })
                Check(!evt.After.ContainsKey(forbidden), $"forbidden {forbidden}", failures);
        });
        Case("Rejected emits no accepted event", failures, () =>
        {
            var system = IngestionOrchestrationTests.Create();
            system.Providers.Snapshot = TelemetryTestData.Provider() with { PointActive = false };
            IngestionOrchestrationTests.Execute(system, TelemetryTestData.Request());
            Check(system.Store.ListCommittedAsync().Result.Count == 0, "Rejected no event", failures);
        });
        Case("Duplicate emits no second event", failures, () =>
        {
            var system = IngestionOrchestrationTests.Create();
            IngestionOrchestrationTests.Execute(system, TelemetryTestData.Request());
            IngestionOrchestrationTests.Execute(system, TelemetryTestData.Request());
            Check(system.Store.ListCommittedAsync().Result.Count == 1, "Duplicate no second event", failures);
        });
        Case("conflict emits no event", failures, () =>
        {
            var system = IngestionOrchestrationTests.Create();
            var request = TelemetryTestData.Request();
            IngestionOrchestrationTests.Execute(system, request);
            var conflict = IngestionOrchestrationTests.Execute(
                system, request with { NumericValue = 99 });
            Check(conflict.ErrorCode == "IDEMPOTENCY_CONFLICT", "conflict result", failures);
            Check(system.Store.ListCommittedAsync().Result.Count == 1, "conflict no event", failures);
        });
        Case("pre-addressable failures emit no event", failures, () =>
        {
            var system = IngestionOrchestrationTests.Create();
            IngestionOrchestrationTests.Execute(system, TelemetryTestData.Request(),
                IngestionOrchestrationTests.Trusted() with { IsTrusted = false });
            Check(system.Store.ListCommittedAsync().Result.Count == 0, "untrusted no event", failures);
        });
        Case("scope mismatch produces no event and factory rejects untrusted scope", failures, () =>
        {
            var provider = TelemetryTestData.Provider();
            var mismatched = provider with { SiteId = "different-site" };
            var scopeMismatch = TelemetryPersistenceService.CheckTrustedScope(mismatched, "corr");
            Check(scopeMismatch is not null && scopeMismatch.ErrorCode == "PROVIDER_SCOPE_MISMATCH",
                "scope mismatch result error", failures);
            var system = IngestionOrchestrationTests.Create();
            system.Providers.Snapshot = mismatched;
            var result = IngestionOrchestrationTests.Execute(system, TelemetryTestData.Request());
            Check(result.Disposition == TelemetryDisposition.Failed &&
                  result.ErrorCode == "PROVIDER_SCOPE_MISMATCH",
                "scope mismatch stable disposition", failures);
            Check(system.Store.ListCommittedTerminalsAsync().Result.Count == 0,
                "scope mismatch no terminal", failures);
            Check(system.Store.ListCommittedAsync().Result.Count == 0,
                "scope mismatch no event", failures);
        });
        Case("factory rejects blank eventSiteId", failures, () =>
        {
            var request = TelemetryTestData.Request();
            var raw = new RawMeasurement(
                Guid.Parse(request.MeasurementId), request.SourceId, request.SimulatorRunId,
                request.PointId, request.MappingId, request.MappingVersion, request.SourceSequence,
                request.SourceTimestampUtc, TelemetryTestData.Now, TelemetryTestData.Now,
                request.NumericValue, request.UnitCode, MeasurementQuality.Good, null,
                request.CorrelationId, request.LineageId);
            var provider = TelemetryTestData.Provider();
            try
            {
                MeasurementAcceptedEventFactory.Create(raw, true, provider, "", provider.TrustedAreaId!);
                failures.Add("blank eventSiteId should throw");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("EVENT_SCOPE_ID_BLANK"))
            {
                CheckCount++;
            }
        });
        Case("factory rejects blank eventAreaId", failures, () =>
        {
            var request = TelemetryTestData.Request();
            var raw = new RawMeasurement(
                Guid.Parse(request.MeasurementId), request.SourceId, request.SimulatorRunId,
                request.PointId, request.MappingId, request.MappingVersion, request.SourceSequence,
                request.SourceTimestampUtc, TelemetryTestData.Now, TelemetryTestData.Now,
                request.NumericValue, request.UnitCode, MeasurementQuality.Good, null,
                request.CorrelationId, request.LineageId);
            var provider = TelemetryTestData.Provider();
            try
            {
                MeasurementAcceptedEventFactory.Create(raw, true, provider, provider.TrustedSiteId, "");
                failures.Add("blank eventAreaId should throw");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("EVENT_SCOPE_ID_BLANK"))
            {
                CheckCount++;
            }
        });
        Case("factory rejects mismatched trusted scope", failures, () =>
        {
            var request = TelemetryTestData.Request();
            var raw = new RawMeasurement(
                Guid.Parse(request.MeasurementId), request.SourceId, request.SimulatorRunId,
                request.PointId, request.MappingId, request.MappingVersion, request.SourceSequence,
                request.SourceTimestampUtc, TelemetryTestData.Now, TelemetryTestData.Now,
                request.NumericValue, request.UnitCode, MeasurementQuality.Good, null,
                request.CorrelationId, request.LineageId);
            var provider = TelemetryTestData.Provider();
            try
            {
                MeasurementAcceptedEventFactory.Create(
                    raw, true, provider, "wrong-site", provider.TrustedAreaId!);
                failures.Add("mismatched eventSiteId should throw");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("EVENT_TRUSTED_SCOPE_MISMATCH"))
            {
                CheckCount++;
            }
        });
        Case("factory rejects mismatched trusted area", failures, () =>
        {
            var request = TelemetryTestData.Request();
            var raw = new RawMeasurement(
                Guid.Parse(request.MeasurementId), request.SourceId, request.SimulatorRunId,
                request.PointId, request.MappingId, request.MappingVersion, request.SourceSequence,
                request.SourceTimestampUtc, TelemetryTestData.Now, TelemetryTestData.Now,
                request.NumericValue, request.UnitCode, MeasurementQuality.Good, null,
                request.CorrelationId, request.LineageId);
            var provider = TelemetryTestData.Provider();
            try
            {
                MeasurementAcceptedEventFactory.Create(
                    raw, true, provider, provider.TrustedSiteId, "wrong-area");
                failures.Add("mismatched eventAreaId should throw");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("EVENT_TRUSTED_SCOPE_MISMATCH"))
            {
                CheckCount++;
            }
        });
        Case("PointLatestAdvanced.v1 is not implemented", failures, () =>
        {
            var system = IngestionOrchestrationTests.Create();
            IngestionOrchestrationTests.Execute(system, TelemetryTestData.Request());
            Check(system.Store.ListCommittedAsync().Result.All(
                    evt => evt.EventType != "PointLatestAdvanced.v1"),
                "no Phase 8 event", failures);
        });
        return failures;
    }

    private static TelemetryOwnerEvent Create()
    {
        var request = TelemetryTestData.Request();
        var raw = new RawMeasurement(
            Guid.Parse(request.MeasurementId), request.SourceId, request.SimulatorRunId,
            request.PointId, request.MappingId, request.MappingVersion, request.SourceSequence,
            request.SourceTimestampUtc, TelemetryTestData.Now, TelemetryTestData.Now,
            request.NumericValue, request.UnitCode, MeasurementQuality.Good, null,
            request.CorrelationId, request.LineageId);
        var provider = TelemetryTestData.Provider();
        return MeasurementAcceptedEventFactory.Create(
            raw, true, provider, provider.TrustedSiteId, provider.TrustedAreaId!);
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
}
