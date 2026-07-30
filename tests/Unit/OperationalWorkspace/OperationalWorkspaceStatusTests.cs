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
        return failures;
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
