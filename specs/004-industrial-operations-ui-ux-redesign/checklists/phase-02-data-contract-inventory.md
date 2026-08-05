# Phase 2 Data-Contract Inventory

Baseline: `559ce393e060242ad3f80065ae29c545b98eb895`
Branch: `feat/004-phase-02-dashboard-telemetry`
Scope: T028–T036 only; read-only inventory, no API/backend/database changes.

## Existing contract inventory

| Evidence field | Operational Dashboard response | Telemetry current response | Status and UI treatment |
|---|---|---|---|
| Point/measurement identity | `latest[].pointId` | `pointId`, `pointCode`, `pointName`, selected option | AVAILABLE_DIRECTLY |
| Numeric value | `latest[].value` | `value` plus `hasData`/`dataState` | AVAILABLE_DIRECTLY; numeric `0` is preserved |
| Unit | `latest[].unit` | `point.unit` / `unit` | AVAILABLE_DIRECTLY |
| Metric | ABSENT | `point.metric` | AVAILABLE_DIRECTLY in telemetry; dashboard says unavailable |
| Data quality | `latest[].quality` | `quality` | AVAILABLE_DIRECTLY |
| Quality reason | ABSENT | `reasonCode` / mapped `reason` | AVAILABLE_DIRECTLY in telemetry; dashboard says unavailable |
| Source timestamp | ABSENT (only received timestamp is returned) | `sourceTimestampUtc` | AVAILABLE_DIRECTLY only in telemetry; dashboard says unavailable |
| Receipt timestamp | `latest[].receivedAtUtc` | `receivedAtUtc` | AVAILABLE_DIRECTLY |
| Freshness/cutoff | Health status and last-received can support safe freshness mapping; no cutoff | Health status, expected interval, no-data-after; no cutoff | AVAILABLE_BY_SAFE_MAPPING for freshness; cutoff ABSENT_FROM_EXISTING_CONTRACT |
| Source health | `health[].status`, `lastReceivedAtUtc` | `health.status` and run fields | AVAILABLE_DIRECTLY |
| Coverage | ABSENT | ABSENT | BLOCKED_BY_EXISTING_CONTRACT; never render a fabricated numerator/denominator |
| Historical points | ABSENT (summary/latest only) | ABSENT | BLOCKED_BY_EXISTING_CONTRACT; chart is truthful Unavailable |
| Missing intervals/gaps | ABSENT | ABSENT | BLOCKED_BY_EXISTING_CONTRACT; SVG fixture semantics only |
| Timezone | Not returned; existing context contract uses `Asia/Ho_Chi_Minh` | Not returned; existing context contract uses `Asia/Ho_Chi_Minh` | AVAILABLE_BY_SAFE_MAPPING, explicitly labelled context |
| Scope | Server-scoped snapshot and authenticated session scope | Server-scoped hierarchy options and selected scope | AVAILABLE_BY_SAFE_MAPPING/direct selection; no out-of-scope metadata is derived |

## Sources inspected

- `src/Hosting/Abstractions/ApplicationPorts.cs` — dashboard/latest read models.
- `src/Hosting/Abstractions/TelemetryWorkspacePorts.cs` — telemetry options/current read models.
- `src/Composition/Postgres/PostgresOperationalDashboardPorts.cs` — dashboard projection fields.
- `src/Composition/Postgres/PostgresTelemetryWorkspacePorts.cs` — telemetry current projection.
- `src/Api/OperationalDashboardEndpoints.cs` and `src/Api/TelemetryQueryEndpoints.cs` — read-only endpoint mapping.
- `src/Web/src/gateways/webGateways.ts` — browser mapping; no browser-time fallback is used for `lastRefreshAt`.
- `specs/004-industrial-operations-ui-ux-redesign/spec.md` — US2/US5, FR-006/011/012/018/020 and C-17 requirements.

## Boundary decisions

No value, timestamp, quality reason, cutoff, coverage, or historical point is invented. Run counters are not coverage. The Phase 2 UI therefore exposes `Unavailable`/`No Data` when the existing contract cannot support the requested evidence. A future contract extension is outside T028–T036 and must be planned before a production coverage or historical-series claim can be made.

## Verification commands

The inventory was informed by the repository source inspection above and the existing Spec Kit artifacts. No package, API, Worker, database, migration, authentication, or deployment file was changed.
