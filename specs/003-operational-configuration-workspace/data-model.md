# Data Model: Operational Configuration Workspace

Feature 003 Phase 1 adds no independent persisted aggregate. It derives workspace state from the
existing model and adds a production application command for an existing IAM relationship.

## Existing persisted entities

| Entity | Owner | Phase 1 use | Lifecycle/version rule |
|---|---|---|---|
| User | IAM | Select existing active Engineer | Existing status, role, and version remain authoritative |
| Site Scope | IAM | Administrator-to-Engineer handoff | Idempotent assignment to existing Site; no browser claims |
| Site | Organization | Wizard root and landing scope | Draft → Active by Administrator; expected version |
| Area | Organization | Wizard step and scope | Draft → Active after Site; expected version |
| Asset | Organization | Wizard step | Draft → Active after Area; expected version |
| Measurement Point | Organization | Wizard step and operational eligibility | Draft → Active after all activation checks; expected version |
| Metric/Unit compatibility | Catalog | Point validation | Existing active/canonical compatibility |
| Data Source | Catalog | Simulator Source | Draft → Active; expected version |
| Source Mapping | Catalog | Source-to-Point lineage | Draft → Active for configuration-ready Draft Point; expected version and effective period |
| Simulator Configuration Head/Version | Acquisition | Pinned immutable runtime behavior | Append immutable version; no invented Active status |
| Simulator Run | Acquisition | Proof setup does not auto-start | No row created by wizard completion |
| Command Idempotency Record | Integration | Duplicate-safe mutation retries | Existing caller/operation/key/fingerprint rules |
| Outbox Event | Integration | Cross-module/Audit delivery | Existing transactional append |
| Audit Event | Audit | Reviewable setup actions | Append-only and redacted |

## Derived Operational Workspace Status

The status document is a read model, never stored as an independent lifecycle.

| Field | Meaning | Derivation |
|---|---|---|
| `landing` | `SetupWizard`, `ContinueSetup`, `Dashboard`, `NoAuthorizedScope`, or `DependencyError` | Principal + authorized persisted state + dependency result |
| `roleMode` | `Administrator`, `Engineer`, or `ReadOnly` | Server principal roles/capabilities |
| `authorizedSites` | Sites visible to caller | Organization query filtered by principal before paging |
| `selectedSiteId` | Current resumable Site when unambiguous | Authorized persisted chain; never response index |
| `completedSteps` | Ordered completed wizard step IDs | Entity existence/status and required relationships |
| `nextStep` | First incomplete or invalid step | Server derivation using eight-step state model |
| `validationFailures` | Safe failures grouped by step | Complete-chain validation result |
| `operationalChainCount` | Authorized valid chain count | Scope-filtered full-chain eligibility |
| `incompleteChainCount` | Authorized incomplete setup count | Scope-filtered derivation |
| `dependency` | `Available` or safe runtime failure | API/database query outcome without secret details |
| `versionTokens` | Current versions needed for mutation | Existing entity versions/ETags |

## Eight-step derivation

1. **Site and Engineer assignment**
   - Administrator: Site exists, is eligible/Active, and selected Engineer has Site scope.
   - Engineer: at least one assigned Site exists; Site fields are read-only.
2. **Area**: an Area exists inside selected Site.
3. **Asset**: an Asset exists inside selected Area.
4. **Measurement Point**: a Draft/eligible Point exists inside selected Asset with Metric, Unit,
   Data Owner, expected interval, and no-data threshold.
5. **Data Source**: a Simulator Source exists and is authorized for the chain.
6. **Mapping**: an effective-period Mapping, Draft or Active, relates Source to Point.
7. **Simulator Configuration**: a current immutable configuration version exists for Source.
8. **Validate and Activate**: complete validation passes and required owner entities are Active.

Step display order stays eight as approved. Activation order within step 8 follows owner
preconditions and is not inferred from display order.

## Complete-chain validation

Validation reads a caller-authorized chain and returns step-local failures without writes.

- Site exists, caller can see it, and Site is Active before Engineer continuation.
- Engineer identity is Active, has Engineer role, and has matching Site/Area scope.
- Area belongs to Site; Asset belongs to Area; Point belongs to Asset.
- Area and Asset are Active before Point activation.
- Point Metric and Unit are Active and compatible.
- Point Data Owner is Active and eligible in scope.
- `expectedIntervalSeconds > 0`.
- `noDataAfterSeconds > expectedIntervalSeconds`.
- Source is Simulator type, authorized, and lifecycle-eligible for activation; it must be Active
  before Point activation/Run eligibility.
- Exactly one non-overlapping effective-period Mapping relates Source and Point and is
  lifecycle-eligible; it must be Active before Point activation.
- Current immutable Simulator Configuration version is valid for Source.
- Current versions are returned so stale activation calls fail instead of overwriting.

## Legal activation transitions

| Order | Entity | Actor | Preconditions |
|---:|---|---|---|
| 1 | Site | Administrator | Required Site fields; current version |
| 2 | Area | Administrator or scoped Engineer | Parent Site Active; current version |
| 3 | Asset | Administrator or scoped Engineer | Parent Area Active; current version |
| 4 | Data Source | Administrator or scoped Engineer | Existing Catalog/source scope checks; current version |
| 5 | Source Mapping | Administrator or scoped Engineer | Draft Point configuration-ready; no effective overlap; current version |
| 6 | Measurement Point | Administrator or scoped Engineer | Ancestors, Data Owner, Metric/Unit, interval, Source, and exactly one Mapping eligible; current version |

Simulator Configuration version creation/validation occurs after Mapping creation and before
Mapping/Point activation, but has no separate Active transition. Simulator Start is explicitly
outside wizard activation.

## Partial failure and retry

- Each owner mutation commits independently in its existing host transaction.
- Status and version are re-read after each success.
- Failure stops later steps and returns `failedStep`, safe `errorCode`, and correlation ID.
- Already committed valid states remain.
- Reusing the same idempotency key and canonical request replays the original result.
- Changing fields or expected version under the same key returns idempotency conflict.
- No compensation changes an entity back solely to simulate an all-or-nothing wizard.

## Forward migration

Implementation review proved that a Draft Data Source has no pre-Mapping Site relationship.
Migration `0014_operational_workspace_scope.sql` therefore adds nullable
`catalog.data_sources.site_id` for scoped resume and a partial unique index for root Site scopes.
Legacy Sources remain nullable; the workspace does not use a global Draft fallback. Progress itself
remains derived and no workflow table is introduced.
