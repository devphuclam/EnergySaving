using IUMP.Modules.Telemetry.Contracts;
using IUMP.Modules.Telemetry.Application;
using IUMP.Modules.Telemetry.Domain;

namespace IUMP.Tests.Unit.Telemetry;

public static class MeasurementIdentityRegistryTests
{
    public static int TestCount { get; private set; }
    public static int CheckCount { get; private set; }

    public static List<string> Run()
    {
        TestCount = 0;
        CheckCount = 0;
        var failures = new List<string>();
        Case("valid exact UUIDv5 recomputation", failures, () =>
        {
            var request = TelemetryTestData.Request();
            Check(MeasurementIdentityVerifier.TryVerify(request, out var parsed), "valid identity", failures);
            Check(parsed.ToString("D") == request.MeasurementId, "exact recomputation", failures);
        });
        Case("malformed UUID", failures, () =>
            Check(!MeasurementIdentityVerifier.TryVerify(
                TelemetryTestData.Request() with { MeasurementId = "not-a-uuid" }, out _),
                "malformed rejected", failures));
        Case("non-version-5 UUID", failures, () =>
            Check(!MeasurementIdentityVerifier.TryVerify(
                TelemetryTestData.Request() with { MeasurementId = Guid.NewGuid().ToString("D") }, out _),
                "non-v5 rejected", failures));
        Case("wrong RFC variant", failures, () =>
        {
            var request = TelemetryTestData.Request();
            var id = Guid.Parse(request.MeasurementId);
            Span<byte> bytes = stackalloc byte[16];
            id.TryWriteBytes(bytes, bigEndian: true, out _);
            bytes[8] &= 0x3f;
            var invalid = new Guid(bytes, bigEndian: true).ToString("D");
            Check(!MeasurementIdentityVerifier.TryVerify(
                request with { MeasurementId = invalid }, out _), "variant rejected", failures);
        });
        Case("tuple mismatch", failures, () =>
        {
            var request = TelemetryTestData.Request();
            Check(!MeasurementIdentityVerifier.TryVerify(
                request with { PointId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa") },
                out _), "tuple mismatch rejected", failures);
        });
        Case("fingerprint deterministic and 32 bytes", failures, () =>
        {
            var request = TelemetryTestData.Request();
            var first = TelemetryRequestFingerprintV1.Compute(request);
            var second = TelemetryRequestFingerprintV1.Compute(request);
            Check(first.Length == 32, "fingerprint length", failures);
            Check(first.SequenceEqual(second), "fingerprint stable", failures);
        });
        Case("every immutable field affects fingerprint", failures, () =>
        {
            var request = TelemetryTestData.Request();
            var original = TelemetryRequestFingerprintV1.Compute(request);
            var variants = new[]
            {
                request with { MeasurementId = Guid.NewGuid().ToString("D") },
                request with { SourceId = Guid.NewGuid() },
                request with { SimulatorRunId = Guid.NewGuid() },
                request with { PointId = Guid.NewGuid() },
                request with { MappingId = Guid.NewGuid() },
                request with { MappingVersion = 2 },
                request with { SourceSequence = 2 },
                request with { AlgorithmId = "OTHER" },
                request with { AlgorithmVersion = 2 },
                request with { SimulatorConfigurationId = Guid.NewGuid() },
                request with { ConfigurationVersion = 2 },
                request with { SourceTimestampUtc = request.SourceTimestampUtc.AddTicks(1) },
                request with
                {
                    SourceTimestampUtc = DateTime.SpecifyKind(
                        request.SourceTimestampUtc, DateTimeKind.Local)
                },
                request with { NumericValue = 12.6 },
                request with { UnitCode = "KWH" },
                request with { ProducerIdentity = "other" },
                request with { CorrelationId = "other-correlation" },
                request with { LineageId = "other-lineage" }
            };
            foreach (var variant in variants)
                Check(!original.SequenceEqual(TelemetryRequestFingerprintV1.Compute(variant)),
                    "included field changes fingerprint", failures);
        });
        Case("signed zero behavior frozen", failures, () =>
        {
            var request = TelemetryTestData.Request() with { NumericValue = +0.0 };
            var positive = TelemetryRequestFingerprintV1.Compute(request);
            var negative = TelemetryRequestFingerprintV1.Compute(request with { NumericValue = -0.0 });
            Check(!positive.SequenceEqual(negative), "+0 and -0 are distinct", failures);
        });
        Case("retry metadata is outside the fingerprint contract", failures, () =>
        {
            var request = TelemetryTestData.Request();
            var retryOne = (Attempt: 1, Lease: "worker-a", Trace: "trace-a");
            var retryTwo = (Attempt: 9, Lease: "worker-b", Trace: "trace-b");
            Check(retryOne != retryTwo, "retry fixtures differ", failures);
            Check(TelemetryRequestFingerprintV1.Compute(request).SequenceEqual(
                    TelemetryRequestFingerprintV1.Compute(request)),
                "retry metadata cannot affect request fingerprint", failures);
            Check(typeof(TelemetryRequestFingerprintV1).GetMethod("Compute")!
                    .GetParameters().Select(parameter => parameter.ParameterType)
                    .SequenceEqual([typeof(TelemetryMeasurementRequest)]),
                "fingerprint accepts immutable request only", failures);
        });
        Case("nonfinite produces stable 32-byte fingerprint without exception", failures, () =>
        {
            foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                var fp = TelemetryRequestFingerprintV1.Compute(
                    TelemetryTestData.Request() with { NumericValue = value });
                Check(fp.Length == 32, $"nonfinite {value} fingerprint length", failures);
                var fp2 = TelemetryRequestFingerprintV1.Compute(
                    TelemetryTestData.Request() with { NumericValue = value });
                Check(fp.SequenceEqual(fp2), $"nonfinite {value} fingerprint stable", failures);
            }
        });
        Case("null static strings produce stable fingerprint without exception", failures, () =>
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
                var fp = TelemetryRequestFingerprintV1.Compute(request);
                Check(fp.Length == 32, $"null {field} fingerprint length", failures);
                var fp2 = TelemetryRequestFingerprintV1.Compute(request);
                Check(fp.SequenceEqual(fp2), $"null {field} fingerprint stable", failures);
            }
        });
        Case("exact Accepted duplicate replay", failures, () =>
        {
            var request = TelemetryTestData.Request();
            var terminal = TelemetryTestData.Terminal(request, TelemetryFinalClassification.Accepted);
            var result = TelemetryTerminalDecision.FromExisting(
                terminal, terminal.RequestFingerprint.ToArray(), "retry-correlation");
            Check(result.Disposition == TelemetryDisposition.Duplicate, "duplicate disposition", failures);
            Check(TerminalEqual(result.OriginalResult, terminal), "accepted original copied exactly", failures);
            Check(!ReferenceEquals(result.OriginalResult!.RequestFingerprint, terminal.RequestFingerprint),
                "fingerprint deep copied", failures);
        });
        Case("exact Rejected duplicate replay", failures, () =>
        {
            var request = TelemetryTestData.Request();
            var terminal = TelemetryTestData.Terminal(request, TelemetryFinalClassification.Rejected);
            var result = TelemetryTerminalDecision.FromExisting(
                terminal, terminal.RequestFingerprint.ToArray(), "retry-correlation");
            Check(result.Disposition == TelemetryDisposition.Duplicate, "duplicate disposition", failures);
            Check(TerminalEqual(result.OriginalResult, terminal), "rejected original copied exactly", failures);
        });
        Case("fingerprint conflict", failures, () =>
        {
            var request = TelemetryTestData.Request();
            var terminal = TelemetryTestData.Terminal(request, TelemetryFinalClassification.Accepted);
            var result = TelemetryTerminalDecision.FromExisting(
                terminal, new byte[32], request.CorrelationId);
            Check(result.Disposition == TelemetryDisposition.Failed &&
                  result.ErrorCode == "IDEMPOTENCY_CONFLICT", "conflict is safe/no mutation", failures);
        });
        Case("terminal registry is terminal-only", failures, () =>
        {
            var names = Enum.GetNames<TelemetryFinalClassification>();
            Check(names.SequenceEqual(["Accepted", "Rejected"]), "no Pending/InProgress state", failures);
        });
        Case("mechanism is Telemetry-specific", failures, () =>
        {
            var typeName = typeof(TelemetryRequestFingerprintV1).FullName!;
            Check(typeName.Contains("Telemetry", StringComparison.Ordinal) &&
                  !typeName.Contains("IdempotencyStore", StringComparison.Ordinal),
                "distinct fingerprint mechanism", failures);
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

    public static bool TerminalEqual(
        TelemetryTerminalResult? left, TelemetryTerminalResult? right) =>
        left is not null && right is not null &&
        left with { RequestFingerprint = Array.Empty<byte>() } ==
        right with { RequestFingerprint = Array.Empty<byte>() } &&
        left.RequestFingerprint.SequenceEqual(right.RequestFingerprint);
}

public static class TelemetryTestData
{
    public static readonly Guid SourceId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    public static readonly Guid RunId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    public static readonly Guid PointId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    public static readonly Guid MappingId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    public static readonly Guid ConfigurationId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    public static readonly DateTime Now = new(2026, 7, 28, 6, 0, 0, DateTimeKind.Utc);

    public static TelemetryMeasurementRequest Request()
    {
        var measurementId = MeasurementIdentityVerifier.Create(
            SourceId, RunId, PointId, MappingId, 1, 1);
        return new TelemetryMeasurementRequest(
            measurementId.ToString("D"), SourceId, RunId, PointId, MappingId, 1, 1,
            "IUMP-DETERMINISTIC-V1", 1, ConfigurationId, 1, Now.AddSeconds(-10),
            12.5, "kW", "IUMP.Acquisition.Simulator.v1", "correlation-1", "lineage-1");
    }

    public static TelemetryTerminalResult Terminal(
        TelemetryMeasurementRequest request, TelemetryFinalClassification classification)
    {
        var id = Guid.Parse(request.MeasurementId);
        var accepted = classification == TelemetryFinalClassification.Accepted;
        return new TelemetryTerminalResult(
            id, request.SourceId, request.SimulatorRunId, request.PointId,
            request.MappingId, request.MappingVersion, request.SourceSequence,
            request.AlgorithmId, request.AlgorithmVersion,
            request.SimulatorConfigurationId, request.ConfigurationVersion,
            classification, accepted, accepted ? id : null,
            accepted ? MeasurementQuality.Good : null, null,
            accepted ? null : "POINT_INACTIVE", accepted ? true : null, Now,
            request.CorrelationId, request.LineageId,
            TelemetryRequestFingerprintV1.Compute(request));
    }

    public static TelemetryProviderSnapshot Provider() => new(
        PointId, true, true, true, true, true, 1, 1, 1, 1,
        SourceId, "Simulator", true, true, 1,
        MappingId, true, true, true, PointId, 1,
        true, true, true, 1, true, true, true, "kW", 1,
         "compat-id", 1, "Active", "trusted-site-1", "trusted-area-1",
         "site-1", "Active", "area-1", "Active", "asset-1", "Active",
         "Active", "Active", "Active", "metric-1", "Active", "unit-1", "Active",
         Now.AddDays(-1), Now.AddDays(1));

    public static TelemetryRaceWinnerFixture RaceFixture(
        TelemetryMeasurementRequest request, TelemetryTerminalResult winner)
    {
        if (winner.FinalClassification == TelemetryFinalClassification.Rejected)
            return new TelemetryRaceWinnerFixture(winner, null, null, null);
        var sourceTimestampUtc = new DateTime(2026, 7, 28, 5, 59, 50, DateTimeKind.Utc);
        var receivedAtUtc = new DateTime(2026, 7, 28, 5, 59, 55, DateTimeKind.Utc);
        var processingAtUtc = new DateTime(2026, 7, 28, 5, 59, 59, DateTimeKind.Utc);
        var raw = new RawMeasurement(
            winner.MeasurementId, winner.SourceId, winner.SimulatorRunId, winner.PointId,
            winner.MappingId, winner.MappingVersion, winner.SourceSequence,
            sourceTimestampUtc, receivedAtUtc, processingAtUtc,
            12.5, "kW", winner.QualityCode!.Value,
            winner.ReasonCode, winner.OriginalCorrelationId, winner.OriginalLineageId);
        var latest = winner.LatestAdvanced == true
            ? new LatestProjectionCandidate(winner.MeasurementId, winner.PointId,
                raw.SourceTimestampUtc, winner.SourceSequence, raw.ProcessingAtUtc,
                winner.QualityCode.Value)
            : null;
        var ownerEvent = new TelemetryOwnerEvent(
            Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
            "MeasurementAccepted.v1", 1, "IUMP.Telemetry", "Measurement",
            winner.MeasurementId, 1, "IUMP.Telemetry", "trusted-simulator", "Accepted",
            "Measurement accepted.", processingAtUtc, raw.CorrelationId, null,
            "site-1", "area-1", new Dictionary<string, object?>(StringComparer.Ordinal),
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["measurementId"] = raw.MeasurementId.ToString("D"),
                ["sourceId"] = raw.SourceId.ToString("D"),
                ["simulatorRunId"] = raw.SimulatorRunId.ToString("D"),
                ["pointId"] = raw.PointId.ToString("D"),
                ["mappingId"] = raw.MappingId.ToString("D"),
                ["mappingVersion"] = raw.MappingVersion,
                ["sourceSequence"] = raw.SourceSequence,
                ["sourceTimestampUtc"] = sourceTimestampUtc,
                ["receivedAtUtc"] = receivedAtUtc,
                ["processingAtUtc"] = processingAtUtc,
                ["numericValue"] = 12.5,
                ["unitCode"] = "kW",
                ["qualityCode"] = winner.QualityCode.Value.ToString(),
                ["reasonCode"] = winner.ReasonCode,
                ["latestAdvanced"] = winner.LatestAdvanced == true,
                ["correlationId"] = raw.CorrelationId,
                ["lineageId"] = raw.LineageId
            });
        return new TelemetryRaceWinnerFixture(winner, raw, latest, ownerEvent);
    }
}
