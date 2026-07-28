using IUMP.Modules.Acquisition.Application;
using IUMP.Modules.Acquisition.Contracts;

namespace IUMP.Tests.Unit.Acquisition;

public static class AcquisitionEventTests
{
    private static readonly HashSet<string> Allowed =
    [
        "runId", "sourceId", "status", "version", "configurationId", "configurationVersion",
        "algorithmId", "algorithmVersion", "generatedCount", "acceptedCount", "rejectedCount",
        "latestErrorCode"
    ];

    public static int TestCount { get; private set; }
    public static int CheckCount { get; private set; }

    public static List<string> Run()
    {
        TestCount = 4;
        CheckCount = 14;
        var failures = new List<string>();
        var running = Phase6Fixtures.Run(Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"));
        var paused = running with
        {
            Status = SimulatorRunStatus.Paused,
            Version = 2,
            PausedAtUtc = Phase6Fixtures.Now
        };
        var resumed = paused with
        {
            Status = SimulatorRunStatus.Running,
            Version = 3,
            ResumedAtUtc = Phase6Fixtures.Now
        };
        var stopped = resumed with
        {
            Status = SimulatorRunStatus.Stopped,
            Version = 4,
            StoppedAtUtc = Phase6Fixtures.Now
        };

        var start = SimulatorRunEventFactory.Create(null, running, ["site-b", "site-a", "site-a"],
            Phase6Fixtures.Administrator, "Start", Phase6Fixtures.Now, "corr", "cause");
        Check(start.EventType == "SimulatorRunStateChanged.v1" &&
              start.SchemaVersion == 1 && start.Producer == "IUMP.Acquisition",
            "Start uses the exact event family, schema and producer", failures);
        Check(start.Before.Count == 0 && start.After["status"]?.ToString() == "Running",
            "Start has empty Before and a Running After", failures);
        Check(start.SiteIds.SequenceEqual(["site-a", "site-b"]),
            "event SiteIds are distinct and ordinally sorted", failures);
        Check(start.After.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(Allowed),
            "event payload contains only the safe allowlist", failures);
        Check(start.ActorId == "admin" && start.ActorUsername == "global-admin" &&
              start.CorrelationId == "corr" && start.CausationId == "cause",
            "actor and correlation snapshots are preserved", failures);

        var pause = SimulatorRunEventFactory.Create(running, paused, ["site-a"],
            Phase6Fixtures.Administrator, "Pause", Phase6Fixtures.Now, "corr-p", null);
        Check(pause.Before["status"]?.ToString() == "Running" &&
              pause.After["status"]?.ToString() == "Paused",
            "Pause records exact Running to Paused state", failures);
        Check((long)pause.Before["version"]! == 1 && (long)pause.After["version"]! == 2,
            "Pause records old and new aggregate versions", failures);

        var resume = SimulatorRunEventFactory.Create(paused, resumed, ["site-a"],
            Phase6Fixtures.Administrator, "Resume", Phase6Fixtures.Now, "corr-r", null);
        Check(resume.Before["status"]?.ToString() == "Paused" &&
              resume.After["status"]?.ToString() == "Running",
            "Resume records exact Paused to Running state", failures);
        Check(resume.Action == "Resume" && resume.AggregateVersion == 3,
            "Resume action and aggregate version are exact", failures);

        var stop = SimulatorRunEventFactory.Create(resumed, stopped, ["site-a"],
            Phase6Fixtures.Administrator, "Stop", Phase6Fixtures.Now, "corr-s", null);
        Check(stop.Before["status"]?.ToString() == "Running" &&
              stop.After["status"]?.ToString() == "Stopped",
            "Stop records exact prior to Stopped state", failures);
        Check(stop.AggregateId == stopped.RunId && stop.AggregateType == "SimulatorRun",
            "owner event aggregate identity is exact", failures);
        Check(stop.Before.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(Allowed) &&
              stop.After.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(Allowed),
            "Before and After both obey the safe allowlist", failures);
        Check(!stop.Before.ContainsKey("actorUsername") &&
              !stop.After.ContainsKey("latestErrorMessage"),
            "sensitive or verbose fields are excluded from state payloads", failures);
        Check(start.EventId != pause.EventId && pause.EventId != resume.EventId,
            "each accepted transition receives a distinct event identity", failures);
        return failures;
    }

    private static void Check(bool condition, string message, List<string> failures)
    {
        if (!condition) failures.Add($"T113: {message}.");
    }
}
