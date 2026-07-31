using IUMP.Modules.Catalog.Application;
using IUMP.Modules.Catalog.Domain;

namespace IUMP.Modules.Catalog.Contracts;

/// <summary>
/// Contracts-visible duplication outcome for Catalog configuration entities.
/// The owner domain always produces a new identity, a unique proposed code,
/// Draft status, and version 1; it never copies Active state, optimistic
/// versions, historical versions, operational Mappings, or credentials.
/// </summary>
public sealed record CatalogConfigurationDuplicationOutcome(
    bool IsSuccess,
    string Code,
    string? Error,
    Guid? NewId = null,
    string? ProposedCode = null,
    string? ProposedName = null,
    string? Status = null,
    long Version = 0,
    IReadOnlyList<string> ReviewRelationships = null!);

/// <summary>
/// Contracts-visible owner event produced by a Catalog duplication. Host
/// adapters stage it into the outbox without referencing module internals.
/// </summary>
public sealed record CatalogConfigurationEvent(
    Guid EventId,
    string EventType,
    string SchemaVersion,
    string Producer,
    string AggregateType,
    string AggregateId,
    long AggregateVersion,
    string ActorId,
    string ActorUsername,
    IReadOnlyDictionary<string, object?> Before,
    IReadOnlyDictionary<string, object?> After,
    string Action,
    string Summary,
    DateTime OccurredAt,
    string? CorrelationId,
    string? CausationId,
    string? SiteId,
    string? AreaId);

/// <summary>
/// Caller snapshot contract accepted by the duplication gateway. Host adapters
/// translate their own caller resolution into this Contracts-visible shape.
/// </summary>
public sealed record CatalogConfigurationCallerSnapshot(
    string UserId,
    string Username,
    bool IsActive,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> SiteScopes,
    IReadOnlyCollection<string> AreaScopes);

public interface ICatalogConfigurationCallerProvider
{
    Task<CatalogConfigurationCallerSnapshot?> ResolveAsync(
        string userId, CancellationToken ct = default);
}

/// <summary>
/// Contract-only host facade for Catalog configuration duplication, mirroring
/// the CatalogRuntimeGateway precedent. The owner-domain duplication service
/// stays inside the Catalog module; hosts consume only this facade.
/// </summary>
public sealed class CatalogConfigurationDuplicationGateway(
    ICatalogCommandRepository repository,
    ICatalogConfigurationCallerProvider callers)
{
    private readonly List<CatalogConfigurationEvent> _events = new();

    public IReadOnlyList<CatalogConfigurationEvent> Events => _events.AsReadOnly();

    public async Task<CatalogConfigurationDuplicationOutcome> DuplicateSourceAsync(
        Guid sourceId, string actorUserId, CancellationToken ct = default)
    {
        var service = NewService();
        var outcome = await service.DuplicateSourceAsync(
            new DataSourceId(sourceId), actorUserId, ct);
        Collect(service.Events);
        return ToOutcome(outcome);
    }

    public async Task<CatalogConfigurationDuplicationOutcome> DuplicateMappingAsync(
        Guid mappingId, string actorUserId, CancellationToken ct = default)
    {
        var service = NewService();
        var outcome = await service.DuplicateMappingAsync(
            new MappingId(mappingId), actorUserId, ct);
        Collect(service.Events);
        return ToOutcome(outcome);
    }

    private CatalogDuplicationService NewService() =>
        new(repository, new CatalogRoleScopeAuthorization(new CallerBridge(callers)));

    private void Collect(IReadOnlyList<CatalogEvent> events)
    {
        foreach (var value in events)
            _events.Add(new CatalogConfigurationEvent(
                value.EventId, value.EventType, value.SchemaVersion,
                value.Producer, value.AggregateType, value.AggregateId,
                value.AggregateVersion, value.ActorId, value.ActorUsername,
                value.Before, value.After, value.Action, value.Summary,
                value.OccurredAt, value.CorrelationId, value.CausationId,
                value.SiteId, value.AreaId));
    }

    private static CatalogConfigurationDuplicationOutcome ToOutcome(
        CatalogDuplicationOutcome outcome) =>
        new(outcome.IsSuccess, outcome.Code, outcome.Error, outcome.NewId,
            outcome.ProposedCode, outcome.ProposedName, outcome.Status,
            outcome.Version, outcome.ReviewRelationships);

    private sealed class CallerBridge(
        ICatalogConfigurationCallerProvider provider) : ICatalogCallerSnapshotProvider
    {
        public async Task<CatalogCallerSnapshot?> ResolveAsync(
            string userId, CancellationToken ct = default)
        {
            var value = await provider.ResolveAsync(userId, ct);
            return value is null
                ? null
                : new CatalogCallerSnapshot(value.UserId, value.Username,
                    value.IsActive, value.Roles, value.SiteScopes, value.AreaScopes);
        }
    }
}
