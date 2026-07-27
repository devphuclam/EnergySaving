using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Integration.Contracts;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;

namespace IUMP.Modules.Organization.Application;

public enum ActivationOutcome
{
    Allowed, NotFound, Forbidden, InactiveUser, DataOwnerNotFound, DataOwnerInactive,
    DataOwnerIneligible, MetricNotFound, UnitNotFound, InactiveMetric, InactiveUnit,
    IncompatibleMetricUnit, MappingMissing, MappingMultiple, ParentAssetNotActive,
    InvalidState, NoOp, VersionConflict, ProviderVersionConflict, Validation
}

public sealed record ActivationResult(
    bool IsSuccess,
    ActivationOutcome Outcome,
    string? ErrorCode,
    string? ErrorDetail,
    PointId? PointId = null,
    PointStatus? NewStatus = null,
    long? NewVersion = null)
{
    public static ActivationResult Success(PointId id, PointStatus status, long version) => new(true, ActivationOutcome.Allowed, null, null, id, status, version);
    public static ActivationResult Failure(ActivationOutcome outcome, string code, string detail) => new(false, outcome, code, detail);
}

public static class ActivateMeasurementPoint
{
    public static async Task<ActivationResult> ExecuteAsync(
        PointId pointId,
        long expectedVersion,
        OrganizationCommandContext ctx,
        IOrganizationCommandRepository orgRepo,
        IActivationIdentityQuery iam,
        IActivationCatalogQuery catalog,
        IOrganizationAuthorization authorization,
        ITransactionalOutboxWriter outbox,
        HostTransactionCoordinator hostTx,
        CancellationToken ct = default)
    {
        var caller = await authorization.ResolveCallerAsync(ctx.ActorUserId, ct);
        if (caller is null || !caller.IsActive) return ActivationResult.Failure(ActivationOutcome.InactiveUser, "FORBIDDEN", "Caller is not authorized.");
        var scope = await orgRepo.GetPointScopeAsync(pointId, ct);
        if (scope is null) return ActivationResult.Failure(ActivationOutcome.NotFound, "NOT_FOUND", "The target is not visible.");
        var allowed = await authorization.AuthorizeAsync(ctx.ActorUserId, OrganizationResource.SiteChild, scope.SiteId.ToString(), ct);
        if (!allowed.IsAllowed)
            return ActivationResult.Failure(allowed.Code.Equals("NotFound", StringComparison.OrdinalIgnoreCase) ? ActivationOutcome.NotFound : ActivationOutcome.Forbidden,
                allowed.Code.Equals("NotFound", StringComparison.OrdinalIgnoreCase) ? "NOT_FOUND" : "FORBIDDEN", "The target is not visible.");

        var point = await orgRepo.GetPointAsync(pointId, ct);
        if (point is null) return ActivationResult.Failure(ActivationOutcome.NotFound, "NOT_FOUND", "The target is not visible.");
        if (expectedVersion != point.Version) return ActivationResult.Failure(ActivationOutcome.VersionConflict, "VERSION_CONFLICT", "ExpectedVersion is stale.");
        if (point.Status == PointStatus.Decommissioned) return ActivationResult.Failure(ActivationOutcome.InvalidState, "INVALID_STATE", "Decommissioned Point cannot be activated.");
        if (point.Status == PointStatus.Active) return ActivationResult.Failure(ActivationOutcome.NoOp, "INVALID_STATE", "Point is already Active.");

        IOrganizationTransaction? orgTransaction = null;
        try
        {
            var outboxParticipant = outbox switch
            {
                IHostTransactionParticipant hostParticipant => hostParticipant,
                IOutboxTransactionParticipant integrationParticipant => new OutboxTransactionParticipantAdapter(integrationParticipant),
                _ => null
            };
            if (outboxParticipant is null)
                return ActivationResult.Failure(ActivationOutcome.Validation, "OUTBOX_PARTICIPANT_REQUIRED", "Outbox must join the host transaction.");
            hostTx.RegisterParticipant(LockTarget.IntegrationOutbox, outboxParticipant);
            await hostTx.BeginAsync(ct);
            await hostTx.LockWithRetryAsync(LockTarget.IamUser, point.DataOwnerUserId, 1, ct);
            await hostTx.LockWithRetryAsync(LockTarget.OrganizationSite, point.SiteId.ToString(), 2, ct);
            await hostTx.LockWithRetryAsync(LockTarget.OrganizationArea, point.AreaId.ToString(), 3, ct);
            await hostTx.LockWithRetryAsync(LockTarget.OrganizationAsset, point.AssetId.ToString(), 4, ct);
            await hostTx.LockWithRetryAsync(LockTarget.OrganizationPoint, point.Id.ToString(), 5, ct);
            await hostTx.LockWithRetryAsync(LockTarget.CatalogMetric, point.MetricId, 6, ct);
            await hostTx.LockWithRetryAsync(LockTarget.CatalogUnit, point.UnitId, 7, ct);
            await hostTx.LockWithRetryAsync(LockTarget.CatalogMapping, point.Id.ToString(), 8, ct);
            await hostTx.LockWithRetryAsync(LockTarget.IntegrationOutbox, point.Id.ToString(), 9, ct);

            var owner = await iam.GetDataOwnerAsync(point.DataOwnerUserId, point.SiteId.ToString(), point.AreaId.ToString(), ct);
            var ownerFailure = ValidateOwner(owner, point.DataOwnerUserId);
            if (ownerFailure is not null) return await Abort(hostTx, ownerFailure);

            var site = await orgRepo.GetSiteAsync(point.SiteId, ct);
            var area = await orgRepo.GetAreaAsync(point.AreaId, ct);
            var asset = await orgRepo.GetAssetAsync(point.AssetId, ct);
            var lockedPoint = await orgRepo.GetPointAsync(pointId, ct);
            var orgFailure = ValidateOrganization(point, lockedPoint, site, area, asset, expectedVersion);
            if (orgFailure is not null) return await Abort(hostTx, orgFailure);

            var catalogSnapshot = await catalog.GetActivationSnapshotAsync(point.Id.ToString(), point.MetricId, point.UnitId, DateTime.UtcNow, ct);
            var catalogFailure = ValidateCatalog(catalogSnapshot, point.MetricId, point.UnitId);
            if (catalogFailure is not null) return await Abort(hostTx, catalogFailure);

            // Exact provider-version recheck; no Max/sum compression is used.
            var ownerAgain = await iam.GetDataOwnerAsync(point.DataOwnerUserId, point.SiteId.ToString(), point.AreaId.ToString(), ct);
            var catalogAgain = await catalog.GetActivationSnapshotAsync(point.Id.ToString(), point.MetricId, point.UnitId, DateTime.UtcNow, ct);
            var siteAgain = await orgRepo.GetSiteAsync(point.SiteId, ct);
            var areaAgain = await orgRepo.GetAreaAsync(point.AreaId, ct);
            var assetAgain = await orgRepo.GetAssetAsync(point.AssetId, ct);
            var pointAgain = await orgRepo.GetPointAsync(pointId, ct);
            if (!Equals(owner, ownerAgain) || !Equals(catalogSnapshot, catalogAgain) ||
                !SameOrganizationFacts(site, siteAgain, area, areaAgain, asset, assetAgain) ||
                pointAgain is null || pointAgain.Version != expectedVersion)
                return await Abort(hostTx, ActivationResult.Failure(ActivationOutcome.ProviderVersionConflict, "PROVIDER_VERSION_CONFLICT", "Provider facts changed before commit."));

            orgTransaction = await orgRepo.BeginTransactionAsync(ct);
            hostTx.RegisterParticipant(LockTarget.OrganizationPoint, new OrganizationTransactionParticipant(orgTransaction));
            var oldStatus = point.Status;
            var transitioned = oldStatus == PointStatus.Draft ? point.TryActivate() : point.TryReactivate();
            if (!transitioned)
            {
                await orgTransaction.RollbackAsync(ct);
                return await Abort(hostTx, ActivationResult.Failure(ActivationOutcome.InvalidState, "INVALID_STATE", "Point state cannot be activated."));
            }
            await orgRepo.UpdatePointAsync(point, ct);
            await orgRepo.AddLifecycleEntryAsync(new PointLifecycleEntry(Guid.NewGuid().ToString(), point.Id.ToString(), point.Version,
                oldStatus, PointStatus.Active, ctx.ActorUserId, caller.Username,
                oldStatus == PointStatus.Draft ? "Activated" : "Reactivated", DateTime.UtcNow, ctx.CorrelationId, ctx.CausationId), ct);
            var envelope = OrganizationEvents.BuildPointStatusChanged(point, oldStatus, PointStatus.Active, ctx, caller);
            await outbox.EnqueueAsync(envelope, hostTx, ct);
            await hostTx.CommitAsync(ct);
            return ActivationResult.Success(pointId, PointStatus.Active, point.Version);
        }
        catch (OperationCanceledException)
        {
            await hostTx.RollbackAsync();
            return ActivationResult.Failure(ActivationOutcome.Validation, "TRANSACTION_ROLLED_BACK", "Activation was cancelled.");
        }
        catch (TransientDatabaseConflictException)
        {
            await hostTx.RollbackAsync();
            return ActivationResult.Failure(ActivationOutcome.Validation, "TRANSIENT_DATABASE_CONFLICT", "Activation could not acquire a stable transaction.");
        }
        catch (Exception ex)
        {
            await hostTx.RollbackAsync();
            var code = ex.Message.Contains("OUTBOX", StringComparison.OrdinalIgnoreCase) ? "OUTBOX_WRITE_FAILED" : "TRANSACTION_ROLLED_BACK";
            return ActivationResult.Failure(ActivationOutcome.Validation, code, "Activation was rolled back.");
        }
    }

    private static ActivationResult? ValidateOwner(ActivationDataOwnerSnapshot owner, string expectedOwnerId)
    {
        if (!owner.Exists || !string.Equals(owner.DataOwnerUserId, expectedOwnerId, StringComparison.Ordinal))
            return ActivationResult.Failure(ActivationOutcome.DataOwnerNotFound, "DATA_OWNER_INELIGIBLE", "Data Owner is not eligible.");
        if (!owner.IsActive) return ActivationResult.Failure(ActivationOutcome.DataOwnerInactive, "DATA_OWNER_INELIGIBLE", "Data Owner is not eligible.");
        if (!owner.HasTrustedSiteScope || !owner.HasTrustedAreaScope || owner.HasForbiddenCapability)
            return ActivationResult.Failure(ActivationOutcome.DataOwnerIneligible, "DATA_OWNER_INELIGIBLE", "Data Owner is not eligible.");
        return null;
    }

    private static ActivationResult? ValidateOrganization(MeasurementPoint expected, MeasurementPoint? point, Site? site, Area? area, Asset? asset, long expectedVersion)
    {
        if (point is null) return ActivationResult.Failure(ActivationOutcome.NotFound, "NOT_FOUND", "The target is not visible.");
        if (point.Version != expectedVersion) return ActivationResult.Failure(ActivationOutcome.VersionConflict, "VERSION_CONFLICT", "ExpectedVersion is stale.");
        if (site is null || area is null || asset is null || asset.SiteId != point.SiteId || asset.AreaId != point.AreaId || area.SiteId != point.SiteId)
            return ActivationResult.Failure(ActivationOutcome.Validation, "INVALID_STATE", "Point ancestry is inconsistent.");
        if (!site.IsActive || !area.IsActive || !asset.IsActive)
            return ActivationResult.Failure(ActivationOutcome.ParentAssetNotActive, "PARENT_NOT_ACTIVE", "Point ancestors are not active.");
        if (point.ExpectedIntervalSeconds <= 0 || point.NoDataAfterSeconds <= point.ExpectedIntervalSeconds)
            return ActivationResult.Failure(ActivationOutcome.Validation, "INTERVAL_INVALID", "Interval configuration is invalid.");
        return null;
    }

    private static ActivationResult? ValidateCatalog(ActivationCatalogSnapshot? snapshot, string expectedMetricId, string expectedUnitId)
    {
        if (snapshot is null) return ActivationResult.Failure(ActivationOutcome.MappingMissing, "MAPPING_MISSING", "No active Mapping exists.");
        if (snapshot.ActiveMappingCount == 0) return ActivationResult.Failure(ActivationOutcome.MappingMissing, "MAPPING_MISSING", "No active Mapping exists.");
        if (snapshot.ActiveMappingCount > 1) return ActivationResult.Failure(ActivationOutcome.MappingMultiple, "MAPPING_MULTIPLE", "Multiple active Mappings exist.");
        if (!string.Equals(snapshot.MetricId, expectedMetricId, StringComparison.Ordinal)) return ActivationResult.Failure(ActivationOutcome.MetricNotFound, "METRIC_NOT_FOUND", "Metric evidence does not match the Point.");
        if (!string.Equals(snapshot.UnitId, expectedUnitId, StringComparison.Ordinal)) return ActivationResult.Failure(ActivationOutcome.UnitNotFound, "UNIT_NOT_FOUND", "Unit evidence does not match the Point.");
        if (snapshot.MetricStatus.Equals("Missing", StringComparison.Ordinal)) return ActivationResult.Failure(ActivationOutcome.MetricNotFound, "METRIC_NOT_FOUND", "Metric does not exist.");
        if (snapshot.UnitStatus.Equals("Missing", StringComparison.Ordinal)) return ActivationResult.Failure(ActivationOutcome.UnitNotFound, "UNIT_NOT_FOUND", "Unit does not exist.");
        if (!snapshot.MetricStatus.Equals("Active", StringComparison.Ordinal)) return ActivationResult.Failure(ActivationOutcome.InactiveMetric, "METRIC_INACTIVE", "Metric is not active.");
        if (!snapshot.UnitStatus.Equals("Active", StringComparison.Ordinal)) return ActivationResult.Failure(ActivationOutcome.InactiveUnit, "UNIT_INACTIVE", "Unit is not active.");
        if (!snapshot.IsCompatible) return ActivationResult.Failure(ActivationOutcome.IncompatibleMetricUnit, "UNIT_INCOMPATIBLE", "Metric and Unit are incompatible.");
        if (!snapshot.MappingStatus.Equals("Active", StringComparison.Ordinal)) return ActivationResult.Failure(ActivationOutcome.MappingMissing, "MAPPING_MISSING", "No active Mapping exists.");
        if (!snapshot.SourceStatus.Equals("Active", StringComparison.Ordinal)) return ActivationResult.Failure(ActivationOutcome.Validation, "SOURCE_NOT_ACTIVE", "Source is not active.");
        if (!snapshot.SourceType.Equals("Simulator", StringComparison.Ordinal)) return ActivationResult.Failure(ActivationOutcome.Validation, "SOURCE_NOT_ACTIVE", "Source is not a Simulator.");
        var now = DateTime.UtcNow;
        if (snapshot.EffectiveFromUtc > now || snapshot.EffectiveToUtc <= now) return ActivationResult.Failure(ActivationOutcome.MappingMissing, "MAPPING_MISSING", "No effective Mapping exists.");
        return null;
    }

    private static bool SameOrganizationFacts(Site? firstSite, Site? secondSite, Area? firstArea, Area? secondArea,
        Asset? firstAsset, Asset? secondAsset) =>
        firstSite is not null && secondSite is not null && firstArea is not null && secondArea is not null &&
        firstAsset is not null && secondAsset is not null &&
        firstSite.Id == secondSite.Id && firstSite.Version == secondSite.Version && firstSite.Status == secondSite.Status &&
        firstArea.Id == secondArea.Id && firstArea.SiteId == secondArea.SiteId && firstArea.Version == secondArea.Version && firstArea.Status == secondArea.Status &&
        firstAsset.Id == secondAsset.Id && firstAsset.SiteId == secondAsset.SiteId && firstAsset.AreaId == secondAsset.AreaId &&
        firstAsset.Version == secondAsset.Version && firstAsset.Status == secondAsset.Status;

    private static async Task<ActivationResult> Abort(HostTransactionCoordinator hostTx, ActivationResult failure)
    {
        await hostTx.RollbackAsync();
        return failure;
    }

    private sealed class OrganizationTransactionParticipant : IHostTransactionParticipant
    {
        private readonly IOrganizationTransaction _transaction;

        public OrganizationTransactionParticipant(IOrganizationTransaction transaction) => _transaction = transaction;

        public ValueTask AcquireLockAsync(IHostTransaction transaction, LockRequest request, CancellationToken ct = default) => ValueTask.CompletedTask;
        public async ValueTask CommitAsync(IHostTransaction transaction, CancellationToken ct = default) => await _transaction.CommitAsync(ct);
        public async ValueTask RollbackAsync(IHostTransaction transaction, CancellationToken ct = default) => await _transaction.RollbackAsync(ct);
    }

    private sealed class OutboxTransactionParticipantAdapter : IHostTransactionParticipant
    {
        private readonly IOutboxTransactionParticipant _participant;

        public OutboxTransactionParticipantAdapter(IOutboxTransactionParticipant participant) => _participant = participant;

        public ValueTask AcquireLockAsync(IHostTransaction transaction, LockRequest request, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask CommitAsync(IHostTransaction transaction, CancellationToken ct = default) => _participant.CommitAsync(transaction, ct);
        public ValueTask RollbackAsync(IHostTransaction transaction, CancellationToken ct = default) => _participant.RollbackAsync(transaction, ct);
    }
}
