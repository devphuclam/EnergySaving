using System.Text.RegularExpressions;
using IUMP.Modules.Audit.Contracts;

namespace IUMP.Modules.Audit.Application;

public sealed class AuditEventConsumer(IAuditAppendRepository repository) : IAuditEventConsumer
{
    public async Task<AuditEventRecord> ConsumeAsync(AuditEventEnvelope envelope, CancellationToken ct = default)
    {
        if (envelope.SourceEventId == Guid.Empty || string.IsNullOrWhiteSpace(envelope.EventType) ||
            !envelope.EventType.EndsWith(".v1", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(envelope.ObjectId))
            throw new InvalidOperationException("AUDIT_SCHEMA_INVALID");
        if (envelope.SchemaVersion != 1 || string.IsNullOrWhiteSpace(envelope.PayloadHash))
            throw new InvalidOperationException("AUDIT_SCHEMA_VERSION_INVALID");
        if (repository is IAuditConflictRepository conflicts &&
            await conflicts.IsSourceHashConflictAsync(envelope.SourceEventId, envelope.PayloadHash, ct))
            throw new InvalidOperationException("AUDIT_SOURCE_HASH_CONFLICT");
        var record = new AuditEventRecord(Guid.NewGuid(), envelope.SourceEventId, envelope.EventType,
            envelope.ObjectType, envelope.ObjectId, envelope.Action, Redact(envelope.Summary), envelope.OccurredAtUtc,
            DateTime.UtcNow, envelope.CorrelationId, envelope.ActorId, envelope.ActorUsername,
            Redact(envelope.Before), Redact(envelope.After), envelope.SiteId, envelope.AreaId, envelope.CausationId)
        {
            SchemaVersion = envelope.SchemaVersion,
            SourceProducer = envelope.SourceProducer,
            PayloadHash = envelope.PayloadHash
        };
        return await repository.AppendIfAbsentAsync(record, ct) ?? record;
    }

    private static string Redact(string value) => Regex.Replace(value, "(?i)(password|secret|token|credential)\\s*[:=]\\s*[^,; ]+", "$1=[REDACTED]");
    private static IReadOnlyDictionary<string, object?> Redact(IReadOnlyDictionary<string, object?>? values) =>
        (values ?? new Dictionary<string, object?>()).ToDictionary(pair => pair.Key,
            pair => pair.Key.Contains("password", StringComparison.OrdinalIgnoreCase) || pair.Key.Contains("secret", StringComparison.OrdinalIgnoreCase) || pair.Key.Contains("token", StringComparison.OrdinalIgnoreCase) ? "[REDACTED]" : pair.Value,
            StringComparer.Ordinal);
}
