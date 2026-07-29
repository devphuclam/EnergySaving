# Phase 10 Acceptance and Release-Evidence Checkpoint

## 1. Baseline SHA

`e2b3c40d00055de8e836801664595f21a6a36204`

## 2. Exact changed files

1. `database/migrations/0012_r1_idempotent_seeds.sql`
2. `database/migrations/0013_r1_validation_reconciliation.sql`
3. `docs/ci-readiness.md`
4. `docs/code-review.md`
5. `specs/002-asset-simulator-latest/checklists/acceptance-traceability.md`
6. `specs/002-asset-simulator-latest/checklists/phase-10-acceptance.md`
7. `specs/002-asset-simulator-latest/checklists/phase-10-fast.md`
8. `specs/002-asset-simulator-latest/checklists/phase-10-full.md`
9. `specs/002-asset-simulator-latest/checklists/requirements-traceability.md`
10. `specs/002-asset-simulator-latest/tasks.md`
11. `tests/Integration/Acceptance/AuditIdempotencyE2ETests.cs`
12. `tests/Integration/Acceptance/ConfigurationRaceTests.cs`
13. `tests/Integration/Acceptance/LatestHealthRaceTests.cs`
14. `tests/Integration/Acceptance/SimulatorCrashRecoveryTests.cs`
15. `tests/Integration/Acceptance/TimedJourneyAcceptanceTests.cs`
16. `tests/Unit/Acceptance/AuthorizationNegativeTests.cs`
17. `tests/Unit/Acceptance/LifecycleAcceptanceTests.cs`
18. `tests/Unit/IUMP.Tests.Unit.csproj`
19. `tests/Unit/Program.cs`
20. `tests/Verification/observability.tests.ps1`

## 3. T224 authorization acceptance

Cases/assertions/failures: **11 / 11 / 0**. Executed evidence covers safe unauthenticated 401,
capability 403, authoritative out-of-scope 404, Administrator global access, scoped Engineer and
Manager behavior, scoped `AUDIT_READ`, inactive user/session, ignored client identity headers,
filter-before-lookup/paging, and response-body anti-enumeration.

## 4. T225 lifecycle acceptance

Cases/assertions/failures: **14 / 14 / 0**. Executed evidence covers dependency-protected deletion,
no cascade, deactivate/suspend/decommission, terminal superseded/decommissioned states, active
dependencies, Audit-only deletion, immutable evidence retention, safe conflict, required
ExpectedVersion, exact replay, fingerprint conflict and no unrelated child mutation.

## 5. T226–T229 source/compile evidence

All four provider-neutral acceptance sources exist, are linked into the Unit project and compile in
Debug and Release. They cover configuration races, Simulator crash recovery, Latest/Health races
and Audit/idempotency delivery. PostgreSQL E2E execution: **BLOCKED / NOT_RUN**.

## 6. T230 timed-harness source evidence

The deterministic SC-001 and SC-002 step sequences compile. SC-001 begins before root Site creation
and stops at an operational hierarchy. SC-002 begins at successful Point activation and stops at
the first Accepted Measurement visible through Latest/API/UI. Execution and elapsed-time capture:
**NOT_EXECUTED / BLOCKED**.

## 7. T231 static review

`0012_r1_idempotent_seeds.sql` uses fixed identifiers, idempotent conflict handling and fail-closed
meaning validation. Its five insert targets are limited to role, capability, Metric, Unit and
Metric/Unit compatibility. It creates no user, credential/hash, session/token, root Site, pre-Site
scope or operational/Audit evidence. Result: **PASS (source/static only)**.

## 8. T232 static review

`0013_r1_validation_reconciliation.sql` opens a read-only transaction and contains zero write
statements. It checks owner/Data Owner state, command registry, Telemetry terminal/raw/Latest,
Source Health, outbox/inbox, Operations jobs, Audit, Published-without-Audit, logical-reference
orphans and ordered schema-signature evidence. Result: **PASS (source/static only)**.

## 9. T233 blocker

**BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE**. Approved PostgreSQL adapters and host registration do not
exist, so ordered clean/N-1 migration execution was not attempted. Database availability is not
the blocker.

## 10. T234 blocker

**BLOCKED_BY_MISSING_TOOL**. `psql` is unavailable; no fake quickstart or database PASS was created.

## 11. T235 exact timing state

**BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE / NOT_EXECUTED**. SC-001 elapsed: **not recorded**. SC-002
elapsed: **not recorded**. No threshold result was invented.

## 12. T236 blocker

**BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE**. T226–T229 were not executed against PostgreSQL or a
substitute database.

## 13. T237 result

`tests/Verification/observability.tests.ps1`: **checks 12, failures 0, PASS**. Evidence covers
correlation/causation, exact replay identity, redaction, safe errors, server identity authority,
sensitive payload exclusion and no false Published/Completed evidence.

## 14. Functional-requirement traceability

**68/68 unique FR mappings**, duplicates 0, missing 0, malformed rows 0.

## 15. User-story traceability

**5/5** stories mapped.

## 16. Success-criterion traceability

**9/9** criteria mapped. SC-001 and SC-002 remain blocked; SC-006 live timing also remains a release
blocker.

## 17. Unresolved review findings

Unresolved Critical: **0**. Unresolved High: **0**. Runnable acceptance failures: **0**.

## 18. Fast command and exit

Exact command: `.\scripts\harness.ps1 -Mode Fast -Feature 002-asset-simulator-latest`  
Actual exit: **0**  
Summary: **PASS=8, FAIL=0**.

## 19. Full command, exit and blockers

Exact command: `& .\scripts\harness.ps1 -Mode Full -Feature 002-asset-simulator-latest`  
Actual exit: **20**  
Summary: **PASS=10, BLOCKED_BY_MISSING_TOOL=1, BLOCKED_BY_COMPANY_APPROVAL=2**. The direct database
probe reported missing `psql`; approved adapter/runtime prerequisites remain package-policy
blocked. Full is **not PASS**.

## 20. T242–T245 states

| Task | Classification | State |
|---|---|---|
| T242 | `BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE` | BLOCKED / unchecked |
| T243 | `BLOCKED_BY_PACKAGE_POLICY` | BLOCKED / unchecked |
| T244 | `BLOCKED_BY_MISSING_TOOL` | BLOCKED / unchecked |
| T245 | `BLOCKED_BY_COMPANY_APPROVAL` | BLOCKED / unchecked |

## 21. Final architecture/policy/observability

- architecture: **PASS**, exit 0
- repository policy: **PASS**, exit 0
- observability: **PASS**, 12 checks / 0 failures
- `git diff --check`: **PASS**, exit 0 after correcting two trailing-space findings

## 22. Task-by-task T224–T247 ledger

| Task | State | Evidence/classification |
|---|---|---|
| T224 | PASS | 11/11 authorization assertions |
| T225 | PASS | 14/14 lifecycle assertions |
| T226 | PASS | provider-neutral source/compile |
| T227 | PASS | provider-neutral source/compile |
| T228 | PASS | provider-neutral source/compile |
| T229 | PASS | provider-neutral source/compile |
| T230 | PASS | deterministic timed-harness source/compile; timing not executed |
| T231 | PASS | migration 0012 static review |
| T232 | PASS | migration 0013 read-only static review |
| T233 | BLOCKED | `BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE` |
| T234 | BLOCKED | `BLOCKED_BY_MISSING_TOOL` |
| T235 | BLOCKED | `BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE`; no elapsed time |
| T236 | BLOCKED | `BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE` |
| T237 | PASS | observability 12/12 |
| T238 | PASS | 68/68 FR mappings |
| T239 | PASS | 5/5 stories and 9/9 criteria |
| T240 | PASS | unresolved Critical 0 / High 0 |
| T241 | PASS | Fast exit 0 |
| T242 | BLOCKED | `BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE` |
| T243 | BLOCKED | `BLOCKED_BY_PACKAGE_POLICY` |
| T244 | BLOCKED | `BLOCKED_BY_MISSING_TOOL` |
| T245 | BLOCKED | `BLOCKED_BY_COMPANY_APPROVAL` |
| T246 | PASS | 19 final checks; all PASS after whitespace correction |
| T247 | PASS | this complete checkpoint and explicit stop |

## 23. Totals

- PASS: **16**
- BLOCKED: **8**
- FAIL: **0**
- runnable NOT_RUN: **0**

## 24. Database capability

PostgreSQL 18 at the approved `127.0.0.1:5433/iump_dev` target: **AVAILABLE**.

## 25. Database mutation

**NOT_RUN**. Migrations 0001–0013 were not executed.

## 26. Prohibited port

Port `5432` contacted: **NO**.

## 27. Browser source/build state

**YES**. Web lint exit 0 (three non-failing fast-refresh warnings) and Web build exit 0.

## 28. Live runtime state

Live registered API/Worker/Web runtime: **NO**.

## 29. PostgreSQL E2E state

**NO / BLOCKED**. Source/compile evidence is not called PostgreSQL E2E.

## 30. Timed SC-001/SC-002 state

**BLOCKED / NOT_EXECUTED**. No start/end/elapsed values exist.

## 31. Feature completion decision

Provider-neutral Phase 10 and the canonical roadmap through T247: **COMPLETE / YES**. Mandatory
runtime, PostgreSQL E2E, timed and approval evidence remains blocked.

## 32. Release readiness

**NO**. The non-passing Full probe and all mandatory blockers prevent release.

## 33. Explicit stop

**STOP AT T247.** No Phase 11, T248+, database execution, package acquisition, public CI, container
or release action is authorized or performed.
