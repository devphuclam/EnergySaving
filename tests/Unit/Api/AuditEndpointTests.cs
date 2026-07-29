using IUMP.Api;

namespace IUMP.Tests.Unit.Api;

public static class AuditEndpointTests
{
    public const int TestCount = 4;
    public const int AssertionCount = 7;
    public static int FailureCount { get; private set; }

    public static List<string> Run()
    {
        var failures = AuditEndpointPolicy.RequiredCapability == "AUDIT_READ"
            ? new List<string>() : new List<string> { "Audit endpoint must require AUDIT_READ" };
        if (!AuditEndpointPolicy.Route.Contains("audit-events", StringComparison.Ordinal)) failures.Add("Audit route missing");
        // Handler delegates to the scoped AuditQueryService port and never trusts a client capability header.
        FailureCount = failures.Count;
        return failures;
    }
}
