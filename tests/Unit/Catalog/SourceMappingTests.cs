using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.Catalog.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Catalog;

public static class SourceMappingTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();
        var repo = new FakeCatalogCommandRepository();
        var source = new DataSource(DataSourceId.New(), "SIM01", "Simulator 1", SourceType.Simulator, SourceStatus.Draft, 1);
        repo.AddDataSourceAsync(source).GetAwaiter().GetResult();

        try { _ = new SourcePointMapping(MappingId.New(), source.Id, "P1", MappingStatus.Draft, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(-1), 1); failures.Add("EffectiveTo <= EffectiveFrom must be rejected"); }
        catch (ArgumentException) { }
        var localTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Local);
        var normalized = new SourcePointMapping(MappingId.New(), source.Id, "UTC_POINT", MappingStatus.Draft, localTime, null, 1);
        if (normalized.EffectiveFrom.Kind != DateTimeKind.Utc) failures.Add("mapping timestamps must be UTC");

        var first = new SourcePointMapping(MappingId.New(), source.Id, "P1", MappingStatus.Active, Utc(2025, 1, 1), Utc(2025, 2, 1), 1);
        var touching = new SourcePointMapping(MappingId.New(), source.Id, "P1", MappingStatus.Active, Utc(2025, 2, 1), Utc(2025, 3, 1), 1);
        repo.AddMappingAsync(first).GetAwaiter().GetResult();
        repo.AddMappingAsync(touching).GetAwaiter().GetResult();
        if (first.OverlapsWith(touching)) failures.Add("touching half-open intervals must not overlap");
        var overlap = new SourcePointMapping(MappingId.New(), source.Id, "P1", MappingStatus.Active, Utc(2025, 1, 15), Utc(2025, 1, 20), 1);
        try { repo.AddMappingAsync(overlap).GetAwaiter().GetResult(); failures.Add("overlapping Active periods must be rejected"); }
        catch (InvalidOperationException) { }

        var historical = new SourcePointMapping(MappingId.New(), source.Id, "P1", MappingStatus.Inactive, Utc(2024, 1, 1), Utc(2024, 2, 1), 1);
        repo.AddMappingAsync(historical).GetAwaiter().GetResult();
        var eligibility = new FakeCatalogEligibilityQueryRepository(repo);
        var missing = eligibility.GetActiveMappingEligibilityAsync("MISSING", Utc(2025, 1, 15)).GetAwaiter().GetResult();
        if (missing.Outcome != MappingEligibilityOutcome.Missing) failures.Add("Missing eligibility outcome must be distinct");
        var multipleA = new SourcePointMapping(MappingId.New(), source.Id, "MULTI", MappingStatus.Active, Utc(2025, 1, 1), Utc(2025, 3, 1), 1);
        var multipleB = new SourcePointMapping(MappingId.New(), source.Id, "MULTI", MappingStatus.Active, Utc(2025, 1, 2), Utc(2025, 2, 1), 1);
        repo.SeedRawMappingForEligibility(multipleA);
        repo.SeedRawMappingForEligibility(multipleB);
        var multiple = eligibility.GetActiveMappingEligibilityAsync("MULTI", Utc(2025, 1, 15)).GetAwaiter().GetResult();
        if (multiple.Outcome != MappingEligibilityOutcome.Multiple) failures.Add("Multiple eligibility outcome must be distinct");

        var unusedSource = new DataSource(DataSourceId.New(), "UNUSED", "Unused", SourceType.Simulator, SourceStatus.Draft, 1);
        repo.AddDataSourceAsync(unusedSource).GetAwaiter().GetResult();
        repo.SetDataSourceDependencies(unusedSource.Id, new CatalogDependencySnapshot(AuditOnlySnapshot: true));
        if (!repo.DeleteDataSourceAsync(unusedSource.Id).GetAwaiter().GetResult().IsAllowed) failures.Add("Audit-only dependency must not block Draft source deletion");
        var blockedSource = new DataSource(DataSourceId.New(), "BLOCKED", "Blocked", SourceType.Simulator, SourceStatus.Draft, 1);
        repo.AddDataSourceAsync(blockedSource).GetAwaiter().GetResult();
        repo.SetDataSourceDependencies(blockedSource.Id, new CatalogDependencySnapshot(SimulatorRun: true));
        if (repo.DeleteDataSourceAsync(blockedSource.Id).GetAwaiter().GetResult().Code != "DEPENDENT_HISTORY") failures.Add("operational dependency must return DEPENDENT_HISTORY");

        var unusedMapping = new SourcePointMapping(MappingId.New(), source.Id, "UNUSED", MappingStatus.Draft, Utc(2025, 1, 1), null, 1);
        repo.AddMappingAsync(unusedMapping).GetAwaiter().GetResult();
        repo.SetMappingDependencies(unusedMapping.Id, new CatalogDependencySnapshot(AuditOnlySnapshot: true));
        if (!repo.DeleteMappingAsync(unusedMapping.Id).GetAwaiter().GetResult().IsAllowed) failures.Add("Audit-only dependency must not block Draft mapping deletion");

        var rollbackSource = new DataSource(DataSourceId.New(), "ROLLBACK", "Rollback", SourceType.Simulator, SourceStatus.Draft, 1);
        repo.AddDataSourceAsync(rollbackSource).GetAwaiter().GetResult();
        var tx = repo.BeginTransactionAsync().GetAwaiter().GetResult();
        repo.DeleteDataSourceAsync(rollbackSource.Id).GetAwaiter().GetResult();
        tx.RollbackAsync().GetAwaiter().GetResult();
        if (repo.GetDataSourceAsync(rollbackSource.Id).GetAwaiter().GetResult() is null) failures.Add("failed deletion must roll back without partial state change");
        return failures;
    }

    private static DateTime Utc(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}
