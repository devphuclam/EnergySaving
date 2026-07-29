# Phase 9 checkpoint — final contract alignment

Frozen corrective baseline SHA: `bd513d25f07c1034398419b068fae88ad0136b0e`.
Stop boundary: **T223 complete; T224+ not executed.**

## Task ledger

| Status | Count | Tasks |
|---|---:|---|
| PASS | 46 | T170–T191, T194–T201, T203–T204, T207–T217, T221–T223 |
| BLOCKED_BY_PACKAGE_POLICY | 5 | T192, T193, T202, T205, T218 |
| BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE | 3 | T206, T219, T220 |
| FAIL | 0 | — |
| Runnable NOT_RUN | 0 | — |

Blocked tasks are not counted as PASS. T218 remains unchecked. T224 and later tasks were not
opened or modified.

## Measured Phase 9 unit evidence

| Task | Cases/scenarios | Assertions | Failures |
|---|---:|---:|---:|
| T170 | 6 | 6 | 0 |
| T171 | 5 | 4 | 0 |
| T172 | 8 | 8 | 0 |
| T173 | 7 | 7 | 0 |
| T174 | 7 | 7 | 0 |
| T175 | 6 | 8 | 0 |
| T176 | 4 | 3 | 0 |
| T177 | 6 | 7 | 0 |
| T178 | 35 | 35 | 0 |
| T179 | 11 | 11 | 0 |
| T180 | 9 | 9 | 0 |
| T181 | 8 | 8 | 0 |

The counts are assigned from executed scenario/assertion paths, not declared constants. T172
executes live/expired Pending, concurrent same-key ownership, crash-after-registration,
transaction completion failure/rollback, exact replay metadata and one owner/outbox mutation.
T174–T177 execute delivery, Audit, keyset and reconciliation operations. T178–T181 invoke actual
endpoint delegates with fake ports and server principals.

## Changed implementation surface

- Canonical Contracts fingerprint; duplicate Application implementation removed.
- Transaction-aware command executor, configuration/Simulator mutation routes and transaction ports.
- Full configuration hierarchy/catalog/source/mapping/simulator/lifecycle route surface and query delegates.
- Required-consumer inbox delivery, live/expired/failed distinction, capped retry and restart deduplication.
- Canonical Audit hash (all event fields), host-transaction append/inbox seam and strict keyset cursor.
- Operations reconciliation/release, diagnostics and operator replay contracts without object shortcuts.
- Backend-aligned Web gateways/routes and fake gateway state-transition behavior evidence.
- Migration 0011 exact command Pending/Completed constraints and Completed immutability trigger while
  retaining the R0 inbox `Processing`/`Completed`/`Failed` vocabulary.
- T221 architecture checks, T222 review and this T223 checkpoint; no database adapter/composition work.

## Commands and exact results

| Command | Exit | Result |
|---|---:|---|
| `dotnet build .\IUMP.slnx --no-restore --configuration Debug` | 0 | PASS |
| Debug unit runner | 0 | PASS; all T170–T181 failures 0. |
| `dotnet build .\IUMP.slnx --no-restore --configuration Release` | 0 | PASS |
| Release unit runner | 0 | PASS; all T170–T181 failures 0. |
| `tests/Verification/architecture.tests.ps1` | 0 | PASS; T221 result PASS |
| `git diff --check` | 0 | PASS (run before handoff) |
| Web `npm run lint` | 0 | PASS (existing oxlint warning only) |
| Web `npm run build` | 0 | PASS |
| Fast harness | 0 | PASS=8 |
| `& .\scripts\harness.ps1 -Mode Full -Feature 002-asset-simulator-latest` | 20 | **Non-passing by policy**: PASS=10, BLOCKED_BY_MISSING_TOOL=1 (`psql`), BLOCKED_BY_COMPANY_APPROVAL=2 (CI/container target). |

## Capability and progression

| Capability | State |
|---|---|
| Browser source/build ready | YES |
| Ready for Phase 10 | YES — runnable Phase 9 contracts pass; Phase 10 remains a separate task phase |
| Live registered API/Worker runtime | NO |
| PostgreSQL E2E/migrations | NO — approved target is `127.0.0.1:5433/iump_dev`; execution `NOT_RUN` |
| Release | NO |

The approved database capability remains available, but this closure did not connect, migrate, or
mutate it. Port `127.0.0.1:5432` was not contacted. No secret value is recorded here.
