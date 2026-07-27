using IUMP.Modules.Acquisition.Contracts;

namespace IUMP.Tests.Integration.Acquisition;

public interface IAcquisitionConfigurationRepositoryFactory
{
    IAcquisitionConfigurationRepository Create();
}

/// <summary>Provider-neutral contract runner. It deliberately knows only the public Acquisition port.</summary>
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
    }

    private async Task CreateAndLookupAsync()
    {
        var repo = _factory.Create(); var id = Guid.NewGuid(); var source = Guid.NewGuid();
        var first = Version(id, 1, 60, 1, 1, "runner-seed", SimulatorScenario.Constant);
        var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(id, source, 1, 1), first); await tx.CommitAsync(); tx.Dispose();
        var head = await repo.GetBySourceIdAsync(source); var exact = await repo.GetVersionAsync(id, 1);
        Assert(head?.ConfigurationId == id && head.CurrentConfigurationVersion == 1 && exact?.DeterministicSeed == "runner-seed", "head, first version and exact lookup");
    }

    private async Task AppendAndOrderAsync()
    {
        var repo = _factory.Create(); var id = Guid.NewGuid(); var source = Guid.NewGuid();
        var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(id, source, 1, 1), Version(id, 1, 10, 0, 0, "a", SimulatorScenario.Constant)); await tx.CommitAsync(); tx.Dispose();
        var head = (await repo.GetHeadAsync(id))!; tx = await repo.BeginTransactionAsync(); await repo.AppendVersionAsync(id, head.Version, Version(id, 2, 20, -1, 1, "b", SimulatorScenario.Normal)); await tx.CommitAsync(); tx.Dispose();
        var list = await repo.ListVersionsAsync(id); Assert(list.Count == 2 && list[0].ConfigurationVersion < list[1].ConfigurationVersion && (await repo.GetHeadAsync(id))!.Version == 2, "append, stable ordering and aggregate version");
        Assert((await repo.GetVersionAsync(id, 1))!.MinimumValue == 0, "historical version is immutable");
    }

    private async Task StaleVersionAsync()
    {
        var repo = _factory.Create(); var id = Guid.NewGuid(); var source = Guid.NewGuid(); var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(id, source, 1, 1), Version(id, 1, 10, 0, 0, "a", SimulatorScenario.Constant)); await tx.CommitAsync(); tx.Dispose();
        var stale = false; try { await repo.AppendVersionAsync(id, 99, Version(id, 2, 20, 1, 2, "b", SimulatorScenario.Normal)); } catch (InvalidOperationException ex) { stale = ex.Message.Contains("VERSION_CONFLICT", StringComparison.Ordinal); }
        Assert(stale && (await repo.ListVersionsAsync(id)).Count == 1, "stale aggregate version fails without mutation");
    }

    private async Task DuplicateSourceAsync()
    {
        var repo = _factory.Create(); var source = Guid.NewGuid(); var firstId = Guid.NewGuid(); var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(firstId, source, 1, 1), Version(firstId, 1, 10, 0, 0, "a", SimulatorScenario.Constant)); await tx.CommitAsync(); tx.Dispose();
        var rejected = false; var duplicateId = Guid.NewGuid(); try { await repo.CreateAsync(new SimulatorConfigurationHead(duplicateId, source, 1, 1), Version(duplicateId, 1, 10, 0, 0, "bad", SimulatorScenario.Constant)); } catch (Exception ex) { rejected = ex is InvalidOperationException; }
        Assert(rejected, "duplicate source head rejected");
    }

    private async Task NewHeadRollbackAsync()
    {
        var repo = _factory.Create(); var id = Guid.NewGuid(); var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(id, Guid.NewGuid(), 1, 1), Version(id, 1, 10, 0, 0, "a", SimulatorScenario.Constant)); await tx.RollbackAsync(); tx.Dispose();
        Assert(await repo.GetHeadAsync(id) is null && (await repo.ListVersionsAsync(id)).Count == 0, "new head rollback removes head and version");
    }

    private async Task DeepRollbackAsync()
    {
        var repo = _factory.Create(); var id = Guid.NewGuid(); var tx = await repo.BeginTransactionAsync(); await repo.CreateAsync(new SimulatorConfigurationHead(id, Guid.NewGuid(), 1, 1), Version(id, 1, 10, 0, 0, "a", SimulatorScenario.Constant)); await tx.CommitAsync(); tx.Dispose();
        var before = (await repo.GetHeadAsync(id))!; tx = await repo.BeginTransactionAsync(); await repo.AppendVersionAsync(id, before.Version, Version(id, 2, 20, 1, 2, "b", SimulatorScenario.Normal)); await tx.RollbackAsync(); tx.Dispose();
        Assert((await repo.GetHeadAsync(id))!.CurrentConfigurationVersion == 1 && (await repo.ListVersionsAsync(id)).Count == 1, "deep rollback restores existing head and history");
    }

    private async Task ConstraintValidationAsync()
    {
        var rejected = false; try { _ = Version(Guid.NewGuid(), 1, 0, 0, 0, "bad", SimulatorScenario.Constant); } catch (ArgumentOutOfRangeException) { rejected = true; }
        Assert(rejected, "interval and scenario constraints remain enforced by public value type");
    }

    private void Assert(bool condition, string message) { _assertionCount++; _testCount++; if (!condition) _failures.Add($"T088: {message}"); }

    private static SimulatorConfigurationVersion Version(Guid id, long version, int interval, double min, double max, string seed, SimulatorScenario scenario) =>
        new(id, version, interval, min, max, seed, scenario, SimulatorConfigurationConstants.AlgorithmId, SimulatorConfigurationConstants.AlgorithmVersion,
            "runner-user", "runner-user", DateTime.UtcNow, "runner-correlation", "runner-causation");
}
