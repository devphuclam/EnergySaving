# Phase 5 Activation Checkpoint (T107)

## Scope and baseline

- Repository: `devphuclam/EnergySaving`
- Feature: `specs/002-asset-simulator-latest/`
- Parent baseline: `3ae683a14385c0272752e5b18a0fccd2b9b39ed0`
- Scope: T094-T107 only; stop before T108.

## Changed files

- `src/BuildingBlocks/Persistence/HostTransactionCoordinator.cs`
- `src/Modules/Integration/Contracts/OutboxContracts.cs`
- `src/Modules/Integration/IUMP.Modules.Integration.csproj`
- `src/Modules/Organization/Application/ActivateMeasurementPoint.cs`
- `src/Modules/Organization/Application/OrganizationEvents.cs`
- `src/Modules/Organization/Contracts/OrganizationQueryContracts.cs`
- `tests/Integration/Organization/PointActivationTransactionTests.cs`
- `tests/Unit/Fakes/FakeActivationOrganizationParticipant.cs`
- `tests/Unit/Fakes/FakeActivationProviders.cs`
- `tests/Unit/Fakes/FakePointActivationProviderFactory.cs`
- `tests/Unit/Fakes/FakeTransactionalOutboxWriter.cs`
- `tests/Unit/Integration/OwnerEventEnvelopeTests.cs`
- `tests/Unit/Organization/PointActivationTests.cs`
- `tests/Unit/Organization/PointActivationTransactionTests.cs`
- `tests/Unit/Program.cs`
- `tests/Verification/architecture.tests.ps1`
- `specs/002-asset-simulator-latest/checklists/phase-05-red.md`
- `specs/002-asset-simulator-latest/checklists/phase-05-review.md`
- `specs/002-asset-simulator-latest/checklists/phase-05-postgresql.md`
- `specs/002-asset-simulator-latest/checklists/phase-05-activation.md`

## Runnable evidence

Debug build: exit `0`, `0` warnings, `0` errors.
Focused Debug run: exit `0`.

```text
T094: cases=41; failures=0
T095: cases=12; failures=0
T096: cases=1; failures=0
T103: cases=4; failures=0
T071: tests=19; assertions=39; failures=0
T088: scenarios=24; assertions=24; failures=0
PASS: all tests
```

The tests prove the same host TransactionId across IAM/Organization/Catalog/Integration, canonical lock order with Integration last, exact `50/150/450` retry delays and four-attempt exhaustion, one staged Point/lifecycle/outbox result, rollback on provider/outbox failure, the complete prerequisite matrix, Mapping-to-Point identity, Active `NO_OP`, nullable causation, and actual T103 orchestrator execution.

`& .\tests\Verification\architecture.tests.ps1` returned:

```text
PASS: architecture boundary contract
```

Fast harness returned exit `0` with `PASS=8`. Fresh Full harness returned exit `1`: ten checks passed and three environment checks were blocked (`database: BLOCKED_BY_MISSING_TOOL [BLK-ENV-002]`, `ci: BLOCKED_BY_COMPANY_APPROVAL [BLK-ENV-003]`, `container-target: BLOCKED_BY_COMPANY_APPROVAL [BLK-ENV-004]`). Those are harness environment classifications, not application or database-access claims; the approved database capability remains available and no database command was run.

## T094-T107 ledger

| Task | Result |
|---|---|
| T094 | PASS |
| T095 | PASS |
| T096 | PASS |
| T097 | PASS |
| T098 | PASS |
| T099 | PASS |
| T100 | PASS |
| T101 | PASS |
| T102 | PASS |
| T103 | PASS |
| T104 | BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE |
| T105 | PASS |
| T106 | PASS |
| T107 | PASS |

Counts: **PASS 13, BLOCKED 1, FAIL 0, runnable NOT_RUN 0**.

## Capability and progression

- PostgreSQL capability: AVAILABLE/VERIFIED at `127.0.0.1:5433/iump_dev`; no database mutation in this invocation.
- Port `5432`: not used.
- Ready for Phase 6: **YES** (runnable Phase 5 scope complete).
- Release-ready: **NO** (T104 remains blocked and this is not a release checkpoint).
- T108 and later: **not executed**.

Stop after T107.
