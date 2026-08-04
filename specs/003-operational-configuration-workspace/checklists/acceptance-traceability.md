# Feature 003 Phase 6 — acceptance traceability (T073)

Date: 2026-08-03
Feature: `003-operational-configuration-workspace`
Baseline: `f93c2da8bcd71c0436c38d502ddd7a770c35e621`
Branch: `003-operational-configuration-workspace`
Corrective baseline: `045f3981f3ba6bb87425009ee8f8cf0e6cf4e56a`
Corrective branch: `fix/003-final-governance-corrective`

This matrix is an evidence index, not a claim that a source file alone proves acceptance. The
Phase 1–5 checkpoints remain the historical records for T001–T072; their wording and dates are
not rewritten here. `PASS` means that the cited historical or current evidence directly exercises
the acceptance criterion. `PARTIAL` identifies a material evidence boundary that remains open.
`BLOCKED` and `NOT_RUN` are never promoted to PASS.

| AC | Requirement summary | Related FR | Implementation files / seams | Unit evidence | PostgreSQL evidence | HTTP evidence | Browser / manual evidence | Security / scope evidence | Checkpoint | Status | Remaining gap |
|---|---|---|---|---|---|---|---|---|---|---|---|
| AC-001 | Server-derived landing routes to Wizard, Continue Setup, Dashboard, No Scope, or Dependency Error | FR-001, FR-005, FR-025 | `src/Web/src/app/AppShell.tsx`, `src/Web/src/features/setup/SetupWizard.tsx`, `src/Hosting/Abstractions/OperationalWorkspacePorts.cs`, `src/Api/OperationalWorkspaceEndpoints.cs` | AppShell transition and landing-state checks; Phase 1 green unit run | Phase 1 setup/status journey, 14 suites/0 failures | Phase 1 authenticated status/landing matrix | Administrator and Engineer landing journey; dependency/no-scope states are rendered without fallback | Server workspace status and principal scope are authoritative | `phase-01-checkpoint.md` | PASS | Fresh authenticated browser runner is unavailable for this Phase 6 run; historical journey is retained. |
| AC-002 | Administrator creates Site, assigns Engineer scope, and hands off without SQL/scripts | FR-003, FR-006, FR-008 | `src/Composition/Postgres/PostgresOperationalWorkspacePorts.cs`, `src/Web/src/features/setup/setupGateway.ts`, `src/Web/src/features/setup/SetupWizard.tsx`, `src/Api/OperationalWorkspaceEndpoints.cs` | Phase 1 setup command/HTTP seams green | T014/T033 PostgreSQL handoff journey, 14 suites/0 failures | Hosted create/activate/assignment response matrix | Fresh browser journey in Phase 1 completed handoff and Engineer login | Administrator-only command, server principal and scope checks | `phase-01-checkpoint.md` | PASS | No new gap; no credential is copied into this artifact. |
| AC-003 | Engineer resumes and completes remaining chain only in assigned scope | FR-004, FR-005, FR-010, FR-030 | `src/Web/src/features/setup/SetupWizard.tsx`, `src/Web/src/features/setup/setupGateway.ts`, `src/Composition/Postgres/PostgresOperationalWorkspacePorts.cs`, `src/Api/OperationalWorkspaceEndpoints.cs` | Phase 1 setup and scope unit seams | Phase 1 Engineer continuation and persisted resume | Hosted authorized/unauthorized setup responses | Phase 1 Engineer completed steps 2–7 after handoff | Site is read-only for Engineer; scope is filtered server-side | `phase-01-checkpoint.md` | PASS | Separate authenticated runner remains unavailable now. |
| AC-004 | Complete chain validates and activates legally without automatic Simulator start | FR-007, FR-008, FR-009, FR-017 | `src/Web/src/features/setup/SetupWizard.tsx`, `src/Composition/Postgres/PostgresOperationalWorkspacePorts.cs`, `src/Api/OperationalWorkspaceEndpoints.cs`, `src/Api/SimulatorEndpoints.cs` | Validation/order/no-auto-start unit assertions | T014 activation and zero-Run assertion; later telemetry fixtures preserve count | Hosted validation/activation and read-only Run reads | Phase 1 browser visited Simulator after activation without starting a Run | Activation is server-owned and idempotent; no client auto-start | `phase-01-checkpoint.md`, `phase-04-checkpoint.md` | PASS | None in runnable evidence; Full environment remains blocked. |
| AC-005 | Browser/host restart preserves committed wizard progress | FR-005, FR-011 | `src/Composition/Postgres/PostgresOperationalWorkspacePorts.cs`, `src/Hosting/Abstractions/OperationalWorkspacePorts.cs`, `src/Web/src/features/setup/SetupWizard.tsx`, `src/Web/src/gateways/webGateways.ts` | Resume-state checks | Persisted resume in Phase 1 PostgreSQL journey | Hosted logout/login/status reads | Phase 1 refresh and logout/login reconstructed completed steps; no fresh host-restart journey | Browser storage is not authority | `phase-01-checkpoint.md`, `post-phase-06-corrective-review.md` | PARTIAL | No approved authenticated browser/process-control runner exists in this runtime to stop/restart the API/Web hosts and verify the exact restart journey; historical refresh/logout-login evidence is not sufficient for PASS. |
| AC-006 | Management pages use real PostgreSQL-backed data and real actions | FR-012, FR-013, FR-030 | `src/Web/src/features/configuration/ConfigurationManagementRoutes.tsx`, `src/Web/src/features/configuration/ConfigurationManagementComponents.tsx`, `src/Composition/Postgres/PostgresConfigurationManagementPorts.cs`, `src/Api/ConfigurationManagementEndpoints.cs` | T037/T079 management contracts green | T038: 9 cases/41 assertions/0 failures; 15 suites/0 failures | Create/detail/edit/validation/lifecycle matrix | Phase 2 authenticated management journey | No controller SQL; owner ports and scope filters | `phase-02-checkpoint.md` | PASS | Frontend behavior runner is package-policy blocked. |
| AC-007 | Duplicate is a new Draft with no operational history or secret material | FR-014, FR-029 | `src/Web/src/features/configuration/ConfigurationManagementRoutes.tsx`, `src/Composition/Postgres/PostgresConfigurationManagementPorts.cs`, `src/Modules/Acquisition/Application/SimulatorConfiguration.cs`, `src/Modules/Acquisition/Infrastructure/PostgresConfigurationRepository.cs` | T043/T079 receipt and duplicate assertions green | T038 duplicate/retry and receipt persistence evidence | Duplicate/replay and safe response matrix | Phase 2 review/refresh/detail journey | Exclusion metadata and server redaction; no credentials/tokens copied | `phase-02-checkpoint.md` | PASS | No additional gap. |
| AC-008 | Behavior-changing edit creates Draft/versioned transition and preserves historical meaning | FR-015, FR-016 | `src/Web/src/features/configuration/ConfigurationManagementRoutes.tsx`, `src/Composition/Postgres/PostgresConfigurationManagementPorts.cs`, `src/Modules/Acquisition/Application/SimulatorConfiguration.cs`, `src/Api/ConfigurationManagementEndpoints.cs` | T043/T079 edit-invalidates-receipt assertions green | T038 edit/review/validation/activation evidence | Stale If-Match 409 and revalidation matrix | Phase 2 edit → re-review → re-validation → activation journey | Optimistic version and pinned run authority are server-side | `phase-02-checkpoint.md` | PASS | No additional gap. |
| AC-009 | Simulator uses explicit Source/configuration selection; no implicit first Source | FR-017, FR-018, FR-019 | `src/Web/src/features/simulator/SimulatorRoute.tsx`, `src/Hosting/Abstractions/SimulatorWorkspacePorts.cs`, `src/Composition/Postgres/PostgresSimulatorWorkspacePorts.cs`, `src/Api/SimulatorEndpoints.cs`, `src/Web/src/gateways/webGateways.ts` | T049/T050 and T110 selection/idempotency checks green | T050 Simulator operations and 15-suite regression, 0 failures | Explicit selection/start/pause/resume/stop matrix | Phase 3 authenticated Simulator journey; no auto-start | Selection, eligibility, scope, version, and idempotency rechecked server-side | `phase-03-checkpoint.md` | PASS | Fresh browser automation is unavailable; historical browser evidence remains valid. |
| AC-010 | Latest/Health use explicit Point selection; no implicit first Point | FR-020, FR-021 | `src/Web/src/features/telemetry/PointCurrentRoute.tsx`, `src/Web/src/features/telemetry/telemetryRefreshCoordinator.ts`, `src/Hosting/Abstractions/TelemetryWorkspacePorts.cs`, `src/Composition/Postgres/PostgresTelemetryWorkspacePorts.cs`, `src/Api/TelemetryQueryEndpoints.cs` | T058/T060 and telemetry closure assertions green | T058 13 cases/19 assertions/0 failures; 15 suites/0 failures | Selected hierarchy, mismatch 404, No Data and Health matrix | Phase 4 selected page-6 Point and rehydration journey | Complete hierarchy is authorized before paging; URL is request only | `phase-04-checkpoint.md` | PASS | None in historical acceptance; frontend runner remains blocked. |
| AC-011 | Manual eligible Start + Worker yields Accepted Measurement visible through Latest UI | FR-018, FR-021, FR-022 | `src/Web/src/features/simulator/SimulatorRoute.tsx`, `src/Composition/Postgres/PostgresSimulatorWorkspacePorts.cs`, `src/Worker/Program.cs`, `src/Web/src/features/telemetry/PointCurrentRoute.tsx`, `src/Web/src/gateways/webGateways.ts` | Unit contracts cover accepted/No Data mapping | Phase 4 fixture proves Accepted `42`, timestamps and selected Source/Run | Hosted current endpoint returns Accepted value/unit/quality/timestamps | Simulator and Latest browser journeys are recorded separately, not as one fresh combined trace | Scope/selection and no-side-effect reads are verified | `phase-03-checkpoint.md`, `phase-04-checkpoint.md` | PARTIAL | A fresh single-session authenticated browser trace joining manual Start, Worker execution, and Latest is not runnable without the approved browser runner. |
| AC-012 | Latest refreshes at interval; No Data is not zero | FR-021, FR-022, FR-025 | `src/Web/src/features/telemetry/PointCurrentRoute.tsx`, `src/Web/src/features/telemetry/telemetryRefreshCoordinator.ts` | Deferred coordinator `requests=5; events=8`; No Data contract green | No Data/accepted-zero distinction in T058 | Hosted refresh/error/No Data responses | Phase 4 auto-refresh off/on and manual refresh journey | Explicit `hasData`/null semantics; dependency error preserves safe state | `phase-04-checkpoint.md` | PASS | No additional gap. |
| AC-013 | Authorized reviewers see filtered/redacted Audit from configuration and Simulator actions | FR-024, FR-029 | `src/Web/src/features/audit/AuditRoute.tsx`, `src/Modules/Audit/Application/AuditQueryService.cs`, `src/Modules/Audit/Application/AuditEventConsumer.cs`, `src/Api/AuditEndpoints.cs`, `src/Web/src/features/dashboard/OperationalDashboard.tsx` | T065/T181 redaction/correlation assertions green | T066 14 cases/15 assertions/0 failures | Filter, keyset, redaction, correlation-permission matrix | Phase 5 authenticated Audit filter and next-page journey | Scope-before-count/page, recursive redaction, Administrator-only correlation | `phase-05-checkpoint.md` | PASS | Fresh authenticated browser runner is unavailable. |
| AC-014 | Engineer cannot access or infer out-of-scope resources | FR-004, FR-010, FR-023, FR-030 | `src/Composition/Postgres/PostgresOperationalWorkspacePorts.cs`, `src/Composition/Postgres/PostgresOperationalDashboardPorts.cs`, `src/Composition/Postgres/PostgresTelemetryWorkspacePorts.cs`, `src/Modules/Audit/Application/AuditQueryService.cs`, `src/Web/src/gateways/webGateways.ts` | Scope and no-global-count unit contracts | T058/T066 Area/site scope and zero out-of-scope rows/counts | Anonymous 401, hierarchy mismatch/out-of-scope 404, scoped dashboard matrix | Phase 4/5 scope journeys | Server principal and scope-before-paging; no client claims | `phase-04-checkpoint.md`, `phase-05-checkpoint.md` | PASS | Company Full runner still blocked; local PostgreSQL evidence is runnable. |
| AC-015 | No Docker/public download/substitute DB/5432/secret emission | FR-026, FR-029, FR-030 | `tests/Verification/repository-policy.tests.ps1`, `tests/Verification/architecture.tests.ps1`, `tests/Verification/observability.tests.ps1`, `scripts/common/PostgresRuntime.ps1`, `src/Modules/Audit/Application/AuditEventConsumer.cs` | Architecture/policy/observability checks | All integration runs target `127.0.0.1:5433/iump_dev` only | Health/readiness bodies are sanitized | Manual evidence contains no credentials | Static policy scans and redaction checks; no SQLite/InMemory/container | all phase checkpoints; `security-scope.md` | PASS | Full CI (`BLK-ENV-003`) and restricted non-containerized target-host/service (`BLK-ENV-005`) remain BLOCKED_BY_COMPANY_APPROVAL; release is not declared ready while any mandatory environment is blocked. |

## Evidence boundary

The matrix intentionally carries forward accepted historical evidence instead of rewriting old
checkpoints. T074–T080 add current Phase 6 audit and verification files. The provider-native
`speckit-analyze` command is unavailable in this runtime, so any provider result is `NOT_RUN`; the
direct artifact comparison is recorded separately and is not represented as a provider PASS.

Numeric reconciliation: the authoritative Phase 5 corrective evidence and the current Integration
rerun both report T066 as `cases=14; assertions=15; failures=0` against the approved target. Any
older conflicting count is superseded by that explicit corrective record and is not used by this
matrix.

## Final deployment-gate closure

The deployment-gate closure on `fix/003-doc05-deployment-gate` (baseline `6dbfaf3bcbc95f2d262ddeacf174232d9d746bd7`)
is historical evidence. The current final corrective branch is `fix/003-trusted-deployment-approval`
from baseline `6b77256f29775bb2a777ddcb555d868d7e671243`. The Full deployment-target seam is now
trust-bounded and fail-closed: it requires an approved company CI context
(`CI=true` plus `IUMP_COMPANY_CI_APPROVED=true`), `IUMP_DEPLOYMENT_TARGET_APPROVED=true`, a trusted
evidence root, manifest path containment, reparse-escape rejection, and SHA-256 attestation.
Missing or untrusted approval remains `BLOCKED_BY_COMPANY_APPROVAL`/`BLK-ENV-005`; malformed,
unsafe, or attestation-failed manifests are `FAIL`. A developer-created manifest is never company
approval. This gate does not change the acceptance matrix: AC-005 and AC-011 remain `PARTIAL`,
acceptance evidence is not complete, and Release-ready remains `NO`.

## Final signed-approval closure

The signed-approval closure on `fix/003-signed-approval-closure` (baseline `2309cfecdd24538e320dcb70c35fcbd5d42bf9e2`,
previous corrective integrated to `main` at that SHA) is the current state. The Full deployment-target
seam now additionally requires a detached CMS/PKCS#7 signature over the exact manifest bytes
verified against a company-managed machine trust policy with LocalMachine certificate-chain
validation; the manifest is read exactly once and the same byte buffer is hashed, signature-verified,
and parsed. Environment-only booleans/digests, self-signed developer signers, and synthetic contract
PASS can never be Full or release PASS. Missing cryptographic capability is `BLOCKED_BY_MISSING_TOOL`;
missing company policy remains `BLOCKED_BY_COMPANY_APPROVAL`/`BLK-ENV-005`; malformed,
unsigned/wrong-signer/modified, or attestation-failed evidence is `FAIL`. Visual DOCX QA is a
documented non-mandatory `NOT_RUN` limitation. This gate does not change the acceptance matrix:
AC-005 and AC-011 remain `PARTIAL`, acceptance evidence is not complete, and Release-ready remains
`NO`.

## Corrective evidence boundary

AC-005 is intentionally PARTIAL after the corrective review. The missing capability is an approved
authenticated browser/process-control runner for an exact API/Web host stop/restart and persisted
resume journey; no database cleanup, seed, or alternate database was substituted. Historical
Phase 5 corrective task registration is `RETROSPECTIVE`, and historical accessibility RED evidence
is `NOT_AVAILABLE`; the new post-merge AppShell regression is a static source-contract check, not
historical TDD evidence.

## Atomic signed-approval corrective evidence boundary

Date: 2026-08-04
Baseline: `90bafced98f80b3bbbe80bf86f81ef1c28b694ef`
Branch: `fix/003-atomic-signed-approval`

The current bounded corrective implementation strengthens the deployment-approval evidence seam
only. It does not change the product acceptance matrix or promote any historical evidence. The
.NET verifier now owns the single-read manifest/signature/policy snapshots, expected-SHA attestation,
strict schema parsing, certificate policy-v2 decisions, revocation policy, and evidence-path trust;
PowerShell propagates one structured result. Focused suites and the sequential PostgreSQL suite are
green as recorded in the implementation checkpoint. AC-005 and AC-011 remain `PARTIAL`, acceptance
evidence remains `NO`, and release readiness remains `NO` pending the approved browser/process-control
runner and company-managed deployment trust target. Standards/Specification review, Converge, and
fresh Fast/Full evidence are `NOT_RUN` at the Constitution-required implementation checkpoint.
