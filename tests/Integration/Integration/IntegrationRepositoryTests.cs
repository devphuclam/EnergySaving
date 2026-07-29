using IUMP.Modules.Integration.Contracts;

namespace IUMP.Tests.Integration.Integration;

/// Provider-neutral contract source for the PostgreSQL adapter. The adapter remains package-policy
/// blocked; these tests document the exact invariants it must satisfy when approved packages exist.
public static class IntegrationRepositoryTests
{
    public static IReadOnlyList<string> RequiredInvariants => new[]
    {
        "unique caller/operation/idempotency key", "SHA-256 fingerprint", "Pending reclaim with optimistic version",
        "FOR UPDATE SKIP LOCKED outbox claim", "30-second lease", "bounded retry and Failed exhaustion",
        "inbox payload-hash conflict", "Completed consumer deduplication"
    };

    public static void AssertProviderNeutralPorts()
    {
        _ = typeof(ICommandIdempotencyStore);
        _ = typeof(IOutboxClaimRepository);
        _ = typeof(IInboxDeduplicationRepository);
    }
}
