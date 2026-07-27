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
        await CreateAndLookupAsync();
        await AppendAndOrderAsync();
        await StaleVersionAsync();
        await DuplicateSourceAsync();
        await NewHeadRollbackAsync();
        await DeepRollbackAsync();
        await ConstraintValidationAsync();
        await SeedValuesAsync();
        await ActorUsernameSnapshotAsync();
        await CorrelationCausationSnapshotAsync();
    }

    private async Task CreateAndLookupAsync()
    {
        var repo = _factory.Create(); var id = Guid.NewGuid(); var source = Guid.NewGuid();
        var first = Version(id, 1, 60, 1, 1, 42, SimulatorScenario.Constant);
        var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(id, source, 1, 1), first); await tx.CommitAsync(); tx.Dispose();
        var head = await repo.GetBySourceIdAsync(source); var exact = await repo.GetVersionAsync(id, 1);
        _testCount++;
        Assert(head?.ConfigurationId == id && head.CurrentConfigurationVersion == 1 && exact?.DeterministicSeed == 42, "create, head, first version and exact lookup");
    }

    private async Task AppendAndOrderAsync()
    {
        var repo = _factory.Create(); var id = Guid.NewGuid(); var source = Guid.NewGuid();
        var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(id, source, 1, 1), Version(id, 1, 10, 0, 0, 0, SimulatorScenario.Constant)); await tx.CommitAsync(); tx.Dispose();
        var head = (await repo.GetHeadAsync(id))!; tx = await repo.BeginTransactionAsync(); await repo.AppendVersionAsync(id, head.Version, Version(id, 2, 20, -1, 1, 1, SimulatorScenario.Normal)); await tx.CommitAsync(); tx.Dispose();
        var list = await repo.ListVersionsAsync(id);
        _testCount++;
        Assert(list.Count == 2 && list[0].ConfigurationVersion < list[1].ConfigurationVersion && (await repo.GetHeadAsync(id))!.Version == 2, "append, stable ordering and aggregate version");
        Assert((await repo.GetVersionAsync(id, 1))!.MinimumValue == 0, "historical version is immutable");
    }

    private async Task StaleVersionAsync()
    {
        var repo = _factory.Create(); var id = Guid.NewGuid(); var source = Guid.NewGuid(); var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(id, source, 1, 1), Version(id, 1, 10, 0, 0, 0, SimulatorScenario.Constant)); await tx.CommitAsync(); tx.Dispose();
        var stale = false; try { await repo.AppendVersionAsync(id, 99, Version(id, 2, 20, 1, 2, 1, SimulatorScenario.Normal)); } catch (InvalidOperationException ex) { stale = ex.Message.Contains("VERSION_CONFLICT", StringComparison.Ordinal); }
        _testCount++;
        Assert(stale && (await repo.ListVersionsAsync(id)).Count == 1, "stale aggregate version fails without mutation");
    }

    private async Task DuplicateSourceAsync()
    {
        var repo = _factory.Create(); var source = Guid.NewGuid(); var firstId = Guid.NewGuid(); var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(firstId, source, 1, 1), Version(firstId, 1, 10, 0, 0, 0, SimulatorScenario.Constant)); await tx.CommitAsync(); tx.Dispose();
        var rejected = false; var duplicateId = Guid.NewGuid(); try { await repo.CreateAsync(new SimulatorConfigurationHead(duplicateId, source, 1, 1), Version(duplicateId, 1, 10, 0, 0, 1, SimulatorScenario.Constant)); } catch (Exception ex) { rejected = ex is InvalidOperationException; }
        _testCount++;
        Assert(rejected, "duplicate source head rejected");
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

    private async Task ConstraintValidationAsync()
    {
        var rejected = false; try { _ = Version(Guid.NewGuid(), 1, 0, 0, 0, 0, SimulatorScenario.Constant); } catch (ArgumentOutOfRangeException) { rejected = true; }
        _testCount++;
        Assert(rejected, "interval and scenario constraints remain enforced by public value type");
    }

    private async Task SeedValuesAsync()
    {
        var repo = _factory.Create(); var id = Guid.NewGuid(); var source = Guid.NewGuid();
        var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(id, source, 1, 1), Version(id, 1, 60, 0, 0, 0, SimulatorScenario.Constant)); await tx.CommitAsync(); tx.Dispose();
        _testCount++;
        Assert((await repo.GetVersionAsync(id, 1))?.DeterministicSeed == 0, "seed value 0 accepted");

        var id2 = Guid.NewGuid(); var source2 = Guid.NewGuid();
        tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(id2, source2, 1, 1), Version(id2, 1, 60, 0, 0, ulong.MaxValue, SimulatorScenario.Constant)); await tx.CommitAsync(); tx.Dispose();
        _testCount++;
        Assert((await repo.GetVersionAsync(id2, 1))?.DeterministicSeed == ulong.MaxValue, "seed UInt64.MaxValue accepted");
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
        Assert(saved?.CreatedByUserId == "actor-id" && saved.CreatedByUsername == "actor-name", "actor username snapshot");
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
