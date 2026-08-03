using IUMP.Api;
using IUMP.Api.Infrastructure;
using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Integration.Contracts;

namespace IUMP.Tests.Unit.Fakes;

public sealed class FakeServerPrincipalAccessor(ServerPrincipal? current) : IServerPrincipalAccessor
{
    public ServerPrincipal? Current { get; } = current;
}

public sealed class FakePhase9TransactionFactory : IHostTransactionFactory
{
    public FakeHostTransaction Last { get; }
    public int BeginCount { get; private set; }
    public FakePhase9TransactionFactory(bool failOnCommit = false)
    {
        Last = new FakeHostTransaction(Guid.NewGuid()) { FailOnCommit = failOnCommit };
    }
    public ValueTask<IHostTransaction> BeginAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        BeginCount++;
        return ValueTask.FromResult<IHostTransaction>(Last);
    }
}

public sealed class FakeConfigurationPorts : IConfigurationCommandPort, IConfigurationQueryPort
{
    public int MutationCalls { get; private set; }
    public Guid LastTransactionId { get; private set; }
    public string? LastOperationCode { get; private set; }
    public long? LastExpectedVersion { get; private set; }
    public Guid? LastTargetId { get; private set; }
    public ServerPrincipal? LastPrincipal { get; private set; }
    public IReadOnlyList<CommandFingerprintField> LastFields { get; private set; } =
        Array.Empty<CommandFingerprintField>();
    public List<string> Queries { get; } = new();

    public Task<CommandExecutionResult> CreateSiteAsync(ConfigurationCommandRequest request, ServerPrincipal principal,
        IHostTransaction transaction, CancellationToken ct = default) =>
        Mutate("Organization.CreateSite.v1", request, principal, transaction);

    public Task<CommandExecutionResult> UpdateSiteAsync(ConfigurationCommandRequest request, ServerPrincipal principal,
        IHostTransaction transaction, CancellationToken ct = default) =>
        Mutate("Organization.UpdateSite.v1", request, principal, transaction);

    public Task<CommandExecutionResult> ExecuteAsync(string operationCode, ConfigurationCommandRequest request,
        ServerPrincipal principal, IHostTransaction transaction, CancellationToken ct = default) =>
        Mutate(operationCode, request, principal, transaction);

    public Task<IReadOnlyList<object>> ListAsync(string resource, ServerPrincipal principal, CancellationToken ct = default)
    {
        Queries.Add(resource);
        return Task.FromResult<IReadOnlyList<object>>(new object[] { new { resource, principal.UserId } });
    }

    private Task<CommandExecutionResult> Mutate(string operation, ConfigurationCommandRequest request,
        ServerPrincipal principal, IHostTransaction transaction)
    {
        MutationCalls++;
        LastTransactionId = transaction.TransactionId;
        LastOperationCode = operation;
        LastExpectedVersion = request.ExpectedVersion;
        LastTargetId = request.TargetId;
        LastPrincipal = principal;
        LastFields = request.Fields;
        return Task.FromResult(CommandExecutionResult.Ok(201, $"{{\"operation\":\"{operation}\"}}", "resource-1",
            "/api/v1/resource-1", "\"1\"", "corr-phase9"));
    }
}

public sealed class FakeSimulatorPorts : ISimulatorCommandPort, ISimulatorQueryPort
{
    public int MutationCalls { get; private set; }
    public Guid LastRunId { get; private set; }
    public string? LastOperationCode { get; private set; }
    public long? LastExpectedVersion { get; private set; }
    public Guid LastTransactionId { get; private set; }
    public ServerPrincipal? LastPrincipal { get; private set; }
    public Task<CommandExecutionResult> ExecuteAsync(string operationCode, Guid targetId,
        long? expectedVersion, ServerPrincipal principal,
        IHostTransaction transaction, CancellationToken ct = default)
    {
        MutationCalls++;
        LastRunId = targetId;
        LastOperationCode = operationCode;
        LastExpectedVersion = expectedVersion;
        LastTransactionId = transaction.TransactionId;
        LastPrincipal = principal;
        return Task.FromResult(CommandExecutionResult.Ok(202, "{\"status\":\"accepted\"}", targetId.ToString("D"),
            $"/api/v1/simulators/{targetId:D}", "\"1\"", "corr-simulator"));
    }

    public Task<object> GetRunAsync(Guid runId, ServerPrincipal principal, CancellationToken ct = default)
    {
        LastRunId = runId; LastPrincipal = principal;
        return Task.FromResult<object>(new { runId, status = "Running", generated = 2, accepted = 1, rejected = 0 });
    }
}

public sealed class FakeTelemetryPorts : ITelemetryQueryPort
{
    public ServerPrincipal? LastPrincipal { get; private set; }
    public Guid LastPointId { get; private set; }
    public Guid LastSiteId { get; private set; }
    public Task<LatestQueryResult> GetLatestAsync(Guid pointId, ServerPrincipal principal, CancellationToken ct = default)
    {
        LastPointId = pointId; LastPrincipal = principal;
        return Task.FromResult(new LatestQueryResult(pointId, null, null, "No Data", true, "NO_DATA"));
    }

    public Task<object> GetSourceHealthAsync(Guid pointId, ServerPrincipal principal, CancellationToken ct = default)
    {
        LastPointId = pointId; LastPrincipal = principal;
        return Task.FromResult<object>(new { pointId, status = "Healthy", lastReceivedAtUtc = DateTime.UtcNow });
    }

    public Task<IReadOnlyList<LatestQueryResult>> GetCurrentAsync(Guid siteId, ServerPrincipal principal, CancellationToken ct = default)
    {
        LastSiteId = siteId; LastPrincipal = principal;
        return Task.FromResult<IReadOnlyList<LatestQueryResult>>(Array.Empty<LatestQueryResult>());
    }
}

public sealed class FakeAuditQueryPort : IAuditQueryPort
{
    private readonly bool _returnForbidden;
    public FakeAuditQueryPort(bool returnForbidden = false) { _returnForbidden = returnForbidden; }
    public IReadOnlyDictionary<string, string?>? LastFilters { get; private set; }
    public string? LastCursor { get; private set; }
    public int LastPageSize { get; private set; }
    public ServerPrincipal? LastPrincipal { get; private set; }
    public int QueryCount { get; private set; }
    public Task<AuditQueryPage> QueryAsync(IReadOnlyDictionary<string, string?> filters, ServerPrincipal principal,
        string? cursor, int pageSize, CancellationToken ct = default)
    {
        QueryCount++;
        LastFilters = filters; LastCursor = cursor; LastPrincipal = principal; LastPageSize = pageSize;
        if (_returnForbidden) return Task.FromResult(new AuditQueryPage(Array.Empty<object>(), "FORBIDDEN", null, 0));
        return Task.FromResult(new AuditQueryPage(new object[] { new { actor = principal.Username, pageSize } }, null, "next", 1));
    }
}
