using IUMP.Api.Infrastructure;
using IUMP.Api;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Integration.Contracts;

namespace IUMP.Tests.Unit.Api;

/// T049: Phase 3 red tests for explicit Simulator selection.
public static class SimulatorSelectionTests
{
    public static int TestCount { get; private set; }
    public static int AssertionCount { get; private set; }

    public static List<string> Run()
    {
        TestCount = 0;
        AssertionCount = 0;
        var failures = new List<string>();
        TestCount++;
        Check(!SimulatorWorkspaceSelectionRules.IsExplicit(null),
            "No selection must remain unselected; the first Source must never be inferred.", failures);

        var first = Option(Guid.NewGuid(), "SITE-A", Guid.NewGuid(), Guid.NewGuid());
        var second = Option(Guid.NewGuid(), "SITE-B", Guid.NewGuid(), Guid.NewGuid());
        TestCount++;
        Check(SimulatorWorkspaceSelectionRules.Resolve(new[] { first, second }, null) is null,
            "Opening Simulator with options must not choose index zero.", failures);

        var selected = new SimulatorSelection(
            first.SiteId, first.AreaId, first.AssetId, first.SourceId,
            first.ConfigurationId, first.ConfigurationVersion);
        TestCount++;
        Check(SimulatorWorkspaceSelectionRules.Resolve(new[] { first, second }, selected) == first,
            "The selected Source/configuration must be resolved by identity, not response order.", failures);

        TestCount++;
        var siteOnly = selected with { AreaId = null, AssetId = null };
        Check(SimulatorWorkspaceSelectionRules.Resolve(new[] { first, second }, siteOnly) == first,
            "Area and Asset may be omitted when the selected Site context is intentionally broad.", failures);

        LegacyMutationRouteTests(failures);
        WebRetryContractTests(failures);

        return failures;
    }

    private static void LegacyMutationRouteTests(List<string> failures)
    {
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.Services.AddSingleton<ISimulatorQueryPort>(_ => throw new NotSupportedException());
        builder.Services.AddSingleton<ISimulatorCommandPort>(_ => throw new NotSupportedException());
        builder.Services.AddSingleton<ISimulatorWorkspaceQueryPort>(_ => throw new NotSupportedException());
        builder.Services.AddSingleton<ISimulatorWorkspaceCommandPort>(_ => throw new NotSupportedException());
        builder.Services.AddSingleton<IServerPrincipalAccessor>(_ => throw new NotSupportedException());
        builder.Services.AddSingleton<IdempotentCommandExecutor>(_ => throw new NotSupportedException());
        builder.Services.AddSingleton<IHostTransactionFactory>(_ => throw new NotSupportedException());
        using var app = builder.Build();
        app.MapSimulatorEndpoints();
        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
                .Contains("POST", StringComparer.OrdinalIgnoreCase) == true)
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(
                "/api/v1/simulators/", StringComparison.Ordinal) == true)
            .ToArray();
        Check(endpoints.Length == 4,
            "Only four selected workspace Simulator mutation routes may remain operational.", failures);
        Check(endpoints.All(endpoint => endpoint.RoutePattern.RawText?.Contains(
                "/workspace/", StringComparison.Ordinal) == true || endpoint.RoutePattern.RawText?.EndsWith(
                "/workspace/start", StringComparison.Ordinal) == true),
            "Source-only and Run-only Simulator mutation routes must be retired.", failures);
        Check(endpoints.All(endpoint => endpoint.Metadata.GetMetadata<IAntiforgeryMetadata>()?.RequiresValidation == true),
            "Every operational Simulator mutation route must require antiforgery validation.", failures);
    }

    private static SimulatorSelectionOption Option(Guid siteId, string siteCode,
        Guid areaId, Guid sourceId) => new(
        siteId, siteCode, siteCode, areaId, "AREA", "Area", Guid.NewGuid(), "ASSET", "Asset",
        sourceId, "SIM", "Simulator", 2, Guid.NewGuid(), 3, 10, true, null);

    private static void WebRetryContractTests(List<string> failures)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "IUMP.slnx")))
            root = root.Parent;
        var helperPath = root is null
            ? string.Empty
            : Path.Combine(root.FullName, "src", "Web", "src", "gateways", "simulatorRetry.ts");
        var gatewayPath = root is null
            ? string.Empty
            : Path.Combine(root.FullName, "src", "Web", "src", "gateways", "webGateways.ts");
        var routePath = root is null
            ? string.Empty
            : Path.Combine(root.FullName, "src", "Web", "src", "features", "simulator", "SimulatorRoute.tsx");
        var helper = File.Exists(helperPath) ? File.ReadAllText(helperPath) : string.Empty;
        var gateway = File.Exists(gatewayPath) ? File.ReadAllText(gatewayPath) : string.Empty;
        var route = File.Exists(routePath) ? File.ReadAllText(routePath) : string.Empty;
        Check(!string.IsNullOrWhiteSpace(helper),
            "The pure Web Simulator retry helper must be present.", failures);
        Check(helper.Contains("operation") && helper.Contains("selection") &&
              helper.Contains("runId") && helper.Contains("expectedVersion") &&
              helper.Contains("idempotencyKey"),
            "Retry identity must include operation, complete selection, Run/version, and key.", failures);
        Check(helper.Contains("selectionFingerprint") && helper.Contains("mutationIdentityMatches") &&
              helper.Contains("createPendingSimulatorMutation"),
            "Retry identity must expose pure fingerprint, match, and creation helpers.", failures);
        Check(helper.Contains("RUNTIME_DEPENDENCY_UNAVAILABLE") &&
              helper.Contains("DEPENDENCY_UNAVAILABLE") && helper.Contains("status === 503") &&
              helper.Contains("runtime-error"),
            "Dependency HTTP/code states and runtime error states must be distinguished by a pure helper.", failures);
        Check(gateway.Contains("mutationIdentityMatches") && gateway.Contains("pending.idempotencyKey") &&
              gateway.Contains("pending.expectedVersion") && gateway.Contains("clearPendingMutation"),
            "The Web gateway must reuse pending identity/version and expose cancellation cleanup.", failures);
        Check(gateway.Contains("simulatorErrorKind") && gateway.Contains("isRetryableSimulatorError") &&
              gateway.Contains("request-503") && gateway.Contains("TypeError") &&
              gateway.Contains("MALFORMED_RESPONSE") &&
              gateway.Contains("error.message === 'MALFORMED_RESPONSE'"),
            "Simulator gateway must map dependency responses separately from network failures while retaining retryability.", failures);
        Check(route.Contains("dependencyMessage") && route.Contains("runtimeMessage"),
            "Simulator UI must display distinct Vietnamese dependency and runtime messages.", failures);
    }

    private static void Check(bool condition, string message, List<string> failures)
    {
        AssertionCount++;
        if (!condition) failures.Add(message);
    }
}
