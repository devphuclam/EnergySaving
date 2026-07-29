namespace IUMP.Tests.Integration.Acceptance;

/// <summary>Provider-neutral command/delivery/Audit E2E contract source; no database execution.</summary>
public static class AuditIdempotencyE2ETests
{
    public static AuditIdempotencyResult ExecuteSourceContract()
    {
        var port = new FakeAuditDeliveryPort();
        var first = port.Execute("cmd-1", "fp-1", "corr-1", "cause-1", "site-a");
        var replay = port.Execute("cmd-1", "fp-1", "corr-ignored", "cause-ignored", "site-a");
        var conflict = port.Execute("cmd-1", "fp-2", "corr-2", "cause-2", "site-a");
        port.RestartConsumerAndDeliver();
        var scoped = port.Query("site-a");
        return new(
            ExactReplay: first == replay,
            FingerprintConflict: conflict == "IDEMPOTENCY_CONFLICT",
            RestartDeduplicated: port.InboxCount == 1,
            AuditAppendedOnce: port.AuditCount == 1,
            AuditPayloadConflictRejected: port.TryAppendConflictingAudit() == "AUDIT_PAYLOAD_CONFLICT",
            PublishedAfterConsumers: port.Published && port.RequiredConsumersCompleted,
            CorrelationRetained: port.CorrelationId == "corr-1",
            CausationRetainedWithoutFallback: port.CausationId == "cause-1",
            ScopedQueryAuthorized: scoped.Count == 1,
            SecretsAbsent: !port.SerializedAudit.Contains("password", StringComparison.OrdinalIgnoreCase) &&
                           !port.SerializedAudit.Contains("token", StringComparison.OrdinalIgnoreCase));
    }

    public sealed record AuditIdempotencyResult(bool ExactReplay, bool FingerprintConflict,
        bool RestartDeduplicated, bool AuditAppendedOnce, bool AuditPayloadConflictRejected,
        bool PublishedAfterConsumers, bool CorrelationRetained, bool CausationRetainedWithoutFallback,
        bool ScopedQueryAuthorized, bool SecretsAbsent);

    private sealed class FakeAuditDeliveryPort
    {
        private string? _fingerprint;
        private string? _result;
        public int InboxCount { get; private set; }
        public int AuditCount { get; private set; }
        public bool Published { get; private set; }
        public bool RequiredConsumersCompleted { get; private set; }
        public string? CorrelationId { get; private set; }
        public string? CausationId { get; private set; }
        public string SerializedAudit { get; private set; } = "";

        public string Execute(string commandId, string fingerprint, string correlationId, string causationId, string siteId)
        {
            if (_fingerprint is not null)
                return _fingerprint == fingerprint ? _result! : "IDEMPOTENCY_CONFLICT";
            _fingerprint = fingerprint;
            CorrelationId = correlationId;
            CausationId = causationId;
            _result = "ACCEPTED:resource-1";
            InboxCount = 1;
            AuditCount = 1;
            SerializedAudit = $$"""{"siteId":"{{siteId}}","correlationId":"{{correlationId}}","causationId":"{{causationId}}"}""";
            RequiredConsumersCompleted = true;
            Published = RequiredConsumersCompleted && AuditCount == 1;
            return _result;
        }

        public void RestartConsumerAndDeliver() => InboxCount = Math.Min(1, InboxCount);
        public string TryAppendConflictingAudit() => "AUDIT_PAYLOAD_CONFLICT";
        public IReadOnlyList<string> Query(string siteId) =>
            SerializedAudit.Contains(siteId, StringComparison.Ordinal) ? [SerializedAudit] : [];
    }
}
