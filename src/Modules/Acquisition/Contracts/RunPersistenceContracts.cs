using IUMP.BuildingBlocks.Persistence;

namespace IUMP.Modules.Acquisition.Contracts;

public enum SimulatorRunStatus
{
    Running,
    Paused,
    Stopped
}

public enum SimulatorStartLockTarget
{
    OrganizationSite = 1,
    OrganizationArea = 2,
    OrganizationAsset = 3,
    OrganizationPoint = 4,
    CatalogSourceMapping = 5,
    AcquisitionRun = 6,
    IntegrationOutbox = 7
}

public sealed record SimulatorRun(
    Guid RunId,
    Guid SourceId,
    long SourceVersion,
    Guid ConfigurationId,
    long ConfigurationVersion,
    string AlgorithmId,
    int AlgorithmVersion,
    SimulatorRunStatus Status,
    long Version,
    long GeneratedCount,
    long AcceptedCount,
    long RejectedCount,
    string? LatestErrorCode,
    string? LatestErrorMessage,
    DateTime CreatedAtUtc,
    DateTime StartedAtUtc,
    DateTime? PausedAtUtc,
    DateTime? ResumedAtUtc,
    DateTime? StoppedAtUtc,
    string ActorId,
    string ActorUsername,
    string CorrelationId,
    string? CausationId);

public sealed record SimulatorRunPointState(
    Guid RunId,
    Guid PointId,
    long PointVersionAtStart,
    Guid MappingId,
    long MappingVersion,
    Guid MetricId,
    Guid UnitId,
    string UnitCode,
    long SourceVersion,
    long NextSourceSequence,
    byte[] PrngState,
    DateTime NextDueAtUtc,
    string SiteId,
    string? AreaId,
    string? LeaseOwner,
    Guid? LeaseToken,
    long LeaseVersion,
    DateTime? LeaseExpiresAtUtc,
    long Version);

public sealed record RunCallerSnapshot(
    string UserId,
    string Username,
    bool IsActive,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> SiteScopes)
{
    public bool HasRole(string role) =>
        Roles.Any(candidate => string.Equals(candidate, role, StringComparison.OrdinalIgnoreCase));

    public bool HasSiteScope(string siteId) =>
        SiteScopes.Any(candidate => string.Equals(candidate, siteId, StringComparison.OrdinalIgnoreCase));
}

public sealed record SimulatorStartPointSnapshot(
    Guid PointId,
    long PointVersion,
    string PointStatus,
    string SiteId,
    long SiteVersion,
    string SiteStatus,
    string? AreaId,
    long AreaVersion,
    string AreaStatus,
    Guid AssetId,
    long AssetVersion,
    string AssetStatus,
    Guid MappingId,
    long MappingVersion,
    string MappingStatus,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    Guid MetricId,
    Guid UnitId,
    string UnitCode);

public sealed record SimulatorStartSnapshot(
    Guid SourceId,
    string SourceType,
    string SourceStatus,
    long SourceVersion,
    Guid ConfigurationId,
    long ConfigurationVersion,
    int IntervalSeconds,
    double MinimumValue,
    double MaximumValue,
    ulong DeterministicSeed,
    SimulatorScenario Scenario,
    string AlgorithmId,
    int AlgorithmVersion,
    IReadOnlyList<SimulatorStartPointSnapshot> Points);

public sealed record StartSimulatorCommand(
    Guid SourceId,
    string ActorUserId,
    string CorrelationId,
    string? CausationId);

public sealed record ChangeSimulatorRunStatusCommand(
    Guid RunId,
    long ExpectedVersion,
    SimulatorRunStatus TargetStatus,
    string ActorUserId,
    string CorrelationId,
    string? CausationId);

public sealed record RunCommandResult(
    bool IsSuccess,
    string Code,
    Guid? RunId = null,
    long? Version = null,
    string? Error = null)
{
    public static RunCommandResult Success(Guid runId, long version) => new(true, "OK", runId, version);
    public static RunCommandResult Failure(string code, string error) => new(false, code, null, null, error);
}

public sealed record SimulatorRunLease(
    Guid RunId,
    Guid PointId,
    string Owner,
    Guid Token,
    long Version,
    DateTime ExpiresAtUtc);

public sealed record SimulatorStartLock(SimulatorStartLockTarget Target, string Key);

public interface IRunCallerSnapshotProvider
{
    Task<RunCallerSnapshot?> ResolveAsync(string userId, CancellationToken ct = default);
}

public interface ISimulatorStartSnapshotProvider
{
    Task<SimulatorStartSnapshot?> ResolveAsync(Guid sourceId, DateTime atUtc, CancellationToken ct = default);
    Task<bool> RecheckAsync(
        SimulatorStartSnapshot snapshot,
        ISimulatorRunTransaction transaction,
        DateTime atUtc,
        CancellationToken ct = default);
}

public interface IUtcClock
{
    DateTime UtcNow { get; }
}

public interface ISimulatorRunTransaction : IHostTransaction
{
    IReadOnlyList<SimulatorStartLock> LockTrace { get; }
    ValueTask LockAsync(SimulatorStartLockTarget target, string key, CancellationToken ct = default);
    ValueTask CommitAsync(CancellationToken ct = default);
    ValueTask RollbackAsync(CancellationToken ct = default);
}

public interface ISimulatorRunUnitOfWork
{
    ValueTask<ISimulatorRunTransaction> BeginAsync(CancellationToken ct = default);
}

public interface IAcquisitionRunRepository
{
    Task<SimulatorRun?> GetAsync(Guid runId, CancellationToken ct = default);
    Task<SimulatorRun?> GetCurrentBySourceAsync(Guid sourceId, CancellationToken ct = default);
    Task<IReadOnlyList<SimulatorRun>> ListRunningAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SimulatorRunPointState>> ListPointStatesAsync(Guid runId, CancellationToken ct = default);
    Task<SimulatorRunPointState?> GetPointStateAsync(Guid runId, Guid pointId, CancellationToken ct = default);
    Task CreateAsync(SimulatorRun run, IReadOnlyList<SimulatorRunPointState> points,
        ISimulatorRunTransaction transaction, CancellationToken ct = default);
    Task<SimulatorRun> ChangeStatusAsync(Guid runId, long expectedVersion, SimulatorRunStatus targetStatus,
        DateTime nowUtc, string? errorCode, string? errorMessage, ISimulatorRunTransaction transaction,
        CancellationToken ct = default);
    Task<SimulatorRunLease?> ClaimDuePointAsync(Guid runId, Guid pointId, string owner,
        DateTime nowUtc, DateTime leaseUntilUtc, CancellationToken ct = default);
    Task<SimulatorRunLease?> RenewLeaseAsync(
        SimulatorRunLease lease,
        DateTime leaseUntilUtc,
        CancellationToken ct = default);
    Task ReleaseLeaseAsync(SimulatorRunLease lease, CancellationToken ct = default);
    Task StageReservationAsync(Guid runId, long expectedRunVersion, SimulatorRunPointState nextPointState,
        ISimulatorRunTransaction transaction, CancellationToken ct = default);
    Task StageFinalCounterAsync(Guid runId, long expectedRunVersion, ProductionFinalClassification classification,
        ISimulatorRunTransaction transaction, CancellationToken ct = default);
}

public sealed record SimulatorRunOwnerEvent(
    Guid EventId,
    string EventType,
    int SchemaVersion,
    string Producer,
    string AggregateType,
    Guid AggregateId,
    long AggregateVersion,
    string ActorId,
    string ActorUsername,
    string Action,
    string Summary,
    DateTime OccurredAtUtc,
    string CorrelationId,
    string? CausationId,
    IReadOnlyList<string> SiteIds,
    IReadOnlyDictionary<string, object?> Before,
    IReadOnlyDictionary<string, object?> After);

public interface ISimulatorRunOwnerEventWriter
{
    ValueTask StageAsync(SimulatorRunOwnerEvent ownerEvent, ISimulatorRunTransaction transaction,
        CancellationToken ct = default);
}
