using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace IUMP.Modules.Integration.Contracts;

/// Host-safe canonical command fingerprint port. It intentionally lives in Contracts so
/// composition roots never reference module Application implementations.
public sealed record CommandFingerprintInput(
    string OperationCode,
    Guid CallerUserId,
    string? TargetScopeType,
    Guid? TargetScopeId,
    string? TargetAggregateType,
    Guid? TargetAggregateId,
    long? ExpectedVersion,
    IReadOnlyList<CommandFingerprintField> Fields);

public sealed record CommandFingerprintField(string Name, string? Kind, object? Value)
{
    public static CommandFingerprintField String(string name, string value) => new(name, "string", value);
    public static CommandFingerprintField Bool(string name, bool value) => new(name, "bool", value);
    public static CommandFingerprintField Int64(string name, long value) => new(name, "int", value);
    public static CommandFingerprintField Decimal(string name, decimal value) => new(name, "decimal", value);
    public static CommandFingerprintField Timestamp(string name, DateTime value) => new(name, "timestamp", value);
    public static CommandFingerprintField Uuid(string name, Guid value) => new(name, "uuid", value);
    public static CommandFingerprintField Null(string name) => new(name, null, null);
}

public static class CommandFingerprintV1
{
    private const uint NullLength = 0xffffffff;

    public static byte[] Compute(CommandFingerprintInput input)
    {
        if (input.CallerUserId == Guid.Empty) throw new ArgumentException("Caller is required.", nameof(input));
        using var stream = new MemoryStream();
        WriteValue(stream, "IUMP:COMMAND-IDEMPOTENCY:V1");
        WriteValue(stream, input.OperationCode);
        WriteValue(stream, input.CallerUserId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant());
        WriteValue(stream, input.TargetScopeType);
        WriteValue(stream, input.TargetScopeId?.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant());
        WriteValue(stream, input.TargetAggregateType);
        WriteValue(stream, input.TargetAggregateId?.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant());
        WriteValue(stream, input.ExpectedVersion?.ToString(CultureInfo.InvariantCulture));
        foreach (var field in (input.Fields ?? Array.Empty<CommandFingerprintField>())
            .Where(field => !IsExcluded(field.Name))
            .OrderBy(field => field.Name.Normalize(NormalizationForm.FormC), StringComparer.Ordinal))
        {
            WriteValue(stream, field.Name);
            WriteValue(stream, field.Kind);
            WriteValue(stream, Canonical(field));
        }
        return SHA256.HashData(stream.ToArray());
    }

    private static bool IsExcluded(string name) => name.Contains("idempotency-key", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("caller", StringComparison.OrdinalIgnoreCase) || name.Contains("authorization", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("password", StringComparison.OrdinalIgnoreCase) || name.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("token", StringComparison.OrdinalIgnoreCase);

    private static string? Canonical(CommandFingerprintField field) => field.Value is null ? null : field.Kind switch
    {
        "string" => ((string)field.Value).Normalize(NormalizationForm.FormC),
        "bool" => ((bool)field.Value) ? "true" : "false",
        "int" => Convert.ToInt64(field.Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
        "decimal" => ((decimal)field.Value).ToString("G29", CultureInfo.InvariantCulture),
        "timestamp" => ((DateTime)field.Value).ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture),
        "uuid" => ((Guid)field.Value).ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
        _ => Convert.ToString(field.Value, CultureInfo.InvariantCulture)?.Normalize(NormalizationForm.FormC)
    };

    private static void WriteValue(Stream stream, string? value)
    {
        if (value is null) { Span<byte> nullBytes = stackalloc byte[4]; BinaryPrimitives.WriteUInt32BigEndian(nullBytes, NullLength); stream.Write(nullBytes); return; }
        var bytes = Encoding.UTF8.GetBytes(value.Normalize(NormalizationForm.FormC));
        Span<byte> length = stackalloc byte[4]; BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)bytes.Length));
        stream.Write(length); stream.Write(bytes);
    }
}
