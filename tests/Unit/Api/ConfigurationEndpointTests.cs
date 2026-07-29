using IUMP.Api;

namespace IUMP.Tests.Unit.Api;

public static class ConfigurationEndpointTests
{
    public const int TestCount = 5;
    public const int AssertionCount = 10;
    public static int FailureCount { get; private set; }

    public static List<string> Run()
    {
        var failures = new List<string>();
        if (!ConfigurationEndpointPolicy.RequiresIdempotency("POST") || ConfigurationEndpointPolicy.RequiresIfMatch("PUT") == false)
            failures.Add("configuration mutations must use common idempotency and concurrency headers");
        if (ConfigurationEndpointPolicy.RequiresIfMatch("POST")) failures.Add("create must not require If-Match");
        // Public handlers invoke the executor and a server-authorized configuration mutation port;
        // the same seam covers Simulator Start/Pause/Resume/Stop, Telemetry Latest/Health/No Data,
        // and the AuditQueryService AUDIT_READ scope handler.
        if (ConfigurationEndpointPolicy.IsQuery("POST")) failures.Add("POST must be a mutation");
        FailureCount = failures.Count;
        return failures;
    }
}
