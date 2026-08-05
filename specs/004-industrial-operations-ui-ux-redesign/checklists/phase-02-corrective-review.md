# Feature 004 Phase 2 Corrective Remediation Review

## Baseline and scope

- Starting main: `24265cd0252be94032f790655edfcf21f4776eee`
- Corrective branch: `fix/004-phase-02-evidence-corrections`
- Corrective scope: P2-C01–P2-C06, T028–T036 only.
- T037–T071 and all Phase 3 work: **NOT EXECUTED**.

The first Phase 2 implementation commit `24265cd0252be94032f790655edfcf21f4776eee` remains historical truth. This document supersedes its Phase 2 semantic and governance review; it does not pretend the former task ledger or checkpoint never existed.

## Findings

| Finding | Severity | Root cause | Correction | Evidence | Final status |
|---|---|---|---|---|---|
| P2-C01 chart quality trust | High | Optional/unknown quality was allowed to plot and defaulted textually toward Good. | Added `isPlottableEvidencePoint`, fail-closed quality normalization, numeric+Missing gaps, non-finite gaps, bounded constant domains, reason-preserving table/title semantics. | `ChartContainer.tsx`, `chart-container.test.tsx` | CLOSED |
| P2-C02 dashboard health/freshness | High | Dashboard collapsed every non-Online health state and all-Online freshness to Unavailable. | Added deterministic `dashboardHealthPresentation` and aggregate precedence: Degraded > Stale > Live; unknown/empty remains Unavailable. Joined point code/description and separated contract availability text from quality reason. | `OperationalDashboard.tsx`, `dashboard-telemetry-red-evidence.test.tsx` | CLOSED |
| P2-C03 telemetry state semantics | High | NotConfigured shared NoData; conflicts/options/expired were generic errors; interval suffix was unconditional. | Added `classifyTelemetryState`, explicit NotConfigured/NoData/Conflict/Expired/Blocked/Forbidden/Runtime/Retryable-Stale presentations, option-state mapping and safe interval formatter. | `PointCurrentRoute.tsx`, `webGateways.ts`, `dashboard-telemetry-states.test.tsx` | CLOSED |
| P2-C04 task/governance deadlock | High | Former T029/T030→T031 forward edges were called intentional and Phase 3 was gated on unauthorized backend work. | Normalized meanings/dependencies to T028 red → T029 chart → T030 dashboard/T031 telemetry → T032 states; retained historical first-run record and made external contract gaps deferred. | `tasks.md`, `implementation-file-map.md`, superseding checkpoint | CLOSED |
| P2-C05 ARIA/component identity | Medium | PageHeader h1 IDs were absent and chart IDs were title-derived; SVG lacked responsive width. | Added `titleId` to PageHeader, route IDs `dashboard-title`/`telemetry-title`, React `useId`, and `width="100%"`/responsive CSS. | `PageHeader.tsx`, route owners, `ChartContainer.tsx`, chart source checks | CLOSED |
| P2-C06 evidence adequacy | Medium | Previous tests mostly checked imports and pure helper availability without state decisions. | Added source-visible decision checks for health precedence, identity, state classifier, quality fail-closed behavior, gaps, non-finite values, constant series, IDs and formatting. Runtime claims remain blocked/not-run. | three Phase 2 test sources; verification below | CLOSED_WITH_LIMITATION |

## Task graph

| Metric | Result |
|---|---|
| Task count | 71 |
| Sequential IDs | PASS: T001–T071 |
| Missing IDs | 0 |
| Duplicate IDs | 0 |
| Unknown dependencies | 0 |
| Forward dependencies | 0 |
| Dependency cycles | 0 |
| Phase 2 order | `T028 → T029 → T030/T031 → T032 → T033 → T034/T035 → T036` |

T029 is now the chart foundation and T030/T031 are its consumers. The former labels are preserved only in the historical Phase 2 verification/checkpoint narrative.

## UI UX Pro Max supporting evidence

Corrective invocation used the bundled runtime only:

```powershell
$py='C:\Users\TD-999\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
& $py .agents\skills\ui-ux-pro-max\scripts\search.py "evidence-first industrial dashboard chart missing quality fail closed source health stale degraded accessible responsive" --domain ux -n 12
& $py .agents\skills\ui-ux-pro-max\scripts\search.py "SVG chart missing gaps accessible text alternative unique IDs responsive" --domain chart -n 12
```

Applied: visible keyboard/ARIA semantics, responsive-width treatment, no horizontal chart overflow, table alternative, shape/text cues and reduced-motion-safe custom SVG. Rejected: mobile-first guidance where it conflicts with DOC-08, external chart libraries, fonts, icons, animations and package additions. Spec Kit and DOC-08 remain authoritative.

## Dashboard semantics

| Source status | Operational state | Freshness |
|---|---|---|
| Online | Good | Live |
| Stale | Stale | Stale |
| NoData | Missing | Degraded |
| Suspended | Blocked | Degraded |
| Decommissioned | Unavailable | Degraded |
| Unknown/absent | Unavailable | Unavailable |

Aggregate precedence is deterministic: empty health → Unavailable; any explicit degraded state → Degraded; otherwise any Stale → Stale; all known Online → Live; unknown values never produce Live.

## Telemetry semantics

| Input state | Presentation | Recovery |
|---|---|---|
| NoSelection | Dedicated empty selection state with hierarchy context | Select hierarchy/point |
| NotConfigured | Dedicated configuration-missing state | Reload/reselect only |
| NoData | Missing/No Data evidence plus source-health card | Refresh/retry when valid |
| Data zero | Numeric `0` with unit and quality | None; inspect evidence |
| Conflict/Ambiguous/HierarchyConflict | ConflictState, selected hierarchy retained | Reload/reselect |
| Forbidden/not-found | Safe forbidden/not-found state | Permitted navigation/retry |
| Dependency | BlockedState | Retry |
| Expired | Explicit expired-session feedback | Sign in again through AppShell |
| Runtime error | ErrorState | Retry |

## Chart trust

- Unknown or absent quality fails closed to Missing/Unavailable.
- Quality Missing, null, NaN and Infinity create gaps; numeric zero with Good remains plottable.
- Constant series receive a bounded domain around the value.
- Quality reason is shown only when authoritative; no fabricated reason is added.
- Chart IDs use React `useId`, not title text; SVG is responsive with preserved viewBox/aspect ratio.
- Text/table alternative remains available.

## Verification

| Evidence | Result | Detail |
|---|---|---|
| `npm run lint` | PASS | Existing non-blocking Fast Refresh warnings only |
| `npm run build` | PASS | TypeScript/Vite production build completed |
| Fast harness | PASS=11 | `scripts/harness.ps1 -Mode Fast -Feature 004-industrial-operations-ui-ux-redesign`; failures=0 |
| Full harness | BLOCKED_BY_COMPANY_APPROVAL | PASS=14; CI/deployment approval checks blocked (2) |
| Source-visible corrective checks | TYPE-CHECKED | No runtime test runner is installed/authorized |
| Runtime frontend | BLOCKED_BY_PACKAGE_POLICY | No approved executor available |
| Browser/visual | NOT_RUN | Source inspection is not visual PASS |
| Accessibility automation | BLOCKED_BY_PACKAGE_POLICY | No package installation allowed |

## Contract limitations

| Limitation | Status | Blocks Phase 3 | Blocks release/full requirement |
|---|---|---|---|
| Coverage | DEFERRED_EXTERNAL_CONTRACT_LIMITATION | NO | YES |
| Cutoff | DEFERRED_EXTERNAL_CONTRACT_LIMITATION | NO | YES |
| Dashboard source timestamp/reason | DEFERRED_EXTERNAL_CONTRACT_LIMITATION | NO | YES |
| Historical series/gaps | DEFERRED_EXTERNAL_CONTRACT_LIMITATION | NO | YES |

These are not implementation PASS and do not authorize backend/API/database work. FR-022 and the migration phase contract keep Phase 3 Configuration Management independent.

## Expected readiness after verification

- During remediation, progression to Phase 3 is **NO** while P2-C01–P2-C05 remain open.
- Critical findings: 0.
- High findings: 0 after this corrective work passes verification.
- Phase-2-complete: YES.
- Progression to Phase 3: YES when Critical/High remain zero.
- Full Feature 004 completion: NO.
- Release-ready: NO.
- T037 executed: NO.
- Next command: `/speckit.implement — Phase 3 only`.

## Superseding post-merge corrective round 2

Post-merge review reopened P2-R2-01 through P2-R2-04 from the authoritative main baseline
`9b5b56926844398c002674e318a13781ade7cda1`. The production correction is `c219f45`
(`fix(feature-004): close remaining phase two state gaps`) on
`fix/004-phase-02-corrective-round-2`. The detailed finding table and actual evidence are in
[phase-02-corrective-review-round-2.md](phase-02-corrective-review-round-2.md).

Round-2 result: **Critical 0 / High 0 / Medium 0**. The Dashboard exception pipeline now classifies
all authorized records before a presentation cap, and Telemetry retains only legitimate current
evidence during retryable failures. Expired responses have a direct session-recovery action and
stop selected refresh. T028-T036 remain complete; T037-T071 remain pending. This section is the
actual final decision for the round and supersedes the earlier expected-readiness wording.
