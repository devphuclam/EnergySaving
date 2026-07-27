using IUMP.Modules.Acquisition.Contracts;

namespace IUMP.Tests.Integration.Acquisition;

public interface IAcquisitionConfigurationRepositoryFactory
{
    IAcquisitionConfigurationRepository Create();
}

public sealed class ConfigurationRepositoryContractRunner
{
    private readonly IAcquisitionConfigurationRepositoryFactory _factory;
    private readonly List<string> _failures = new();
    private int _testCount;
    private int _assertionCount;

    public ConfigurationRepositoryContractRunner(IAcquisitionConfigurationRepositoryFactory factory) => _factory = factory;
    public IReadOnlyList<string> Failures => _failures;
    public int TestCount => _testCount;
    public int AssertionCount => _assertionCount;

    public async Task RunAllAsync()
    {
        await CreateAndLookupHeadBySourceIdAsync();
        await CreateAndLookupHeadByConfigurationIdAsync();
        await CreateAndLookupFirstVersionAsync();
        await AppendVersionAndOrderAsync();
        await HistoricalVersionImmutableAsync();
        await StaleVersionConflictAsync();
        await DuplicateSourceRejectedAsync();
        await NewHeadRollbackAsync();
        await DeepRollbackAsync();
        await IntervalPositiveConstraintAsync();
        await ConstantBoundsMatchConstraintAsync();
        await NormalMinLessThanMaxConstraintAsync();
        await NaNMinimumRejectedAsync();
        await NaNMaximumRejectedAsync();
        await InfinityMaximumRejectedAsync();
        await NegativeInfinityMinimumRejectedAsync();
        await NegativeInfinityMaximumRejectedAsync();
        await ConstantEqualAcceptedAsync();
        await NormalMinLessThanMaxAcceptedAsync();
        await SeedMinAcceptedAsync();
        await SeedMaxAcceptedAsync();
        await SeedMidValueAcceptedAsync();
        await ActorUsernameSnapshotAsync();
        await CorrelationCausationSnapshotAsync();
    }

    private async Task CreateAndLookupHeadBySourceIdAsync()
    {
        var repo = _factory.Create(); var id = Guid.NewGuid(); var source = Guid.NewGuid();
        var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(id, source, 1, 1), Version(id, 1, 60, 1, 1, 42, SimulatorScenario.Constant)); await tx.CommitAsync(); tx.Dispose();
        var head = await repo.GetBySourceIdAsync(source);
        _testCount++;
        Assert(head?.ConfigurationId == id, "head lookup by sourceId returns matching configuration");
    }

    private async Task CreateAndLookupHeadByConfigurationIdAsync()
    {
        var repo = _factory.Create(); var id = Guid.NewGuid(); var source = Guid.NewGuid();
        var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(id, source, 1, 1), Version(id, 1, 60, 1, 1, 42, SimulatorScenario.Constant)); await tx.CommitAsync(); tx.Dispose();
        var head = await repo.GetHeadAsync(id);
        _testCount++;
        Assert(head?.ConfigurationId == id && head.CurrentConfigurationVersion == 1, "head lookup by configurationId returns correct version");
    }

    private async Task CreateAndLookupFirstVersionAsync()
    {
        var repo = _factory.Create(); var id = Guid.NewGuid(); var source = Guid.NewGuid();
        var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(id, source, 1, 1), Version(id, 1, 60, 1, 1, 42, SimulatorScenario.Constant)); await tx.CommitAsync(); tx.Dispose();
        var exact = await repo.GetVersionAsync(id, 1);
        _testCount++;
        Assert(exact?.DeterministicSeed == 42, "first version exact lookup returns correct seed");
    }

    private async Task AppendVersionAndOrderAsync()
    {
        var repo = _factory.Create(); var id = Guid.NewGuid(); var source = Guid.NewGuid();
        var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(id, source, 1, 1), Version(id, 1, 10, 0, 0, 0, SimulatorScenario.Constant)); await tx.CommitAsync(); tx.Dispose();
        var head = (await repo.GetHeadAsync(id))!; tx = await repo.BeginTransactionAsync(); await repo.AppendVersionAsync(id, head.Version, Version(id, 2, 20, -1, 1, 1, SimulatorScenario.Normal)); await tx.CommitAsync(); tx.Dispose();
        var list = await repo.ListVersionsAsync(id);
        _testCount++;
        Assert(list.Count == 2 && list[0].ConfigurationVersion < list[1].ConfigurationVersion && (await repo.GetHeadAsync(id))!.Version == 2, "append produces stable ordering and aggregate version");
    }

    private async Task HistoricalVersionImmutableAsync()
    {
        var repo = _factory.Create(); var id = Guid.NewGuid(); var source = Guid.NewGuid();
        var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(id, source, 1, 1), Version(id, 1, 10, 0, 0, 0, SimulatorScenario.Constant)); await tx.CommitAsync(); tx.Dispose();
        var head = (await repo.GetHeadAsync(id))!; tx = await repo.BeginTransactionAsync(); await repo.AppendVersionAsync(id, head.Version, Version(id, 2, 20, -1, 1, 1, SimulatorScenario.Normal)); await tx.CommitAsync(); tx.Dispose();
        _testCount++;
        Assert((await repo.GetVersionAsync(id, 1))!.MinimumValue == 0, "historical version value remains unchanged after append");
    }

    private async Task StaleVersionConflictAsync()
    {
        var repo = _factory.Create(); var id = Guid.NewGuid(); var source = Guid.NewGuid(); var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(id, source, 1, 1), Version(id, 1, 10, 0, 0, 0, SimulatorScenario.Constant)); await tx.CommitAsync(); tx.Dispose();
        var stale = false; try { await repo.AppendVersionAsync(id, 99, Version(id, 2, 20, 1, 2, 1, SimulatorScenario.Normal)); } catch (InvalidOperationException ex) { stale = ex.Message.Contains("VERSION_CONFLICT", StringComparison.Ordinal); }
        _testCount++;
        Assert(stale && (await repo.ListVersionsAsync(id)).Count == 1, "stale aggregate version fails without mutation");
    }

    private async Task DuplicateSourceRejectedAsync()
    {
        var repo = _factory.Create(); var source = Guid.NewGuid(); var firstId = Guid.NewGuid(); var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(firstId, source, 1, 1), Version(firstId, 1, 10, 0, 0, 0, SimulatorScenario.Constant)); await tx.CommitAsync(); tx.Dispose();
        var rejected = false; try { await repo.CreateAsync(new SimulatorConfigurationHead(Guid.NewGuid(), source, 1, 1), Version(Guid.NewGuid(), 1, 10, 0, 0, 1, SimulatorScenario.Constant)); } catch { rejected = true; }
        _testCount++;
        Assert(rejected, "duplicate source head is rejected");
    }

    private async Task NewHeadRollbackAsync()
    {
        var repo = _factory.Create(); var id = Guid.NewGuid(); var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(id, Guid.NewGuid(), 1, 1), Version(id, 1, 10, 0, 0, 0, SimulatorScenario.Constant)); await tx.RollbackAsync(); tx.Dispose();
        _testCount++;
        Assert(await repo.GetHeadAsync(id) is null && (await repo.ListVersionsAsync(id)).Count == 0, "new head rollback removes head and version");
    }

    private async Task DeepRollbackAsync()
    {
        var repo = _factory.Create(); var id = Guid.NewGuid(); var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(id, Guid.NewGuid(), 1, 1), Version(id, 1, 10, 0, 0, 0, SimulatorScenario.Constant)); await tx.CommitAsync(); tx.Dispose();
        var before = (await repo.GetHeadAsync(id))!; tx = await repo.BeginTransactionAsync(); await repo.AppendVersionAsync(id, before.Version, Version(id, 2, 20, 1, 2, 1, SimulatorScenario.Normal)); await tx.RollbackAsync(); tx.Dispose();
        _testCount++;
        Assert((await repo.GetHeadAsync(id))!.CurrentConfigurationVersion == 1 && (await repo.ListVersionsAsync(id)).Count == 1, "deep rollback restores existing head and history");
    }

    private async Task IntervalPositiveConstraintAsync()
    {
        var rejected = false; try { _ = Version(Guid.NewGuid(), 1, 0, 0, 0, 0, SimulatorScenario.Constant); } catch (ArgumentOutOfRangeException) { rejected = true; }
        _testCount++;
        Assert(rejected, "interval must be positive");
    }

    private async Task ConstantBoundsMatchConstraintAsync()
    {
        var rejected = false; try { _ = Version(Guid.NewGuid(), 1, 10, 2, 3, 1, SimulatorScenario.Constant); } catch (ArgumentException) { rejected = true; }
        _testCount++;
        Assert(rejected, "Constant scenario requires equal bounds");
    }

    private async Task NormalMinLessThanMaxConstraintAsync()
    {
        var rejected = false; try { _ = Version(Guid.NewGuid(), 1, 10, 4, 4, 1, SimulatorScenario.Normal); } catch (ArgumentException) { rejected = true; }
        _testCount++;
        Assert(rejected, "Normal scenario requires minimum below maximum");
    }

    private async Task NaNMinimumRejectedAsync()
    {
        var rejected = false; try { _ = Version(Guid.NewGuid(), 1, 10, double.NaN, 1, 0, SimulatorScenario.Normal); } catch (ArgumentException) { rejected = true; }
        _testCount++;
        Assert(rejected, "NaN minimum value is rejected");
    }

    private async Task NaNMaximumRejectedAsync()
    {
        var rejected = false; try { _ = Version(Guid.NewGuid(), 1, 10, 1, double.NaN, 0, SimulatorScenario.Normal); } catch (ArgumentException) { rejected = true; }
        _testCount++;
        Assert(rejected, "NaN maximum value is rejected");
    }

    private async Task InfinityMaximumRejectedAsync()
    {
        var rejected = false; try { _ = Version(Guid.NewGuid(), 1, 10, 1, double.PositiveInfinity, 0, SimulatorScenario.Normal); } catch (ArgumentException) { rejected = true; }
        _testCount++;
        Assert(rejected, "Infinity maximum value is rejected");
    }

    private async Task NegativeInfinityMinimumRejectedAsync()
    {
        var rejected = false; try { _ = Version(Guid.NewGuid(), 1, 10, double.NegativeInfinity, 1, 0, SimulatorScenario.Normal); } catch (ArgumentException) { rejected = true; }
        _testCount++;
        Assert(rejected, "Negative Infinity minimum value is rejected");
    }

    private async Task NegativeInfinityMaximumRejectedAsync()
    {
        var rejected = false; try { _ = Version(Guid.NewGuid(), 1, 10, 1, double.NegativeInfinity, 0, SimulatorScenario.Normal); } catch (ArgumentException) { rejected = true; }
        _testCount++;
        Assert(rejected, "Negative Infinity maximum value is rejected");
    }

    private async Task ConstantEqualAcceptedAsync()
    {
        var repo = _factory.Create(); var id = Guid.NewGuid(); var source = Guid.NewGuid();
        var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(id, source, 1, 1), Version(id, 1, 60, 5, 5, 42, SimulatorScenario.Constant)); await tx.CommitAsync(); tx.Dispose();
        _testCount++;
        Assert((await repo.GetVersionAsync(id, 1))?.MinimumValue == 5 && (await repo.GetVersionAsync(id, 1))?.MaximumValue == 5, "Constant equal bounds accepted");
    }

    private async Task NormalMinLessThanMaxAcceptedAsync()
    {
        var repo = _factory.Create(); var id = Guid.NewGuid(); var source = Guid.NewGuid();
        var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(id, source, 1, 1), Version(id, 1, 60, 2, 8, 99, SimulatorScenario.Normal)); await tx.CommitAsync(); tx.Dispose();
        _testCount++;
        Assert((await repo.GetVersionAsync(id, 1))?.MinimumValue == 2 && (await repo.GetVersionAsync(id, 1))?.MaximumValue == 8, "Normal min < max accepted");
    }

    private async Task SeedMinAcceptedAsync()
    {
        var repo = _factory.Create(); var id = Guid.NewGuid(); var source = Guid.NewGuid();
        var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(id, source, 1, 1), Version(id, 1, 60, 0, 0, 0, SimulatorScenario.Constant)); await tx.CommitAsync(); tx.Dispose();
        _testCount++;
        Assert((await repo.GetVersionAsync(id, 1))?.DeterministicSeed == 0, "seed value 0 accepted");
    }

    private async Task SeedMaxAcceptedAsync()
    {
        var repo = _factory.Create(); var id = Guid.NewGuid(); var source = Guid.NewGuid();
        var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(id, source, 1, 1), Version(id, 1, 60, 0, 0, ulong.MaxValue, SimulatorScenario.Constant)); await tx.CommitAsync(); tx.Dispose();
        _testCount++;
        Assert((await repo.GetVersionAsync(id, 1))?.DeterministicSeed == ulong.MaxValue, "seed UInt64.MaxValue accepted");
    }

    private async Task SeedMidValueAcceptedAsync()
    {
        var repo = _factory.Create(); var id = Guid.NewGuid(); var source = Guid.NewGuid();
        var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(id, source, 1, 1), Version(id, 1, 60, 0, 0, 123456789, SimulatorScenario.Constant)); await tx.CommitAsync(); tx.Dispose();
        _testCount++;
        Assert((await repo.GetVersionAsync(id, 1))?.DeterministicSeed == 123456789, "seed mid-value accepted");
    }

    private async Task ActorUsernameSnapshotAsync()
    {
        var repo = _factory.Create(); var id = Guid.NewGuid();
        var v = new SimulatorConfigurationVersion(id, 1, 60, 0, 0, 0, SimulatorScenario.Constant,
            SimulatorConfigurationConstants.AlgorithmId, SimulatorConfigurationConstants.AlgorithmVersion,
            "actor-id", "actor-name", DateTime.UtcNow, "corr", "caus");
        var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(id, Guid.NewGuid(), 1, 1), v); await tx.CommitAsync(); tx.Dispose();
        _testCount++;
        var saved = await repo.GetVersionAsync(id, 1);
        Assert(saved?.CreatedByUserId == "actor-id" && saved.CreatedByUsername == "actor-name", "actor username snapshot preserved");
    }

    private async Task CorrelationCausationSnapshotAsync()
    {
        var repo = _factory.Create(); var id = Guid.NewGuid();
        var v = new SimulatorConfigurationVersion(id, 1, 60, 0, 0, 0, SimulatorScenario.Constant,
            SimulatorConfigurationConstants.AlgorithmId, SimulatorConfigurationConstants.AlgorithmVersion,
            "u", "user", DateTime.UtcNow, "exact-corr", "exact-caus");
        var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(id, Guid.NewGuid(), 1, 1), v); await tx.CommitAsync(); tx.Dispose();
        _testCount++;
        var saved = await repo.GetVersionAsync(id, 1);
        Assert(saved?.CorrelationId == "exact-corr" && saved.CausationId == "exact-caus", "exact correlation/causation snapshot");
    }

    private void Assert(bool condition, string message) { _assertionCount++; if (!condition) _failures.Add($"T088: {message}"); }

    private static SimulatorConfigurationVersion Version(Guid id, long version, int interval, double min, double max, ulong seed, SimulatorScenario scenario) =>
        new(id, version, interval, min, max, seed, scenario, SimulatorConfigurationConstants.AlgorithmId, SimulatorConfigurationConstants.AlgorithmVersion,
            "runner-user", "runner-user", DateTime.UtcNow, "runner-correlation", "runner-causation");
}
