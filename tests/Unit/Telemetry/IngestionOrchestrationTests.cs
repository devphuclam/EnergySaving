using IUMP.Modules.Telemetry.Application;
using IUMP.Modules.Telemetry.Contracts;
using IUMP.Modules.Telemetry.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Telemetry;

public static class IngestionOrchestrationTests
{
    public static int TestCount { get; private set; }
    public static int CheckCount { get; private set; }

    public static List<string> Run()
    {
        TestCount = 0;
        CheckCount = 0;
        var failures = new List<string>();
        Case("untrusted producer is pre-addressable", failures, () =>
        {
            var system = Create();
            var result = Execute(system, TelemetryTestData.Request(),
                Trusted() with { IsTrusted = false });
            Check(result.ErrorCode == "UNTRUSTED_PRODUCER", "untrusted result", failures);
            Check(system.Store.ListCommittedTerminalsAsync().Result.Count == 0, "untrusted no registry", failures);
            Check(system.Providers.Reads == 0, "untrusted no provider lookup", failures);
        });
        Case("malformed and mismatched identity are pre-addressable", failures, () =>
        {
            foreach (var id in new[] { "", "bad", Guid.NewGuid().ToString("D") })
            {
                var system = Create();
                var result = Execute(system, TelemetryTestData.Request() with { MeasurementId = id });
                Check(result.ErrorCode == "MEASUREMENT_ID_INVALID", "invalid identity result", failures);
                Check(system.Store.ListCommittedTerminalsAsync().Result.Count == 0, "invalid no registry", failures);
            }
        });
        Case("nonfinite values create stable Rejected terminal", failures, () =>
        {
            foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                var system = Create();
                var result = Execute(system, TelemetryTestData.Request() with { NumericValue = value });
                Check(result.Disposition == TelemetryDisposition.Rejected, "nonfinite rejected", failures);
                Check(result.OriginalResult?.RejectionCode == "NUMERIC_VALUE_NONFINITE", "nonfinite code", failures);
                Check(system.Store.ListCommittedTerminalsAsync().Result.Count == 1, "nonfinite registry created", failures);
                Check(system.Store.ListCommittedRawAsync().Result.Count == 0, "nonfinite no raw", failures);
                // Same ID + same nonfinite value = Duplicate
                var dup = Execute(system, TelemetryTestData.Request() with { NumericValue = value });
                Check(dup.Disposition == TelemetryDisposition.Duplicate, "nonfinite duplicate", failures);
                // Same ID + different nonfinite value = IDEMPOTENCY_CONFLICT
                var differentValue = double.IsNaN(value) ? double.PositiveInfinity : double.NaN;
                var conflict = Execute(system, TelemetryTestData.Request() with
                {
                    NumericValue = differentValue,
                    CorrelationId = "conflict-correlation"
                });
                Check(conflict.ErrorCode == "IDEMPOTENCY_CONFLICT", "nonfinite conflict", failures);
            }
        });
        Case("null static string fields produce stable Rejected terminal", failures, () =>
        {
            foreach (var field in new[] { "AlgorithmId", "UnitCode", "CorrelationId", "LineageId" })
            {
                var request = field switch
                {
                    "AlgorithmId" => TelemetryTestData.Request() with { AlgorithmId = null! },
                    "UnitCode" => TelemetryTestData.Request() with { UnitCode = null! },
                    "CorrelationId" => TelemetryTestData.Request() with { CorrelationId = null! },
                    "LineageId" => TelemetryTestData.Request() with { LineageId = null! },
                    _ => TelemetryTestData.Request()
                };
                var system = Create();
                var result = Execute(system, request);
                Check(result.Disposition == TelemetryDisposition.Rejected, $"null {field} rejected", failures);
                Check(result.OriginalResult?.RejectionCode == "PROVENANCE_INVALID", $"null {field} provenance code", failures);
                Check(system.Store.ListCommittedTerminalsAsync().Result.Count == 1, $"null {field} registry", failures);
                Check(system.Store.ListCommittedRawAsync().Result.Count == 0, $"null {field} no raw", failures);
                // Same exact null+payload = Duplicate
                var dup = Execute(system, request);
                Check(dup.Disposition == TelemetryDisposition.Duplicate, $"null {field} duplicate", failures);
            }
        });
        Case("static payload failures become stable Rejected", failures, () =>
        {
            var requests = new[]
            {
                TelemetryTestData.Request() with { CorrelationId = "" },
                TelemetryTestData.Request() with { UnitCode = "" },
                TelemetryTestData.Request() with { AlgorithmId = "" },
                TelemetryTestData.Request() with { SourceTimestampUtc = DateTime.SpecifyKind(TelemetryTestData.Now, DateTimeKind.Local) }
            };
            foreach (var request in requests)
            {
                var system = Create();
                var result = Execute(system, request);
                Check(result.Disposition == TelemetryDisposition.Rejected, "static rejected", failures);
                Check(system.Store.ListCommittedRawAsync().Result.Count == 0, "static rejected no raw", failures);
            }
        });
        Case("invalid required identity shape is pre-addressable", failures, () =>
        {
            var requests = new[]
            {
                WithIdentity(TelemetryTestData.Request() with { SourceSequence = -1 }),
                WithIdentity(TelemetryTestData.Request() with { MappingVersion = 0 }),
                WithIdentity(TelemetryTestData.Request() with { ConfigurationVersion = 0 }),
                WithIdentity(TelemetryTestData.Request() with { AlgorithmVersion = 0 })
            };
            foreach (var request in requests)
            {
                var system = Create();
                var result = Execute(system, request);
                Check(result.ErrorCode == "MEASUREMENT_ID_INVALID", "identity shape error", failures);
                Check(system.Store.ListCommittedTerminalsAsync().Result.Count == 0, "identity shape no registry", failures);
            }
        });
        Case("missing immutable configuration is stable Rejected", failures, () =>
        {
            var system = Create();
            system.Configurations.Snapshot = null;
            var result = Execute(system, TelemetryTestData.Request());
            Check(result.Disposition == TelemetryDisposition.Rejected, "configuration rejected", failures);
            Check(result.OriginalResult?.RejectionCode == "CONFIGURATION_VERSION_MISSING",
                "configuration code", failures);
        });
        Case("provider validation matrix", failures, () =>
        {
            var baseline = TelemetryTestData.Provider();
            var variants = new (TelemetryProviderSnapshot Snapshot, string Code)[]
            {
                (baseline with { PointExists = false }, "POINT_MISSING"),
                (baseline with { PointId = Guid.NewGuid() }, "POINT_MISMATCH"),
                (baseline with { PointActive = false }, "POINT_INACTIVE"),
                (baseline with { SiteActive = false }, "SITE_INACTIVE"),
                (baseline with { AreaActive = false }, "AREA_INACTIVE"),
                (baseline with { AssetActive = false }, "ASSET_INACTIVE"),
                (baseline with { SourceExists = false }, "SOURCE_MISSING"),
                (baseline with { SourceId = Guid.NewGuid() }, "SOURCE_MISMATCH"),
                (baseline with { SourceActive = false }, "SOURCE_INACTIVE"),
                (baseline with { MappingExists = false }, "MAPPING_MISSING"),
                (baseline with { MappingActive = false }, "MAPPING_NOT_ACTIVE"),
                (baseline with { MappingEffective = false }, "MAPPING_NOT_ACTIVE"),
                (baseline with { MappingPointId = Guid.NewGuid() }, "MAPPING_POINT_MISMATCH"),
                (baseline with { MappingVersion = 2 }, "MAPPING_VERSION_MISMATCH"),
                (baseline with { MetricExists = false }, "METRIC_MISSING"),
                (baseline with { MetricMatchesPoint = false }, "METRIC_MISMATCH"),
                (baseline with { MetricActive = false }, "METRIC_INACTIVE"),
                (baseline with { UnitExists = false }, "UNIT_MISSING"),
                (baseline with { UnitActive = false }, "UNIT_INACTIVE"),
                (baseline with { UnitCompatible = false }, "UNIT_INCOMPATIBLE"),
                (baseline with { UnitCode = "KWH" }, "UNIT_MISMATCH"),
                (baseline with { SourceVersion = 0 }, "PROVIDER_VERSION_INVALID"),
                (baseline with { SourceType = "Modbus" }, "SOURCE_TYPE_NOT_SIMULATOR"),
                (baseline with { SiteVersion = 0 }, "PROVIDER_VERSION_INVALID"),
                (baseline with { AreaVersion = 0 }, "PROVIDER_VERSION_INVALID"),
                (baseline with { AssetVersion = 0 }, "PROVIDER_VERSION_INVALID"),
                (baseline with { PointVersion = 0 }, "PROVIDER_VERSION_INVALID"),
                (baseline with { MetricVersion = 0 }, "PROVIDER_VERSION_INVALID"),
                (baseline with { UnitVersion = 0 }, "PROVIDER_VERSION_INVALID"),
                (baseline with { CompatibilityVersion = 0 }, "PROVIDER_VERSION_INVALID"),
                (baseline with { CompatibilityIdentity = "" }, "COMPATIBILITY_IDENTITY_MISSING"),
                (baseline with { CompatibilityStatus = "Inactive" }, "COMPATIBILITY_STATUS_NOT_ACTIVE")
            };
            foreach (var item in variants)
            {
                var system = Create();
                system.Providers.Snapshot = item.Snapshot;
                var result = Execute(system, TelemetryTestData.Request());
                Check(result.OriginalResult?.RejectionCode == item.Code, item.Code, failures);
                Check(system.Store.ListCommittedRawAsync().Result.Count == 0, "provider rejection no raw", failures);
            }
        });
        Case("finite in-range is Good Accepted", failures, () =>
        {
            var system = Create();
            var result = Execute(system, TelemetryTestData.Request());
            Check(result.Disposition == TelemetryDisposition.Accepted, "accepted", failures);
            Check(result.OriginalResult?.QualityCode == MeasurementQuality.Good, "Good quality", failures);
            Check(result.OriginalResult?.ReasonCode is null, "Good reason null", failures);
        });
        Case("future skew threshold", failures, () =>
        {
            var future = Create();
            var request = TelemetryTestData.Request() with
            {
                SourceTimestampUtc = TelemetryTestData.Now.AddSeconds(301)
            };
            var result = Execute(future, request);
            Check(result.OriginalResult?.QualityCode == MeasurementQuality.Uncertain, "future Uncertain", failures);
            Check(result.OriginalResult?.ReasonCode == "SOURCE_TIMESTAMP_FUTURE", "future reason", failures);

            var threshold = Create();
            result = Execute(threshold, request with
            {
                SourceTimestampUtc = TelemetryTestData.Now.AddSeconds(300)
            });
            Check(result.OriginalResult?.QualityCode == MeasurementQuality.Good, "threshold Good", failures);
        });
        Case("out-of-range is Bad and never advances Latest", failures, () =>
        {
            foreach (var value in new[] { -0.01, 100.01 })
            {
                var system = Create();
                var result = Execute(system, TelemetryTestData.Request() with { NumericValue = value });
                Check(result.OriginalResult?.QualityCode == MeasurementQuality.Bad, "Bad quality", failures);
                Check(result.OriginalResult?.ReasonCode == "VALUE_OUT_OF_RANGE", "range reason", failures);
                Check(result.OriginalResult?.LatestAdvanced == false, "Bad latest false", failures);
            }
        });
        Case("out-of-range takes precedence over future skew", failures, () =>
        {
            var system = Create();
            var result = Execute(system, TelemetryTestData.Request() with
            {
                NumericValue = 101,
                SourceTimestampUtc = TelemetryTestData.Now.AddHours(1)
            });
            Check(result.OriginalResult?.QualityCode == MeasurementQuality.Bad, "precedence Bad", failures);
            Check(result.OriginalResult?.ReasonCode == "VALUE_OUT_OF_RANGE", "precedence reason", failures);
        });
        Case("Duplicate bypasses changed provider state", failures, () =>
        {
            var system = Create();
            var first = Execute(system, TelemetryTestData.Request());
            var reads = system.Providers.Reads;
            system.Providers.Snapshot = TelemetryTestData.Provider() with { PointActive = false };
            var duplicate = Execute(system, TelemetryTestData.Request());
            Check(first.Disposition == TelemetryDisposition.Accepted, "first accepted", failures);
            Check(duplicate.Disposition == TelemetryDisposition.Duplicate, "duplicate", failures);
            Check(system.Providers.Reads == reads, "no owner revalidation", failures);
            Check(MeasurementIdentityRegistryTests.TerminalEqual(
                duplicate.OriginalResult, first.OriginalResult), "exact stored result", failures);
        });
        Case("provider version drift rolls back", failures, () =>
        {
            var system = Create();
            system.Providers.RecheckResult = false;
            try
            {
                Execute(system, TelemetryTestData.Request());
                failures.Add("provider drift did not fail");
            }
            catch (InvalidOperationException ex)
            {
                Check(ex.Message == "PROVIDER_VERSION_DRIFT", "drift code", failures);
            }
            Check(system.Store.ListCommittedTerminalsAsync().Result.Count == 0, "drift rollback", failures);
        });
        return failures;
    }

    public static TelemetrySystem Create()
    {
        var store = new FakeTelemetryRepositories();
        var providers = new FakeTelemetryProviderQuery { Snapshot = TelemetryTestData.Provider() };
        var configurations = new FakeImmutableConfigurationQuery
        {
            Snapshot = new ImmutableConfigurationSnapshot(
                TelemetryTestData.ConfigurationId, 1, 0, 100)
        };
        var clock = new FakeTelemetryClock { UtcNow = TelemetryTestData.Now };
        var persistence = new TelemetryPersistenceService(
            store, store, store, store, providers);
        var service = new IngestMeasurement(
            store, configurations, providers, persistence, clock);
        return new TelemetrySystem(service, store, providers, configurations, clock);
    }

    public static TrustedProducerContext Trusted() =>
        new(true, "IUMP.Acquisition.Simulator.v1", "Simulator", 1);

    public static TelemetryIngestionResult Execute(
        TelemetrySystem system,
        TelemetryMeasurementRequest request,
        TrustedProducerContext? producer = null) =>
        system.Service.ExecuteAsync(request, producer ?? Trusted()).GetAwaiter().GetResult();

    public static TelemetryMeasurementRequest WithIdentity(TelemetryMeasurementRequest request) =>
        request with
        {
            MeasurementId = MeasurementIdentityVerifier.Create(
                request.SourceId, request.SimulatorRunId, request.PointId,
                request.MappingId, request.SourceSequence, request.AlgorithmVersion).ToString("D")
        };

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

public sealed record TelemetrySystem(
    IngestMeasurement Service,
    FakeTelemetryRepositories Store,
    FakeTelemetryProviderQuery Providers,
    FakeImmutableConfigurationQuery Configurations,
    FakeTelemetryClock Clock);
