# State-owning Persistence Adapter Inventory

Migrations create storage and integration tests verify it; neither is a runtime repository
implementation. Each port below requires a provider-owned PostgreSQL adapter and composition-root
registration. A consumer never writes another module's schema.

For every row, port/fake/composition architecture checks are `RUNNABLE_NOW`; adapter compilation is
`BLOCKED_BY_PACKAGE_POLICY` only when approved locked packages are unavailable; constraints,
transactions, concurrency, leases, and query plans are `BLOCKED_BY_DATABASE_ACCESS` when approved
PostgreSQL is unavailable. CI/deployment evidence is `BLOCKED_BY_COMPANY_APPROVAL`. A blocker is
never a pass.

| Owner/project and state | Public repository port(s) | Transaction responsibility | Required PostgreSQL verification |
|---|---|---|---|
| IAM: users, roles, scopes, capabilities, sessions, auth/session lookup | `IIamCommandRepository`, `IIamPrincipalSessionRepository` | API host UoW; IAM locks first; own state plus outbox via Integration last | username and active-assignment uniqueness; token-hash lookup; expiry/revocation/Disabled behavior; optimistic version; rollback + outbox |
| Organization: Site, Area, Asset, Point, lifecycle history | `IOrganizationCommandRepository`, `IOrganizationQueryRepository` | API/Worker host UoW; after IAM and before Catalog; activation/decommission row locks | hierarchy/code/interval constraints; ordered `FOR UPDATE`; concurrent activation/decommission guards; append history; rollback + outbox |
| Catalog: Metric, Unit, compatibility, Source, Mapping | `ICatalogCommandRepository`, `ICatalogEligibilityQueryRepository` | API/Worker host UoW; after Organization, before Acquisition | Metric/Unit uniqueness; effective-period exclusion; concurrent Mapping activation; lifecycle/dependency queries; rollback + outbox |
| Acquisition: configuration head/version, Run, Run-Point state, production attempts, leases | `IAcquisitionRunRepository`, `ISimulatorProductionAttemptRepository` | API/Worker host UoW; after Catalog, before Telemetry | immutable configuration versions; unique Run/Point/sequence; Pending/Completed attempt transition; cursor/PRNG/counters atomicity; lease reclaim |
| Telemetry: terminal identity/result registry, raw Measurement, Latest, Source Health | `ITelemetryIngestionRepository`, `ILatestProjectionRepository`, `ISourceHealthRepository`, `ITelemetryQueryRepository` | Worker/API host UoW; after Acquisition and before Integration | identity/fingerprint uniqueness; Accepted + raw atomicity; Rejected without raw; exact replay/conflict; Latest CAS/no regression; health/index/query behavior |
| Audit: append-only event and filtered query | `IAuditAppendRepository`, `IAuditQueryRepository`; `IAuditEventConsumer` is the application consumer port | Worker transaction appends Audit then completes Integration inbox last; API read transaction applies authorization first | unique source event; append-only grants; atomic append + inbox completion; keyset/filter/scope plan; global event restriction |
| Integration: command idempotency, outbox, inbox | `ICommandIdempotencyStore`, `ITransactionalOutboxWriter`, `IOutboxClaimRepository`, `IInboxDeduplicationRepository` | API/Worker composition root; short registrations/claims; Integration last in host mutation/consumer transactions | caller/operation/key uniqueness; fingerprint conflict/replay; Pending reclaim; original status/result; expiry cleanup; SKIP LOCKED; leases/retries/dedup |
| Operations: durable jobs, scheduling, leases, retries | `IDurableJobScheduler`, `IJobClaimRepository` | Operations-owned scheduling/claim transactions; handlers use provider ports, never provider tables | job type/key uniqueness; SKIP LOCKED; renew/expiry/reclaim; retry/Failed poison; reconciliation and replay idempotence |

Logical cross-schema identifiers have no cross-schema FK. Strict invariants are validated through
versioned provider ports inside the host-coordinated transaction and locked in the global order
IAM -> Organization -> Catalog -> Acquisition -> Telemetry -> Integration.
