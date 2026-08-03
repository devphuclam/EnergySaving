# Feature 003 Phase 6 — final verification (T076, T077, T079)

Date: 2026-08-03
Baseline: `045f3981f3ba6bb87425009ee8f8cf0e6cf4e56a`
Corrective branch: `fix/003-final-governance-corrective` (historical implementation branch:
`003-operational-configuration-workspace`)
Database target: PostgreSQL `127.0.0.1:5433/iump_dev` only; password was read from the approved
local environment/configuration path and never printed or persisted.

## T076 — runnable acceptance journeys

| Command | Exit | Classification | Evidence |
|---|---:|---|---|
| `dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-restore` | 0 | PASS / RUNNABLE_NOW | All registered Unit suites; `PASS: all tests`; Feature 003 seams include T065 `5 cases/21 assertions/0 failures`, T037 `15/61/0`, T049 `4/14/0`, T057 `1/17/0`, T079 `106/0`, T080 `62/0`, T108 `13/19/0`, T109 `12/12/0`, T110 `66/192/0`, T181 `12/12/0`. |
| `dotnet run --project .\tests\Integration\IUMP.Tests.Integration.csproj --no-restore` | 0 | PASS / RUNNABLE_NOW | `T066 target=127.0.0.1:5433/iump_dev cases=14; assertions=15; failures=0`; T038 `9 cases/41 assertions/0`; T050 `1/34/0`; T058 `13/19/0`; `postgres-integration ... suites=15 failures=0`. |

No migration, seed, or cleanup mutation was required by T076. No command targeted port 5432. No
SQLite, InMemory, Docker, Testcontainers, or public package/download substitute was used.

## T077 — frontend behavior capability

| Check | Status | Classification | Evidence |
|---|---|---|---|
| Approved frontend behavior suite | BLOCKED | `BLOCKED_BY_PACKAGE_POLICY` | No approved frontend behavior runner is installed in the existing workspace dependencies. The task explicitly forbids installing/downloading a runner; no false PASS is claimed. |
| Authenticated browser runner for a fresh Phase 6 journey | BLOCKED | `BLOCKED_BY_MISSING_TOOL` | No approved authenticated automation runner is available in this runtime and no credential was recorded. Historical Chrome journeys remain cited in phase checkpoints only. |

## T079 — exact final verification commands

| Command | Exit | Classification | Evidence |
|---|---:|---|---|
| `dotnet build .\IUMP.slnx --no-restore` | 0 | PASS / RUNNABLE_NOW | Build succeeded; 0 warnings, 0 errors. |
| `dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-restore` | 0 | PASS / RUNNABLE_NOW | See T076 table. |
| `dotnet run --project .\tests\Integration\IUMP.Tests.Integration.csproj --no-restore` | 0 | PASS / RUNNABLE_NOW | See T076 table; approved PostgreSQL target. |
| `npm run lint` from `src/Web` | 0 | PASS / RUNNABLE_NOW | Oxlint exits 0; only pre-existing Fast Refresh warnings. |
| `npm run build` from `src/Web` | 0 | PASS / RUNNABLE_NOW | `tsc -b && vite build` exits 0. |
| `.\tests\Verification\architecture.tests.ps1` | 0 | PASS / RUNNABLE_NOW | `PASS: architecture boundary contract`. |
| `.\tests\Verification\repository-policy.tests.ps1` | 0 | PASS / RUNNABLE_NOW | `PASS: repository policy contract`. |
| `.\tests\Verification\observability.tests.ps1` | 0 | PASS / RUNNABLE_NOW | `checks=12 failures=0`. |
| `.\scripts\harness.ps1 -Mode Fast -Feature 003-operational-configuration-workspace` | 0 | PASS / RUNNABLE_NOW | `Harness Fast summary: PASS=12` (includes AppShell accessibility and deployment contract regressions). |
| `.\scripts\harness.ps1 -Mode Full -Feature 003-operational-configuration-workspace` | 20 | BLOCKED / `BLOCKED_BY_COMPANY_APPROVAL` | Fresh Full summary: `PASS=15`, `BLOCKED_BY_COMPANY_APPROVAL=2`; `BLK-ENV-003` (company CI runner) and `BLK-ENV-005` (approved non-containerized target host/service); no mandatory FAIL. `BLK-ENV-004` is not emitted. |

| `.\tests\Verification\app-shell-accessibility.tests.ps1` | 0 | PASS / RUNNABLE_NOW | Static AppShell contract covers visible Vietnamese labels, stable ids, invalid-credential `aria-describedby`/alert association, and Vietnamese navigation/auth region names. Browser behavior is not exercised. |

The frontend lint/build portion of Full is PASS; its warnings are non-fatal existing Fast Refresh
warnings. Full is not PASS because mandatory company-approval checks are blocked. Exit code `20`
therefore means no mandatory FAIL but at least one blocked/NOT_RUN check, per
`docs/repository-harness.md`; it is not a release approval.

## Current verification totals

- Runnable test/policy/build checks: PASS, 0 FAIL.
- Capability blockers: 2 company-approval checks in Full (`BLK-ENV-003`, `BLK-ENV-005`); frontend behavior runner blocked by package
  policy; authenticated browser runner blocked by missing approved tool.
- Runnable NOT_RUN: none for the listed backend/policy commands. Browser capability is BLOCKED, not
  silently counted as PASS.

## Corrective closure evidence

- AC-005: `PARTIAL`. No approved authenticated browser/process-control runner exists for the exact
  API/Web stop-restart journey; historical refresh/logout-login evidence is retained but not
  promoted to PASS.
- Historical Phase 5 corrective task registration: `RETROSPECTIVE`.
- Historical accessibility RED evidence: `NOT_AVAILABLE`.
- Post-merge accessibility regression: `PASS` for the static source-contract seam; this is not
  browser behavior or historical TDD evidence.
- SpecKit `analyze` and `converge`: `NOT_RUN; reason=SpecKit provider command unavailable in this
runtime`; direct artifact comparison is used and is not represented as provider PASS.

## Final documentation and deployment-gate corrective closure

This verification is refreshed on branch `fix/003-doc05-deployment-gate` from baseline
`6dbfaf3bcbc95f2d262ddeacf174232d9d746bd7`. The new deployment-target contract test passes its
blocked, malformed/unsafe, valid/pass, redaction, Fast/Full-plan, and exit-code cases. The Full
environment check remains fail-closed: without approved sanitized evidence it is
`BLOCKED_BY_COMPANY_APPROVAL` with `BLK-ENV-005`; a blocked Full result is not PASS. The approved
manifest branch was exercised only with a temporary sanitized fixture and is contract evidence, not
company release approval. AC-005 and
AC-011 remain PARTIAL, acceptance evidence is incomplete, and Release-ready remains NO.

TDD evidence for the new seam is explicit: the pre-implementation run stopped with
`RED: deployment-target verifier is missing`; after the verifier was added, the same contract suite
passed 25 checks. The historical RED was not reconstructed or inferred.
