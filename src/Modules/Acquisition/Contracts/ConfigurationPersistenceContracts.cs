namespace IUMP.Modules.Acquisition.Contracts;

/// <summary>Provider-neutral identity for an Acquisition configuration aggregate.</summary>
public sealed record SimulatorConfigurationHead
{
    public Guid ConfigurationId { get; }
    public Guid SourceId { get; }
    public long CurrentConfigurationVersion { get; }
    public long Version { get; }

    public SimulatorConfigurationHead(Guid configurationId, Guid sourceId, long currentConfigurationVersion, long version)
    {
        if (configurationId == Guid.Empty) throw new ArgumentException("ConfigurationId is required.", nameof(configurationId));
        if (sourceId == Guid.Empty) throw new ArgumentException("SourceId is required.", nameof(sourceId));
        if (currentConfigurationVersion <= 0) throw new ArgumentOutOfRangeException(nameof(currentConfigurationVersion));
        if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
        ConfigurationId = configurationId;
        SourceId = sourceId;
        CurrentConfigurationVersion = currentConfigurationVersion;
        Version = version;
    }
}

public enum SimulatorScenario
{
    Constant,
    Normal
}

/// <summary>Immutable, append-only configuration version.  There is intentionally no update/delete port.</summary>
public sealed record SimulatorConfigurationVersion
{
    public Guid ConfigurationId { get; }
    public long ConfigurationVersion { get; }
    public int IntervalSeconds { get; }
    public double MinimumValue { get; }
    public double MaximumValue { get; }
    public string DeterministicSeed { get; }
    public SimulatorScenario ScenarioType { get; }
    public string AlgorithmId { get; }
    public int AlgorithmVersion { get; }
    public string CreatedByUserId { get; }
    public string CreatedByUsername { get; }
    public DateTime CreatedAtUtc { get; }
    public string? CorrelationId { get; }
    public string? CausationId { get; }

    public SimulatorConfigurationVersion(Guid configurationId, long configurationVersion, int intervalSeconds,
        double minimumValue, double maximumValue, string deterministicSeed, SimulatorScenario scenarioType,
        string algorithmId, int algorithmVersion, string createdByUserId, string createdByUsername,
        DateTime createdAtUtc, string? correlationId, string? causationId)
    {
        if (configurationId == Guid.Empty) throw new ArgumentException("ConfigurationId is required.", nameof(configurationId));
        if (configurationVersion <= 0) throw new ArgumentOutOfRangeException(nameof(configurationVersion));
        if (intervalSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
        if (!double.IsFinite(minimumValue) || !double.IsFinite(maximumValue)) throw new ArgumentException("Bounds must be finite.");
        if (string.IsNullOrWhiteSpace(deterministicSeed)) throw new ArgumentException("A deterministic seed is required.", nameof(deterministicSeed));
        if (scenarioType is not (SimulatorScenario.Constant or SimulatorScenario.Normal)) throw new ArgumentOutOfRangeException(nameof(scenarioType));
        if (scenarioType == SimulatorScenario.Constant && minimumValue != maximumValue) throw new ArgumentException("Constant bounds must match.");
        if (scenarioType == SimulatorScenario.Normal && minimumValue >= maximumValue) throw new ArgumentException("Normal minimum must be less than maximum.");
        if (!string.Equals(algorithmId, SimulatorConfigurationConstants.AlgorithmId, StringComparison.Ordinal)) throw new ArgumentException("Unsupported algorithm.", nameof(algorithmId));
        if (algorithmVersion <= 0) throw new ArgumentOutOfRangeException(nameof(algorithmVersion));
        if (string.IsNullOrWhiteSpace(createdByUserId)) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        if (string.IsNullOrWhiteSpace(createdByUsername)) throw new ArgumentException("CreatedByUsername is required.", nameof(createdByUsername));
        if (createdAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("CreatedAtUtc must be UTC.", nameof(createdAtUtc));
        ConfigurationId = configurationId;
        ConfigurationVersion = configurationVersion;
        IntervalSeconds = intervalSeconds;
        MinimumValue = minimumValue;
        MaximumValue = maximumValue;
        DeterministicSeed = deterministicSeed;
        ScenarioType = scenarioType;
        AlgorithmId = algorithmId;
        AlgorithmVersion = algorithmVersion;
        CreatedByUserId = createdByUserId;
        CreatedByUsername = createdByUsername;
        CreatedAtUtc = createdAtUtc;
        CorrelationId = correlationId;
        CausationId = causationId;
    }
}

public sealed record SimulatorConfigurationCreateCommand(
    Guid SourceId,
    string? ClientSiteId,
    int IntervalSeconds,
    double MinimumValue,
    double MaximumValue,
    string? DeterministicSeed,
    SimulatorScenario ScenarioType,
    string AlgorithmId,
    int AlgorithmVersion,
    string ActorUserId,
    string? CorrelationId,
    string? CausationId);

public sealed record SimulatorConfigurationEditCommand(
    Guid ConfigurationId,
    long ExpectedVersion,
    string? ClientSiteId,
    int IntervalSeconds,
    double MinimumValue,
    double MaximumValue,
    string? DeterministicSeed,
    SimulatorScenario ScenarioType,
    string AlgorithmId,
    int AlgorithmVersion,
    string ActorUserId,
    string? CorrelationId,
    string? CausationId);

public sealed record ConfigurationCallerSnapshot(
    string UserId,
    string Username,
    bool IsActive,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> SiteScopes)
{
    public bool HasRole(string role) => Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
    public bool HasSiteScope(string siteId) => SiteScopes.Any(s => string.Equals(s, siteId, StringComparison.OrdinalIgnoreCase));
}

public interface IConfigurationCallerSnapshotProvider
{
    Task<ConfigurationCallerSnapshot?> ResolveAsync(string userId, CancellationToken ct = default);
}

public interface IConfigurationTransaction : IDisposable
{
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}

public interface IAcquisitionConfigurationRepository
{
    Task<SimulatorConfigurationHead?> GetBySourceIdAsync(Guid sourceId, CancellationToken ct = default);
    Task<SimulatorConfigurationHead?> GetHeadAsync(Guid configurationId, CancellationToken ct = default);
    Task<SimulatorConfigurationVersion?> GetVersionAsync(Guid configurationId, long configurationVersion, CancellationToken ct = default);
    Task<IReadOnlyList<SimulatorConfigurationVersion>> ListVersionsAsync(Guid configurationId, CancellationToken ct = default);
    Task CreateAsync(SimulatorConfigurationHead head, SimulatorConfigurationVersion firstVersion, CancellationToken ct = default);
    Task AppendVersionAsync(Guid configurationId, long expectedAggregateVersion, SimulatorConfigurationVersion nextVersion, CancellationToken ct = default);
    Task<IConfigurationTransaction> BeginTransactionAsync(CancellationToken ct = default);
}

public sealed record ConfigurationCommandResult(bool IsSuccess, string Code, string? Error = null)
{
    public static ConfigurationCommandResult Success() => new(true, "OK");
    public static ConfigurationCommandResult Failure(string code, string error) => new(false, code, error);
}

public sealed record SimulatorConfigurationEvent(
    Guid EventId,
    string EventType,
    string SchemaVersion,
    string Producer,
    string AggregateType,
    string AggregateId,
    long AggregateVersion,
    string ActorId,
    string ActorUsername,
    string Action,
    string Summary,
    DateTime OccurredAtUtc,
    string? CorrelationId,
    string? CausationId,
    string SiteId,
    IReadOnlyDictionary<string, object?> Before,
    IReadOnlyDictionary<string, object?> After);

public static class SimulatorConfigurationConstants
{
    public const string AlgorithmId = "IUMP-DETERMINISTIC-V1";
    public const int AlgorithmVersion = 1;
    public const string EventType = "SimulatorConfigurationChanged.v1";
    public const string Producer = "IUMP.Acquisition";
}
