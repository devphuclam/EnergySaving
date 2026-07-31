using System.Collections.ObjectModel;
using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.Catalog.Domain;

namespace IUMP.Modules.Catalog.Application;

public sealed record CatalogDuplicationOutcome(
    bool IsSuccess,
    string Code,
    string? Error,
    Guid? NewId = null,
    string? ProposedCode = null,
    string? ProposedName = null,
    string? Status = null,
    long Version = 0,
    IReadOnlyList<string> ReviewRelationships = null!)
{
    public static CatalogDuplicationOutcome Success(Guid newId, string code, string name,
        IReadOnlyList<string> relationships) =>
        new(true, "OK", null, newId, code, name, "Draft", 1, relationships);

    public static CatalogDuplicationOutcome Failure(string code, string error) =>
        new(false, code, error, null, null, null, null, 0, Array.Empty<string>());
}

/// <summary>
/// Owner-domain duplicate-to-Draft behavior for Catalog configuration entities.
/// A duplicate receives a new identity, a unique proposed code, Draft status,
/// and version 1. It never copies Active state, optimistic versions, historical
/// versions, operational Mappings, credentials, or secrets; parent references
/// are returned as reviewable relationships for explicit review.
/// </summary>
public sealed class CatalogDuplicationService
{
    private readonly ICatalogCommandRepository _repo;
    private readonly ICatalogAuthorization _auth;
    private readonly List<CatalogEvent> _events = new();
    private CatalogCallerSnapshot? _currentCaller;

    public IReadOnlyList<CatalogEvent> Events => _events.AsReadOnly();

    public CatalogDuplicationService(ICatalogCommandRepository repo, ICatalogAuthorization auth)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
    }

    public async Task<CatalogDuplicationOutcome> DuplicateSourceAsync(
        DataSourceId sourceId, string actorUserId, CancellationToken ct = default)
    {
        var source = await _repo.GetDataSourceAsync(sourceId, ct);
        if (source is null) return CatalogDuplicationOutcome.Failure("NotFound", "Data source not found.");
        var denied = await AuthorizeAsync(actorUserId, CatalogResource.DataSource, source.SiteId?.ToString(), ct);
        if (denied is not null) return denied;
        try
        {
            var proposedCode = await UniqueCodeAsync(
                value => _repo.FindDataSourceByCodeAsync(value, ct)
                    .ContinueWith(task => task.Result is not null),
                source.Code, ct);
            var copy = new DataSource(DataSourceId.New(), proposedCode, source.Name,
                source.SourceType, SourceStatus.Draft, 1, source.SiteId);
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.AddDataSourceAsync(copy, ct);
            await tx.CommitAsync(ct);
            AddEvent("DataSourceStatusChanged.v1", "DataSource", copy.Id.ToString(), copy.Version, ctx(actorUserId),
                "Duplicated", "Data source duplicated as Draft",
                SourceSnapshot(source), SourceSnapshot(copy), source.SiteId?.ToString());
            return CatalogDuplicationOutcome.Success(copy.Id.Value, copy.Code, copy.Name,
                source.SiteId is null ? [] : [$"site:{source.SiteId.Value:D}"]);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return CatalogDuplicationOutcome.Failure(
                ex is InvalidOperationException ? "Conflict" : "Validation", ex.Message);
        }
    }

    public async Task<CatalogDuplicationOutcome> DuplicateMappingAsync(
        MappingId mappingId, string actorUserId, CancellationToken ct = default)
    {
        var mapping = await _repo.GetMappingAsync(mappingId, ct);
        if (mapping is null) return CatalogDuplicationOutcome.Failure("NotFound", "Mapping not found.");
        var source = await _repo.GetDataSourceAsync(mapping.DataSourceId, ct);
        if (source is null) return CatalogDuplicationOutcome.Failure("NotFound", "Data source not found.");
        var denied = await AuthorizeAsync(actorUserId, CatalogResource.Mapping, source.SiteId?.ToString(), ct);
        if (denied is not null) return denied;
        try
        {
            var copy = new SourcePointMapping(MappingId.New(), mapping.DataSourceId, mapping.PointId,
                MappingStatus.Draft, mapping.EffectiveFrom, mapping.EffectiveTo, 1);
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.AddMappingAsync(copy, ct);
            await tx.CommitAsync(ct);
            AddEvent("SourcePointMappingChanged.v1", "SourcePointMapping", copy.Id.ToString(), copy.Version, ctx(actorUserId),
                "Duplicated", "Source-point mapping duplicated as Draft",
                MappingSnapshot(mapping), MappingSnapshot(copy), source.SiteId?.ToString());
            return CatalogDuplicationOutcome.Success(copy.Id.Value, mapping.Id.ToString(), mapping.PointId,
            [
                $"source:{mapping.DataSourceId.Value:D}", $"point:{mapping.PointId}"
            ]);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return CatalogDuplicationOutcome.Failure(
                ex is InvalidOperationException ? "Conflict" : "Validation", ex.Message);
        }
    }

    private static async Task<string> UniqueCodeAsync(
        Func<string, Task<bool>> isTaken, string baseCode, CancellationToken ct)
    {
        var candidate = $"{baseCode}-COPY";
        var suffix = 2;
        while (await isTaken(candidate))
        {
            candidate = $"{baseCode}-COPY{suffix}";
            suffix++;
        }
        return candidate;
    }

    private static CatalogCommandContext ctx(string actorUserId) =>
        new(actorUserId, null, null);

    private async Task<CatalogDuplicationOutcome?> AuthorizeAsync(
        string userId, CatalogResource resource, string? targetSiteId, CancellationToken ct)
    {
        var decision = await _auth.AuthorizeAsync(userId, resource, targetSiteId, ct);
        _currentCaller = decision.IsAllowed ? await _auth.ResolveCallerAsync(userId, ct) : null;
        return decision.IsAllowed
            ? null
            : CatalogDuplicationOutcome.Failure(decision.Code, decision.Error ?? "Not authorized.");
    }

    private void AddEvent(string eventType, string aggregateType, string aggregateId, long version,
        CatalogCommandContext context, string action, string summary,
        IReadOnlyDictionary<string, object?> before, IReadOnlyDictionary<string, object?> after,
        string? siteId, string? areaId = null)
    {
        _events.Add(new CatalogEvent(Guid.NewGuid(), eventType, "1", "IUMP.Catalog",
            aggregateType, aggregateId, version,
            context.ActorUserId, _currentCaller?.Username ?? context.ActorUserId,
            before, after, action, summary, DateTime.UtcNow,
            context.CorrelationId, context.CausationId, siteId, areaId));
    }

    private static IReadOnlyDictionary<string, object?> SourceSnapshot(DataSource source) =>
        MakeSnap(("code", source.Code), ("name", source.Name),
            ("sourceType", source.SourceType.ToString()), ("status", source.Status.ToString()),
            ("siteId", source.SiteId?.ToString("D")));

    private static IReadOnlyDictionary<string, object?> MappingSnapshot(SourcePointMapping mapping) =>
        MakeSnap(("sourceId", mapping.DataSourceId.ToString()), ("pointId", mapping.PointId),
            ("status", mapping.Status.ToString()), ("effectiveFrom", mapping.EffectiveFrom),
            ("effectiveTo", mapping.EffectiveTo));

    private static IReadOnlyDictionary<string, object?> MakeSnap(params (string Key, object? Value)[] values)
    {
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Event snapshot keys are required.");
            map[key] = value;
        }
        return new ReadOnlyDictionary<string, object?>(map);
    }

    private sealed class AsyncTransaction : IAsyncDisposable
    {
        private readonly ICatalogTransaction _inner;
        private bool _committed;
        public AsyncTransaction(ICatalogTransaction inner) => _inner = inner;
        public async Task CommitAsync(CancellationToken ct) { await _inner.CommitAsync(ct); _committed = true; }
        public async ValueTask DisposeAsync()
        {
            if (!_committed) await _inner.RollbackAsync();
        }
    }
}
