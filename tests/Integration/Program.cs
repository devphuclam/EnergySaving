using IUMP.Composition.Postgres;
using IUMP.Infrastructure.Postgres;
using IUMP.Tests.Integration.Audit;
using IUMP.Tests.Integration.Acceptance;
using IUMP.Tests.Integration.Integration;
using IUMP.Tests.Integration.Operations;
using IUMP.Tests.Integration.OperationalWorkspace;
using IUMP.Tests.Integration.Runtime;
using Microsoft.Extensions.DependencyInjection;

LocalEnvironmentFile.LoadFromAncestors(Directory.GetCurrentDirectory());
var configuration = PostgresRuntimeConfiguration.CreateRuntime();
if (configuration.Host != PostgresRuntimeConfiguration.ApprovedLocalHost ||
    configuration.Port != PostgresRuntimeConfiguration.ApprovedLocalPort ||
    configuration.Database != PostgresRuntimeConfiguration.ApprovedLocalDatabase)
    throw new InvalidOperationException("RUNTIME_TARGET_REJECTED");

var services = new ServiceCollection();
services.AddIumpPostgresModules(configuration);
await using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

var failures = new List<string>();
failures.AddRange(await CommandIdempotencyApiTests.RunAsync(scope.ServiceProvider));
failures.AddRange(await AuditDeliveryTests.RunAsync(scope.ServiceProvider));
failures.AddRange(await AcceptancePostgresTests.RunAsync(provider));
failures.AddRange(await PostgresRuntimeLeafTests.RunAsync(provider));
failures.AddRange(await OperationalSetupJourneyTests.RunAsync(provider));
failures.AddRange(await ConfigurationManagementTests.RunAsync(provider));
failures.AddRange(await SimulatorOperationsTests.RunAsync(provider));
var latestHealth = await LatestHealthTests.RunAsync(provider);
failures.AddRange(latestHealth);
Console.WriteLine($"T058 latest-health target=127.0.0.1:5433/iump_dev cases={LatestHealthTests.TestCount}; assertions={LatestHealthTests.AssertionCount}; failures={latestHealth.Count}");
var operationsContract = new OperationsJobRepositoryContractRunner();
await operationsContract.RunAllAsync(
    new PostgresOperationsJobRepositoryTestProviderFactory(provider));
failures.AddRange(operationsContract.Failures.Select(
    failure => $"T206 canonical Operations contract: {failure}"));

Console.WriteLine(
    $"postgres-integration target=127.0.0.1:5433/iump_dev suites=15 failures={failures.Count}");
foreach (var failure in failures)
    Console.WriteLine($"FAIL: {failure}");
Environment.ExitCode = failures.Count == 0 ? 0 : 1;

file sealed class PostgresOperationsJobRepositoryTestProviderFactory(
    IServiceProvider services) : IOperationsJobRepositoryTestProviderFactory
{
    public OperationsJobRepositoryFixture Create() => new(
        services.GetRequiredService<IUMP.Modules.Operations.Contracts.IDurableJobScheduler>(),
        services.GetRequiredService<IUMP.Modules.Operations.Contracts.IJobClaimRepository>());
}
