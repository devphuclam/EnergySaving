using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using IUMP.Modules.Catalog.Contracts;

namespace IUMP.Modules.Acquisition.Contracts;

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

public sealed record SimulatorConfigurationVersion
{
    public Guid ConfigurationId { get; }
    public long ConfigurationVersion { get; }
    public int IntervalSeconds { get; }
    public double MinimumValue { get; }
    public double MaximumValue { get; }
    public ulong DeterministicSeed { get; }
    public SimulatorScenario ScenarioType { get; }
    public string AlgorithmId { get; }
    public int AlgorithmVersion { get; }
    public string CreatedByUserId { get; }
    public string CreatedByUsername { get; }
    public DateTime CreatedAtUtc { get; }
    public string? CorrelationId { get; }
    public string? CausationId { get; }

    public SimulatorConfigurationVersion(Guid configurationId, long configurationVersion, int intervalSeconds,
        double minimumValue, double maximumValue, ulong deterministicSeed, SimulatorScenario scenarioType,
        string algorithmId, int algorithmVersion, string createdByUserId, string createdByUsername,
        DateTime createdAtUtc, string? correlationId, string? causationId)
    {
        if (configurationId == Guid.Empty) throw new ArgumentException("ConfigurationId is required.", nameof(configurationId));
        if (configurationVersion <= 0) throw new ArgumentOutOfRangeException(nameof(configurationVersion));
        if (intervalSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
        if (!double.IsFinite(minimumValue) || !double.IsFinite(maximumValue)) throw new ArgumentException("Bounds must be finite.");
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

public sealed record SimulatorConfigurationReceipt(
    Guid ConfigurationId,
    long DraftConfigurationVersion,
    Guid SourceId,
    string RelationshipFingerprint,
    string ReviewedByUserId,
    DateTime ReviewedAtUtc,
    string? ValidatedPayloadFingerprint,
    string? ValidatedByUserId,
    DateTime? ValidatedAtUtc);

public static class SimulatorConfigurationReceiptFingerprint
{
    public static string Relationship(Guid sourceId) =>
        Hash($"relationship|{sourceId:D}");

    public static string Relationship(CatalogSourceScopeSnapshot scope)
    {
        var mapped = scope.MappedScopes
            .OrderBy(value => value.MappingId.Value)
            .ThenBy(value => value.MappingVersion)
            .Select(value => string.Join('|',
                value.MappingId.Value.ToString("D"), value.MappingVersion,
                value.PointId, value.SiteId, value.AreaId,
                value.OrganizationReadinessVersions.SiteVersion,
                value.OrganizationReadinessVersions.AreaVersion,
                value.OrganizationReadinessVersions.AssetVersion,
                value.OrganizationReadinessVersions.PointVersion));
        return Hash(string.Join('|', new[]
        {
            "relationship", scope.SourceId.ToString("D"), scope.SourceType,
            scope.SourceStatus, scope.SourceVersion.ToString(CultureInfo.InvariantCulture),
            string.Join(';', mapped)
        }));
    }

    public static string Payload(SimulatorConfigurationHead head, SimulatorConfigurationVersion version) =>
        Hash(string.Join('|',
            "payload", head.ConfigurationId.ToString("D"), head.SourceId.ToString("D"),
            version.ConfigurationVersion.ToString(CultureInfo.InvariantCulture),
            version.IntervalSeconds.ToString(CultureInfo.InvariantCulture),
            version.MinimumValue.ToString("R", CultureInfo.InvariantCulture),
            version.MaximumValue.ToString("R", CultureInfo.InvariantCulture),
            version.DeterministicSeed.ToString(CultureInfo.InvariantCulture),
            version.ScenarioType.ToString(), version.AlgorithmId,
            version.AlgorithmVersion.ToString(CultureInfo.InvariantCulture)));

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

public sealed record SimulatorConfigurationCreateCommand(
    Guid SourceId,
    ulong DeterministicSeed,
    int IntervalSeconds,
    double MinimumValue,
    double MaximumValue,
    SimulatorScenario ScenarioType,
    string AlgorithmId,
    int AlgorithmVersion,
    string ActorUserId,
    string? CorrelationId,
    string? CausationId);

public sealed record SimulatorConfigurationEditCommand(
    Guid ConfigurationId,
    long ExpectedVersion,
    ulong DeterministicSeed,
    int IntervalSeconds,
    double MinimumValue,
    double MaximumValue,
    SimulatorScenario ScenarioType,
    string AlgorithmId,
    int AlgorithmVersion,
    string ActorUserId,
    string? CorrelationId,
    string? CausationId);

/// <summary>Explicit activation of an appended Draft version. The aggregate head version
/// is validated optimistically; only the named Draft becomes current.</summary>
public sealed record SimulatorConfigurationActivateVersionCommand(
    Guid ConfigurationId,
    long ExpectedVersion,
    long DraftConfigurationVersion,
    string ActorUserId,
    string? CorrelationId,
    string? CausationId);

/// <summary>Duplicate-to-Draft for a Simulator Configuration. The current behavior is
/// copied into a new head for the target Source as a version-1 baseline plus an explicit
/// version-2 Draft; it never copies history, Active state, or Run pins.</summary>
public sealed record SimulatorConfigurationDuplicateCommand(
    Guid ConfigurationId,
    Guid SourceId,
    string ActorUserId,
    string? CorrelationId,
    string? CausationId);

public sealed record ConfigurationDuplicateOutcome(
    bool IsSuccess,
    string Code,
    string? Error = null,
    Guid? NewConfigurationId = null,
    long? DraftConfigurationVersion = null)
{
    public static ConfigurationDuplicateOutcome Success(Guid newConfigurationId, long draftConfigurationVersion) =>
        new(true, "OK", null, newConfigurationId, draftConfigurationVersion);

    public static ConfigurationDuplicateOutcome Failure(string code, string error) =>
        new(false, code, error);
}

public sealed record ConfigurationCallerSnapshot(
    string UserId,
    string Username,
    bool IsActive,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> SiteScopes,
    IReadOnlyCollection<string>? AreaScopes = null)
{
    public bool HasRole(string role) => Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
    public bool HasSiteScope(string siteId) => SiteScopes.Any(s => string.Equals(s, siteId, StringComparison.OrdinalIgnoreCase));
    public bool HasAreaScope(string areaId) => (AreaScopes ?? Array.Empty<string>())
        .Any(s => string.Equals(s, areaId, StringComparison.OrdinalIgnoreCase));
    public bool HasScope(string siteId, string? areaId) =>
        HasSiteScope(siteId) ||
        (!string.IsNullOrWhiteSpace(areaId) && HasAreaScope(areaId));
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
    bool HasAmbientTransaction { get; }
    Task<SimulatorConfigurationHead?> GetBySourceIdAsync(Guid sourceId, CancellationToken ct = default);
    Task<SimulatorConfigurationHead?> GetHeadAsync(Guid configurationId, CancellationToken ct = default);
    Task<SimulatorConfigurationVersion?> GetVersionAsync(Guid configurationId, long configurationVersion, CancellationToken ct = default);
    Task<IReadOnlyList<SimulatorConfigurationHead>> ListHeadsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SimulatorConfigurationVersion>> ListVersionsAsync(Guid configurationId, CancellationToken ct = default);
    Task<SimulatorConfigurationReceipt?> GetReceiptAsync(Guid configurationId, long draftConfigurationVersion, CancellationToken ct = default);
    Task SaveRelationshipReviewAsync(SimulatorConfigurationReceipt receipt, CancellationToken ct = default);
    Task InvalidateReceiptAsync(Guid configurationId, long draftConfigurationVersion, CancellationToken ct = default);
    Task<bool> SaveValidationReceiptAsync(Guid configurationId, long draftConfigurationVersion, Guid sourceId,
        string payloadFingerprint, string validatedByUserId, DateTime validatedAtUtc, CancellationToken ct = default);
    Task CreateAsync(SimulatorConfigurationHead head, SimulatorConfigurationVersion firstVersion, CancellationToken ct = default);
    Task AppendVersionAsync(Guid configurationId, long expectedAggregateVersion, SimulatorConfigurationVersion nextVersion, CancellationToken ct = default);
    Task AppendDraftVersionAsync(Guid configurationId, long expectedAggregateVersion, SimulatorConfigurationVersion draftVersion, CancellationToken ct = default);
    Task ActivateVersionAsync(Guid configurationId, long expectedAggregateVersion, long draftConfigurationVersion, CancellationToken ct = default);
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
    IReadOnlyList<string> SiteIds,
    IReadOnlyDictionary<string, object?> Before,
    IReadOnlyDictionary<string, object?> After);

public static class SimulatorConfigurationConstants
{
    public const string AlgorithmId = "IUMP-DETERMINISTIC-V1";
    public const int AlgorithmVersion = 1;
    public const string EventType = "SimulatorConfigurationChanged.v1";
    public const string Producer = "IUMP.Acquisition";
}
