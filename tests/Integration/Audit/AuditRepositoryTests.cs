using IUMP.Modules.Audit.Contracts;

namespace IUMP.Tests.Integration.Audit;

/// Provider-neutral contract source for the append-only Audit PostgreSQL adapter.
public static class AuditRepositoryTests
{
    public static IReadOnlyList<string> RequiredInvariants => new[]
    {
        "unique source_event_id", "append-only insert with no update/delete surface",
        "authorized scope before filters", "keyset order occurred_at DESC, audit_event_id DESC"
    };

    public static void AssertProviderNeutralPorts()
    {
        _ = typeof(IAuditAppendRepository);
        _ = typeof(IAuditQueryRepository);
    }
}
