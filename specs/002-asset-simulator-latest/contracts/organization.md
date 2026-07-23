# Organization Contracts

## HTTP surface

- CRUD/list: `/api/v1/sites`, `/api/v1/sites/{siteId}/areas`,
  `/api/v1/areas/{areaId}/assets`, `/api/v1/assets/{assetId}/points`.
- Lifecycle: `POST .../{id}/activate`, `.../inactivate`, and for Asset/Point
  `.../decommission`.
- Mutations require Engineer in scope or Administrator, expected version, and correlation ID.
- Reads are scope-filtered at the server before filtering/paging.

## Activation contracts

`ActivatePoint(pointId, expectedVersion)` asks:

1. IAM: Data Owner exists, is Active, and is appropriately scoped.
2. Catalog: Metric and Unit are Active and compatible.
3. Acquisition: exactly one Simulator mapping is effective and Active.
4. Organization-owned checks: Active ancestors, positive expected interval, and no-data threshold
   greater than expected interval.

It commits only if every result remains current at the transaction boundary. Failure returns
specific codes such as `PARENT_NOT_ACTIVE`, `METRIC_INACTIVE`, `UNIT_INCOMPATIBLE`,
`DATA_OWNER_INELIGIBLE`, `INTERVAL_INVALID`, `MAPPING_MISSING`, or `MAPPING_MULTIPLE`.

`IPointOperationalEligibility(pointId, sourceId, at)` returns hierarchy status, interval thresholds,
Metric/Unit IDs, Site/Area scope, and owner version. Telemetry uses the returned snapshot to validate
ingestion but never changes Organization state.

## Lifecycle behavior

Draft children can be configured under Draft/Active parents. They cannot activate beneath an
inactive parent, receive production data, or appear Online. Asset decommission fails while an
Active Point exists. Decommissioned Asset/Point records are terminal; Point codes are not reused.

## Events

`SiteStatusChanged`, `AreaStatusChanged`, `AssetStatusChanged`, `PointConfigurationChanged`, and
`PointStatusChanged`. Payloads contain identity/scope/status/version only; sensitive user data and
full database rows are excluded.

