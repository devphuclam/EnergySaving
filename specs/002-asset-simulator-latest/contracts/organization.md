# Organization Contracts

## HTTP surface

- CRUD/list: /api/v1/sites, /api/v1/sites/{siteId}/areas,
  /api/v1/areas/{areaId}/assets, /api/v1/assets/{assetId}/points.
- Lifecycle: POST .../{id}/activate, .../inactivate and Asset/Point .../decommission.
- Root Site create/update/activate/inactivate is Administrator-only.
- Area/Asset/Point mutations are Administrator or Engineer with existing Site scope.
- Create commands require Idempotency-Key but no If-Match.
- Update/lifecycle/delete commands require both Idempotency-Key and If-Match.
- Reads are scope-filtered before paging. Version conflicts return 409 VERSION_CONFLICT.

## Bootstrap contract

CreateRootSite is an Administrator-only command. AssignEngineerSiteScope is an
Administrator-only IAM command executed after Site creation. An Engineer without Site scope cannot
create a root Site or use a global bypass. Administrator is global; scoped Engineer manages lower
hierarchy.

## Point activation contract

ActivatePoint(pointId, expectedVersion) asks:

1. IAM: Data Owner exists, is Active and is appropriately scoped.
2. Catalog: Metric and Unit are Active and compatible.
3. Catalog: exactly one Simulator Mapping is effective and Active.
4. Organization: ancestors are Active, expected interval is positive, and no-data threshold is
   greater than expected interval.

It commits only when returned provider versions remain valid at the transaction boundary.
Provider rows required for strict invariants are locked in the global deterministic order
(1. IAM user, 2. Organization hierarchy, 3. Catalog Metric/Unit/Mapping) before commit.

**Point activation MUST NOT commit if Data Owner, Metric, Unit or Mapping can change between
validation and commit.** The activation command rechecks provider version snapshots inside the
same REPEATABLE READ transaction and rolls back on version mismatch.

Specific failures include PARENT_NOT_ACTIVE, METRIC_INACTIVE, UNIT_INCOMPATIBLE,
DATA_OWNER_INELIGIBLE, INTERVAL_INVALID, MAPPING_MISSING and MAPPING_MULTIPLE.

IPointOperationalEligibility returns hierarchy status, interval thresholds, Metric/Unit IDs,
Site/Area scope and owner version. Acquisition and Telemetry consume this snapshot but never mutate
Organization.

IPointDecommissionDependency is a synchronous Organization-to-Acquisition query. It returns whether
any mapped Simulator Run is Running. Decommission fails with RUNNING_SIMULATOR before mutation.
Both locks (Organization Point, then Acquisition Run) are acquired in global order.

## Lifecycle behavior

Draft children may be configured beneath Draft or Active parents, but cannot activate beneath an
inactive parent, receive production data or appear Online. Asset decommission fails while any child
Point is Active and never cascades. Point decommission fails while a mapped Run is Running, requires
explicit stop/inactivation, triggers Source Status reconciliation and is terminal. Point codes are
never reused after decommission.

## Events

SiteStatusChanged.v1, AreaStatusChanged.v1, AssetStatusChanged.v1, PointConfigurationChanged.v1
and PointStatusChanged.v1 contain identity, scope, status and version only. Producers publish through
integration.outbox_event; Audit consumes through inbox_message.
