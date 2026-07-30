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
            failures.Add("configuration mutations must use idempotency and If-Match concurrency headers");
        assertions++; if (ConfigurationEndpointPolicy.RequiresIfMatch("POST")) failures.Add("create POST must not require If-Match");
        assertions++; if (!ConfigurationEndpointPolicy.IsLifecyclePost("POST", "Organization.ActivatePoint.v1"))
            failures.Add("lifecycle POST must require If-Match");
        assertions++; if (ConfigurationEndpointPolicy.IsLifecyclePost("POST", "Organization.CreateSite.v1"))
            failures.Add("create POST must not be classified as lifecycle");
        assertions++; if (ConfigurationEndpointPolicy.IsQuery("POST")) failures.Add("POST must be a mutation");
        // Site/post-Site fixture: CreateSiteAsync through transactional executor
        var store = new FakeCommandIdempotencyStore();
        var executor = new IdempotentCommandExecutor(store);
        var principal = new ServerPrincipal(Guid.NewGuid(), "administrator", new HashSet<string>(), new HashSet<string>(), true);
        var accessor = new FakeServerPrincipalAccessor(principal);
        var transactionFactory = new FakePhase9TransactionFactory();
        var commands = new FakeConfigurationPorts();
        var context = new DefaultHttpContext();
        context.Request.Headers["Idempotency-Key"] = "configuration-1";
        context.Request.QueryString = new QueryString("?name=Site%20One");
        var siteResult = await ConfigurationEndpoints.CreateSiteAsync(context.Request, commands, executor, accessor, transactionFactory, CancellationToken.None);
        assertions++; if (commands.MutationCalls != 1 || transactionFactory.BeginCount != 1) failures.Add("Site handler must delegate one mutation through host transaction");
        assertions++; if (commands.LastOperationCode != "Organization.CreateSite.v1") failures.Add("Site handler must use CreateSite operation code");
        assertions++; if (commands.LastExpectedVersion != null) failures.Add("CreateSite must have null ExpectedVersion");
        // Replay
        var siteReplay = await ConfigurationEndpoints.CreateSiteAsync(context.Request, commands, executor, accessor, transactionFactory, CancellationToken.None);
        assertions++; if (commands.MutationCalls != 1 || transactionFactory.BeginCount != 1 || siteReplay is not IdempotentHttpResult httpReplay || httpReplay.Response.Code != "DUPLICATE")
            failures.Add("Completed Site replay must bypass owner transaction and return DUPLICATE");
        // Area mutation via MapCommandMethods
        var areaContext = new DefaultHttpContext();
        areaContext.Request.Method = "PUT";
        areaContext.Request.Headers["Idempotency-Key"] = "area-update-1";
        areaContext.Request.Headers["If-Match"] = "\"3\"";
        areaContext.Request.RouteValues["areaId"] = Guid.NewGuid().ToString("D");
        areaContext.Request.QueryString = new QueryString("?name=Area%20Two");
        var areaResult = await ConfigurationEndpoints.ExecuteGenericAsync("Organization.UpdateArea.v1",
            new Guid(areaContext.Request.RouteValues["areaId"]!.ToString()!), areaContext.Request,
            commands, executor, accessor, transactionFactory, CancellationToken.None);
        assertions++; if (commands.MutationCalls != 2 || commands.LastExpectedVersion != 3) failures.Add("Area PUT must forward ExpectedVersion=3");
        // JSON command fields are canonicalized into the fingerprint and forwarded to the runtime port.
        var bodyContext = new DefaultHttpContext();
        bodyContext.Request.Method = "POST";
        bodyContext.Request.Headers["Idempotency-Key"] = "point-json-body";
        var bodyJson = """
            {"name":"Point One","metricId":"11111111-1111-4111-8111-111111111111",
             "expectedIntervalSeconds":1,"enabled":true}
            """;
        bodyContext.Request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(bodyJson));
        bodyContext.Request.ContentType = "application/json";
        await ConfigurationEndpoints.ExecuteGenericAsync(
            "Organization.CreatePoint.v1", Guid.NewGuid(), bodyContext.Request,
            commands, executor, accessor, transactionFactory, CancellationToken.None);
        assertions++;
        if (!commands.LastFields.Any(field => field.Name == "metricId" &&
                Equals(field.Value, Guid.Parse("11111111-1111-4111-8111-111111111111"))) ||
            !commands.LastFields.Any(field => field.Name == "expectedIntervalSeconds" &&
                Convert.ToInt64(field.Value) == 1) ||
            !commands.LastFields.Any(field => field.Name == "enabled" &&
                Equals(field.Value, true)))
            failures.Add("JSON command body must be typed, fingerprinted and forwarded to the runtime port");
        // Missing If-Match on PUT
        var noIfMatchContext = new DefaultHttpContext();
        noIfMatchContext.Request.Method = "PUT";
        noIfMatchContext.Request.Headers["Idempotency-Key"] = "no-ifmatch-1";
        var noIfMatchResult = await ConfigurationEndpoints.ExecuteGenericAsync("Organization.UpdateArea.v1",
            Guid.NewGuid(), noIfMatchContext.Request, commands, executor, accessor, transactionFactory, CancellationToken.None);
        assertions++; if (noIfMatchResult is not Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult problem || problem.StatusCode != 400)
            failures.Add("PUT without If-Match must return 400");
        // Asset scoped query
        var assetQueryContext = new DefaultHttpContext();
        var accessor2 = new FakeServerPrincipalAccessor(new ServerPrincipal(Guid.NewGuid(), "engineer", new HashSet<string> { Guid.NewGuid().ToString("D") }, new HashSet<string>()));
        var sitesResult = await ConfigurationEndpoints.ListAsync("sites", commands, accessor2, CancellationToken.None);
        assertions++; if (commands.Queries.Count < 1) failures.Add("ListAsync must delegate to query port");
        // Source, Mapping, SimulatorConfiguration target binding
        var sourceContext = new DefaultHttpContext();
        sourceContext.Request.Method = "POST";
        sourceContext.Request.Headers["Idempotency-Key"] = "source-lifecycle-1";
        sourceContext.Request.Headers["If-Match"] = "\"5\"";
        sourceContext.Request.RouteValues["sourceId"] = Guid.NewGuid().ToString("D");
        var sourceLifecycleResult = await ConfigurationEndpoints.ExecuteGenericAsync("Acquisition.SuspendSource.v1",
            new Guid(sourceContext.Request.RouteValues["sourceId"]!.ToString()!), sourceContext.Request,
            commands, executor, accessor, transactionFactory, CancellationToken.None);
        assertions++; if (commands.MutationCalls != 4 || commands.LastTargetId != new Guid(sourceContext.Request.RouteValues["sourceId"]!.ToString()!) ||
            commands.LastExpectedVersion != 5)
            failures.Add("Source lifecycle must forward sourceId and ExpectedVersion");
        var mappingContext = new DefaultHttpContext();
        mappingContext.Request.Method = "POST";
        mappingContext.Request.Headers["Idempotency-Key"] = "mapping-lifecycle-1";
        mappingContext.Request.Headers["If-Match"] = "\"2\"";
        mappingContext.Request.RouteValues["mappingId"] = Guid.NewGuid().ToString("D");
        var mappingLifecycleResult = await ConfigurationEndpoints.ExecuteGenericAsync("Acquisition.ActivateMapping.v1",
            new Guid(mappingContext.Request.RouteValues["mappingId"]!.ToString()!), mappingContext.Request,
            commands, executor, accessor, transactionFactory, CancellationToken.None);
        assertions++; if (commands.MutationCalls != 5 || commands.LastTargetId != new Guid(mappingContext.Request.RouteValues["mappingId"]!.ToString()!) ||
            commands.LastExpectedVersion != 2)
            failures.Add("Mapping lifecycle must forward mappingId and ExpectedVersion");
        var configContext = new DefaultHttpContext();
        configContext.Request.Method = "PUT";
        configContext.Request.Headers["Idempotency-Key"] = "config-lifecycle-1";
        configContext.Request.Headers["If-Match"] = "\"1\"";
        configContext.Request.RouteValues["configurationId"] = Guid.NewGuid().ToString("D");
        var configLifecycleResult = await ConfigurationEndpoints.ExecuteGenericAsync("Acquisition.UpdateSimulatorConfiguration.v1",
            new Guid(configContext.Request.RouteValues["configurationId"]!.ToString()!), configContext.Request,
            commands, executor, accessor, transactionFactory, CancellationToken.None);
        assertions++; if (commands.MutationCalls != 6 || commands.LastTargetId != new Guid(configContext.Request.RouteValues["configurationId"]!.ToString()!) ||
            commands.LastExpectedVersion != 1)
            failures.Add("SimulatorConfiguration update must forward configurationId and ExpectedVersion");
        // Every target-bearing configuration route binds and forwards its exact identifier.
        var targetCases = new (string RouteKey, string OperationCode)[]
        {
            ("siteId", "Organization.UpdateSite.v1"),
            ("areaId", "Organization.UpdateArea.v1"),
            ("assetId", "Organization.UpdateAsset.v1"),
            ("pointId", "Organization.UpdatePoint.v1"),
            ("metricId", "Catalog.UpdateMetric.v1"),
            ("unitId", "Catalog.UpdateUnit.v1"),
            ("sourceId", "Acquisition.UpdateSource.v1"),
            ("mappingId", "Acquisition.UpdateMapping.v1"),
            ("configurationId", "Acquisition.UpdateSimulatorConfiguration.v1"),
        };
        foreach (var (routeKey, operationCode) in targetCases)
        {
            var target = Guid.NewGuid();
            var targetContext = new DefaultHttpContext();
            targetContext.Request.Method = "PUT";
            targetContext.Request.RouteValues[routeKey] = target.ToString("D");
            targetContext.Request.Headers["Idempotency-Key"] = $"target-{routeKey}";
            targetContext.Request.Headers["If-Match"] = "\"7\"";
            var resolved = ConfigurationEndpoints.ResolveRouteTarget(targetContext.Request);
            var beforeCalls = commands.MutationCalls;
            await ConfigurationEndpoints.ExecuteGenericAsync(operationCode, resolved, targetContext.Request,
                commands, executor, accessor, transactionFactory, CancellationToken.None);
            assertions++;
            if (resolved != target || commands.MutationCalls != beforeCalls + 1 ||
                commands.LastTargetId != target || commands.LastExpectedVersion != 7 ||
                commands.LastOperationCode != operationCode || commands.LastPrincipal?.UserId != principal.UserId)
                failures.Add($"{routeKey} must bind target, operation, expected version and server principal");
        }
        // Representative create operations cover every remaining configuration group.
        foreach (var operationCode in new[]
        {
            "Organization.CreateAsset.v1", "Organization.CreatePoint.v1",
            "Catalog.CreateMetric.v1", "Catalog.CreateUnit.v1",
            "Acquisition.CreateSource.v1", "Acquisition.CreateMapping.v1",
            "Acquisition.CreateSimulatorConfiguration.v1"
        })
        {
            var createContext = new DefaultHttpContext();
            createContext.Request.Method = "POST";
            createContext.Request.Headers["Idempotency-Key"] = $"create-{operationCode}";
            var beforeCalls = commands.MutationCalls;
            await ConfigurationEndpoints.ExecuteGenericAsync(operationCode, null, createContext.Request,
                commands, executor, accessor, transactionFactory, CancellationToken.None);
            assertions++;
            if (commands.MutationCalls != beforeCalls + 1 || commands.LastOperationCode != operationCode ||
                commands.LastTargetId is not null || commands.LastExpectedVersion is not null)
                failures.Add($"{operationCode} create handler seam must execute with the canonical operation and no invented target/version");
        }
        // Malformed If-Match is rejected before mutation.
        var malformedContext = new DefaultHttpContext();
        malformedContext.Request.Method = "DELETE";
        malformedContext.Request.Headers["Idempotency-Key"] = "malformed-if-match";
        malformedContext.Request.Headers["If-Match"] = "garbage";
        var beforeMalformed = commands.MutationCalls;
        var malformedResult = await ConfigurationEndpoints.ExecuteGenericAsync("Acquisition.UpdateSource.v1",
            Guid.NewGuid(), malformedContext.Request, commands, executor, accessor, transactionFactory, CancellationToken.None);
        assertions++; if (malformedResult is not Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult malformedProblem ||
            malformedProblem.StatusCode != 400 || commands.MutationCalls != beforeMalformed)
            failures.Add("malformed If-Match must return 400 without executing a mutation");
        // ExpectedVersion participates in the fingerprint, preserving the exact conflict response.
        var conflictTarget = Guid.NewGuid();
        var conflictContext = new DefaultHttpContext();
        conflictContext.Request.Method = "PUT";
        conflictContext.Request.Headers["Idempotency-Key"] = "expected-version-conflict";
        conflictContext.Request.Headers["If-Match"] = "\"1\"";
        await ConfigurationEndpoints.ExecuteGenericAsync("Organization.UpdateAsset.v1", conflictTarget,
            conflictContext.Request, commands, executor, accessor, transactionFactory, CancellationToken.None);
        conflictContext.Request.Headers["If-Match"] = "\"2\"";
        var conflictResult = await ConfigurationEndpoints.ExecuteGenericAsync("Organization.UpdateAsset.v1", conflictTarget,
            conflictContext.Request, commands, executor, accessor, transactionFactory, CancellationToken.None);
        assertions++; if (conflictResult is not IdempotentHttpResult conflictHttp ||
            conflictHttp.Response.StatusCode != 409 ||
            conflictHttp.Response.Code != "IDEMPOTENCY_CONFLICT" ||
            conflictHttp.Response.Body != "{\"errorCode\":\"IDEMPOTENCY_CONFLICT\"}")
            failures.Add("ExpectedVersion fingerprint mismatch must preserve the exact idempotency conflict response");
        // Scoped query bypasses command idempotency and owner transactions.
        var beginBeforeQuery = transactionFactory.BeginCount;
        var queryCountBefore = commands.Queries.Count;
        await ConfigurationEndpoints.ListAsync($"assets:{Guid.NewGuid():D}", commands, accessor, CancellationToken.None);
        assertions++; if (commands.Queries.Count != queryCountBefore + 1 || transactionFactory.BeginCount != beginBeforeQuery)
            failures.Add("scoped query must call the query port and bypass command idempotency/transactions");
        // Operation code checks
        assertions++; if (!CommandOperationCodes.IsKnown("Simulator.Start.v1") || CommandOperationCodes.IsKnown("Auth.Login.v1") || CommandOperationCodes.IsKnown("Auth.Logout.v1") || CommandOperationCodes.IsKnown("Telemetry.Query.v1"))
            failures.Add("login/logout/query must not enter command idempotency");
        TestCount = assertions; AssertionCount = assertions;
        FailureCount = failures.Count;
        return failures;
    }
}
