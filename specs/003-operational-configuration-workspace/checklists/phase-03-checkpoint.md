# Phase 3 Stop Checkpoint

Date: 2026-08-01
Feature: `003-operational-configuration-workspace`
Corrective scope: final Phase 3 closure (`T054`-`T056` only)
Authoritative baseline: `2741429fb1a28d403adde69e36810bab16d12af5`

## Scope and ledger

This final corrective run started from the merged `main` baseline above and reopened only the
Web selector/retry/error-state tasks. `T057` and every later task were not started. No new task
IDs were created, Feature 002 was not modified, and no alternate database or port was used.

| Disposition | Count | Tasks / evidence |
|---|---:|---|
| PASS | 8 | T049-T056 |
| FAIL / incomplete | 0 | — |
| Runnable NOT_RUN | 0 | Fresh backend, Web, verification, Fast/Full checks completed; hosted/browser evidence remains valid |
| Capability blocked | 3 | Frontend behavior runner (`BLOCKED_BY_PACKAGE_POLICY`); Full CI (`BLK-ENV-003`) and container target (`BLK-ENV-004`) (`BLOCKED_BY_COMPANY_APPROVAL`) |

## Final corrective evidence (T054-T056)

- Dependency-versus-runtime contract was added to the existing Phase 3 closure verification and
  Unit contract. The new red check failed before the helper/gateway/UI correction because the
  dependency mapping and distinct messages were absent.
- The malformed-response red check also failed before the final transport correction. Malformed
  non-authenticated Simulator payloads now remain `runtime-error`; an unknown mutation outcome
  retains the pending identity for same-key retry.
- `RUNTIME_DEPENDENCY_UNAVAILABLE`, established dependency codes, and authenticated HTTP 503 are
  classified as `dependency`; TypeError, proxy/network failures, malformed responses, and other
  unclassified failures remain `runtime-error`.
- Dependency and unknown-outcome mutation failures remain retryable. The existing pending
  operation/complete selection/Run/version/Idempotency-Key identity is retained; selection
  changes and confirmed non-retryable responses clear it. Post-mutation refresh uses the same
  dependency/runtime mapping.
- The Simulator UI now shows distinct dependency and runtime messages and exposes workspace and
  operation Retry actions for both states. No automatic Start was introduced.

## Existing Phase 3 behavior retained

- Exactly four selected workspace mutation routes remain; legacy Source-only and Run-only routes
  fail closed. Antiforgery and `Idempotency-Key` requirements remain enforced.
- Selected Start carries Site/Area/Asset/Source/Configuration/version through the owner seam and
  transactionally rechecks exact active eligibility, mapping, points, scope, and concurrency.
- URL-backed selection and server-authoritative Run/history reconstruction remain in place across
  refresh and logout/login. The authenticated browser journey previously completed with console
  error count `0`, no auto-selection, and no auto-start.

## Verification

| Check | Result | Evidence |
|---|---:|---|
| Solution/backend build | PASS | `dotnet build .\\IUMP.slnx --no-restore`; 0 warnings / 0 errors |
| Unit | PASS | T049 `cases=4; assertions=14; failures=0`; T110 `cases=66; checks=192; failures=0` |
| PostgreSQL integration | PASS | T038 + T050; 15 suites / 0 failures; `127.0.0.1:5433/iump_dev` |
| Web lint | PASS | `npm run lint`; only pre-existing Fast Refresh warnings |
| Web build | PASS | `npm run build` (`tsc -b && vite build`) |
| Architecture / policy / observability | PASS | All three verification scripts exit 0 |
| Phase 3 closure contract | PASS | `simulator-phase3-closure: failures=0` |
| Fast harness | PASS | `PASS=9` |
| Full harness | BLOCKED | Exit code `20` per the authoritative verification contract: `PASS=12`, `FAIL=0`, `BLOCKED=2`, `NOT_RUN=0`; `ci`=`BLOCKED_BY_COMPANY_APPROVAL` (`BLK-ENV-003`), `container-target`=`BLOCKED_BY_COMPANY_APPROVAL` (`BLK-ENV-004`) |
| Frontend behavior runner | BLOCKED | `BLOCKED_BY_PACKAGE_POLICY`; no runner/package installed |
| Hosted HTTP matrix | PASS | Prior real API/DB matrix: failures `0`, exact pin/replay/conflict/history/audit/logout coverage |
| Authenticated browser journey | PASS | Prior Chrome journey: dependency/retry path, refresh/logout-login, history, no auto-start, console errors `0` |

The Full harness exit code is intentionally `20` for mandatory blocked checks; exit code `1` is
reserved for a mandatory `FAIL`. No SQL mutation, Docker, package download, port `5432`, or
secret output was used.

### Full harness check classifications

| Check ID | Classification |
|---|---|
| `feature-artifacts` | PASS |
| `verification-contract` | PASS |
| `repository-harness` | PASS |
| `repository-policy` | PASS |
| `repository-scope` | PASS |
| `architecture` | PASS |
| `architecture-red-fixture` | PASS |
| `simulator-phase3-closure` | PASS |
| `unit` | PASS |
| `backend-build` | PASS |
| `frontend` | PASS |
| `database` | PASS |
| `ci` | BLOCKED_BY_COMPANY_APPROVAL (`BLK-ENV-003`) |
| `container-target` | BLOCKED_BY_COMPANY_APPROVAL (`BLK-ENV-004`) |

## Review and stop gate

Fresh Standards and Specification reviews against this corrective diff completed with
**C0 / H0 / M0 / L0** on both axes. Standards found no documented violation or actionable smell;
Spec found no missing Phase 3 requirement or scope creep. This phase is accepted only for the runnable
provider-neutral, PostgreSQL, hosted HTTP, and authenticated browser paths: **YES**. Release-ready:
**NO** while the approved Full environments and frontend behavior runner remain unavailable, and
because later feature phases are intentionally out of scope.

Stop here. Do not begin `T057` or Phase 4.
