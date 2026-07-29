using IUMP.Api;

namespace IUMP.Tests.Unit.Api;

public static class ConfigurationEndpointTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();
        if (!ConfigurationEndpointPolicy.RequiresIdempotency("POST") || ConfigurationEndpointPolicy.RequiresIfMatch("PUT") == false)
            failures.Add("configuration mutations must use common idempotency and concurrency headers");
        if (ConfigurationEndpointPolicy.RequiresIfMatch("POST")) failures.Add("create must not require If-Match");
        return failures;
    }
}
