using IUMP.Modules.Catalog.Application;
using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.Catalog.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Integration.Catalog;

public sealed record CatalogRepositoryTestProvider(
    ICatalogCommandRepository Commands,
    ICatalogEligibilityQueryRepository Eligibility,
    ICatalogPointReadinessQuery Readiness,
    Action<DataSourceId, CatalogDependencySnapshot> ConfigureSourceDependencies,
    Action<MappingId, CatalogDependencySnapshot> ConfigureMappingDependencies,
    Func<string, PointReadinessSnapshot?> CreatePointReadiness,
    Action Reset);

public interface ICatalogRepositoryTestProviderFactory
{
    CatalogRepositoryTestProvider Create();
}

public sealed class FakeCatalogRepositoryTestProviderFactory : ICatalogRepositoryTestProviderFactory
{
    public CatalogRepositoryTestProvider Create()
    {
        var commands = new FakeCatalogCommandRepository();
        var fakeReadiness = new FakePointReadinessQuery();
        return new CatalogRepositoryTestProvider(
            commands,
            new FakeCatalogEligibilityQueryRepository(commands),
            fakeReadiness,
            (id, deps) => commands.SetDataSourceDependencies(id, deps),
            (id, deps) => commands.SetMappingDependencies(id, deps),
            pointId => null,
            () => { });
    }
}

public sealed class CatalogRepositoryContractRunner
{
    private readonly ICatalogRepositoryTestProviderFactory _factory;
    public List<string> Failures { get; } = new();
    public CatalogRepositoryContractRunner(ICatalogRepositoryTestProviderFactory factory) => _factory = factory;

    public async Task RunAllAsync()
    {
        await SourceCodeUniquenessAsync();
        await SourceLifecyclePersistenceAsync();
        await MappingOverlapRejectionAsync();
        await DraftMappingDeletionAsync();
        await AuditOnlyDependencyAsync();
        await OperationalDependencyAsync();
        await TransactionCommitAsync();
        await TransactionRollbackAsync();
        await OptimisticVersionConflictAsync();
    }

    private async Task SourceCodeUniquenessAsync()
    {
        var p = _factory.Create();
        var repo = p.Commands;
        var source = new DataSource(DataSourceId.New(), "UNIQUE_SRC", "Unique Source", SourceType.Simulator, SourceStatus.Draft, 1);
        await repo.AddDataSourceAsync(source);
        var dup = new DataSource(DataSourceId.New(), "unique_src", "Duplicate Source", SourceType.Simulator, SourceStatus.Draft, 1);
        await ExpectThrows(() => repo.AddDataSourceAsync(dup), "T049: duplicate source code rejection");
    }

    private async Task SourceLifecyclePersistenceAsync()
    {
        var p = _factory.Create();
        var repo = p.Commands;
        var source = new DataSource(DataSourceId.New(), "LIFECYCLE_SRC", "Lifecycle", SourceType.Simulator, SourceStatus.Draft, 1);
        await repo.AddDataSourceAsync(source);
        source.TryTransitionTo(SourceStatus.Active);
        await repo.UpdateDataSourceAsync(source);
        var afterActivate = await repo.GetDataSourceAsync(source.Id);
        if (afterActivate?.Status != SourceStatus.Active) Failures.Add("T049: source lifecycle Draft->Active persistence");

        afterActivate!.TryTransitionTo(SourceStatus.Suspended);
        await repo.UpdateDataSourceAsync(afterActivate);
        var afterSuspended = await repo.GetDataSourceAsync(source.Id);
        if (afterSuspended?.Status != SourceStatus.Suspended) Failures.Add("T049: source lifecycle Active->Suspended persistence");
    }

    private async Task MappingOverlapRejectionAsync()
    {
        var p = _factory.Create();
        var repo = p.Commands;
        var source = new DataSource(DataSourceId.New(), "OVERLAP_SRC", "Overlap", SourceType.Simulator, SourceStatus.Active, 1);
        await repo.AddDataSourceAsync(source);
        var first = new SourcePointMapping(MappingId.New(), source.Id, "OVERLAP_PT", MappingStatus.Active,
            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc), 1);
        await repo.AddMappingAsync(first);
        var overlapping = new SourcePointMapping(MappingId.New(), source.Id, "OVERLAP_PT", MappingStatus.Active,
            new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc), 1);
        await ExpectThrows(() => repo.AddMappingAsync(overlapping), "T049: overlapping Active mapping rejection");
    }

    private async Task DraftMappingDeletionAsync()
    {
        var p = _factory.Create();
        var repo = p.Commands;
        var source = new DataSource(DataSourceId.New(), "DEL_SRC", "Delete", SourceType.Simulator, SourceStatus.Draft, 1);
        await repo.AddDataSourceAsync(source);
        var mapping = new SourcePointMapping(MappingId.New(), source.Id, "DEL_PT", MappingStatus.Draft,
            DateTime.UtcNow, null, 1);
        await repo.AddMappingAsync(mapping);
        var result = await repo.DeleteMappingAsync(mapping.Id);
        if (!result.IsAllowed) Failures.Add("T049: Draft mapping deletion");
        var gone = await repo.GetMappingAsync(mapping.Id);
        if (gone is not null) Failures.Add("T049: Draft mapping deletion removed");
    }

    private async Task AuditOnlyDependencyAsync()
    {
        var p = _factory.Create();
        var repo = p.Commands;
        p.ConfigureSourceDependencies(DataSourceId.New(), new CatalogDependencySnapshot(AuditOnlySnapshot: true));
        // No source with that ID in repo, so deletion will return NotFound, not block
        var source = new DataSource(DataSourceId.New(), "AUDIT_SRC", "Audit", SourceType.Simulator, SourceStatus.Draft, 1);
        await repo.AddDataSourceAsync(source);
        p.ConfigureSourceDependencies(source.Id, new CatalogDependencySnapshot(AuditOnlySnapshot: true));
        var result = await repo.DeleteDataSourceAsync(source.Id);
        if (!result.IsAllowed) Failures.Add("T049: AuditOnlySnapshot must not block Draft source deletion");
    }

    private async Task OperationalDependencyAsync()
    {
        var p = _factory.Create();
        var repo = p.Commands;
        var source = new DataSource(DataSourceId.New(), "OPS_SRC", "Ops", SourceType.Simulator, SourceStatus.Draft, 1);
        await repo.AddDataSourceAsync(source);
        p.ConfigureSourceDependencies(source.Id, new CatalogDependencySnapshot(SimulatorRun: true));
        var result = await repo.DeleteDataSourceAsync(source.Id);
        if (result.Code != "DEPENDENT_HISTORY") Failures.Add("T049: operational dependency must return DEPENDENT_HISTORY");
    }

    private async Task TransactionCommitAsync()
    {
        var p = _factory.Create();
        var repo = p.Commands;
        var source = new DataSource(DataSourceId.New(), "COMMIT_SRC", "Commit", SourceType.Simulator, SourceStatus.Draft, 1);
        var tx = await repo.BeginTransactionAsync();
        await repo.AddDataSourceAsync(source);
        await tx.CommitAsync();
        var after = await repo.GetDataSourceAsync(source.Id);
        if (after is null) Failures.Add("T049: transaction commit with mutation after BeginTransaction");
    }

    private async Task TransactionRollbackAsync()
    {
        var p = _factory.Create();
        var repo = p.Commands;
        var source = new DataSource(DataSourceId.New(), "ROLL_SRC", "Rollback", SourceType.Simulator, SourceStatus.Draft, 1);
        await repo.AddDataSourceAsync(source);
        var tx = await repo.BeginTransactionAsync();
        await repo.DeleteDataSourceAsync(source.Id);
        await tx.RollbackAsync();
        var after = await repo.GetDataSourceAsync(source.Id);
        if (after is null) Failures.Add("T049: transaction rollback must restore mutation");
    }

    private async Task OptimisticVersionConflictAsync()
    {
        var p = _factory.Create();
        var repo = p.Commands;
        var metric = new Metric(MetricId.New(), "VERSION_M", "Version Test", MetricStatus.Active, 1);
        await repo.AddMetricAsync(metric);

        var fresh = await repo.GetMetricAsync(metric.Id);
        var v0Version = fresh!.Version;
        fresh!.Inactivate();
        await repo.UpdateMetricAsync(fresh!);
        var stale = (await repo.GetMetricAsync(metric.Id))!;
        stale = new Metric(stale!.Id, stale.Code, stale.Name, stale.Status, v0Version);
        try { await repo.UpdateMetricAsync(stale); Failures.Add("T049: optimistic version conflict"); }
        catch (InvalidOperationException) { }
    }

    private async Task ExpectThrows(Func<Task> action, string name)
    {
        try { await action(); Failures.Add(name); }
        catch (InvalidOperationException) { }
    }
}
