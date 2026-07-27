# Phase 5 Activation Checkpoint (T107)

## 1. Scope

- Repository: `devphuclam/EnergySaving`
- Feature: `specs/002-asset-simulator-latest/`
- Phase: 5 only, T094–T107
- Explicit stop: no T108+ work, no Simulator Run, Worker production, Telemetry, API/Web,
  migrations, or release work.

## 2. Parent and governance

- Parent baseline HEAD: `4e68ca46d124d867a0737b17711a069bd83417aa`
- Accepted predecessor: T093 / Phase 4 checkpoint
- Constitution: `1.1.0`
- Phase 0 governance gate: accepted before green implementation
- Result is the working-tree diff; no commit was created by this invocation.

## 3. Implemented artifacts

- `src/BuildingBlocks/Persistence/HostTransactionCoordinator.cs`
- `src/Modules/Integration/Contracts/OutboxContracts.cs`
- `src/Modules/Organization/Contracts/OrganizationQueryContracts.cs`
- `src/Modules/Organization/Application/ActivateMeasurementPoint.cs`
- `src/Modules/Organization/Application/OrganizationEvents.cs`
- `src/Modules/Organization/Application/HierarchyCommands.cs` (activation remains deferred from
  the ordinary Phase 3 handler)
- `src/Modules/Organization/IUMP.Modules.Organization.csproj`
- `tests/Unit/Fakes/FakeActivationProviders.cs`
- `tests/Unit/Fakes/FakeTransactionalOutboxWriter.cs`
- `tests/Unit/Organization/PointActivationTests.cs`
- `tests/Unit/Organization/PointActivationTransactionTests.cs`
- `tests/Unit/Integration/OwnerEventEnvelopeTests.cs`
- `tests/Integration/Organization/PointActivationTransactionTests.cs`
- `tests/Unit/IUMP.Tests.Unit.csproj` and `tests/Unit/Program.cs`
- `tests/Verification/architecture.tests.ps1`
- `docs/architecture/r0-boundaries.md` (documents the provider-neutral Phase 5 coordination primitive)
- `specs/002-asset-simulator-latest/checklists/phase-05-red.md`
- `specs/002-asset-simulator-latest/checklists/phase-05-review.md`

## 4. Behavioral result

The provider-neutral orchestrator enforces authorization/scope, exact expected version, Draft or
Inactive → Active transitions, Active no-op, Decommissioned rejection, active parent ancestry,
Data Owner eligibility/scope, active Metric/Unit compatibility, exactly one effective active
Simulator Mapping, exact provider-version rechecks, one lifecycle row, and one staged owner event.

The host transaction records the canonical nine-target order and coordinates the Organization and
Integration participants through one commit/rollback path. Lock conflicts use the P-016 2-second
timeout, three attempts, 50/150/450ms backoff, and `TRANSIENT_DATABASE_CONFLICT` exhaustion code:

`IAM User → Organization Site → Organization Area → Organization Asset → Organization Point → Catalog Metric → Catalog Unit → Catalog Mapping → Integration Outbox`.

## 5. RED evidence (T097)

Recorded in `phase-05-red.md`: Debug build exit 0 followed by the pre-green focused run exit 1 with
the six expected `PHASE5_REQUIRED`/missing-envelope failures.

## 6. GREEN evidence (T094–T103, T105)

Exact commands and results:

```text
dotnet build tests/Unit/IUMP.Tests.Unit.csproj --no-restore --configuration Debug
Build succeeded; 0 warnings; 0 errors
dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj --no-build --configuration Debug
T079 87/0; T080 62/0; T071 19/39/0; T088 24/24/0; PASS: all tests

dotnet build tests/Unit/IUMP.Tests.Unit.csproj --no-restore --configuration Release
Build succeeded; 0 warnings; 0 errors
dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj --no-build --configuration Release
T079 87/0; T080 62/0; T071 19/39/0; T088 24/24/0; PASS: all tests

& .\tests\Verification\architecture.tests.ps1
PASS: architecture boundary contract
```

## 7. Fast harness

`& .\scripts\harness.ps1 -Mode Fast -Feature 002-asset-simulator-latest`

Result: **PASS=8**, including feature artifacts, verification contract, repository harness/policy/
scope, architecture and red-fixture checks, and unit tests.

## 8. Full harness

`& .\scripts\harness.ps1 -Mode Full -Feature 002-asset-simulator-latest`

Result: **PASS=10 individual checks**; the aggregate Full result is **BLOCKED/non-passing**
(exit-code 20) because mandatory environment checks are truthfully blocked:

| Check | Status | Classification | Blocker |
|---|---|---|---|
| database harness tool | BLOCKED | BLOCKED_BY_MISSING_TOOL | BLK-ENV-002 (`psql` executable missing) |
| CI | BLOCKED | BLOCKED_BY_COMPANY_APPROVAL | BLK-ENV-003 |
| container target | BLOCKED | BLOCKED_BY_COMPANY_APPROVAL | BLK-ENV-004 |

These are harness environment checks, not a Phase 5 application failure. A blocked check is not
reported as passing.

## 9. T094–T107 ledger

| Task | Result |
|---|---|
| T094 | PASS — activation prerequisite/error suite |
| T095 | PASS — lock order, transaction identity, rollback/cancellation |
| T096 | PASS — safe owner-event envelope and correlation/causation |
| T097 | PASS — chronological RED evidence |
| T098 | PASS — versioned envelope and enqueue port |
| T099 | PASS — deterministic staged outbox fake |
| T100 | PASS — host coordinator and exact global lock order |
| T101 | PASS — provider-neutral Point activation orchestrator |
| T102 | PASS — safe `PointStatusChanged.v1` owner event |
| T103 | PASS — provider-neutral integration transaction source compiles and asserts the canonical lock/rollback contract; live adapter execution is intentionally T104 |
| T104 | BLOCKED — `BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE`; PostgreSQL adapter prerequisites T029/T050/T072 remain package-policy blocked; unchecked |
| T105 | PASS — architecture ownership/lock/provider-neutral checks |
| T106 | PASS — Standards/Spec review, zero Critical/High |
| T107 | PASS — this checkpoint |

Counts: **PASS 13, FAIL 0, BLOCKED 1, runnable NOT_RUN 0**.

## 10. Database and package boundary

- Approved target remains `127.0.0.1:5433/iump_dev`; port `5432` was not contacted.
- No database mutation, migration execution, `psql` command, Docker, SQLite/InMemory substitute,
  package installation, public restore, or secret output was used.
- T104 is not classified as database-access blocked; it is transitive to the package-policy blocks.

## 11. Progression and release

- Phase 5 runnable progression: **YES** (all runnable tasks pass).
- Phase 6 progression: **NO / not executed**; T108+ remain unchecked.
- Release readiness: **NO**. Full remains non-passing while mandatory environment checks are blocked.
- Demo readiness: activation transaction and owner-event provider-neutral capability is available;
  Simulator Run/Telemetry/latest-value behavior is not implemented in this phase.

## 12. Explicit stop

Stop after T107. Continue only on a new explicit `/speckit.implement` invocation for the next phase.
