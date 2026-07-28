using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using IUMP.Modules.Telemetry.Contracts;

namespace IUMP.Modules.Telemetry.Domain;

public static class MeasurementIdentityVerifier
{
    public static readonly Guid NamespaceId =
        Guid.Parse("02e993bb-c767-5ff6-963f-530e1dfdff6b");

    public static bool TryVerify(TelemetryMeasurementRequest request, out Guid measurementId)
    {
        measurementId = Guid.Empty;
        if (request.SourceId == Guid.Empty || request.SimulatorRunId == Guid.Empty ||
            request.PointId == Guid.Empty || request.MappingId == Guid.Empty ||
            request.SimulatorConfigurationId == Guid.Empty ||
            request.SourceSequence < 0 || request.MappingVersion <= 0 ||
            request.AlgorithmVersion <= 0 || request.ConfigurationVersion <= 0 ||
            !Guid.TryParseExact(request.MeasurementId, "D", out var parsed) ||
            request.MeasurementId != request.MeasurementId.ToLowerInvariant() ||
            GetVersion(parsed) != 5 ||
            GetVariant(parsed) != 2)
            return false;
        var expected = Create(request.SourceId, request.SimulatorRunId, request.PointId,
            request.MappingId, request.SourceSequence, request.AlgorithmVersion);
        if (parsed != expected) return false;
        measurementId = parsed;
        return true;
    }

    public static Guid Create(Guid sourceId, Guid runId, Guid pointId, Guid mappingId,
        long sourceSequence, int algorithmVersion)
    {
        var name = string.Join('|',
            "IUMP:SIMULATOR:V1",
            sourceId.ToString("D"),
            runId.ToString("D"),
            pointId.ToString("D"),
            mappingId.ToString("D"),
            sourceSequence.ToString(CultureInfo.InvariantCulture),
            algorithmVersion.ToString(CultureInfo.InvariantCulture));
        Span<byte> namespaceBytes = stackalloc byte[16];
        NamespaceId.TryWriteBytes(namespaceBytes, bigEndian: true, out _);
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var material = new byte[namespaceBytes.Length + nameBytes.Length];
        namespaceBytes.CopyTo(material);
        nameBytes.CopyTo(material.AsSpan(16));
        var hash = SHA1.HashData(material);
        hash[6] = (byte)((hash[6] & 0x0f) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
        return new Guid(hash.AsSpan(0, 16), bigEndian: true);
    }

    private static int GetVersion(Guid value)
    {
        Span<byte> bytes = stackalloc byte[16];
        value.TryWriteBytes(bytes, bigEndian: true, out _);
        return bytes[6] >> 4;
    }

    private static int GetVariant(Guid value)
    {
        Span<byte> bytes = stackalloc byte[16];
        value.TryWriteBytes(bytes, bigEndian: true, out _);
        return bytes[8] >> 6;
    }
}

public static class TelemetryRequestFingerprintV1
{
    private static readonly byte[] VersionMarker = Encoding.ASCII.GetBytes("IUMP:TELEMETRY:FINGERPRINT:V1");
    private const int NullMarker = -1;

    public static byte[] Compute(TelemetryMeasurementRequest request)
    {
        using var stream = new MemoryStream();
        Write(stream, VersionMarker);
        Write(stream, Encoding.ASCII.GetBytes(request.MeasurementId));
        WriteGuid(stream, request.SourceId);
        WriteGuid(stream, request.SimulatorRunId);
        WriteGuid(stream, request.PointId);
        WriteGuid(stream, request.MappingId);
        WriteInt64(stream, request.MappingVersion);
        WriteInt64(stream, request.SourceSequence);
        WriteNullableString(stream, request.AlgorithmId);
        WriteInt32(stream, request.AlgorithmVersion);
        WriteGuid(stream, request.SimulatorConfigurationId);
        WriteInt64(stream, request.ConfigurationVersion);
        WriteInt64(stream, request.SourceTimestampUtc.Ticks);
        WriteInt32(stream, (int)request.SourceTimestampUtc.Kind);
        WriteInt64(stream, BitConverter.DoubleToInt64Bits(request.NumericValue));
        WriteNullableString(stream, request.UnitCode);
        WriteNullableString(stream, request.ProducerIdentity);
        WriteNullableString(stream, request.CorrelationId);
        WriteNullableString(stream, request.LineageId);
        return SHA256.HashData(stream.ToArray());
    }

    private static void WriteNullableString(Stream stream, string? value)
    {
        if (value is null)
        {
            Span<byte> nullLen = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(nullLen, NullMarker);
            stream.Write(nullLen);
        }
        else
        {
            Write(stream, Encoding.UTF8.GetBytes(value));
        }
    }

    private static void WriteGuid(Stream stream, Guid value)
    {
        Span<byte> bytes = stackalloc byte[16];
        value.TryWriteBytes(bytes, bigEndian: true, out _);
        Write(stream, bytes);
    }

    private static void WriteInt64(Stream stream, long value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        Write(stream, bytes);
    }

    private static void WriteInt32(Stream stream, int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        Write(stream, bytes);
    }

    private static void Write(Stream stream, ReadOnlySpan<byte> value)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, value.Length);
        stream.Write(length);
        stream.Write(value);
    }
}

public static class TelemetryTerminalDecision
{
    public static TelemetryIngestionResult FromExisting(
        TelemetryTerminalResult existing, byte[] fingerprint, string correlationId)
    {
        if (!CryptographicOperations.FixedTimeEquals(
                existing.RequestFingerprint, fingerprint))
            return TelemetryIngestionResult.Failed("IDEMPOTENCY_CONFLICT", correlationId);
        return new TelemetryIngestionResult(
            TelemetryDisposition.Duplicate, existing.Copy(), null, correlationId);
    }
}

public static class TelemetryTerminalResultValidator
{
    public static void EnsureValid(TelemetryTerminalResult result)
    {
        if (result.MeasurementId == Guid.Empty || result.SourceId == Guid.Empty ||
            result.SimulatorRunId == Guid.Empty || result.PointId == Guid.Empty ||
            result.MappingId == Guid.Empty || result.SimulatorConfigurationId == Guid.Empty ||
            result.SourceSequence < 0 || result.MappingVersion <= 0 ||
            result.AlgorithmVersion <= 0 || result.ConfigurationVersion <= 0 ||
            result.RequestFingerprint.Length != 32 ||
            result.CompletedAtUtc.Kind != DateTimeKind.Utc ||
            string.IsNullOrWhiteSpace(result.OriginalCorrelationId) ||
            string.IsNullOrWhiteSpace(result.OriginalLineageId))
            throw new InvalidOperationException("TERMINAL_RESULT_INVALID");

        var valid = result.FinalClassification switch
        {
            TelemetryFinalClassification.Accepted =>
                result.MeasurementPersisted &&
                result.PersistedMeasurementId == result.MeasurementId &&
                result.QualityCode is not null &&
                result.RejectionCode is null &&
                result.LatestAdvanced is not null &&
                QualityShape(result),
            TelemetryFinalClassification.Rejected =>
                !result.MeasurementPersisted &&
                result.PersistedMeasurementId is null &&
                result.QualityCode is null &&
                result.ReasonCode is null &&
                !string.IsNullOrWhiteSpace(result.RejectionCode) &&
                result.LatestAdvanced is null,
            _ => false
        };
        if (!valid) throw new InvalidOperationException("TERMINAL_RESULT_INVALID");
    }

    private static bool QualityShape(TelemetryTerminalResult result) =>
        result.QualityCode switch
        {
            MeasurementQuality.Good => result.ReasonCode is null,
            MeasurementQuality.Uncertain =>
                result.ReasonCode == "SOURCE_TIMESTAMP_FUTURE",
            MeasurementQuality.Bad =>
                result.ReasonCode == "VALUE_OUT_OF_RANGE" &&
                result.LatestAdvanced == false,
            _ => false
        };
}
