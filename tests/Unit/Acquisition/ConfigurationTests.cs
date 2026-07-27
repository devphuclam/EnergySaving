using IUMP.Modules.Acquisition.Contracts;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Acquisition;

public static class ConfigurationTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();
        OneHeadAndImmutableVersions(failures).GetAwaiter().GetResult();
        ValidationAndVersionConflict(failures).GetAwaiter().GetResult();
        return failures;
    }

    private static async Task OneHeadAndImmutableVersions(List<string> failures)
    {
        var repo = new FakeAcquisitionConfigurationRepository();
        var sourceId = Guid.NewGuid();
        var configurationId = Guid.NewGuid();
        var first = Version(configurationId, 1, 60, 5, 5, 42, SimulatorScenario.Constant, "creator", "creator@example", "corr-1", "caus-1");
        var tx = await repo.BeginTransactionAsync();
        await repo.CreateAsync(new SimulatorConfigurationHead(configurationId, sourceId, 1, 1), first);
        await tx.CommitAsync();
        tx.Dispose();

        var head = await repo.GetBySourceIdAsync(sourceId);
        Assert(head is not null && head.CurrentConfigurationVersion == 1 && head.Version > 0, failures, "Head starts at version 1 with a positive aggregate version.");
        var original = await repo.GetVersionAsync(configurationId, 1);
        var second = Version(configurationId, 2, 30, 1, 3, 99, SimulatorScenario.Normal, "editor", "editor@example", "corr-2", "caus-2");
        tx = await repo.BeginTransactionAsync();
        await repo.AppendVersionAsync(configurationId, head!.Version, second);
        await tx.CommitAsync();
        tx.Dispose();

        var versions = await repo.ListVersionsAsync(configurationId);
        Assert(versions.Count == 2 && versions[0].ConfigurationVersion == 1 && versions[1].ConfigurationVersion == 2, failures, "Versions are stable and monotonic.");
        Assert((await repo.GetVersionAsync(configurationId, 1)) == original, failures, "Historical version remains byte/value unchanged after edit.");
        Assert((await repo.GetVersionAsync(configurationId, 2))?.DeterministicSeed == 99, failures, "Exact immutable version lookup works.");

        var duplicate = false;
        try { await repo.CreateAsync(new SimulatorConfigurationHead(Guid.NewGuid(), sourceId, 1, 1), first with { }); }
        catch (InvalidOperationException) { duplicate = true; }
        Assert(duplicate, failures, "One configuration head is enforced per Source.");
    }

    private static async Task ValidationAndVersionConflict(List<string> failures)
    {
        var invalid = false;
        try { _ = Version(Guid.NewGuid(), 1, 10, 2, 3, 1, SimulatorScenario.Constant, "u", "user", null, null); }
        catch (ArgumentException) { invalid = true; }
        Assert(invalid, failures, "Constant scenario requires equal bounds.");
        invalid = false;
        try { _ = Version(Guid.NewGuid(), 1, 10, 4, 4, 1, SimulatorScenario.Normal, "u", "user", null, null); }
        catch (ArgumentException) { invalid = true; }
        Assert(invalid, failures, "Normal scenario requires minimum below maximum.");
        invalid = false;
        try { _ = Version(Guid.NewGuid(), 1, 0, 4, 4, 1, SimulatorScenario.Constant, "u", "user", null, null); }
        catch (ArgumentOutOfRangeException) { invalid = true; }
        Assert(invalid, failures, "Interval must be positive.");

        var repo = new FakeAcquisitionConfigurationRepository();
        var sourceId = Guid.NewGuid(); var configurationId = Guid.NewGuid();
        var first = Version(configurationId, 1, 10, 1, 1, 0, SimulatorScenario.Constant, "u", "user", null, null);
        var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(configurationId, sourceId, 1, 1), first); await tx.CommitAsync(); tx.Dispose();
        var stale = false;
        try { await repo.AppendVersionAsync(configurationId, 99, Version(configurationId, 2, 10, 2, 2, 7, SimulatorScenario.Constant, "u", "user", null, null)); }
        catch (InvalidOperationException ex) { stale = ex.Message.Contains("VERSION_CONFLICT", StringComparison.Ordinal); }
        Assert(stale && (await repo.ListVersionsAsync(configurationId)).Count == 1, failures, "Stale ExpectedVersion rejects without adding a version.");
    }

    internal static SimulatorConfigurationVersion Version(Guid configurationId, long version, int interval,
        double min, double max, ulong seed, SimulatorScenario scenario, string userId, string username,
        string? correlation, string? causation) => new(configurationId, version, interval, min, max, seed,
        scenario, SimulatorConfigurationConstants.AlgorithmId, SimulatorConfigurationConstants.AlgorithmVersion,
        userId, username, DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc), correlation, causation);

    internal static void Assert(bool condition, List<string> failures, string message)
    {
        if (!condition) failures.Add($"T078: {message}");
    }
}
