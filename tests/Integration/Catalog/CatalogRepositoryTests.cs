using IUMP.Modules.Catalog.Application;
using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.Catalog.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Integration.Catalog;

public sealed record CatalogRepositoryTestProvider(ICatalogCommandRepository Commands, ICatalogEligibilityQueryRepository Eligibility);

public interface ICatalogRepositoryTestProviderFactory
{
    CatalogRepositoryTestProvider Create();
}

public sealed class FakeCatalogRepositoryTestProviderFactory : ICatalogRepositoryTestProviderFactory
{
    public CatalogRepositoryTestProvider Create()
    {
        var commands = new FakeCatalogCommandRepository();
        return new CatalogRepositoryTestProvider(commands, new FakeCatalogEligibilityQueryRepository(commands));
    }
}

/// <summary>
/// Adapter-agnostic Catalog repository contract suite. PostgreSQL adapters can use the same
/// provider factory when the approved package source and database become available.
/// </summary>
public sealed class CatalogRepositoryContractRunner
{
    private readonly ICatalogRepositoryTestProviderFactory _factory;
    public List<string> Failures { get; } = new();
    public CatalogRepositoryContractRunner(ICatalogRepositoryTestProviderFactory factory) => _factory = factory;

    public async Task RunAllAsync()
    {
        var provider = _factory.Create();
        var repo = provider.Commands;
        var eligibility = provider.Eligibility;
        var metric = new Metric(MetricId.New(), "CONTRACT_METRIC", "Contract Metric", MetricStatus.Active, 1);
        var unit = new MetricUnit(UnitId.New(), "CONTRACT_UNIT", "cu", MetricUnitStatus.Active, 1);
        await repo.AddMetricAsync(metric);
        await repo.AddUnitAsync(unit);
        await ExpectThrows(() => repo.AddMetricAsync(new Metric(MetricId.New(), "contract_metric", "Duplicate", MetricStatus.Active, 1)), "metric code uniqueness");
        await ExpectThrows(() => repo.AddUnitAsync(new MetricUnit(UnitId.New(), "contract_unit", "duplicate", MetricUnitStatus.Active, 1)), "unit code uniqueness");
        await repo.AddCompatibilityAsync(new MetricUnitCompatibility(metric.Id, unit.Id, true, 1));
        await ExpectThrows(() => repo.AddCompatibilityAsync(new MetricUnitCompatibility(metric.Id, unit.Id, false, 1)), "compatibility pair uniqueness");
        var unitTwo = new MetricUnit(UnitId.New(), "CONTRACT_UNIT_TWO", "cu2", MetricUnitStatus.Active, 1);
        await repo.AddUnitAsync(unitTwo);
        await ExpectThrows(() => repo.AddCompatibilityAsync(new MetricUnitCompatibility(metric.Id, unitTwo.Id, true, 1)), "canonical uniqueness");

        metric = (await repo.GetMetricAsync(metric.Id))!;
        metric.Inactivate();
        await repo.UpdateMetricAsync(metric);
        var inactiveMetric = await eligibility.GetMetricUnitEligibilityAsync(metric.Id, unit.Id);
        if (inactiveMetric.IsEligible) Failures.Add("inactive metric eligibility");
        unit = (await repo.GetUnitAsync(unit.Id))!;
        unit.Inactivate();
        await repo.UpdateUnitAsync(unit);
        var inactiveUnit = await eligibility.GetMetricUnitEligibilityAsync(metric.Id, unit.Id);
        if (inactiveUnit.IsEligible) Failures.Add("inactive unit eligibility");

        var seedRepo = new FakeCatalogCommandRepository();
        var seed = new CatalogSeedApplicationService(seedRepo);
        var first = await seed.ApplyAsync();
        var second = await seed.ApplyAsync();
        if (first.MetricsAdded != 2 || first.UnitsAdded != 2 || first.CompatibilitiesAdded != 2 || second.MetricsAdded != 0 || second.UnitsAdded != 0 || second.CompatibilitiesAdded != 0 || second.VersionsChanged != 0)
            Failures.Add("seed idempotency");

        var source = new DataSource(DataSourceId.New(), "CONTRACT_SOURCE", "Contract Source", SourceType.Simulator, SourceStatus.Draft, 1);
        await repo.AddDataSourceAsync(source);
        var mapping = new SourcePointMapping(MappingId.New(), source.Id, "CONTRACT_POINT", MappingStatus.Draft, DateTime.UtcNow, null, 1);
        await repo.AddMappingAsync(mapping);
        var mappingCopy = (await repo.GetMappingAsync(mapping.Id))!;
        mappingCopy.TryActivate();
        await repo.UpdateMappingAsync(mappingCopy);
        var active = await eligibility.GetActiveMappingEligibilityAsync(mapping.PointId, DateTime.UtcNow);
        if (active.Outcome != MappingEligibilityOutcome.Missing) Failures.Add("draft source mapping must not be active until source is active");

        var committedSource = new DataSource(DataSourceId.New(), "COMMITTED_SOURCE", "Committed", SourceType.Simulator, SourceStatus.Draft, 1);
        await repo.AddDataSourceAsync(committedSource);
        var tx = await repo.BeginTransactionAsync();
        await tx.CommitAsync();
        if (await repo.GetDataSourceAsync(committedSource.Id) is null) Failures.Add("transaction commit");

        var rollbackSource = new DataSource(DataSourceId.New(), "ROLLBACK_SOURCE", "Rollback", SourceType.Simulator, SourceStatus.Draft, 1);
        await repo.AddDataSourceAsync(rollbackSource);
        var rollback = await repo.BeginTransactionAsync();
        var deleted = await repo.DeleteDataSourceAsync(rollbackSource.Id);
        await rollback.RollbackAsync();
        if (!deleted.IsAllowed || await repo.GetDataSourceAsync(rollbackSource.Id) is null) Failures.Add("deep transaction rollback");

        var stale = (await repo.GetMetricAsync(metric.Id))!;
        var current = (await repo.GetMetricAsync(metric.Id))!;
        current.Activate();
        await repo.UpdateMetricAsync(current);
        try { await repo.UpdateMetricAsync(stale); Failures.Add("optimistic version conflict"); }
        catch (InvalidOperationException) { }

        var auditOnly = new DataSource(DataSourceId.New(), "AUDIT_ONLY", "Audit only", SourceType.Simulator, SourceStatus.Draft, 1);
        await repo.AddDataSourceAsync(auditOnly);
        var auditDeletion = await repo.DeleteDataSourceAsync(auditOnly.Id);
        if (!auditDeletion.IsAllowed) Failures.Add("audit-only deletion");
        var dependent = new DataSource(DataSourceId.New(), "DEPENDENT", "Dependent", SourceType.Simulator, SourceStatus.Draft, 1);
        await repo.AddDataSourceAsync(dependent);
        if (repo is FakeCatalogCommandRepository fake) fake.SetDataSourceDependencies(dependent.Id, new CatalogDependencySnapshot(SimulatorRun: true));
        if ((await repo.DeleteDataSourceAsync(dependent.Id)).Code != "DEPENDENT_HISTORY") Failures.Add("dependent deletion");
    }

    private async Task ExpectThrows(Func<Task> action, string name)
    {
        try { await action(); Failures.Add(name); }
        catch (InvalidOperationException) { }
    }
}
