using IUMP.Modules.Catalog.Application;
using IUMP.Modules.Catalog.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Catalog;

public static class MetricUnitTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();
        var repo = new FakeCatalogCommandRepository();
        var metric = new Metric(MetricId.New(), "POWER_KW", "Power", MetricStatus.Active, 1);
        var unit = new MetricUnit(UnitId.New(), "KW", "kW", MetricUnitStatus.Active, 1);
        repo.AddMetricAsync(metric).GetAwaiter().GetResult();
        repo.AddUnitAsync(unit).GetAwaiter().GetResult();

        ExpectFailure(() => repo.AddMetricAsync(new Metric(MetricId.New(), "power_kw", "Duplicate", MetricStatus.Active, 1)).GetAwaiter().GetResult(), failures, "duplicate normalized Metric code");
        ExpectFailure(() => repo.AddUnitAsync(new MetricUnit(UnitId.New(), "kw", "kilowatt", MetricUnitStatus.Active, 1)).GetAwaiter().GetResult(), failures, "duplicate normalized Unit code");

        repo.AddCompatibilityAsync(new MetricUnitCompatibility(metric.Id, unit.Id, true, 1)).GetAwaiter().GetResult();
        ExpectFailure(() => repo.AddCompatibilityAsync(new MetricUnitCompatibility(metric.Id, unit.Id, false, 1)).GetAwaiter().GetResult(), failures, "duplicate compatibility pair");
        var unit2 = new MetricUnit(UnitId.New(), "KWH", "kWh", MetricUnitStatus.Active, 1);
        repo.AddUnitAsync(unit2).GetAwaiter().GetResult();
        ExpectFailure(() => repo.AddCompatibilityAsync(new MetricUnitCompatibility(metric.Id, unit2.Id, true, 1)).GetAwaiter().GetResult(), failures, "second canonical Unit");

        metric = repo.GetMetricAsync(metric.Id).GetAwaiter().GetResult()!;
        metric.Inactivate();
        repo.UpdateMetricAsync(metric).GetAwaiter().GetResult();
        var eligibility = new FakeCatalogEligibilityQueryRepository(repo);
        var metricInactive = eligibility.GetMetricUnitEligibilityAsync(metric.Id, unit.Id).GetAwaiter().GetResult();
        if (metricInactive.IsEligible || metricInactive.Outcome != IUMP.Modules.Catalog.Contracts.MetricUnitEligibilityOutcome.InactiveMetric)
            failures.Add("inactive Metric must be ineligible");
        metric = repo.GetMetricAsync(metric.Id).GetAwaiter().GetResult()!;
        metric.Activate();
        repo.UpdateMetricAsync(metric).GetAwaiter().GetResult();
        unit = repo.GetUnitAsync(unit.Id).GetAwaiter().GetResult()!;
        unit.Inactivate();
        repo.UpdateUnitAsync(unit).GetAwaiter().GetResult();
        var unitInactive = eligibility.GetMetricUnitEligibilityAsync(metric.Id, unit.Id).GetAwaiter().GetResult();
        if (unitInactive.IsEligible || unitInactive.Outcome != IUMP.Modules.Catalog.Contracts.MetricUnitEligibilityOutcome.InactiveUnit)
            failures.Add("inactive Unit must be ineligible");

        var seedRepo = new FakeCatalogCommandRepository();
        var seedService = new CatalogSeedApplicationService(seedRepo);
        var first = seedService.ApplyAsync().GetAwaiter().GetResult();
        var countsAfterFirst = (seedRepo.GetAllMetricsAsync().GetAwaiter().GetResult().Count, seedRepo.GetAllUnitsAsync().GetAwaiter().GetResult().Count);
        var second = seedService.ApplyAsync().GetAwaiter().GetResult();
        var countsAfterSecond = (seedRepo.GetAllMetricsAsync().GetAwaiter().GetResult().Count, seedRepo.GetAllUnitsAsync().GetAwaiter().GetResult().Count);
        if (first.MetricsAdded != 2 || first.UnitsAdded != 2 || first.CompatibilitiesAdded != 2)
            failures.Add("first deterministic seed application must add exactly two Metric/Unit/compatibility sets");
        if (second.MetricsAdded != 0 || second.UnitsAdded != 0 || second.CompatibilitiesAdded != 0 || second.VersionsChanged != 0 || countsAfterFirst != countsAfterSecond)
            failures.Add("second deterministic seed application must not change counts or versions");
        foreach (var seed in CatalogSeedDefinitions.All)
        {
            var canonical = seedRepo.GetCanonicalUnitAsync(seed.MetricId).GetAwaiter().GetResult();
            if (canonical is null || canonical.UnitId != seed.UnitId)
                failures.Add($"seed {seed.MetricCode} must have exactly one canonical Unit");
        }
        return failures;
    }

    private static void ExpectFailure(Action action, List<string> failures, string invariant)
    {
        try { action(); failures.Add($"{invariant} must be rejected"); }
        catch (InvalidOperationException) { }
    }
}
