using IUMP.Modules.Acquisition.Contracts;
using IUMP.Tests.Integration.Acquisition;

namespace IUMP.Tests.Unit.Fakes;

public sealed class FakeAcquisitionConfigurationRepository : IAcquisitionConfigurationRepository
{
    private Dictionary<Guid, SimulatorConfigurationHead> _heads = new();
    private Dictionary<(Guid Id, long Version), SimulatorConfigurationVersion> _versions = new();
    private Transaction? _transaction;

    public Task<SimulatorConfigurationHead?> GetBySourceIdAsync(Guid sourceId, CancellationToken ct = default) =>
        Task.FromResult(_heads.Values.FirstOrDefault(h => h.SourceId == sourceId));

    public Task<SimulatorConfigurationHead?> GetHeadAsync(Guid configurationId, CancellationToken ct = default) =>
        Task.FromResult(_heads.GetValueOrDefault(configurationId));

    public Task<SimulatorConfigurationVersion?> GetVersionAsync(Guid configurationId, long configurationVersion, CancellationToken ct = default) =>
        Task.FromResult(_versions.GetValueOrDefault((configurationId, configurationVersion)) is { } v ? Clone(v) : null);

    public Task<IReadOnlyList<SimulatorConfigurationHead>> ListHeadsAsync(
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SimulatorConfigurationHead>>(
            _heads.Values.OrderBy(value => value.ConfigurationId)
                .Select(Clone).ToArray());

    public Task<IReadOnlyList<SimulatorConfigurationVersion>> ListVersionsAsync(Guid configurationId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SimulatorConfigurationVersion>>(_versions.Values
            .Where(v => v.ConfigurationId == configurationId).OrderBy(v => v.ConfigurationVersion).Select(Clone).ToList());

    public Task CreateAsync(SimulatorConfigurationHead head, SimulatorConfigurationVersion firstVersion, CancellationToken ct = default)
    {
        if (_heads.ContainsKey(head.ConfigurationId) || _heads.Values.Any(h => h.SourceId == head.SourceId))
            throw new InvalidOperationException("Duplicate configuration/source.");
        if (head.CurrentConfigurationVersion != 1 || head.Version <= 0 || firstVersion.ConfigurationId != head.ConfigurationId || firstVersion.ConfigurationVersion != 1)
            throw new InvalidOperationException("First configuration version is invalid.");
        if (_versions.ContainsKey((firstVersion.ConfigurationId, firstVersion.ConfigurationVersion)))
            throw new InvalidOperationException("Duplicate configuration version.");
        _heads[head.ConfigurationId] = Clone(head);
        _versions[(firstVersion.ConfigurationId, firstVersion.ConfigurationVersion)] = Clone(firstVersion);
        return Task.CompletedTask;
    }

    public Task AppendVersionAsync(Guid configurationId, long expectedAggregateVersion, SimulatorConfigurationVersion nextVersion, CancellationToken ct = default)
    {
        if (!_heads.TryGetValue(configurationId, out var head)) throw new InvalidOperationException("Configuration not found.");
        if (head.Version != expectedAggregateVersion) throw new InvalidOperationException("VERSION_CONFLICT");
        if (nextVersion.ConfigurationId != configurationId || nextVersion.ConfigurationVersion != head.CurrentConfigurationVersion + 1)
            throw new InvalidOperationException("Configuration version must be monotonic.");
        if (_versions.ContainsKey((configurationId, nextVersion.ConfigurationVersion))) throw new InvalidOperationException("Duplicate configuration version.");
        _versions[(configurationId, nextVersion.ConfigurationVersion)] = Clone(nextVersion);
        _heads[configurationId] = new SimulatorConfigurationHead(configurationId, head.SourceId,
            nextVersion.ConfigurationVersion, checked(head.Version + 1));
        return Task.CompletedTask;
    }

    public Task AppendDraftVersionAsync(Guid configurationId, long expectedAggregateVersion, SimulatorConfigurationVersion draftVersion, CancellationToken ct = default)
    {
        if (!_heads.TryGetValue(configurationId, out var head)) throw new InvalidOperationException("Configuration not found.");
        if (head.Version != expectedAggregateVersion) throw new InvalidOperationException("VERSION_CONFLICT");
        var latest = _versions.Values.Where(v => v.ConfigurationId == configurationId)
            .Max(v => v.ConfigurationVersion);
        if (draftVersion.ConfigurationId != configurationId || draftVersion.ConfigurationVersion != latest + 1)
            throw new InvalidOperationException("Configuration version must be monotonic.");
        if (_versions.ContainsKey((configurationId, draftVersion.ConfigurationVersion))) throw new InvalidOperationException("Duplicate configuration version.");
        _versions[(configurationId, draftVersion.ConfigurationVersion)] = Clone(draftVersion);
        _heads[configurationId] = new SimulatorConfigurationHead(configurationId, head.SourceId,
            head.CurrentConfigurationVersion, checked(head.Version + 1));
        return Task.CompletedTask;
    }

    public Task ActivateVersionAsync(Guid configurationId, long expectedAggregateVersion, long draftConfigurationVersion, CancellationToken ct = default)
    {
        if (!_heads.TryGetValue(configurationId, out var head)) throw new InvalidOperationException("Configuration not found.");
        if (head.Version != expectedAggregateVersion) throw new InvalidOperationException("VERSION_CONFLICT");
        if (!_versions.ContainsKey((configurationId, draftConfigurationVersion)) || draftConfigurationVersion <= head.CurrentConfigurationVersion)
            throw new InvalidOperationException("Draft version is not activatable.");
        _heads[configurationId] = new SimulatorConfigurationHead(configurationId, head.SourceId,
            draftConfigurationVersion, checked(head.Version + 1));
        return Task.CompletedTask;
    }

    public Task<IConfigurationTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction is not null) throw new InvalidOperationException("A transaction is already active.");
        _transaction = new Transaction(this, _heads, _versions);
        return Task.FromResult<IConfigurationTransaction>(_transaction);
    }

    private void Restore(Dictionary<Guid, SimulatorConfigurationHead> heads,
        Dictionary<(Guid Id, long Version), SimulatorConfigurationVersion> versions)
    {
        _heads = heads.ToDictionary(x => x.Key, x => Clone(x.Value));
        _versions = versions.ToDictionary(x => x.Key, x => Clone(x.Value));
    }

    private static SimulatorConfigurationHead Clone(SimulatorConfigurationHead h) =>
        new(h.ConfigurationId, h.SourceId, h.CurrentConfigurationVersion, h.Version);

    private static SimulatorConfigurationVersion Clone(SimulatorConfigurationVersion v) =>
        new(v.ConfigurationId, v.ConfigurationVersion, v.IntervalSeconds, v.MinimumValue, v.MaximumValue,
            v.DeterministicSeed, v.ScenarioType, v.AlgorithmId, v.AlgorithmVersion, v.CreatedByUserId,
            v.CreatedByUsername, v.CreatedAtUtc, v.CorrelationId, v.CausationId);

    private sealed class Transaction : IConfigurationTransaction
    {
        private readonly FakeAcquisitionConfigurationRepository _owner;
        private readonly Dictionary<Guid, SimulatorConfigurationHead> _heads;
        private readonly Dictionary<(Guid Id, long Version), SimulatorConfigurationVersion> _versions;
        private bool _committed;
        private bool _disposed;

        public Transaction(FakeAcquisitionConfigurationRepository owner,
            Dictionary<Guid, SimulatorConfigurationHead> heads,
            Dictionary<(Guid Id, long Version), SimulatorConfigurationVersion> versions)
        {
            _owner = owner;
            _heads = heads.ToDictionary(x => x.Key, x => Clone(x.Value));
            _versions = versions.ToDictionary(x => x.Key, x => Clone(x.Value));
        }

        public Task CommitAsync(CancellationToken ct = default) { _committed = true; _owner._transaction = null; return Task.CompletedTask; }

        public Task RollbackAsync(CancellationToken ct = default)
        {
            if (!_committed) _owner.Restore(_heads, _versions);
            _owner._transaction = null;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (!_committed) _owner.Restore(_heads, _versions);
            _owner._transaction = null;
        }
    }
}

public sealed class FakeAcquisitionConfigurationRepositoryFactory : IAcquisitionConfigurationRepositoryFactory
{
    public IAcquisitionConfigurationRepository Create() => new FakeAcquisitionConfigurationRepository();
}
