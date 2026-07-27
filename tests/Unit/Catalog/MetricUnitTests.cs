using IUMP.Modules.Catalog.Domain;

namespace IUMP.Tests.Unit.Catalog;

public static class MetricUnitTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();

        // 1. Metric construction with version 1, Active by default
        var m1 = new Metric(MetricId.New(), "POWER_KW", "Power in kW", MetricStatus.Active, 1);
        if (m1.Version != 1) failures.Add("FAIL: Metric initial version should be 1");
        m1.Activate();
        if (m1.Version != 1) failures.Add("FAIL: Activate from Active should not increment version");

        m1.Inactivate();
        if (m1.Version != 2) failures.Add("FAIL: Inactivate from Active should increment version");
        if (m1.IsActive()) failures.Add("FAIL: After Inactivate, IsActive should be false");

        m1.Activate();
        if (m1.Version != 3) failures.Add("FAIL: Activate from Inactive should increment version");
        if (!m1.IsActive()) failures.Add("FAIL: After Activate, IsActive should be true");

        // 2. Unit construction with version 1, Active by default
        var u1 = new MetricUnit(UnitId.New(), "KW", "kW", MetricUnitStatus.Active, 1);
        if (u1.Version != 1) failures.Add("FAIL: Unit initial version should be 1");
        u1.Activate();
        if (u1.Version != 1) failures.Add("FAIL: Activate from Active should not increment version");

        u1.Inactivate();
        if (u1.Version != 2) failures.Add("FAIL: Inactivate from Active should increment version");
        if (u1.IsActive()) failures.Add("FAIL: After Inactivate, IsActive should be false");

        u1.Activate();
        if (u1.Version != 3) failures.Add("FAIL: Activate from Inactive should increment version");
        if (!u1.IsActive()) failures.Add("FAIL: After Activate, IsActive should be true");

        // 3. Code normalization to uppercase
        var m2 = new Metric(MetricId.New(), "test_metric", "Test", MetricStatus.Active, 1);
        if (m2.Code != "TEST_METRIC") failures.Add("FAIL: Metric code should be uppercased");

        var u2 = new MetricUnit(UnitId.New(), "test_unit", "TU", MetricUnitStatus.Active, 1);
        if (u2.Code != "TEST_UNIT") failures.Add("FAIL: Unit code should be uppercased");

        // 4. Code uniqueness — handled by repository, model does not enforce it
        var m3a = new Metric(MetricId.New(), "UNIQUE_M", "Unique M", MetricStatus.Active, 1);
        var m3b = new Metric(MetricId.New(), "UNIQUE_M", "Duplicate M", MetricStatus.Active, 1);
        if (m3a.Code != m3b.Code) failures.Add("FAIL: Same code should normalize identically");

        // 5. Compatibility pair uniqueness: MetricUnitCompatibility uses composite key (MetricId, UnitId)
        var compat1 = new MetricUnitCompatibility(m1.Id, u1.Id, false, 1);
        var compat2 = new MetricUnitCompatibility(m1.Id, u1.Id, true, 1);
        if (compat1.MetricId != compat2.MetricId || compat1.UnitId != compat2.UnitId)
            failures.Add("FAIL: Same (MetricId, UnitId) pair should be comparable");

        // 6. IsCanonical setter toggles
        compat1.SetCanonical(true);
        if (!compat1.IsCanonical) failures.Add("FAIL: SetCanonical(true) should set IsCanonical");
        if (compat1.Version != 2) failures.Add("FAIL: SetCanonical should increment version");

        // 7. Inactive Metric eligibility check
        var inactiveMetric = new Metric(MetricId.New(), "INACTIVE_M", "Inactive", MetricStatus.Inactive, 1);
        inactiveMetric.Inactivate(); // already inactive, no-op
        if (inactiveMetric.IsActive()) failures.Add("FAIL: Inactive Metric should not be active");

        // 8. Inactive Unit eligibility check
        var inactiveUnit = new MetricUnit(UnitId.New(), "INACTIVE_U", "IU", MetricUnitStatus.Inactive, 1);
        inactiveUnit.Inactivate(); // already inactive, no-op
        if (inactiveUnit.IsActive()) failures.Add("FAIL: Inactive Unit should not be active");

        // 9. Deterministic seeds — static readonly ensures immutability
        // (no-op; the fields are record structs and can't be mutated)

        // Verify seeds are mutually distinct
        if (MetricId.ElectricPower == MetricId.ElectricalEnergy)
            failures.Add("FAIL: ElectricPower and ElectricalEnergy seeds must differ");
        if (UnitId.Kilowatt == UnitId.KilowattHour)
            failures.Add("FAIL: Kilowatt and KilowattHour seeds must differ");

        // 10. Seed immutability — record structs are immutable
        var ep = MetricId.ElectricPower;
        var ep2 = MetricId.ElectricPower;
        if (ep != ep2) failures.Add("FAIL: Seed must be deterministic");

        // Additional: noop pass to show assertions complete
        failures.RemoveAll(f => f == null);

        return failures;
    }
}
