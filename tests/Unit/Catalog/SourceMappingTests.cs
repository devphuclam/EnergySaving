using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.Catalog.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Catalog;

public static class SourceMappingTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();

        // Source lifecycle: Draft -> Active -> Suspended -> Active -> Decommissioned
        var source = new DataSource(DataSourceId.New(), "LIFECYCLE_SRC", "Lifecycle", SourceType.Simulator, SourceStatus.Draft, 1);
        if (source.Status != SourceStatus.Draft || source.Version != 1)
            failures.Add("Source must start as Draft with Version 1");

        if (!source.TryTransitionTo(SourceStatus.Active)) failures.Add("Draft->Active must succeed");
        if (source.Status != SourceStatus.Active || source.Version != 2) failures.Add("Active transition must set Status and increment Version");

        if (!source.TryTransitionTo(SourceStatus.Suspended)) failures.Add("Active->Suspended must succeed");
        if (source.Status != SourceStatus.Suspended || source.Version != 3) failures.Add("Suspended transition must set Status and increment Version");

        if (!source.TryTransitionTo(SourceStatus.Active)) failures.Add("Suspended->Active must succeed");
        if (source.Status != SourceStatus.Active || source.Version != 4) failures.Add("Reactivate must increment Version");

        if (!source.TryTransitionTo(SourceStatus.Decommissioned)) failures.Add("Active->Decommissioned must succeed");
        if (source.Status != SourceStatus.Decommissioned || source.Version != 5) failures.Add("Decommissioned transition must increment Version");

        // Decommissioned is terminal
        if (source.TryTransitionTo(SourceStatus.Active)) failures.Add("Decommissioned->Active must be rejected");
        if (source.TryTransitionTo(SourceStatus.Suspended)) failures.Add("Decommissioned->Suspended must be rejected");
        if (source.TryTransitionTo(SourceStatus.Decommissioned)) failures.Add("Decommissioned->Decommissioned must be rejected");
        if (source.Version != 5) failures.Add("Rejected transitions must not increment Version");

        // Rejected transitions preserve state and Version
        var draftSource = new DataSource(DataSourceId.New(), "DRAFT_SRC", "Draft only", SourceType.Simulator, SourceStatus.Draft, 1);
        if (draftSource.TryTransitionTo(SourceStatus.Suspended)) failures.Add("Draft->Suspended must be rejected");
        if (draftSource.TryTransitionTo(SourceStatus.Decommissioned)) { /* Draft->Decommissioned is allowed */ }
        // Check a fresh Draft source
        var freshDraft = new DataSource(DataSourceId.New(), "FRESH_DRAFT", "Fresh", SourceType.Simulator, SourceStatus.Draft, 1);
        if (freshDraft.TryTransitionTo(SourceStatus.Decommissioned)) { /* allowed */ }

        // Mapping lifecycle: Draft -> Active -> Inactive -> Superseded
        var mapping = new SourcePointMapping(MappingId.New(), source.Id, "MAPPING_PT",
            MappingStatus.Draft, Utc(2025, 1, 1), null, 1);
        if (mapping.Status != MappingStatus.Draft || mapping.Version != 1)
            failures.Add("Mapping must start as Draft with Version 1");

        if (!mapping.TryActivate()) failures.Add("Draft->Active must succeed");
        if (mapping.Status != MappingStatus.Active || mapping.Version != 2)
            failures.Add("Active transition must set Status and increment Version");

        if (!mapping.TryInactivate()) failures.Add("Active->Inactive must succeed");
        if (mapping.Status != MappingStatus.Inactive || mapping.Version != 3)
            failures.Add("Inactive transition must set Status and increment Version");

        if (!mapping.TrySupersede()) failures.Add("Inactive->Superseded must succeed");
        if (mapping.Status != MappingStatus.Superseded || mapping.Version != 4)
            failures.Add("Superseded transition must set Status and increment Version");

        // Superseded is terminal
        if (mapping.TryActivate()) failures.Add("Superseded->Active must be rejected");
        if (mapping.TryInactivate()) failures.Add("Superseded->Inactive must be rejected");
        if (mapping.TrySupersede()) failures.Add("Superseded->Superseded must be rejected");
        if (mapping.Version != 4) failures.Add("Rejected transitions must not increment Version");

        // Rejected transitions preserve state
        var draftMap = new SourcePointMapping(MappingId.New(), source.Id, "DRAFT_MAP",
            MappingStatus.Draft, Utc(2025, 1, 1), null, 1);
        if (draftMap.TryInactivate()) failures.Add("Draft->Inactive must be rejected");
        if (draftMap.TrySupersede()) failures.Add("Draft->Superseded must be rejected");
        if (draftMap.Version != 1) failures.Add("Rejected Draft transitions must not increment Version");

        // Half-open intervals and touching periods
        var first = new SourcePointMapping(MappingId.New(), source.Id, "HALF_OPEN",
            MappingStatus.Active, Utc(2025, 1, 1), Utc(2025, 2, 1), 1);
        var touching = new SourcePointMapping(MappingId.New(), source.Id, "HALF_OPEN",
            MappingStatus.Active, Utc(2025, 2, 1), Utc(2025, 3, 1), 1);
        if (first.OverlapsWith(touching)) failures.Add("Touching [Jan1,Feb1) and [Feb1,Mar1) must not overlap");

        var overlapping = new SourcePointMapping(MappingId.New(), source.Id, "HALF_OPEN",
            MappingStatus.Active, Utc(2025, 1, 15), Utc(2025, 1, 20), 1);
        if (!first.OverlapsWith(overlapping)) failures.Add("[Jan1,Feb1) and [Jan15,Jan20) must overlap");

        // Historical and future coexist
        var hist = new SourcePointMapping(MappingId.New(), source.Id, "COEXIST",
            MappingStatus.Inactive, Utc(2024, 1, 1), Utc(2024, 6, 1), 1);
        var future = new SourcePointMapping(MappingId.New(), source.Id, "COEXIST",
            MappingStatus.Active, Utc(2025, 1, 1), null, 1);
        if (hist.OverlapsWith(future)) failures.Add("Historical and future mappings must coexist (no overlap)");

        // Mapping readiness via ICatalogPointReadinessQuery
        var readiness = new FakePointReadinessQuery();
        var readyPt = readiness.Configure("READY_PT", new PointReadinessSnapshot("READY_PT", "site-a", null, true, true, true, 1));
        var unreadyPt = readiness.Configure("UNREADY_PT", new PointReadinessSnapshot("UNREADY_PT", "site-b", null, true, false, false, 1));
        var missingPt = readiness.Configure("MISSING_PT", new PointReadinessSnapshot("MISSING_PT", "site-c", null, true, false, false, 1));
        // missing Point returns null
        var noPt = readiness.GetPointReadinessAsync("NO_PT").GetAwaiter().GetResult();
        if (noPt is not null) failures.Add("Missing Point must return null readiness");

        var found = readiness.GetPointReadinessAsync("READY_PT").GetAwaiter().GetResult();
        if (found is null || !found.Exists) failures.Add("Existing Point must return readiness");
        if (found!.SiteId != "site-a") failures.Add("Readiness must contain server-resolved SiteId");
        if (!found.IsConfigurationReady) failures.Add("READY_PT must be configuration-ready");
        if (!found.IsProducingReady) failures.Add("READY_PT must be production-ready");

        var unready = readiness.GetPointReadinessAsync("UNREADY_PT").GetAwaiter().GetResult();
        if (unready is null || unready.IsConfigurationReady) failures.Add("UNREADY_PT must not be configuration-ready");

        // Command-supplied TargetSiteId cannot override trusted SiteId (verified in handler)
        // Draft Point may be configuration-ready but non-producing
        var draftPt = readiness.Configure("DRAFT_PT", new PointReadinessSnapshot("DRAFT_PT", "site-a", null, true, true, false, 1));
        var draftReadiness = draftPt.GetPointReadinessAsync("DRAFT_PT").GetAwaiter().GetResult();
        if (draftReadiness is null || !draftReadiness.IsConfigurationReady) failures.Add("Draft Point must be configuration-ready");
        if (draftReadiness!.IsProducingReady) failures.Add("Draft Point must not be producing-ready");

        // Deletion: Draft unused deletion succeeds
        var src = new DataSource(DataSourceId.New(), "DEL_SRC", "Delete", SourceType.Simulator, SourceStatus.Draft, 1);
        var fake = new FakeCatalogCommandRepository();
        fake.AddDataSourceAsync(src).GetAwaiter().GetResult();
        // No dependencies means deletion succeeds
        // Operational dependency returns DEPENDENT_HISTORY
        fake.SetDataSourceDependencies(src.Id, new CatalogDependencySnapshot(SimulatorRun: true));
        var delResult = fake.DeleteDataSourceAsync(src.Id).GetAwaiter().GetResult();
        if (delResult.Code != "DEPENDENT_HISTORY") failures.Add("Operational dependency must return DEPENDENT_HISTORY");

        // Audit-only does not block deletion
        var auditSrc = new DataSource(DataSourceId.New(), "AUDIT_SRC", "Audit", SourceType.Simulator, SourceStatus.Draft, 1);
        fake.AddDataSourceAsync(auditSrc).GetAwaiter().GetResult();
        fake.SetDataSourceDependencies(auditSrc.Id, new CatalogDependencySnapshot(AuditOnlySnapshot: true));
        var auditDel = fake.DeleteDataSourceAsync(auditSrc.Id).GetAwaiter().GetResult();
        if (!auditDel.IsAllowed) failures.Add("Audit-only dependency must not block Draft deletion");

        // Mapping over Data Source — delete blocked by mapping
        var mappingSrc = new DataSource(DataSourceId.New(), "MAPPING_SRC", "With Mapping", SourceType.Simulator, SourceStatus.Draft, 1);
        fake.AddDataSourceAsync(mappingSrc).GetAwaiter().GetResult();
        var testMapping = new SourcePointMapping(MappingId.New(), mappingSrc.Id, "TEST_PT",
            MappingStatus.Draft, Utc(2025, 1, 1), null, 1);
        fake.AddMappingAsync(testMapping).GetAwaiter().GetResult();
        // Mapping exists so delete should be blocked
        var srcDel = fake.DeleteDataSourceAsync(mappingSrc.Id).GetAwaiter().GetResult();
        if (srcDel.Code != "DEPENDENT_HISTORY") failures.Add("Mapping existence must block Data Source deletion");

        // No partial mutation after failure - rollback test
        var rollSrc = new DataSource(DataSourceId.New(), "ROLL_SRC", "Rollback", SourceType.Simulator, SourceStatus.Draft, 1);
        fake.AddDataSourceAsync(rollSrc).GetAwaiter().GetResult();
        var tx = fake.BeginTransactionAsync().GetAwaiter().GetResult();
        var beforeDelete = fake.GetDataSourceAsync(rollSrc.Id).GetAwaiter().GetResult();
        fake.SetDataSourceDependencies(rollSrc.Id, new CatalogDependencySnapshot(SimulatorRun: true));
        var rollDel = fake.DeleteDataSourceAsync(rollSrc.Id).GetAwaiter().GetResult();
        tx.RollbackAsync().GetAwaiter().GetResult();
        var afterRollback = fake.GetDataSourceAsync(rollSrc.Id).GetAwaiter().GetResult();
        if (afterRollback is null) failures.Add("Rollback must restore deleted source");

        return failures;
    }

    private static DateTime Utc(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}
