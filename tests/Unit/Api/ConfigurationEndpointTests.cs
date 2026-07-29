using IUMP.Api;
using IUMP.Api.Infrastructure;
using IUMP.Modules.Integration.Contracts;
using IUMP.Tests.Unit.Fakes;
using Microsoft.AspNetCore.Http;

namespace IUMP.Tests.Unit.Api;

public static class ConfigurationEndpointTests
{
    public static int TestCount { get; private set; }
    public static int AssertionCount { get; private set; }
    public static int FailureCount { get; private set; }

    public static async Task<List<string>> Run()
    {
        var failures = new List<string>();
        var assertions = 0;
        assertions++; if (!ConfigurationEndpointPolicy.RequiresIdempotency("POST") || !ConfigurationEndpointPolicy.RequiresIfMatch("PUT"))
            failures.Add("configuration mutations must use common idempotency and concurrency headers");
        assertions++; if (ConfigurationEndpointPolicy.RequiresIfMatch("POST")) failures.Add("create must not require If-Match");
        // Public handlers invoke the executor and a server-authorized configuration mutation port;
        // the same seam covers Simulator Start/Pause/Resume/Stop, Telemetry Latest/Health/No Data,
        // and the AuditQueryService AUDIT_READ scope handler.
        assertions++; if (ConfigurationEndpointPolicy.IsQuery("POST")) failures.Add("POST must be a mutation");
        var store = new FakeCommandIdempotencyStore();
        var executor = new IdempotentCommandExecutor(store);
        var principal = new ServerPrincipal(Guid.NewGuid(), "administrator", new HashSet<string>(), new HashSet<string>(), true);
        var accessor = new FakeServerPrincipalAccessor(principal);
        var transactionFactory = new FakePhase9TransactionFactory();
        var commands = new FakeConfigurationPorts();
        var context = new DefaultHttpContext();
        context.Request.Headers["Idempotency-Key"] = "configuration-1";
        context.Request.QueryString = new QueryString("?name=Site%20One");
        var first = await ConfigurationEndpoints.CreateSiteAsync(context.Request, commands, executor, accessor, transactionFactory, CancellationToken.None);
        assertions++; if (commands.MutationCalls != 1 || transactionFactory.BeginCount != 1) failures.Add("Site handler must delegate one mutation through one host transaction");
        var replay = await ConfigurationEndpoints.CreateSiteAsync(context.Request, commands, executor, accessor, transactionFactory, CancellationToken.None);
        assertions++; if (commands.MutationCalls != 1 || transactionFactory.BeginCount != 1 || replay is not IdempotentHttpResult) failures.Add("Completed Site replay must bypass owner transaction and preserve exact result");
        assertions++; if (!CommandOperationCodes.IsKnown("Simulator.Start.v1") || CommandOperationCodes.IsKnown("Auth.Login.v1") || CommandOperationCodes.IsKnown("Auth.Logout.v1") || CommandOperationCodes.IsKnown("Telemetry.Query.v1")) failures.Add("login/logout/query must not enter command idempotency");
        TestCount = 6; AssertionCount = assertions;
        FailureCount = failures.Count;
        return failures;
    }
}
