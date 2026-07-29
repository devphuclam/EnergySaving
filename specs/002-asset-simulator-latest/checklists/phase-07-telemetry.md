# Phase 7 Canonical Telemetry Checkpoint — Exact-Result Closure

## 1. Gate identity

- Baseline: `b6b2510820f5ab8f0af5569a2fc18b4ee4b2f892`
- Feature: `002-asset-simulator-latest`
- Stop task: `T151`
- Constitution: `1.1.0`
- T146/T147/T148 remain explicitly package-policy blocked; no Phase 8, Latest, Source Health,
  durable jobs, Audit/API/Web, runtime registration, PostgreSQL adapter, migration execution, or
  port `5432` work was performed.

## 2. Exact-result corrections

- Canonical ingestion is an explicit `DispatchCanonicalAsync` contract; no default legacy bridge
  can fabricate completion, correlation, lineage, or persistence metadata.
- Accepted, Rejected, and Duplicate canonical results are payload-aware and fail closed with
  `CANONICAL_ORIGINAL_RESULT_INVALID`; Accepted IDs/quality/reason/latest and Rejected null
  quality/reason/latest are enforced.
- `TelemetryDispatchResult.LatestAdvanced` and the Acquisition attempt field are nullable.
- Completion must be non-null UTC and is persisted exactly; no local-clock fallback exists.
- Repository finalization stores and replays every terminal field, including provenance and
  `CompletedAtUtc`; `GetAsync` round-trip and per-field conflict evidence are in T134.
- Provider snapshots carry exact hierarchy/catalog IDs, versions, statuses, compatibility, and
  effective dates. Recheck returns independent fact comparisons, not a generic boolean.
- Race winners are complete terminal/raw/latest/event fixtures. Rejected fixtures carry no raw,
  Latest, or accepted event and no timestamp/value/unit is synthesized.
- 0007 source enforces Pending null terminal metadata, exact terminal shapes, persisted ID equality,
  quality/reason rules, provenance, and Completed-terminal immutability.

## 3. RED evidence

- Temporary native worktree at the b6 baseline:
  `C:\Users\TD-999\AppData\Local\Temp\iump-phase7-exact-red-8108d367ace44e3bbff46835a6bbe42b`.
- Test/static-only RED build: exit `0`.
- Focused RED run: exit `1`, exactly 12 natural A–N assertions.
- Worktree was removed after capture. No restore/download, database connection/mutation, migration
  execution, Docker, secret output, or source sabotage occurred.

## 4. GREEN and contract evidence

Focused provider-neutral run (`dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj -c Debug --no-build --no-restore`):

| Task | Cases / scenarios | Checks / assertions | Result |
|---|---:|---:|---|
| T131 | 16 | 52 | PASS |
| T132 | 15 | 217 | PASS |
| T133 | 9 | 72 | PASS |
| T134 | 22 | 96 | PASS |
| T135 | 8 | 25 | PASS |
| T145 | 21 | 58 | PASS |
| T149 | — | 52 | PASS |
| T150 | — | 16 | PASS |

Previous phase regressions T079/T080/T094/T095/T096/T103/T108–T113 and repository contracts
T071/T088/T124 also pass in the same run. Debug and Release solution builds are zero-warning,
zero-error when recorded by the harness.

The final exact-result edits add T149 checks for the explicit canonical fixture, all provider
recheck facts, the malformed canonical matrix, and provider-neutral replay/race fixture seams;
the current T149 count is `52`. T150 is `16` review checks. T134 is `22` cases / `96` checks and
T145 is `21` scenarios / `58` assertions.

## 5. Harness classification

Fresh Full evidence (command run 2026-07-29):

`scripts/harness.ps1 -Mode Full -Feature 002-asset-simulator-latest` -> exit `20`.

| Status | Count | Classification |
|---|---:|---|
| PASS | 10 | `RUNNABLE_NOW` |
| BLOCKED | 3 | approved PostgreSQL `psql` tool, company CI runner approval, company container approval |
| FAIL | 0 | — |
| NOT_RUN | 0 | — |

The fresh Full machine result is PASS `10`, BLOCKED `3`, FAIL `0`, NOT_RUN `0`; the task ledger
remains PASS `18` / BLOCKED `3` because T131-T145 and T149-T151 are runnable task evidence while
T146-T148 are the three package-policy blockers.

The database capability is available at the approved runtime target `127.0.0.1:5433/iump_dev`,
but this Phase 7 closure does not execute migrations or the package-policy-blocked adapter. Any
runtime failure after the verified capability is classified `DATABASE_CONNECTION_RUNTIME_FAILURE`
with secrets redacted, never as a missing database.

## 6. T151 checkpoint decision

- Standards/Spec review: `PASS`; unresolved Critical/High: `0`.
- Phase 7 runnable provider-neutral work: `PASS`.
- Package-policy transitive migration/adapter work: `BLOCKED` and preserved.
- Ready to begin Phase 8: `YES` (only after the next explicit `/speckit.implement` invocation).
- Release-ready: `NO`; mandatory environment/package blockers remain.
- Stop: `T151`; do not execute T152+ in this invocation.

## Historical pre-correction checkpoint (retained)

The previous Phase 7 checkpoint was recorded at baseline
`fdc56735dbd6c9c44599fdf498b010bab151f11e`. It listed the original T131-T145 provider-neutral
results, T146-T148 package-policy blockers, T149 architecture verification, and T150 review,
with PostgreSQL migration execution explicitly not run and port `5432` untouched. Its Full
harness evidence and release decision remain historical. The exact-result RED, corrective review,
and fresh Full evidence in this document are additive closure evidence against baseline
`b6b2510820f5ab8f0af5569a2fc18b4ee4b2f892`; they do not rewrite the earlier checkpoint.
