namespace IUMP.Tests.Integration.Acceptance;

/// <summary>
/// Deterministic source-only harness for SC-001 and SC-002. It deliberately records no elapsed
/// time until the approved registered runtime and PostgreSQL adapters are available.
/// </summary>
public static class TimedJourneyAcceptanceTests
{
    public static IReadOnlyList<JourneyStep> Sc001Steps { get; } =
    [
        new(1, "BEGIN_TIMER", "Before root Site creation"),
        new(2, "ADMIN_CREATE_SITE", "Authenticated Administrator creates root Site"),
        new(3, "ADMIN_ASSIGN_ENGINEER_SCOPE", "Administrator assigns Engineer Site scope"),
        new(4, "ENGINEER_CREATE_DRAFT_HIERARCHY", "Engineer creates Draft Area, Asset and Point"),
        new(5, "ENGINEER_CREATE_SOURCE_CONFIGURATION_MAPPING", "Engineer creates Source, configuration and Mapping"),
        new(6, "ACTIVATE_TOP_DOWN", "Site, Area, Asset, Point, Source and Mapping activate in canonical order"),
        new(7, "STOP_TIMER", "Hierarchy is operational without consulting documentation")
    ];

    public static IReadOnlyList<JourneyStep> Sc002Steps { get; } =
    [
        new(1, "BEGIN_TIMER", "At successful Point activation result"),
        new(2, "START_SIMULATOR", "Start Simulator with current Mapping/Point versions"),
        new(3, "WAIT_ACCEPTED_MEASUREMENT", "Obtain first Accepted Measurement"),
        new(4, "OBSERVE_LATEST_API_UI", "Observe supported Latest through API and Web journey"),
        new(5, "STOP_TIMER", "Latest is visible with value, quality, timestamp and source status")
    ];

    public static async Task<JourneySourceResult> ExecuteAsync(
        ITimedJourneyDriver driver, CancellationToken cancellationToken = default)
    {
        var sc001 = await driver.ExecuteAsync("SC-001", Sc001Steps, cancellationToken);
        var sc002 = await driver.ExecuteAsync("SC-002", Sc002Steps, cancellationToken);
        return new(sc001, sc002);
    }

    public sealed record JourneyStep(int Order, string Operation, string ExpectedResult);
    public sealed record JourneyObservation(string Criterion, bool Completed, TimeSpan? Elapsed);
    public sealed record JourneySourceResult(JourneyObservation Sc001, JourneyObservation Sc002);

    public interface ITimedJourneyDriver
    {
        Task<JourneyObservation> ExecuteAsync(string criterion, IReadOnlyList<JourneyStep> steps,
            CancellationToken cancellationToken);
    }
}
