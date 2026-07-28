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

public sealed record ActivationResult(bool IsSuccess, ActivationOutcome Outcome, string? ErrorCode, string? ErrorDetail,
    PointId? PointId = null, PointStatus? NewStatus = null, long? NewVersion = null)
{
    public static ActivationResult Success(PointId id, PointStatus status, long version) => new(true, ActivationOutcome.Allowed, null, null, id, status, version);
    public static ActivationResult NoChange(PointId id, long version) => new(true, ActivationOutcome.NoOp, "NO_OP", "Point is already Active.", id, PointStatus.Active, version);
    public static ActivationResult Failure(ActivationOutcome outcome, string code, string detail) => new(false, outcome, code, detail);
}

public static class ActivateMeasurementPoint
{
    public static async Task<ActivationResult> ExecuteAsync(
        PointId pointId,
        long expectedVersion,
        OrganizationCommandContext ctx,
        IOrganizationCommandRepository targetLookup,
        IActivationIdentityParticipant iam,
        IActivationOrganizationParticipant organization,
        IActivationCatalogParticipant catalog,
        IOrganizationAuthorization authorization,
        ITransactionalOutboxWriter outbox,
        HostTransactionCoordinator hostTx,
        CancellationToken ct = default)
    {
        var caller = await authorization.ResolveCallerAsync(ctx.ActorUserId, ct);
        if (caller is null || !caller.IsActive) return ActivationResult.Failure(ActivationOutcome.InactiveUser, "FORBIDDEN", "Caller is not authorized.");
        var scope = await targetLookup.GetPointScopeAsync(pointId, ct);
        if (scope is null) return ActivationResult.Failure(ActivationOutcome.NotFound, "NOT_FOUND", "The target is not visible.");
        var allowed = await authorization.AuthorizeAsync(ctx.ActorUserId, OrganizationResource.SiteChild, scope.SiteId.ToString(), ct);
        if (!allowed.IsAllowed)
            return ActivationResult.Failure(allowed.Code.Equals("NotFound", StringComparison.OrdinalIgnoreCase) ? ActivationOutcome.NotFound : ActivationOutcome.Forbidden,
                allowed.Code.Equals("NotFound", StringComparison.OrdinalIgnoreCase) ? "NOT_FOUND" : "FORBIDDEN", "The target is not visible.");
        var preflightPoint = await targetLookup.GetPointAsync(pointId, ct);
        if (preflightPoint is null) return ActivationResult.Failure(ActivationOutcome.NotFound, "NOT_FOUND", "The target is not visible.");
        if (expectedVersion != preflightPoint.Version) return ActivationResult.Failure(ActivationOutcome.VersionConflict, "VERSION_CONFLICT", "ExpectedVersion is stale.");
        if (preflightPoint.Status == PointStatus.Decommissioned) return ActivationResult.Failure(ActivationOutcome.InvalidState, "INVALID_STATE", "Decommissioned Point cannot be activated.");
        if (preflightPoint.Status == PointStatus.Active) return ActivationResult.NoChange(pointId, preflightPoint.Version);

        RegisterRequiredParticipants(hostTx, iam, organization, catalog, outbox);
        try
        {
            await hostTx.BeginAsync(ct);
            await hostTx.LockWithRetryAsync(LockTarget.IamUser, preflightPoint.DataOwnerUserId, 1, ct);
            await hostTx.LockWithRetryAsync(LockTarget.OrganizationSite, preflightPoint.SiteId.ToString(), 2, ct);
            await hostTx.LockWithRetryAsync(LockTarget.OrganizationArea, preflightPoint.AreaId.ToString(), 3, ct);
            await hostTx.LockWithRetryAsync(LockTarget.OrganizationAsset, preflightPoint.AssetId.ToString(), 4, ct);
            await hostTx.LockWithRetryAsync(LockTarget.OrganizationPoint, pointId.ToString(), 5, ct);
            await hostTx.LockWithRetryAsync(LockTarget.CatalogMetric, preflightPoint.MetricId, 6, ct);
            await hostTx.LockWithRetryAsync(LockTarget.CatalogUnit, preflightPoint.UnitId, 7, ct);
            await hostTx.LockWithRetryAsync(LockTarget.CatalogMapping, pointId.ToString(), 8, ct);
            await hostTx.LockWithRetryAsync(LockTarget.IntegrationOutbox, pointId.ToString(), 9, ct);

            var owner = await iam.ReadDataOwnerAsync(hostTx, preflightPoint.DataOwnerUserId, preflightPoint.SiteId.ToString(), preflightPoint.AreaId.ToString(), ct);
            var orgSnapshot = await organization.ReadLockedSnapshotAsync(hostTx, pointId, ct);
            var catalogSnapshot = await catalog.ReadActivationSnapshotAsync(hostTx, pointId.ToString(), preflightPoint.MetricId, preflightPoint.UnitId, DateTime.UtcNow, ct);
            var failure = ValidateOwner(owner, preflightPoint.DataOwnerUserId, preflightPoint.SiteId.ToString(), preflightPoint.AreaId.ToString())
                ?? ValidateOrganization(orgSnapshot, expectedVersion)
                ?? ValidateCatalog(catalogSnapshot, pointId.ToString(), preflightPoint.MetricId, preflightPoint.UnitId);
            if (failure is not null) return await Abort(hostTx, failure);

            var ownerAgain = await iam.RecheckDataOwnerAsync(hostTx, preflightPoint.DataOwnerUserId, preflightPoint.SiteId.ToString(), preflightPoint.AreaId.ToString(), ct);
            var catalogAgain = await catalog.RecheckActivationSnapshotAsync(hostTx, pointId.ToString(), preflightPoint.MetricId, preflightPoint.UnitId, DateTime.UtcNow, ct);
            if (!Equals(owner, ownerAgain) || !Equals(catalogSnapshot, catalogAgain))
                return await Abort(hostTx, ActivationResult.Failure(ActivationOutcome.ProviderVersionConflict, "PROVIDER_VERSION_CONFLICT", "Provider facts changed before commit."));
            var lockedAgain = await organization.ReadLockedSnapshotAsync(hostTx, pointId, ct);
            if (!SameOrganizationSnapshot(orgSnapshot, lockedAgain, expectedVersion))
                return await Abort(hostTx, ActivationResult.Failure(ActivationOutcome.ProviderVersionConflict, "PROVIDER_VERSION_CONFLICT", "Organization facts changed before commit."));

            var oldStatus = lockedAgain!.Point.Status;
            var activatedPoint = await organization.StageActivationAsync(hostTx, lockedAgain, ctx.ActorUserId, caller.Username, ctx.CorrelationId, ctx.CausationId, ct);
            var envelope = OrganizationEvents.BuildPointStatusChanged(activatedPoint, oldStatus, PointStatus.Active, ctx, caller);
            await outbox.EnqueueAsync(envelope, hostTx, ct);
            await hostTx.CommitAsync(ct);
            return ActivationResult.Success(pointId, PointStatus.Active, activatedPoint.Version);
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

    private static void RegisterRequiredParticipants(HostTransactionCoordinator host, IActivationIdentityParticipant iam,
        IActivationOrganizationParticipant organization, IActivationCatalogParticipant catalog, ITransactionalOutboxWriter outbox)
    {
        host.RegisterParticipant(LockTarget.IamUser, iam);
        foreach (var target in new[] { LockTarget.OrganizationSite, LockTarget.OrganizationArea, LockTarget.OrganizationAsset, LockTarget.OrganizationPoint }) host.RegisterParticipant(target, organization);
        foreach (var target in new[] { LockTarget.CatalogMetric, LockTarget.CatalogUnit, LockTarget.CatalogMapping }) host.RegisterParticipant(target, catalog);
        host.RegisterParticipant(LockTarget.IntegrationOutbox, outbox);
    }

    private static ActivationResult? ValidateOwner(ActivationDataOwnerSnapshot owner, string expectedOwner, string siteId, string areaId)
    {
        if (!owner.Exists || !string.Equals(owner.DataOwnerUserId, expectedOwner, StringComparison.Ordinal)) return ActivationResult.Failure(ActivationOutcome.DataOwnerNotFound, "DATA_OWNER_INELIGIBLE", "Data Owner does not exist.");
        if (!owner.IsActive) return ActivationResult.Failure(ActivationOutcome.DataOwnerInactive, "DATA_OWNER_INELIGIBLE", "Data Owner is inactive.");
        if (owner.UserVersion <= 0) return ActivationResult.Failure(ActivationOutcome.DataOwnerIneligible, "DATA_OWNER_INELIGIBLE", "Data Owner user version must be positive.");
        if (owner.ScopeVersion <= 0) return ActivationResult.Failure(ActivationOutcome.DataOwnerIneligible, "DATA_OWNER_INELIGIBLE", "Data Owner scope version must be positive.");
        if (!owner.HasTrustedSiteScope || !owner.HasTrustedAreaScope || owner.HasForbiddenCapability || owner.TrustedSiteId != siteId || owner.TrustedAreaId != areaId)
            return ActivationResult.Failure(ActivationOutcome.DataOwnerIneligible, "DATA_OWNER_INELIGIBLE", "Data Owner scope is not eligible.");
        return null;
    }

    private static ActivationResult? ValidateOrganization(ActivationOrganizationSnapshot? snapshot, long expectedVersion)
    {
        if (snapshot is null) return ActivationResult.Failure(ActivationOutcome.NotFound, "NOT_FOUND", "The target is not visible.");
        var p = snapshot.Point;
        if (p.Version != expectedVersion) return ActivationResult.Failure(ActivationOutcome.VersionConflict, "VERSION_CONFLICT", "ExpectedVersion is stale.");
        if (p.SiteId != snapshot.Site.Id || p.AreaId != snapshot.Area.Id || p.AssetId != snapshot.Asset.Id ||
            snapshot.Area.SiteId != snapshot.Site.Id || snapshot.Asset.SiteId != snapshot.Site.Id || snapshot.Asset.AreaId != snapshot.Area.Id)
            return ActivationResult.Failure(ActivationOutcome.Validation, "INVALID_STATE", "Point ancestry is inconsistent.");
        if (!snapshot.Site.IsActive || !snapshot.Area.IsActive || !snapshot.Asset.IsActive) return ActivationResult.Failure(ActivationOutcome.ParentAssetNotActive, "PARENT_NOT_ACTIVE", "Point ancestors are not active.");
        if (snapshot.IntervalValidOverride == false || p.ExpectedIntervalSeconds <= 0 || p.NoDataAfterSeconds <= p.ExpectedIntervalSeconds) return ActivationResult.Failure(ActivationOutcome.Validation, "INTERVAL_INVALID", "Interval configuration is invalid.");
        return null;
    }

    private static ActivationResult? ValidateCatalog(ActivationCatalogSnapshot? snapshot, string pointId, string metricId, string unitId)
    {
        if (snapshot is null || snapshot.ActiveMappingCount == 0) return ActivationResult.Failure(ActivationOutcome.MappingMissing, "MAPPING_MISSING", "No active Mapping exists.");
        if (snapshot.ActiveMappingCount > 1) return ActivationResult.Failure(ActivationOutcome.MappingMultiple, "MAPPING_MULTIPLE", "Multiple active Mappings exist.");
        if (snapshot.PointId != pointId || snapshot.MappingPointId != pointId) return ActivationResult.Failure(ActivationOutcome.MappingMissing, "MAPPING_POINT_MISMATCH", "Mapping does not belong to the target Point.");
        if (snapshot.MetricId != metricId || snapshot.MetricStatus == "Missing") return ActivationResult.Failure(ActivationOutcome.MetricNotFound, "METRIC_NOT_FOUND", "Metric evidence is missing or mismatched.");
        if (snapshot.MetricVersion <= 0) return ActivationResult.Failure(ActivationOutcome.MetricNotFound, "METRIC_NOT_FOUND", "Metric version must be positive.");
        if (snapshot.UnitId != unitId || snapshot.UnitStatus == "Missing") return ActivationResult.Failure(ActivationOutcome.UnitNotFound, "UNIT_NOT_FOUND", "Unit evidence is missing or mismatched.");
        if (snapshot.UnitVersion <= 0) return ActivationResult.Failure(ActivationOutcome.UnitNotFound, "UNIT_NOT_FOUND", "Unit version must be positive.");
        if (!snapshot.MetricStatus.Equals("Active", StringComparison.Ordinal)) return ActivationResult.Failure(ActivationOutcome.InactiveMetric, "METRIC_INACTIVE", "Metric is not active.");
        if (!snapshot.UnitStatus.Equals("Active", StringComparison.Ordinal)) return ActivationResult.Failure(ActivationOutcome.InactiveUnit, "UNIT_INACTIVE", "Unit is not active.");
        if (!snapshot.IsCompatible || snapshot.CompatibilityVersion <= 0 || string.IsNullOrWhiteSpace(snapshot.CompatibilityIdentity) ||
            (snapshot.CompatibilityStatus is null || !snapshot.CompatibilityStatus.Equals("Active", StringComparison.Ordinal)))
            return ActivationResult.Failure(ActivationOutcome.IncompatibleMetricUnit, "UNIT_INCOMPATIBLE", "Metric and Unit are incompatible.");
        if (!snapshot.MappingStatus.Equals("Active", StringComparison.Ordinal)) return ActivationResult.Failure(ActivationOutcome.MappingMissing, "MAPPING_MISSING", "Mapping is not active.");
        if (snapshot.MappingVersion <= 0) return ActivationResult.Failure(ActivationOutcome.MappingMissing, "MAPPING_MISSING", "Mapping version must be positive.");
        if (!snapshot.SourceStatus.Equals("Active", StringComparison.Ordinal) || !snapshot.SourceType.Equals("Simulator", StringComparison.Ordinal)) return ActivationResult.Failure(ActivationOutcome.Validation, "SOURCE_NOT_ACTIVE", "Source is not an active Simulator.");
        if (snapshot.SourceVersion <= 0) return ActivationResult.Failure(ActivationOutcome.Validation, "SOURCE_NOT_ACTIVE", "Source version must be positive.");
        var now = DateTime.UtcNow;
        if (snapshot.EffectiveFromUtc > now || snapshot.EffectiveToUtc <= now) return ActivationResult.Failure(ActivationOutcome.MappingMissing, "MAPPING_MISSING", "Mapping is not effective.");
        return null;
    }

    private static bool SameOrganizationSnapshot(ActivationOrganizationSnapshot? first, ActivationOrganizationSnapshot? second, long expectedVersion) =>
        first is not null && second is not null && first.Point.Version == expectedVersion && second.Point.Version == expectedVersion &&
        first.Point.Id == second.Point.Id && first.Point.Status == second.Point.Status && first.Point.SiteId == second.Point.SiteId &&
        first.Point.AreaId == second.Point.AreaId && first.Point.AssetId == second.Point.AssetId && first.Point.MetricId == second.Point.MetricId &&
        first.Point.UnitId == second.Point.UnitId && first.Point.DataOwnerUserId == second.Point.DataOwnerUserId &&
        first.Site.Version == second.Site.Version && first.Area.Version == second.Area.Version && first.Asset.Version == second.Asset.Version &&
        first.Site.Status == second.Site.Status && first.Area.Status == second.Area.Status && first.Asset.Status == second.Asset.Status;

    private static async Task<ActivationResult> Abort(HostTransactionCoordinator hostTx, ActivationResult failure)
    {
        await hostTx.RollbackAsync();
        return failure;
    }
}
