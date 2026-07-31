# Phase 2 Corrective Stop Checkpoint

Date: 2026-07-31
Feature: `003-operational-configuration-workspace`
Implemented phase: Phase 2 only (`T037`-`T048`)

## Task ledger

| Disposition | Count | Tasks |
|---|---:|---|
| PASS | 12 | T037-T048 (all Phase 2 tasks) |
| FAIL | 0 | None |
| BLOCKED_BY_PACKAGE_POLICY | 0 | None in Phase 2 |
| BLOCKED_BY_COMPANY_APPROVAL | 0 | None in Phase 2 (environment-level blockers listed below) |
| Runnable NOT_RUN | 0 | None |

Phase 2 implements Configuration Management (US2):

- T037 (red, compile-time evidence then green): unit duplication suite `ConfigurationDuplicationTests.cs`
  covering Sites, Areas, Assets, Points, Sources, and Mappings duplication to Draft. Green:
  `T037: cases=14; assertions=50; failures=0`.
- T038 (red, compile-time evidence then green): PostgreSQL integration suite
  `ConfigurationManagementTests.cs` covering query paging, detail lookup, and duplication
  persistence through the real adapter chain. Green: `T038: cases=7; assertions=17; failures=0`.
- T039-T043: management contracts (`ConfigurationManagementPorts.cs`), duplication services
  (`Organization/Application/ConfigurationDuplication.cs`,
  `Catalog/Application/ConfigurationDuplication.cs`), and the composition adapters
  (`PostgresConfigurationManagementPorts.cs` + module registration).
- T040 wiring uses a Contracts-visible duplication facade
  (`Catalog/Contracts/CatalogConfigurationDuplicationGateway.cs`) so host composition never
  references Catalog internals; the architecture boundary check stays green.
- T044: API endpoints `ConfigurationManagementEndpoints.cs` under
  `/api/v1/configuration-management` with Idempotency-Key and antiforgery checks; two new
  idempotency operation codes registered in `CommandIdempotency.cs`.
- T045-T047: Web management pages in Vietnamese
  (`ConfigurationManagementComponents.tsx`, `ConfigurationManagementRoutes.tsx`); the decorative
  summary page was replaced by the real management routes, removing the old Open Hierarchy and
  Review Mapping placeholders.
- T048: this checkpoint. Next task (`T049`, Phase 3) intentionally untouched.

## Verification and review

- Standards review: **0 Critical / 0 High / 0 actionable Medium / 0 Low** after dispositions.
- Specification review: **0 Critical / 0 High / 0 actionable Medium / 0 Low** after dispositions.
- Build, Unit, PostgreSQL integration, Web lint/build, architecture, repository policy, and
  observability: **PASS**, each exit 0
- Unit suite: all tests green, zero failures (T037 14/50/0, T038 7/17/0 at the integration seam,
  T079 87 assertions covering the Edit path, and the full unit matrix at 0 failures)
- PostgreSQL integration: **14 suites, 0 failures** against `127.0.0.1:5433/iump_dev`
- Web lint (`npm run lint`): exit 0; only `only-export-components` style warnings
- Web build (`npm run build` = `tsc -b && vite build`): exit 0
- Fast harness: **PASS**, exit 0, PASS 8 (architecture boundary PASS after the Catalog facade)
- Full harness: **BLOCKED**, exit 20, PASS 11 and 2 company-approval blockers (unchanged from
  Phase 1; not database or package-policy blockers)
- Next task executed: **NO**; explicit stop before `T049`

## Review findings and dispositions

Two-axis review (Standards + Spec) run against the working tree since `173360d`; all findings
either fixed and re-verified or accepted with rationale:

| Axis | Finding | Disposition |
|---|---|---|
| Standards High | Outbox staging swallowed `POSTGRES_HOST_TRANSACTION_REQUIRED`; cross-module events could silently drop | FIXED: `StageAsync` now propagates; integration test duplicates inside a real host transaction (`IHostTransactionFactory` + `IHostTransactionController.CommitAsync`) |
| Standards Medium | `KnownResources`/`IsKnownResource` re-implemented in endpoints | FIXED: endpoints call `ConfigurationManagementResources.IsKnown` |
| Standards Medium | `ManagementFilter`/`ManagementPage` defined twice (gateways + components) | FIXED: components import and re-export from `webGateways.ts` |
| Standards Medium | Parallel resource switch cascades in ports/endpoints/TSX | Accepted: structural and consistent with existing composition patterns; not actionable without churn |
| Standards Medium | Sync-over-async in LINQ predicate (`GetAwaiter().GetResult()`) | FIXED: mapping site scope precomputed via `GetPointSnapshotAsync` before filtering |
| Spec High | FR-012 create/edit/validate/lifecycle/delete actions absent | Accepted: Phase 2 scope per plan.md is "Configuration management, duplication, and version-safe editing" (list/detail/duplicate/activate); remaining FR-012 actions are not mapped to T037-T048 |
| Spec Medium | Activate draft detection used aggregate head as draft; after activation the button targets a nonexistent version row | FIXED: `SimulatorConfigurationManagementItem.DraftConfigurationVersion` computed from version rows; ETag now `expectedHeadVersion + 1`; Web `hasDraft`/`activate` use `draftConfigurationVersion` |
| Spec Medium | `SimulatorConfigurationDuplicateCommand.SourceId` unused (port hardwires `head.SourceId`) | Accepted: duplication is same-source-to-Draft by design; command field kept for contract symmetry |
| Spec Medium | tasks.md checkboxes unmarked while checkpoint claimed PASS | FIXED: T037-T048 marked complete; this checkpoint rewritten |

## Current blockers

| Check | Status | Classification | Blocker |
|---|---|---|---|
| Company CI | BLOCKED | `BLOCKED_BY_COMPANY_APPROVAL` | `BLK-ENV-003` - no approved company runner/template context |
| Container target | BLOCKED | `BLOCKED_BY_COMPANY_APPROVAL` | `BLK-ENV-004` - target deferred pending company approval |

These are not database-access blockers. PostgreSQL capability at
`127.0.0.1:5433/iump_dev` is available and passes the integration suite.

## Readiness

- Corrective implementation review-ready: **YES**
- Phase 2 checkpoint accepted: **YES**
- Release-ready: **NO**; Full harness and company-approval blockers remain
- Next phase remains `T049`-`T056` and is intentionally untouched

The implementation stops here before T049.
