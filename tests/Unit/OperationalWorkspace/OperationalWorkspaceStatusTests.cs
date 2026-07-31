using IUMP.Api.Infrastructure;

namespace IUMP.Tests.Unit.OperationalWorkspace;

public static class OperationalWorkspaceStatusTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();
        AssertLanding(false, false, false, 0, true, WorkspaceLanding.NoAuthorizedScope, failures);
        AssertLanding(true, true, false, 0, true, WorkspaceLanding.SetupWizard, failures);
        AssertLanding(false, true, true, 4, true, WorkspaceLanding.ContinueSetup, failures);
        AssertLanding(false, true, true, 8, true, WorkspaceLanding.Dashboard, failures);
        AssertLanding(true, true, true, 8, false, WorkspaceLanding.DependencyError, failures);

        var status = OperationalWorkspaceStatusBuilder.Build(
            false, true, true, 3, 0, true, Array.Empty<WorkspaceSiteSummary>());
        if (status.NextStep != WorkspaceStep.MeasurementPoint ||
            status.CompletedSteps.Count != 3)
            failures.Add("T012: progress must identify the first incomplete persisted step.");
        if (status.SimulatorAutoStart)
            failures.Add("T012: setup status must never request automatic Simulator Start.");

        MultiSiteAndOrderIndependentDerivation(failures);
        RelationshipAndScopeDerivation(failures);
        ExplicitNewSetupAndSelection(failures);
        return failures;
    }

    private static void ExplicitNewSetupAndSelection(List<string> failures)
    {
        var site = Guid.Parse("f0000000-0000-0000-0000-00000000000f");
        var area = Guid.Parse("f1000000-0000-0000-0000-00000000000f");
        var snapshot = new WorkspacePersistedSnapshot(
            [
                new(site, "S-NEW", "New candidate", "Draft", 1, false, true),
                new(Guid.Parse("f2000000-0000-0000-0000-00000000000f"), "S-OTHER", "Other", "Active", 1, true, true)
            ],
            [new(area, site, "Draft", 1)],
            [], [], [], [], []);

        var newSetup = OperationalWorkspaceStatusBuilder.BuildFromSnapshot(
            true, true, true, snapshot, WorkspaceStatusRequest.NewSetup());
        if (newSetup.Landing != WorkspaceLanding.SetupWizard ||
            newSetup.NextStep != WorkspaceStep.SiteAndEngineer ||
            newSetup.SelectedSiteId is not null ||
            newSetup.OperationalChainCount != 0)
            failures.Add("T012: Administrator new setup mode must start at NoSite without unrelated chain progress.");

        var selected = OperationalWorkspaceStatusBuilder.BuildFromSnapshot(
            true, true, true, snapshot, WorkspaceStatusRequest.ForSite(site));
        if (selected.SelectedSiteId != site ||
            selected.AuthorizedSites.Count != 1 ||
            selected.AuthorizedSites[0].SiteId != site ||
            selected.NextStep != WorkspaceStep.SiteAndEngineer)
            failures.Add("T012: selected Site status must reconstruct only the requested persisted chain.");

        var reordered = OperationalWorkspaceStatusBuilder.BuildFromSnapshot(
            true, true, true, snapshot with { Sites = snapshot.Sites.Reverse().ToArray() },
            WorkspaceStatusRequest.ForSite(site));
        if (reordered.Chain != selected.Chain ||
            reordered.SelectedSiteId != selected.SelectedSiteId)
            failures.Add("T012: selected Site reconstruction must not depend on repository order.");
    }

    private static void MultiSiteAndOrderIndependentDerivation(List<string> failures)
    {
        var incompleteSite = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var operationalSite = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var areaA = Guid.Parse("30000000-0000-0000-0000-000000000003");
        var areaB = Guid.Parse("40000000-0000-0000-0000-000000000004");
        var assetA = Guid.Parse("50000000-0000-0000-0000-000000000005");
        var assetB = Guid.Parse("60000000-0000-0000-0000-000000000006");
        var pointA = Guid.Parse("70000000-0000-0000-0000-000000000007");
        var pointB = Guid.Parse("80000000-0000-0000-0000-000000000008");
        var sourceA = Guid.Parse("90000000-0000-0000-0000-000000000009");
        var sourceB = Guid.Parse("a0000000-0000-0000-0000-00000000000a");
        var mappingA = Guid.Parse("b0000000-0000-0000-0000-00000000000b");
        var mappingB = Guid.Parse("c0000000-0000-0000-0000-00000000000c");
        var configurationB = Guid.Parse("d0000000-0000-0000-0000-00000000000d");
        var snapshot = new WorkspacePersistedSnapshot(
            [
                new(incompleteSite, "S-A", "Incomplete", "Active", 1, true, true),
                new(operationalSite, "S-B", "Operational", "Active", 1, true, true)
            ],
            [
                new(areaA, incompleteSite, "Active", 1),
                new(areaB, operationalSite, "Active", 1)
            ],
            [
                new(assetA, incompleteSite, areaA, "Active", 1),
                new(assetB, operationalSite, areaB, "Active", 1)
            ],
            [
                new(pointA, incompleteSite, areaA, assetA, "Active", 1),
                new(pointB, operationalSite, areaB, assetB, "Active", 1)
            ],
            [
                new(sourceA, incompleteSite, "Draft", 1),
                new(sourceB, operationalSite, "Active", 1)
            ],
            [
                new(mappingA, sourceA, pointA, "Draft", 1),
                new(mappingB, sourceB, pointB, "Active", 1)
            ],
            [new(configurationB, sourceB, 1)]);

        var result = OperationalWorkspaceStatusBuilder.BuildFromSnapshot(
            true, true, true, snapshot);
        if (result.Landing != WorkspaceLanding.Dashboard ||
            result.Chain?.SiteId != operationalSite ||
            result.OperationalChainCount != 1 ||
            result.IncompleteChainCount != 1)
            failures.Add(
                "T012: any authorized operational chain must win Dashboard and all chains must be counted.");

        var reversed = snapshot with
        {
            Sites = snapshot.Sites.Reverse().ToArray(),
            Areas = snapshot.Areas.Reverse().ToArray(),
            Assets = snapshot.Assets.Reverse().ToArray(),
            Points = snapshot.Points.Reverse().ToArray(),
            Sources = snapshot.Sources.Reverse().ToArray(),
            Mappings = snapshot.Mappings.Reverse().ToArray()
        };
        var reordered = OperationalWorkspaceStatusBuilder.BuildFromSnapshot(
            true, true, true, reversed);
        if (reordered.Chain != result.Chain ||
            reordered.OperationalChainCount != result.OperationalChainCount ||
            reordered.IncompleteChainCount != result.IncompleteChainCount)
            failures.Add(
                "T012: repository order changes must not change selected chain or counts.");

        var tiedIncomplete = snapshot with
        {
            Sources = snapshot.Sources
                .Select(value => value with { Status = "Draft" }).ToArray(),
            Mappings = snapshot.Mappings
                .Select(value => value with { Status = "Draft" }).ToArray(),
            Configurations =
            [
                new(Guid.Parse("e0000000-0000-0000-0000-00000000000e"), sourceA, 1),
                new(configurationB, sourceB, 1)
            ]
        };
        var tiedResult = OperationalWorkspaceStatusBuilder.BuildFromSnapshot(
            true, true, true, tiedIncomplete);
        var tiedReordered = OperationalWorkspaceStatusBuilder.BuildFromSnapshot(
            true, true, true, tiedIncomplete with
            {
                Sites = tiedIncomplete.Sites.Reverse().ToArray(),
                Areas = tiedIncomplete.Areas.Reverse().ToArray(),
                Assets = tiedIncomplete.Assets.Reverse().ToArray(),
                Points = tiedIncomplete.Points.Reverse().ToArray(),
                Sources = tiedIncomplete.Sources.Reverse().ToArray(),
                Mappings = tiedIncomplete.Mappings.Reverse().ToArray(),
                Configurations = tiedIncomplete.Configurations.Reverse().ToArray()
            });
        if (tiedResult.Landing != WorkspaceLanding.ContinueSetup ||
            tiedResult.Chain?.SiteId != incompleteSite ||
            tiedReordered.Chain != tiedResult.Chain ||
            tiedResult.IncompleteChainCount != 2)
            failures.Add(
                "T012: equally complete resumable chains must use stable identity, not return order.");
    }

    private static void RelationshipAndScopeDerivation(List<string> failures)
    {
        var authorizedSite = Guid.Parse("01000000-0000-0000-0000-000000000001");
        var hiddenSite = Guid.Parse("02000000-0000-0000-0000-000000000002");
        var area = Guid.Parse("03000000-0000-0000-0000-000000000003");
        var unrelatedAsset = Guid.Parse("04000000-0000-0000-0000-000000000004");
        var hiddenArea = Guid.Parse("05000000-0000-0000-0000-000000000005");
        var hiddenAsset = Guid.Parse("06000000-0000-0000-0000-000000000006");
        var hiddenPoint = Guid.Parse("07000000-0000-0000-0000-000000000007");
        var hiddenSource = Guid.Parse("08000000-0000-0000-0000-000000000008");
        var hiddenMapping = Guid.Parse("09000000-0000-0000-0000-000000000009");
        var hiddenConfiguration = Guid.Parse("0a000000-0000-0000-0000-00000000000a");
        var snapshot = new WorkspacePersistedSnapshot(
            [
                new(authorizedSite, "S-A", "Authorized", "Active", 1, true, true),
                new(hiddenSite, "S-H", "Hidden", "Active", 1, true, false)
            ],
            [
                new(area, authorizedSite, "Active", 1),
                new(hiddenArea, hiddenSite, "Active", 1)
            ],
            [
                new(unrelatedAsset, hiddenSite, area, "Active", 1),
                new(hiddenAsset, hiddenSite, hiddenArea, "Active", 1)
            ],
            [new(hiddenPoint, hiddenSite, hiddenArea, hiddenAsset, "Active", 1)],
            [new(hiddenSource, hiddenSite, "Active", 1)],
            [new(hiddenMapping, hiddenSource, hiddenPoint, "Active", 1)],
            [new(hiddenConfiguration, hiddenSource, 1)]);

        var result = OperationalWorkspaceStatusBuilder.BuildFromSnapshot(
            false, true, true, snapshot);
        if (result.AuthorizedSites.Count != 1 ||
            result.AuthorizedSites[0].SiteId != authorizedSite ||
            result.OperationalChainCount != 0 ||
            result.IncompleteChainCount != 1)
            failures.Add("T012: scope filtering must occur before chain exposure and counting.");
        if (result.CompletedSteps.Count != 2 ||
            result.Chain?.AssetId is not null ||
            result.NextStep != WorkspaceStep.Asset)
            failures.Add(
                "T012: unrelated Area, Asset, and Point records must never be combined.");

        var secondArea = Guid.Parse("0b000000-0000-0000-0000-00000000000b");
        var secondAsset = Guid.Parse("0c000000-0000-0000-0000-00000000000c");
        var firstPoint = Guid.Parse("0d000000-0000-0000-0000-00000000000d");
        var secondPoint = Guid.Parse("0e000000-0000-0000-0000-00000000000e");
        var unmappedSource = Guid.Parse("0f000000-0000-0000-0000-00000000000f");
        var ambiguousPreMapping = new WorkspacePersistedSnapshot(
            [new(authorizedSite, "S-A", "Authorized", "Active", 1, true, true)],
            [
                new(area, authorizedSite, "Active", 1),
                new(secondArea, authorizedSite, "Active", 1)
            ],
            [
                new(unrelatedAsset, authorizedSite, area, "Active", 1),
                new(secondAsset, authorizedSite, secondArea, "Active", 1)
            ],
            [
                new(firstPoint, authorizedSite, area, unrelatedAsset, "Active", 1),
                new(secondPoint, authorizedSite, secondArea, secondAsset, "Active", 1)
            ],
            [new(unmappedSource, authorizedSite, "Draft", 1)],
            [],
            []);
        var ambiguousResult = OperationalWorkspaceStatusBuilder.BuildFromSnapshot(
            false, true, true, ambiguousPreMapping);
        if (ambiguousResult.CompletedSteps.Count != 4 ||
            ambiguousResult.NextStep != WorkspaceStep.DataSource ||
            ambiguousResult.Chain?.SourceId is not null)
            failures.Add(
                "T012: an unmapped Source must not attach to the first of multiple Point branches.");
    }

    private static void AssertLanding(bool admin, bool scope, bool site, int completed,
        bool dependency, WorkspaceLanding expected, List<string> failures)
    {
        var status = OperationalWorkspaceStatusBuilder.Build(
            admin, scope, site, completed, completed == 8 ? 1 : 0, dependency,
            Array.Empty<WorkspaceSiteSummary>());
        if (status.Landing != expected)
            failures.Add($"T012: expected {expected}, got {status.Landing}.");
    }
}
