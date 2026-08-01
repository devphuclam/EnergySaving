using IUMP.Api.Infrastructure;

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

        return failures;
    }

    private static SimulatorSelectionOption Option(Guid siteId, string siteCode,
        Guid areaId, Guid sourceId) => new(
        siteId, siteCode, siteCode, areaId, "AREA", "Area", Guid.NewGuid(), "ASSET", "Asset",
        sourceId, "SIM", "Simulator", 2, Guid.NewGuid(), 3, 10, true, null);

    private static void Check(bool condition, string message, List<string> failures)
    {
        AssertionCount++;
        if (!condition) failures.Add(message);
    }
}
