# Tasks: Operational Configuration Workspace

**Input**: `spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`

**Tests**: Required. New backend/HTTP/PostgreSQL behavior follows red → green at the public seams
defined in `plan.md`. The frontend behavior runner remains package-policy blocked unless an already
approved runnable dependency exists.

**Historical execution rule**: Phase 0 through Phase 5 are accepted historical checkpoints. The
previous Phase 6
invocation starts from authoritative merged `main` baseline
`f93c2da8bcd71c0436c38d502ddd7a770c35e621` on branch
`003-operational-configuration-workspace`, executes only T073–T080, records acceptance and final
verification evidence honestly, commits and pushes the feature branch, and stops after T080. It
must not merge automatically, execute work after T080, create Spec 004, or expand Rule/Alert/CSV/
Reporting capability. This paragraph is retained as historical scope and is not the execution gate
for the corrective branch below.

---

## Post-Phase-6 Corrective Closure

This is a narrowly bounded governance, traceability, evidence, regression, and source-precedence
repair on corrective branch `fix/003-final-governance-corrective` from merged `main` baseline
`045f3981f3ba6bb87425009ee8f8cf0e6cf4e56a`. It does not reopen Phase 1–6 implementation, create
Phase 7, create Feature 004, or expand Rule/Alert/CSV/Reporting scope. Historical implementation
and evidence are registered retrospectively where marked; no historical TDD red result is invented.

- [x] T081 [RETROSPECTIVE] [RUNNABLE_NOW] Register the Phase 5 corrective implementation, tests, and evidence in the canonical ledger, mapping commit `986b3dca8673b455710835bc252cd17980f9cac5` and merge `f93c2da8bcd71c0436c38d502ddd7a770c35e621`; disclose that this is retrospective registration and not historical task execution
- [x] T082 [US1] [BLOCKED_BY_MISSING_TOOL] Reconcile AC-005 host-restart evidence by running the approved API/Web restart journey if an approved authenticated browser/process-control surface exists; otherwise change AC-005 to PARTIAL and record the exact missing capability without retaining PASS
- [x] T083 [P] [US1] [RUNNABLE_NOW] Add direct AppShell accessibility regression coverage at the existing static verification seam for visible labels, stable ids, invalid-credential error association/focus contract, and Vietnamese navigation/auth names; record historical RED as `NOT_AVAILABLE` and distinguish the post-merge regression result
- [x] T084 [RUNNABLE_NOW] Synchronize `spec.md`, `plan.md`, acceptance traceability, final verification, and release checkpoint with bounded implementation-complete status and `Release-ready=NO`; use `Implemented — Release Evidence Blocked`, never `Released`
- [x] T085 [RUNNABLE_NOW] Reconcile DOC-05 v0.2/DOC-07 v0.2 against ADR-010 and the harness; supersede stale container-target wording, remove `BLK-ENV-004` emission, and use a concrete non-containerized target-host/service approval blocker without fabricating deployment approval
- [x] T086 [RUNNABLE_NOW] Rerun all approved Unit, PostgreSQL, Web, policy, architecture, Fast, and Full checks, update numeric evidence and blocker classifications, and write `checklists/post-phase-06-corrective-review.md`
- [x] T087 [RUNNABLE_NOW] Obtain independent Standards and Specification reviews of this corrective diff, perform direct artifact comparison because the SpecKit provider is unavailable, prepare a corrective PR without merging, and record the explicit Feature 003 final stop

### Corrective closure dependencies

```text
T073–T080 (accepted historical Phase 6)
  -> T081–T085 corrective provenance, AC-005, accessibility, status, and deployment reconciliation
  -> T086 verification/evidence refresh
  -> T087 independent review, PR preparation, and final stop
```

The corrective ledger is additive. Historical T001–T080 wording and checkboxes remain unchanged;
T081 is explicitly retrospective and cannot be used as proof that the historical Phase 5 work was
test-first. No Phase 7 task is created.

**Historical note (not operative for this run)**: Earlier corrective closure from merged `main`
baseline `2741429fb1a28d403adde69e36810bab16d12af5` addressed T054–T056. That historical work is
superseded by the Phase 6 execution gate below; it does not reopen Phase 3 or Phase 4 and does not
change the current T073–T080 scope. Feature 002 remains untouched.

**Phase 6 execution gate**: The current run is authorized only for T073–T080 from authoritative
merged `main` baseline `f93c2da8bcd71c0436c38d502ddd7a770c35e621`. T001–T072 are accepted
historical work and must not be rewritten. Feature 002, work after T080, Spec 004, and
Rule/Alert/CSV/Reporting capability remain out of scope. T080 is checked only after deterministic
PostgreSQL, hosted HTTP where runnable, honest frontend/browser blocker evidence, final
Standards/Specification review, convergence, and final analysis are recorded.

**Current corrective execution gate**: This invocation executes only T081–T087 on
`fix/003-final-governance-corrective` from baseline `045f3981f3ba6bb87425009ee8f8cf0e6cf4e56a`.
The Phase 6 gate above is historical context; it does not prohibit this explicitly authorized
post-Phase-6 governance closure. No Phase 7 or Feature 004 work is authorized.

## Final Documentation and Deployment-Gate Corrective Closure

This additive closure starts from merged-main baseline `6dbfaf3bcbc95f2d262ddeacf174232d9d746bd7`
on branch `fix/003-doc05-deployment-gate`. It addresses only the six final documentation,
deployment-gate, evidence, and review findings in `checklists/final-corrective-analysis.md`.
It does not rewrite T001-T087, create Phase 7, create Spec 004, expand product capability, or
merge the corrective branch.

- [x] T088 [RUNNABLE_NOW] Record the read-only Spec Kit Analyze result as `NOT_RUN` when the provider command is unavailable and document all six findings plus direct artifact comparison in `specs/003-operational-configuration-workspace/checklists/final-corrective-analysis.md`; preserve constitution 1.1.0 and source precedence
- [x] T089 [P] [RUNNABLE_NOW] Add failing deployment-target contract tests before implementation in `tests/Verification/deployment-target.tests.ps1`, covering blocked, malformed/unsafe, valid/pass, redaction, Fast/Full plan, and exit-code cases without production credentials
- [x] T090 [RUNNABLE_NOW] Reconcile canonical `Business Docs/DOC-05_Software_Architecture_Document_v0.2.docx` with its detailed restricted non-containerized deployment decision, version history, Architecture Summary, deployment view, ADR catalogue, and AR-11; do not claim DOC-05 corrected if an approved DOCX editing path is unavailable
- [x] T091 [P] [RUNNABLE_NOW] Correct `docs/adr/ADR-010-containerized-on-prem.md` to repository status vocabulary so it records an accepted MVP-1 architecture decision with deployment approval pending, without claiming the current ADR is superseded
- [x] T092 [RUNNABLE_NOW] Implement the fail-closed deployment-target verification contract in `scripts/common/DeploymentTarget.ps1` and integrate it into `scripts/harness.ps1`; require approval flag plus sanitized manifest, validate schema/model/date/secret-like keys, emit only `PASS`, `BLOCKED_BY_COMPANY_APPROVAL` with `BLK-ENV-005`, or `FAIL`, and preserve exit codes 0/1/20
- [x] T093 [RUNNABLE_NOW] Synchronize `spec.md`, `plan.md`, acceptance traceability, final verification, release checkpoint, post-phase corrective review, `docs/source-register.md`, `docs/decision-log.md`, `docs/repository-harness.md`, and `README.md` with AC-005=PARTIAL, AC-011=PARTIAL, implementation/readiness separation, the deployment manifest contract, and `Release-ready=NO`
- [x] T094 [RUNNABLE_NOW] Run the new verification seam, approved Unit/PostgreSQL/Web/policy/architecture checks, Fast harness, and Full harness; refresh numeric evidence and classify every blocker without secrets, port 5432, substitutes, or fabricated approval
- [x] T095 [RUNNABLE_NOW] Perform an independent Standards review of the corrective diff from the baseline and resolve all Critical/High/actionable Medium findings; record provider status honestly if the review capability is unavailable
- [x] T096 [RUNNABLE_NOW] Perform an independent Specification review against DOC-05/DOC-07, constitution, Feature 003 artifacts, and the six findings; resolve all Critical/High/actionable Medium findings and record the result
- [x] T097 [RUNNABLE_NOW] Run Spec Kit Converge and final Analyze when provider commands are available, otherwise perform direct append-only artifact comparison; verify unique task IDs, no stale current containerized wording, prepare but do not merge a real PR, and record the explicit Feature 003 stop

## Final Trusted-Approval and Checkpoint Corrective Closure

This additive closure starts from merged-main baseline `6b77256f29775bb2a777ddcb555d868d7e671243`
on branch `fix/003-trusted-deployment-approval`. It addresses the five corrective findings recorded
in the read-only Spec Kit Analyze for the current closure scope: (1) fail-closed trusted deployment
approval with an approved company CI context and trusted evidence root, (2) release checkpoint
normalization to a single current state, (3) harness check-plan/registration consistency for
repository-wide checks, (4) DOCX structural verification for DOC-05 v0.2, and (5) PR/human review
boundary. It does not rewrite T001-T097, create Phase 7, create Spec 004, expand product
capability, or merge the corrective branch.

- [x] T098 [P] [RUNNABLE_NOW] Add failing deployment-target trust-boundary contract tests before implementation in `tests/Verification/deployment-target.tests.ps1`, covering: approved company runner context required (`CI=true` plus `IUMP_COMPANY_CI_APPROVED=true`), `IUMP_DEPLOYMENT_TARGET_APPROVED=true` required, trusted evidence root required, manifest path must be inside the trusted evidence root, path-traversal and reparse-point escape rejection, protected expected SHA-256/attestation, schema/scalar/UTC/deploymentModel/secret-like validation, missing approved context classified as `BLOCKED_BY_COMPANY_APPROVAL` with `BLK-ENV-005`, synthetic contract evidence never classified as company approval, and no bypass variables
- [x] T099 [RUNNABLE_NOW] Implement the fail-closed trusted deployment approval contract in `scripts/common/DeploymentTarget.ps1`: require approved company runner context (`CI=true` plus `IUMP_COMPANY_CI_APPROVED=true`) and `IUMP_DEPLOYMENT_TARGET_APPROVED=true`, require a trusted evidence root, require the manifest path to be inside that root, reject path traversal and reparse-point escapes, compare expected SHA-256/attestation against the manifest, keep schema/scalar/UTC/deploymentModel/secret-like validation, emit only `PASS`, `BLOCKED_BY_COMPANY_APPROVAL` (`BLK-ENV-005`), or `FAIL`, preserve exit codes 0/1/20, and introduce no bypass variables
- [x] T100 [RUNNABLE_NOW] Integrate the trusted deployment approval contract into `scripts/harness.ps1` and `scripts/common/Harness.ps1` for the Full deployment-target check without exposing secrets and without treating a developer-created approved-manifest as company approval
- [x] T101 [P] [RUNNABLE_NOW] Fix harness check-plan/registration consistency in `scripts/harness.ps1` and `scripts/common/Harness.ps1` so repository-wide checks (for example `deployment-target-contract` and the new `doc05-architecture` check) are registered and executed for all relevant Features 001/002/003 rather than only Feature 003; add repository-harness contract tests in `tests/Verification/repository-harness.tests.ps1` proving no planned Fast/Full check is silently skipped for any Feature
- [x] T102 [RUNNABLE_NOW] Create `tests/Verification/doc05-architecture.tests.ps1` using only built-in `System.IO.Compression.ZipFile` and `System.Xml.Linq.XDocument` to structurally verify `Business Docs/DOC-05_Software_Architecture_Document_v0.2.docx` (copying to a temporary path when the original is locked): document exists, ZIP and XML parse, current restricted non-containerized wording present, containerized reference wording absent, version-history correction date 2026-08-03 present, deployment components (static files, Windows Service, internal PostgreSQL, AR-11) present; never overwrite or extract into the repository; treat an unavailable approved visual renderer as `BLOCKED_BY_MISSING_TOOL` and never claim a structural PASS is a visual PASS
- [x] T103 [RUNNABLE_NOW] Register the `doc05-architecture` verification check in `scripts/common/Harness.ps1` and `scripts/harness.ps1` so it runs for every relevant Feature and is included in the Fast and Full plans; verify the check is not silently skipped
- [x] T104 [RUNNABLE_NOW] Normalize `specs/003-operational-configuration-workspace/checklists/release-checkpoint.md` to a single current state without appended overrides: current task count and unique/unchecked status, AC-005=PARTIAL, AC-011=PARTIAL, readiness states (code implementation complete=YES, acceptance evidence complete=NO, release-ready=NO), current verification and blocker list, and clearly labeled historical entries for T080/T087/T097
- [x] T105 [RUNNABLE_NOW] Synchronize `spec.md`, `plan.md`, acceptance traceability, final verification, final corrective analysis, post-phase corrective review, `docs/source-register.md`, `docs/decision-log.md`, `docs/repository-harness.md`, and `README.md` with the trusted approval model, repository-wide harness checks, DOCX structural verification, AC-005=PARTIAL, AC-011=PARTIAL, implementation/readiness separation, and `Release-ready=NO`
- [x] T106 [RUNNABLE_NOW] Run the new trust-boundary and DOCX structural verification seams, approved Unit/PostgreSQL/Web/policy/architecture checks, Fast harness, and Full harness; refresh numeric evidence and classify every blocker without secrets, port 5432, substitutes, or fabricated approval
- [x] T107 [RUNNABLE_NOW] Perform an independent Standards review of the corrective diff from baseline `6b77256f29775bb2a777ddcb555d868d7e671243` and resolve all Critical/High/actionable Medium findings; record provider status honestly if the review capability is unavailable
- [x] T108 [RUNNABLE_NOW] Perform an independent Specification review against DOC-05/DOC-07, constitution, Feature 003 artifacts, and the five corrective findings; resolve all Critical/High/actionable Medium findings and record the result
- [x] T109 [RUNNABLE_NOW] Run Spec Kit Converge and final Analyze when provider commands are available, otherwise perform direct append-only artifact comparison; verify unique task IDs, no silently skipped harness check, no stale current containerized wording, prepare a PR title/body with baseline, tasks, trust model, evidence, and blockers/status but do not merge it, and record the explicit Feature 003 stop

## Final Signed-Approval and Release-Evidence Corrective Closure

This additive closure starts from authoritative merged-main ancestry `2309cfecdd24538e320dcb70c35fcbd5d42bf9e2`.
It addresses the signed approval trust anchor, atomic manifest verification, DOCX package integrity,
visual-QA gate classification, current Git/checkpoint truth, and provider-specific governance cleanup.
It does not rewrite T001-T109, create Phase 7, create Spec 004, add product capability, or merge the
corrective branch.

- [x] T110 [P] [RUNNABLE_NOW] Run the read-only capability/trust-anchor analysis and record the exact availability of `System.Security.Cryptography.Pkcs`, `SignedCms`, `X509Certificate2`, `X509Chain`, Windows certificate stores, and SHA-256 in `specs/003-operational-configuration-workspace/checklists/final-signed-approval-analyze.md`; classify missing built-in capability as `BLOCKED_BY_MISSING_TOOL` and preserve production `BLOCKED_BY_COMPANY_APPROVAL`/`BLK-ENV-005` without inventing policy
- [x] T111 [P] [RUNNABLE_NOW] Add failing signed-manifest regression tests in `tests/Verification/deployment-signature.tests.ps1` using only built-in APIs and temporary sanitized fixtures; cover missing company trust anchor, unsigned/malformed/modified manifests, wrong or expired signer, self-signed developer signer outside policy, required EKU/OID mismatch, valid synthetic signature as contract-only PASS, production Full remaining blocked, single-read/reopen count, secret/path redaction, and cleanup
- [x] T112 [P] [RUNNABLE_NOW] Extend `tests/Verification/doc05-architecture.tests.ps1` with red tests for required Open XML package entries, relationship XML parsing, office-document relationship target, relationship-target traversal/entry existence, duplicate critical entries, and malformed relationship XML without writing or extracting into the repository
- [x] T113 [RUNNABLE_NOW] Add the provider-neutral signed-approval verifier utility under `src/Infrastructure/DeploymentApproval/` with only preinstalled .NET framework capabilities and no new package; expose a machine-readable contract for exact manifest bytes, detached CMS/PKCS#7 verification, certificate validity/chain/signer policy, synthetic contract mode, and fail-closed missing-tool status
- [x] T114 [RUNNABLE_NOW] Implement the atomic manifest snapshot and production trust-boundary contract: open/read manifest bytes exactly once with write/delete sharing denied, hash/signature-verify/decode/parse the same byte buffer, require a company-managed machine trust policy and LocalMachine certificate-chain validation, reject environment-only booleans/digests/self-signed developer trust, preserve redacted evidence and exit codes 0/1/20, and never reopen the manifest
- [x] T115 [RUNNABLE_NOW] Integrate signed approval into `scripts/common/DeploymentTarget.ps1` and `scripts/harness.ps1`; require detached signature evidence for production PASS, classify missing cryptographic capability as `BLOCKED_BY_MISSING_TOOL`, missing company policy as `BLOCKED_BY_COMPANY_APPROVAL`/`BLK-ENV-005`, malformed/unsigned/wrong-signer/modified evidence as `FAIL`, and ensure synthetic contract PASS cannot become Full release PASS
- [x] T116 [RUNNABLE_NOW] Implement the DOCX package-integrity checks in `tests/Verification/doc05-architecture.tests.ps1` using built-in ZIP/XML APIs, preserving the structural-only classification and temporary-copy/no-repository-write guarantees
- [x] T117 [RUNNABLE_NOW] Resolve the visual DOCX QA source-of-truth decision in `docs/decision-log.md`, `docs/repository-harness.md`, `specs/003-operational-configuration-workspace/checklists/release-checkpoint.md`, and `specs/003-operational-configuration-workspace/checklists/final-verification.md`; make visual QA either a machine-enforced mandatory Full gate or an explicit non-mandatory `NOT_RUN` limitation, never both a release blocker and an unenforced check
- [x] T118 [RUNNABLE_NOW] Synchronize current Git/release evidence in `specs/003-operational-configuration-workspace/checklists/release-checkpoint.md`, `final-verification.md`, `final-pr-body.md`, and `acceptance-traceability.md`: previous corrective integrated directly to main at `2309cfec`, current branch/commit/PR/human-review/CI state truthful, AC-005 and AC-011 remain PARTIAL, and Release-ready remains NO
- [x] T119 [RUNNABLE_NOW] Verify provider-specific OpenCode/DeepSeek instructions are absent from `AGENTS.md`; retain only the stable external-delegation principle and do not introduce provider/model scope into Feature 003 artifacts
- [x] T120 [RUNNABLE_NOW] Run exact focused contract tests, approved Unit/PostgreSQL/Web/policy/architecture checks, Fast harnesses, and a fresh Full harness; record PASS/FAIL/BLOCKED/NOT_RUN with exact blocker IDs, no secrets, no port 5432, no package/container/substitute database, and no fabricated company signature
- [x] T121 [RUNNABLE_NOW] Perform the independent Standards review of the signed-approval corrective diff from baseline `2309cfecdd24538e320dcb70c35fcbd5d42bf9e2`, resolve all Critical/High/actionable Medium findings, and record the two-axis result
- [x] T122 [RUNNABLE_NOW] Perform the independent Specification review against DOC-05/DOC-07, constitution, Feature 003 artifacts, and findings FINDING-01 through FINDING-06; resolve all Critical/High/actionable Medium findings and record the result
- [x] T123 [RUNNABLE_NOW] Run provider-native Spec Kit Converge and final Analyze when available, otherwise perform direct append-only artifact comparison; verify unique task IDs, no environment-only production PASS, atomic hash/verify/parse, complete DOCX package checks, visual-QA classification, truthful Git state, AC-005/AC-011 PARTIAL, Release-ready NO, no Phase 7/Spec 004, prepare but do not merge a real PR, push only the corrective branch, and stop

## Checklist format

Every task uses `- [ ] T### [P?] [Story?] [classification] description with exact file path`.

## Phase 0: Specification, contracts, UX state model, gap analysis, and implementation gate

**Goal**: Produce canonical implementation-ready artifacts and red-test definitions without green
application changes.

- [x] T001 [RUNNABLE_NOW] Register Feature 003 purpose, exclusions, source precedence, and authoritative baseline in `specs/003-operational-configuration-workspace/spec.md`
- [x] T002 [RUNNABLE_NOW] Complete the formal clarification coverage scan and encode all resolved role, scope, persistence, failure, localization, and stop-gate decisions in `specs/003-operational-configuration-workspace/spec.md`
- [x] T003 [P] [RUNNABLE_NOW] Record existing endpoint reuse, missing operational status, missing production Engineer assignment, and actual legal activation order in `specs/003-operational-configuration-workspace/research.md`
- [x] T004 [P] [RUNNABLE_NOW] Define derived workspace status, eight-step progress, complete-chain validation, and persistence rationale in `specs/003-operational-configuration-workspace/data-model.md`
- [x] T005 [P] [RUNNABLE_NOW] Define status, Engineer listing/assignment, chain validation, owner-command reuse, idempotency, and safe error contracts in `specs/003-operational-configuration-workspace/contracts/operational-workspace.md`
- [x] T006 [P] [RUNNABLE_NOW] Define landing, wizard, feedback, conflict, partial activation, navigation, accessibility, and frontend evidence states in `specs/003-operational-configuration-workspace/contracts/ui-state-model.md`
- [x] T007 [RUNNABLE_NOW] Complete architecture/module ownership, deep interface seams, TDD seams, six implementation phases, and readiness gates in `specs/003-operational-configuration-workspace/plan.md`
- [x] T008 [P] [RUNNABLE_NOW] Define runnable Phase 1 acceptance and negative journeys without secrets or prohibited dependencies in `specs/003-operational-configuration-workspace/quickstart.md`
- [x] T009 [RUNNABLE_NOW] Run the first read-only `/speckit.analyze` and record every Critical, High, and Medium actionable finding in `specs/003-operational-configuration-workspace/checklists/phase-00-governance.md`
- [x] T010 [RUNNABLE_NOW] Resolve all Critical/High/Medium findings, rerun `/speckit.analyze` to PASS, evaluate constitution impact, and finalize Implementation-ready evidence in `specs/003-operational-configuration-workspace/checklists/phase-00-governance.md`

**Checkpoint**: Analysis PASS; Constitution 1.1.0 impact evaluated; Implementation-ready YES; no
green application code has bypassed Phase 0.

---

## Phase 1: Role-aware Setup Wizard vertical slice and state-aware landing

**User Story**: US1 — Complete and Resume Initial Setup

**Goal**: Deliver a visible PostgreSQL-backed Administrator handoff and Engineer continuation
through one complete chain, with persisted resume, legal activation, navigation to Simulator, and
no automatic Start.

**Independent Test**: On the approved PostgreSQL target, an Administrator creates/activates a Site
and assigns an existing Engineer; the Engineer resumes, creates the remaining chain, refreshes/
restarts without loss, validates and activates legally, reaches Simulator, and zero Run exists.

### Red evidence

- [x] T011 [P] [US1] [RUNNABLE_NOW] Add failing IAM application tests for active-Engineer listing, Administrator-only idempotent Site-scope assignment, invalid role/status, and duplicate prevention in `tests/Unit/IAM/EngineerScopeAssignmentTests.cs`, then register the Phase 1 Unit suites in `tests/Unit/Program.cs`
- [x] T012 [P] [US1] [RUNNABLE_NOW] Add failing operational-status tests for all five landing states, eight-step derivation, scope-before-counts, dependency error, validation grouping, and zero implicit Start in `tests/Unit/OperationalWorkspace/OperationalWorkspaceStatusTests.cs`
- [x] T013 [P] [US1] [RUNNABLE_NOW] Add failing HTTP contract tests for status, Engineer list/assignment, chain validation, safe 401/403/404/409/422/503, idempotency, ETag/version, and antiforgery metadata in `tests/Unit/Api/OperationalWorkspaceEndpointTests.cs`
- [x] T014 [P] [US1] [RUNNABLE_NOW] Add failing PostgreSQL vertical-journey tests for Administrator handoff, Engineer continuation, persisted resume, ordered activation, partial retry, and zero Simulator Run in `tests/Integration/OperationalWorkspace/OperationalSetupJourneyTests.cs`, then register the suite in `tests/Integration/Program.cs`
- [x] T015 [US1] [RUNNABLE_NOW] Run the exact new Unit and PostgreSQL Integration seams before green and record numeric non-zero red evidence without exposing secrets in `specs/003-operational-configuration-workspace/checklists/phase-01-red.md`

### Minimal green backend and HTTP

- [x] T016 [P] [US1] [RUNNABLE_NOW] Define typed operational landing, step, validation, Engineer candidate, assignment, and dependency outcomes in `src/Hosting/Abstractions/OperationalWorkspacePorts.cs`
- [x] T017 [P] [US1] [RUNNABLE_NOW] Define the IAM-owned Engineer query/assignment interface and safe result codes in `src/Modules/IAM/Contracts/OperationalWorkspaceIamContracts.cs`
- [x] T018 [US1] [RUNNABLE_NOW] Implement active Engineer discovery and Administrator-authorized duplicate-safe Site scope assignment through `IIamCommandRepository` in `src/Modules/IAM/Application/EngineerScopeAssignment.cs`
- [x] T019 [US1] [RUNNABLE_NOW] Add forward migration `0014_operational_workspace_scope.sql` for scoped pre-Mapping Source resume and atomic root Site-scope uniqueness; extend owner persistence without cross-scope fallback
- [x] T020 [US1] [RUNNABLE_NOW] Implement the deep PostgreSQL operational workspace query/command adapter over public IAM, Organization, Catalog, Acquisition, Integration, and Audit contracts in `src/Composition/Postgres/PostgresOperationalWorkspacePorts.cs`
- [x] T021 [US1] [RUNNABLE_NOW] Register operational workspace ports without cross-module logic in `src/Composition/Postgres/PostgresModuleRegistration.cs`
- [x] T022 [US1] [RUNNABLE_NOW] Expose typed status, Engineer list/assignment, and read-only chain validation with server principal, idempotency, antiforgery, transaction, and safe errors in `src/Api/OperationalWorkspaceEndpoints.cs`
- [x] T023 [US1] [RUNNABLE_NOW] Register Operational Workspace endpoints without business logic in `src/Api/Program.cs`

### Minimal green Web vertical slice

- [x] T024 [P] [US1] [RUNNABLE_NOW] Add typed operational workspace state, step IDs, form inputs, command outcomes, and error mapping in `src/Web/src/features/setup/setupTypes.ts`
- [x] T025 [US1] [RUNNABLE_NOW] Add the focused workspace gateway for status, Engineer assignment, owner mutations, complete validation, retry-key reuse, and ETag handling in `src/Web/src/features/setup/setupGateway.ts`
- [x] T026 [US1] [RUNNABLE_NOW] Extend gateway composition with the operational workspace interface while preserving existing auth/session behavior in `src/Web/src/gateways/webGateways.ts`
- [x] T027 [US1] [RUNNABLE_NOW] Implement state-aware post-login routing for Wizard, Continue Setup, Dashboard, No Authorized Scope, and Dependency Error in `src/Web/src/app/AppShell.tsx` and integrate the route content in `src/Web/src/App.tsx`
- [x] T028 [P] [US1] [RUNNABLE_NOW] Implement the responsive eight-step progress, summary, validation, Back, Save/Continue, Cancel, loading, submitting, conflict, forbidden, and error shell in `src/Web/src/features/setup/SetupWizard.tsx`
- [x] T029 [US1] [RUNNABLE_NOW] Implement Administrator Site creation/activation and existing Engineer selection/scope handoff, plus read-only assigned Site for Engineers, in `src/Web/src/features/setup/SetupWizard.tsx`
- [x] T030 [US1] [RUNNABLE_NOW] Implement real Area, Asset, Measurement Point, Data Source, Mapping, and Simulator Configuration step forms using authorized persisted selections in `src/Web/src/features/setup/SetupWizard.tsx`
- [x] T031 [US1] [RUNNABLE_NOW] Implement complete-chain validation, legal ordered activation with stop-on-failure/status reload, navigation to Simulator, and an invariant that no Start request occurs in `src/Web/src/features/setup/SetupWizard.tsx`
- [x] T032 [US1] [RUNNABLE_NOW] Add Vietnamese Industrial Light wizard, compact tablet step list, visible focus, badges with text, feedback, and responsive layouts in `src/Web/src/App.css`

### Phase 1 verification, review, and checkpoint

- [x] T033 [US1] [RUNNABLE_NOW] Run new Unit and PostgreSQL Integration seams to green, a manual browser acceptance journey for Administrator handoff/Engineer continuation/persisted resume/no auto-start, existing Fast mode, Web lint/build, and runtime HTTP checks; record exact commands and numeric exit codes in `specs/003-operational-configuration-workspace/checklists/phase-01-verification.md`
- [ ] T034 [US1] [BLOCKED_BY_PACKAGE_POLICY] Execute approved frontend behavior tests for role-aware landing, persisted wizard resume, conflict focus, and no auto-start if a runner is already available; otherwise record the exact separate package-policy blocker in `specs/003-operational-configuration-workspace/checklists/phase-01-verification.md`
- [x] T035 [US1] [RUNNABLE_NOW] Perform separate Standards and Specification reviews against corrective baseline `a08e28eb0e2299d12403af37f275cb9d862421a9`, resolve all Critical/High findings, and record both axes in `specs/003-operational-configuration-workspace/checklists/phase-01-review.md`
- [x] T036 [US1] [RUNNABLE_NOW] Run architecture/repository-policy checks and a fresh Full harness, report company blockers truthfully, update completed task boxes, and create the explicit stop checkpoint in `specs/003-operational-configuration-workspace/checklists/phase-01-checkpoint.md`

**Checkpoint**: Phase 1 functionality visible and usable against PostgreSQL; Administrator handoff,
Engineer continuation, ordered activation, and persisted resume evidenced; Simulator auto-started
NO; explicit stop before T037.

---

## Phase 2: Configuration management, duplication, and version-safe editing

**User Story**: US2 — Manage Configuration Safely

- [x] T037 [P] [US2] [RUNNABLE_NOW] Add failing duplicate-to-Draft and exclusion tests for all eligible entity types in `tests/Unit/OperationalWorkspace/ConfigurationDuplicationTests.cs`
- [x] T038 [P] [US2] [RUNNABLE_NOW] **Corrective closure complete**: PostgreSQL scoped search/filter/paging, explicit target-Source Simulator duplication, activation/mutation malformed-JSON handling, public command-seam lifecycle coverage, hosted HTTP matrix, and the authenticated browser journey are green in `tests/Integration/OperationalWorkspace/ConfigurationManagementTests.cs`
- [x] T039 [US2] [RUNNABLE_NOW] **Corrective closure**: Define typed paged management and create/edit/validate/lifecycle/delete command contracts in `src/Hosting/Abstractions/ConfigurationManagementPorts.cs`
- [x] T040 [US2] [RUNNABLE_NOW] **Corrective closure**: Implement scope-before-paging management queries, including Simulator ID/source/version search before paging, through owner contracts in `src/Composition/Postgres/PostgresConfigurationManagementPorts.cs`
- [x] T041 [US2] [RUNNABLE_NOW] Implement owner-domain duplicate-to-Draft behavior without history/secrets in `src/Modules/Organization/Application/ConfigurationDuplication.cs`
- [x] T042 [US2] [RUNNABLE_NOW] Implement Source/Mapping duplicate-to-Draft and dependency-safe lifecycle behavior in `src/Modules/Catalog/Application/ConfigurationDuplication.cs`
- [x] T043 [US2] [RUNNABLE_NOW] **Corrective closure complete**: Persisted Acquisition receipts and activation authority remain green; server-derived management detail/list state, stale receipt flags, and invalidation coverage after the hosted edit path are verified in `src/Modules/Acquisition/Application/SimulatorConfiguration.cs`, `src/Modules/Acquisition/Infrastructure/PostgresConfigurationRepository.cs`, `src/Composition/Postgres/PostgresApplicationPorts.cs`, and management UI
- [x] T044 [US2] [RUNNABLE_NOW] **Corrective closure reevaluated — no regression**: Management paging, details, create, version-safe edit, validate, legal lifecycle, duplicate, and safe Draft deletion endpoints remain exposed in `src/Api/ConfigurationManagementEndpoints.cs`; detail responses now include authorized Simulator Draft review metadata
- [x] T045 [P] [US2] [RUNNABLE_NOW] **Corrective closure**: Build reusable Vietnamese table/filter/pagination/detail/editor/action/feedback primitives with loading, validation, conflict, forbidden, not-found, dependency, and runtime states in `src/Web/src/features/configuration/ConfigurationManagementComponents.tsx`
- [x] T046 [US2] [RUNNABLE_NOW] **Corrective closure complete**: Implement server-resumable Simulator relationship review, selector loading/empty/forbidden/dependency/runtime states with Retry, exact field validation messages, and first-invalid focus across the Sites, Areas, Assets, Points, Sources, Mappings, and Simulator Configuration pages in `src/Web/src/features/configuration/ConfigurationManagementRoutes.tsx`
- [x] T047 [US2] [RUNNABLE_NOW] Remove or replace decorative Open Hierarchy and Review Mapping actions in `src/Web/src/features/configuration/ConfigurationRoutes.tsx`
- [x] T048 [US2] [RUNNABLE_NOW] **Corrective closure complete**: Verification and review artifacts record receipt, selector, malformed-JSON, hosted health, authenticated hosted HTTP lifecycle/replay/receipt/Audit-outbox evidence, fresh harness evidence, and the completed authenticated browser journey in `specs/003-operational-configuration-workspace/checklists/phase-02-checkpoint.md`

---

## Phase 3: Explicit Simulator selection, controls, and Run history

**User Story**: US3 — Operate an Explicitly Selected Simulator

- [x] T049 [P] [US3] [RUNNABLE_NOW] Add failing selected-Source/configuration, no-first-Source, legacy-route retirement, and retry-key tests in `tests/Unit/Api/SimulatorSelectionTests.cs`
- [x] T050 [P] [US3] [RUNNABLE_NOW] Add failing PostgreSQL control/history/idempotency/conflict, exact pinning, and concurrency tests in `tests/Integration/OperationalWorkspace/SimulatorOperationsTests.cs`
- [x] T051 [US3] [RUNNABLE_NOW] Define selected-start ownership and retry-key contracts in `src/Hosting/Abstractions/SimulatorWorkspacePorts.cs` and Web pure helpers
- [x] T052 [US3] [RUNNABLE_NOW] Implement PostgreSQL selected Simulator queries and atomic exact-configuration Start recheck in `src/Composition/Postgres/PostgresSimulatorWorkspacePorts.cs`
- [x] T053 [US3] [RUNNABLE_NOW] Retire legacy Simulator mutations and enforce the four selected workspace routes in `src/Api/SimulatorEndpoints.cs`
- [x] T054 [US3] [RUNNABLE_NOW] Implement URL-backed dependent Site/Area/Asset/Source/configuration selectors and explicit retry UX in `src/Web/src/features/simulator/SimulatorRoute.tsx`
- [x] T055 [US3] [RUNNABLE_NOW] Implement true Web retry-key state, refresh/logout reconstruction, Run details/history/states in `src/Web/src/gateways/webGateways.ts` and `src/Web/src/features/simulator/SimulatorRoute.tsx`
- [x] T056 [US3] [RUNNABLE_NOW] Complete hosted matrix, authenticated browser journey, reviews, Fast/Full evidence, and checkpoint in `specs/003-operational-configuration-workspace/checklists/phase-03-checkpoint.md`

---

## Phase 4: Explicit Latest/Health selection and refresh

**User Story**: US4 — Observe Selected Latest and Source Health

- [x] T057 [P] [US4] [RUNNABLE_NOW] Add failing selected-Point/no-first-Point/No-Data-not-zero tests in `tests/Unit/Api/LatestSelectionTests.cs`
- [x] T058 [P] [US4] [RUNNABLE_NOW] Add failing PostgreSQL Latest/Health/counter scope tests in `tests/Integration/OperationalWorkspace/LatestHealthTests.cs`
- [x] T059 [US4] [RUNNABLE_NOW] Add authorized hierarchy selector query contract with scope-before-paging in `src/Hosting/Abstractions/TelemetryWorkspacePorts.cs`
- [x] T060 [US4] [RUNNABLE_NOW] Implement PostgreSQL Point selector and selected Latest/Health query adapter in `src/Composition/Postgres/PostgresTelemetryWorkspacePorts.cs`
- [x] T061 [US4] [RUNNABLE_NOW] Expose authorized selector endpoints while preserving safe Point queries in `src/Api/TelemetryQueryEndpoints.cs`
- [x] T062 [US4] [RUNNABLE_NOW] Replace implicit Point lookup with explicit Site/Area/Asset/Point selection and server-authorized Point rehydration in `src/Web/src/features/telemetry/PointCurrentRoute.tsx`
- [x] T063 [US4] [RUNNABLE_NOW] Add ten-second default auto refresh, disable/manual refresh, timestamps, quality, Health, Run/counters, explicit No Data, and same-selection request coordination in `src/Web/src/features/telemetry/PointCurrentRoute.tsx`
- [x] T064 [US4] [RUNNABLE_NOW] Verify US4 rehydration/refresh behavior, review, Fast/Full evidence, and checkpoint in `specs/003-operational-configuration-workspace/checklists/phase-04-checkpoint.md`

---

## Phase 5: Audit usability and Operational Dashboard

**User Story**: US5 — Navigate Operational State and Review Audit

- [x] T065 [P] [US5] [RUNNABLE_NOW] Add failing Audit filter/paging/redaction and authorized Dashboard summary tests in `tests/Unit/OperationalWorkspace/AuditDashboardTests.cs`, then register and invoke the suite from `tests/Unit/Program.cs`
- [x] T066 [P] [US5] [RUNNABLE_NOW] Add failing PostgreSQL scope-before-paging Audit/Dashboard tests in `tests/Integration/OperationalWorkspace/AuditDashboardTests.cs`, then register and invoke the suite from `tests/Integration/Program.cs`
- [x] T067 [US5] [RUNNABLE_NOW] Extend the Audit-owned query contract for `fromUtc`/`toUtc`, actor, action, entity type, entity ID, Site/Area scope, server-redacted safe before/after diff, Administrator-only correlation permission (implemented by the existing `ServerPrincipal.IsAdministrator` gate), and strict keyset paging in `src/Modules/Audit/Contracts/AuditContracts.cs` and `src/Modules/Audit/Application/AuditQueryService.cs`
- [x] T068 [US5] [RUNNABLE_NOW] Implement the Audit-owned server-side filters/redaction and scope-before-paging path in `src/Modules/Audit/Infrastructure/PostgresAuditRepositories.cs` and `src/Composition/Postgres/PostgresApplicationPorts.cs` (cursor requests are strict keyset pages without OFFSET), then compose authorized Dashboard summaries through public contracts in `src/Composition/Postgres/PostgresOperationalDashboardPorts.cs`
- [x] T069 [US5] [RUNNABLE_NOW] Expose only the typed `/api/v1/operational-dashboard` route in `src/Api/OperationalDashboardEndpoints.cs`, register it in `src/Api/Program.cs`, and keep the existing `/api/v1/audit-events` ownership in `src/Api/AuditEndpoints.cs`
- [x] T070 [US5] [RUNNABLE_NOW] Add the `dashboard.getSnapshot` gateway seam in `src/Web/src/gateways/webGateways.ts`, implement authorized Vietnamese Operational Dashboard navigation in `src/Web/src/features/dashboard/OperationalDashboard.tsx`, and wire it from `src/Web/src/App.tsx`
- [x] T071 [US5] [RUNNABLE_NOW] Extend `audit.getSnapshot` in `src/Web/src/gateways/webGateways.ts` to send server-side date/actor/action/entity/scope filters and implement the resulting pagination, redacted safe diff, Administrator-only correlation display, and explicit states in `src/Web/src/features/audit/AuditRoute.tsx`
- [x] T072 [US5] [RUNNABLE_NOW] Verify US5 behavior, review, Fast/Full evidence, and checkpoint in `specs/003-operational-configuration-workspace/checklists/phase-05-checkpoint.md`

---

## Phase 6: Acceptance hardening, accessibility, traceability, and final evidence

- [x] T073 [P] [RUNNABLE_NOW] Add complete AC-001..AC-015 acceptance traceability to `specs/003-operational-configuration-workspace/checklists/acceptance-traceability.md`
- [x] T074 [P] [RUNNABLE_NOW] Audit Vietnamese content, keyboard focus, responsive desktop/tablet behavior, labels, and non-color status in `specs/003-operational-configuration-workspace/checklists/accessibility.md`
- [x] T075 [P] [RUNNABLE_NOW] Verify no secret, port 5432, fake fallback, public download, container, savings, AI, or control behavior in `specs/003-operational-configuration-workspace/checklists/security-scope.md`
- [x] T076 [RUNNABLE_NOW] Run all runnable Unit and PostgreSQL Integration acceptance journeys and record numeric evidence in `specs/003-operational-configuration-workspace/checklists/final-verification.md`
- [x] T077 [BLOCKED_BY_PACKAGE_POLICY] Run approved frontend behavior suite if available or record the exact package-policy blocker without false PASS in `specs/003-operational-configuration-workspace/checklists/final-verification.md`
- [x] T078 [RUNNABLE_NOW] Perform final Standards and Specification reviews and resolve all Critical/High findings in `specs/003-operational-configuration-workspace/checklists/final-review.md`
- [x] T079 [RUNNABLE_NOW] Run final architecture, repository policy, Web lint/build, Fast, and fresh Full harness evidence in `specs/003-operational-configuration-workspace/checklists/final-verification.md`
- [x] T080 [RUNNABLE_NOW] Assess Planning-ready, Implementation-ready, and Release-ready independently and record explicit remaining blockers in `specs/003-operational-configuration-workspace/checklists/release-checkpoint.md`

## Dependencies

```text
T001-T008
  -> T009 first analysis
  -> remediation
  -> T010 final analysis + Phase 0 checkpoint
  -> T011-T015 red
  -> T016-T023 backend/HTTP green
  -> T024-T032 Web vertical slice
  -> T033-T036 Phase 1 verification/review/checkpoint
  -> STOP

T037-T048 Phase 2
  -> T049-T056 Phase 3
  -> T057-T064 Phase 4
  -> T065-T072 Phase 5
  -> T073-T080 Phase 6
```

- T011-T014 are parallel red-test sources at different public seams.
- T016 and T017 may proceed in parallel after recorded red evidence.
- T024 may proceed with backend contracts while T018-T023 remain green work because it touches
  separate files, but T025-T032 depend on the final contract shape.
- Later phases depend on the Phase 1 checkpoint and require separate `/speckit.implement`
  invocations.

## Requirement coverage

| Requirement group | Task coverage |
|---|---|
| FR-001..FR-011, FR-025..FR-030; AC-001..AC-005, AC-014, AC-015 | T011-T036 |
| FR-012..FR-016; AC-006..AC-008 | T037-T048 |
| FR-017..FR-019; AC-009 | T049-T056 |
| FR-020..FR-022; AC-010..AC-012 | T057-T064 |
| FR-023..FR-024; AC-013 | T065-T072 (FR-029 creation is historical; this phase covers only its no-secrets review facet) |
| Cross-cutting SC-001..SC-012 and AC-001..AC-015 | T073-T080 |

## Parallel example: Phase 1

```text
T011 IAM red       ┐
T012 status red    ├─> T015 red evidence
T013 HTTP red      ┤
T014 PostgreSQL red┘

T016 host contracts ─┐
T017 IAM contracts   ├─> T018-T023 backend/API green
T024 Web types       ┘           └─> T025-T032 Web green
```

## Implementation strategy

1. Phase 0 proves implementation readiness without green code.
2. Phase 1 delivers the smallest operationally usable vertical slice against PostgreSQL.
3. Stop and review before management breadth.
4. Add later stories one phase at a time so each remains independently demonstrable.

## Task count and phase breakdown

| Phase | Tasks | Range |
|---|---:|---|
| Phase 0 | 10 | T001-T010 |
| Phase 1 / US1 | 26 | T011-T036 |
| Phase 2 / US2 | 12 | T037-T048 |
| Phase 3 / US3 | 8 | T049-T056 |
| Phase 4 / US4 | 8 | T057-T064 |
| Phase 5 / US5 | 8 | T065-T072 |
| Phase 6 | 8 | T073-T080 |
| Post-Phase-6 Corrective Closure | 7 | T081-T087 |
| Final Documentation and Deployment-Gate Corrective Closure | 10 | T088-T097 |
| Final Trusted-Approval and Checkpoint Corrective Closure | 12 | T098-T109 |
| Final Signed-Approval and Release-Evidence Corrective Closure | 14 | T110-T123 |
| Atomic Signed-Approval and Post-Merge Corrective Closure | 17 | T124-T140 |
| Atomic Signed-Approval Review Remediation | 16 | T141-T156 |
| Historical total before current phase | **170** | **T001-T170** |
| Post-Merge Handle-Bound Trust Closure | 14 | T157-T170 (historical) |
| Final Handle-Trust Review Closure | 11 | T171-T181 |
| **Current total** | **181** | **T001-T181** |

**Historical MVP**: Phase 6 acceptance hardening and final evidence only
(T073–T080). Stop after T080.

**Historical MVP for the prior corrective run**: Post-Phase-6 governance, evidence, regression,
and traceability closure only (T081-T087). The preceding corrective run was the additive T098-T109
trusted-approval and checkpoint closure and stopped after T109. The current run is the additive
T110-T123 signed-approval and release-evidence closure and stops after T123.

The current atomic signed-approval run is the additive T124-T140 corrective closure. This invocation
stops at the Constitution-required implementation checkpoint after T137; T138-T140 remain pending
for an explicitly authorized continuation.

The Post-Merge Handle-Bound Trust Closure is historical additive work T157-T170 from merged main
`4b4713cb42b1a03270a2688b344988d2945bab2c` on `fix/003-handle-bound-trust-closure`. T138-T140 are
reconciled as complete by their later evidence; T034 remains historically classified by its package
or company-approval blocker. The current invocation is the additive Final Handle-Trust Review
Closure T171-T181 from merged main `f0ed6cb8a2e8875415b737683aaebf4d3409d367` on
`fix/003-final-handle-trust-review`; it does not create Phase 7, Spec 004, or product capability.

---

## Atomic Signed-Approval and Post-Merge Corrective Closure

- [x] T124 [RUNNABLE_NOW] Record read-only direct analysis of the end-to-end atomic manifest flow, atomic trust-policy snapshot, structured result contract, certificate policy v2, revocation, signature-path trust, framework dependency, and post-merge Git truth in `specs/003-operational-configuration-workspace/checklists/atomic-signed-approval-analyze.md`.
- [x] T125 [P] [RUNNABLE_NOW] Add red regression assertions proving PowerShell delegates manifest hash/schema/signature decisions to the .NET verifier, passes `--expected-sha256`, and preserves verifier JSON classification in `tests/Verification/deployment-target.tests.ps1`.
- [x] T126 [P] [RUNNABLE_NOW] Add red regression assertions for one-read trust-policy loading, file/parent ACL hardening, policy replacement/delete-child protection, and fail-closed capability handling in `tests/Verification/deployment-signature.tests.ps1`.
- [x] T127 [P] [RUNNABLE_NOW] Add red structured-result tests for PASS, FAIL, `BLOCKED_BY_MISSING_TOOL`, `BLOCKED_BY_COMPANY_APPROVAL`, malformed JSON, multiple JSON results, exit-code mismatch, blocker IDs, redaction, and read counts in `tests/Verification/deployment-target.tests.ps1`.
- [x] T128 [P] [RUNNABLE_NOW] Add red certificate policy tests for policy version 2, certificate SHA-256 raw-byte identity, SHA-1/MD5 digest rejection, weak keys, EKU mismatch, revocation modes, and synthetic-only boundaries in `tests/Verification/deployment-signature.tests.ps1`.
- [x] T129 [RUNNABLE_NOW] Refactor `scripts/common/DeploymentTarget.ps1` to perform only invocation prerequisites, fixed policy/root argument resolution, verifier invocation, and structured result mapping; remove PowerShell manifest hashing, parsing, schema, and signature conclusions.
- [x] T130 [RUNNABLE_NOW] Refactor `src/Infrastructure/DeploymentApproval/Program.cs` so manifest and detached signature evidence are verified from immutable single-read byte snapshots with expected SHA-256 attestation, strict UTF-8 parsing, and `manifestReadCount=1`.
- [x] T131 [RUNNABLE_NOW] Implement one-read company trust-policy loading from the fixed machine path, reparse rejection, file/parent owner and ACL checks while the handle is open, policy `policyReadCount=1`, and fail-closed company/missing-tool classifications.
- [x] T132 [RUNNABLE_NOW] Implement and validate the structured verifier-result contract, including status/classification/exitCode/blockerId/synthetic/read counts, exact JSON cardinality, and redacted evidence propagation through PowerShell.
- [x] T133 [RUNNABLE_NOW] Implement certificate trust-policy v2 with SHA-256 certificate raw-byte fingerprints, required EKUs, strong digest/public-key algorithms, and explicit Online/Offline revocation handling without `X509RevocationMode.NoCheck`.
- [x] T134 [RUNNABLE_NOW] Harden manifest/signature evidence-pair trust to the same root, reject traversal/absolute escape/reparse/repository paths, require regular files, and never emit sensitive path details.
- [x] T135 [RUNNABLE_NOW] Replace the unnecessary WPF framework reference with the preinstalled non-UI framework that provides `System.Security.Cryptography.Pkcs`, or record the remaining architecture debt without installing packages.
- [x] T136 [RUNNABLE_NOW] Update the temporary synthetic fixture and focused regression suites for policy v2, certificate SHA-256 identity, revocation/algorithm/path cases, cleanup, and no private-key or real-policy artifacts.
- [x] T137 [RUNNABLE_NOW] Synchronize the current post-merge checkpoint, decision log, acceptance traceability, release checkpoint, and atomic corrective report with PR #7 merge truth and current blockers.
- [x] T138 [RUNNABLE_NOW] Run focused verification, unit/integration, Web lint/build, repository checks, Fast, and fresh Full harness; fulfilled by T142 and T151 with PostgreSQL target and every PASS/FAIL/BLOCKED result recorded without using port 5432 or substitutes.
- [x] T139 [RUNNABLE_NOW] Perform separate Standards and Specification reviews against the constitution, DOC-05, DOC-07, Feature 003 artifacts, and T124-T140; fulfilled by T141 and T152-T153 with review terminology corrected in the current phase.
- [x] T140 [RUNNABLE_NOW] Run direct artifact Converge/final Analyze when provider commands are unavailable, verify unique task IDs and no scope creep, run `git diff --check`, prepare the PR, push only `fix/003-atomic-signed-approval`, and stop without merging or releasing; fulfilled by T154-T156 and reconciled by T164-T170 without rewriting historical evidence.

---

## Atomic Signed-Approval Review Remediation

- [x] T141 [RUNNABLE_NOW] Record read-only Analyze and the Standards/Specification findings F-01 through F-07 against baseline `37606adde7ac39476e53d9aaf43ded608e45038e` in `specs/003-operational-configuration-workspace/checklists/atomic-review-remediation-analyze.md` and `atomic-review-remediation-review.md`.
- [x] T142 [RUNNABLE_NOW] Run and record fresh initial T138 verification on the merged-main implementation before remediation, including Release build, Unit, PostgreSQL Integration at `127.0.0.1:5433/iump_dev`, Web lint/build, focused checks, Fast, and Full classification.
- [x] T143 [RUNNABLE_NOW] Add red tests and correct `ContainsReparsePoint` so Windows drive roots and UNC roots remain rooted while every existing ancestor, trusted root, manifest, and signature path is canonicalized and fail-closed.
- [x] T144 [P] [RUNNABLE_NOW] Build a reusable signed manifest/signature/policy fixture and repair deployment-target negative tests to use a valid evidence pair with exactly one injected fault, asserting classification, exit code, and evidence reason for SHA, JSON/schema, UTC, secret, deployment-model, path, and reparse cases.
- [x] T145 [P] [RUNNABLE_NOW] Refine company policy ACL evaluation into file, immediate-directory, and higher-ancestor threat checks with inheritance/propagation, deny precedence, effective applicability, replacement/delete-child semantics, and deterministic locked-policy contract tests; fail closed as `BLOCKED_BY_MISSING_TOOL` when safe capability is unavailable.
- [x] T146 [P] [RUNNABLE_NOW] Extract and test chain-status classification so Revoked and fatal trust/validity statuses yield FAIL, while BLOCKED is reserved for revocation-unavailable-only statuses.
- [x] T147 [P] [RUNNABLE_NOW] Add stable missing-tool/process-start/runtime capability classification so dotnet/project/framework/process-start failures return structured `BLOCKED_BY_MISSING_TOOL`, exit 20, and the repository blocker ID without reinterpreting them as malformed evidence.
- [x] T148 [P] [RUNNABLE_NOW] Extract a behavioral `ConvertFrom-DeploymentVerifierProcessResult` parser seam and cover valid PASS/FAIL/company/missing-tool results, malformed/multiple/no JSON, extra output, mismatches, blocker IDs, read counts, synthetic production output, evidence redaction, and process failure cases.
- [x] T149 [RUNNABLE_NOW] Synchronize current post-main checkpoint truth for `37606adde7ac39476e53d9aaf43ded608e45038e`, direct integration on `main`, no new PR/reviewer/workflow/status checks, and the new remediation branch without rewriting historical evidence.
- [x] T150 [RUNNABLE_NOW] Complete the single Atomic Signed-Approval Review Remediation implementation phase using red evidence, minimal green changes, refactor, architecture/repository-policy checks, and an explicit checkpoint with AC-005/AC-011 PARTIAL and Release-ready NO.
- [x] T151 [RUNNABLE_NOW] Rerun fresh T138 verification after remediation: focused suites, Release build, Unit, PostgreSQL Integration at `127.0.0.1:5433/iump_dev`, Web lint/build, repository checks, Fast, and Full with every PASS/FAIL/BLOCKED/NOT_RUN classification.
- [x] T152 [RUNNABLE_NOW] Rerun and record the independent Standards Review, resolving all High and actionable Medium findings and documenting any remaining judgement-call smells.
- [x] T153 [RUNNABLE_NOW] Rerun and record the independent Specification Review against DOC-05, DOC-07, spec.md, plan.md, tasks.md, acceptance criteria, and release gates; resolve all High/actionable Medium gaps.
- [x] T154 [RUNNABLE_NOW] Run the actual final Analyze/convergence command if available; otherwise record provider `NOT_RUN` and perform direct artifact/code/test convergence, including unique IDs and no scope creep.
- [x] T155 [RUNNABLE_NOW] Complete T138-T140 ledger evidence, update acceptance/release/final-verification artifacts and prepare the Feature 003 Atomic Review Remediation report and PR body; keep AC-005/AC-011 PARTIAL and Release-ready NO.
- [x] T156 [RUNNABLE_NOW] Run `git diff --check`, verify no secrets/private keys/real policy/temporary fixture artifacts, commit `fix(feature-003): remediate atomic approval review findings`, push only `fix/003-atomic-review-remediation`, prepare/request the PR when capability exists, and stop without merging or creating Phase 7/Spec 004.

## Post-Merge Handle-Bound Trust Closure

This additive corrective phase starts from authoritative merged `main` commit
`4b4713cb42b1a03270a2688b344988d2945bab2c` and uses branch
`fix/003-handle-bound-trust-closure`. It addresses only handle-bound policy trust, Windows
effective-access evaluation, process-start/crash classification, ledger reconciliation, and
post-merge evidence truth. It does not create Phase 7, Spec 004, deployment, release approval, or
product capability.

- [x] T157 [RUNNABLE_NOW] Record the read-only direct Analyze findings F-08 through F-12, provider `NOT_RUN` status, source precedence, and implementation gate in `checklists/handle-bound-trust-analyze.md`.
- [x] T158 [P] [RUNNABLE_NOW] Add red handle-bound regression assertions for single policy-file open/read, stable file identity, no pathname ACL authority, no-write/no-delete sharing, and replacement blocking in `tests/Verification/deployment-signature.tests.ps1` and the synthetic fixture.
- [x] T159 [P] [RUNNABLE_NOW] Add red handle-security/effective-access contract scenarios for file, immediate-directory, and ancestor threat rights, capability-unavailable fail-closed behavior, and safe evidence redaction.
- [x] T160 [P] [RUNNABLE_NOW] Add red process classification cases distinguishing missing command/project/runtime/process-start from started-process crash/no-protocol, malformed protocol, and valid structured results in `tests/Verification/deployment-target.tests.ps1`.
- [x] T161 [RUNNABLE_NOW] Implement handle-bound policy file and directory opening with `SafeFileHandle`, file identity, handle security-descriptor retrieval, and Windows `AccessCheck` against the current process token; remove production reliance on pathname ACL evaluation and fail closed when the capability is unavailable.
- [x] T162 [RUNNABLE_NOW] Implement the single-handle policy snapshot flow: fixed production path, no write/delete sharing, security decision from the opened file and directory handles, one byte read, identity before/after comparison, and parsing from the captured bytes without reopening.
- [x] T163 [RUNNABLE_NOW] Correct `DeploymentTarget.ps1` process-result invocation mapping so only pre-start capability failures are `BLOCKED_BY_MISSING_TOOL`; any started process without one valid protocol result is `FAIL` with redacted evidence.
- [x] T164 [RUNNABLE_NOW] Reconcile T138–T140 with T142/T151, T141/T152/T153, and T154–T156; verify unique task IDs, retain historical T034 classification, and remove contradictory current task counts.
- [x] T165 [RUNNABLE_NOW] Synchronize one current post-merge state across `spec.md`, `plan.md`, acceptance traceability, release checkpoint, final verification, decision log, repository harness guidance, and the implementation checkpoint with `main` `4b4713c`, corrective commit, no PR/reviewer/CI evidence, and Release-ready `NO`.
- [x] T166 [RUNNABLE_NOW] Correct current review terminology to `Internal two-axis Standards/Specification self-review: PASS`, `Independent human review: NO`, and `GitHub CI/status evidence: NO`, while preserving historical wording under historical headings.
- [x] T167 [RUNNABLE_NOW] Run focused deployment-signature, deployment-target, repository-policy, architecture, repository-harness, and Release build checks; record exact RED/GREEN evidence and all PASS/FAIL/BLOCKED/NOT_RUN classifications.
- [x] T168 [RUNNABLE_NOW] Run architecture/repository-policy/diff checks and the approved Unit, PostgreSQL Integration at `127.0.0.1:5433/iump_dev`, Web, Fast, and fresh Full verification where applicable; never use port 5432, Docker, package installation, substitute databases, or secrets.
- [x] T169 [RUNNABLE_NOW] Create `checklists/handle-bound-trust-implementation-checkpoint.md` with handle/read/identity/effective-access/process-classification evidence, T138–T140 reconciliation, current Git truth, blockers, AC-005/AC-011 `PARTIAL`, Acceptance evidence `NO`, Release-ready `NO`, and explicit no-Phase-7/no-Spec-004 scope.
- [x] T170 [RUNNABLE_NOW] Run `git diff --check`, inspect the diff for secrets/private keys/real policy/SID/security-descriptor/temporary fixture artifacts, commit `fix(feature-003): bind deployment policy trust to file handles`, and stop without push, PR, merge, release, final review, or convergence.

---

## Final Handle-Trust Review Closure

This additive corrective phase starts from merged `main` commit
`f0ed6cb8a2e8875415b737683aaebf4d3409d367` and uses branch
`fix/003-final-handle-trust-review`. It addresses only higher-ancestor delete-child threat
coverage, positive Windows effective-access evidence, handle/path boundary review, and post-merge
truth synchronization. It does not redesign the trust architecture, change policy/CMS/certificate
semantics, create Phase 7 or Spec 004, add product capability, or perform release approval.

- [x] T171 [RUNNABLE_NOW] Record read-only direct Analyze findings F-13 through F-15, provider `NOT_RUN`, source precedence, and the bounded Final Handle-Trust Review Closure gate in `checklists/final-handle-trust-review-analyze.md`.
- [x] T172 [P] [RUNNABLE_NOW] Add a red deterministic fixture assertion proving higher-ancestor `FILE_DELETE_CHILD` is unsafe while read/execute-only and unrelated sibling-creation rights remain safe for `HandleSecurityTarget.AncestorDirectory`.
- [x] T173 [P] [RUNNABLE_NOW] Add a red positive effective-access contract using real Windows security descriptors and `AccessCheck` for safe/no-unsafe, read-only, write-data, delete, delete-child, `WRITE_DAC`, `WRITE_OWNER`, and explicit-deny scenarios, including invalid-descriptor capability classification.
- [x] T174 [RUNNABLE_NOW] Implement `FILE_DELETE_CHILD` in higher-ancestor threat rights without adding unrelated sibling-creation rights, preserving the fixed policy path and existing handle-bound flow.
- [x] T175 [RUNNABLE_NOW] Add the test-only effective-access seam that exercises the production Windows `AccessCheck` implementation against deterministic in-memory self-relative descriptors without making the seam production authority.
- [x] T176 [RUNNABLE_NOW] Run focused regression, Release build, Unit, PostgreSQL Integration at `127.0.0.1:5433/iump_dev`, Web lint/build, repository policy/architecture/harness, Fast, and fresh Full checks; record exact PASS/FAIL/BLOCKED/NOT_RUN classifications without port 5432, Docker, package installation, substitutes, or secrets.
- [x] T177 [RUNNABLE_NOW] Perform the final Standards Review for T171-T176 covering ancestor delete-child, handle identity/security, effective access, path/reparse boundaries, process classification, redaction, fixture cleanup, and maintainability; resolve all Critical/High/actionable Medium findings.
- [x] T178 [RUNNABLE_NOW] Perform the final Specification Review for T171-T176 against Constitution 1.1.0, DOC-05, DOC-07, Feature 003 artifacts, acceptance criteria, task dependencies, and release gates; resolve all Critical/High/actionable Medium findings without calling self-review human approval.
- [x] T179 [RUNNABLE_NOW] Synchronize current post-merge truth in `spec.md`, `plan.md`, acceptance traceability, release checkpoint, final verification/review, decision log, and checkpoint artifacts with `main` `f0ed6cb8a2e8875415b737683aaebf4d3409d367`, merged corrective `22ba9164b64fed51e13ad47780afc4fb354185fb`, direct integration `YES`, PR/reviewer/CI evidence `NO`, AC-005/AC-011 `PARTIAL`, and Release-ready `NO`.
- [x] T180 [RUNNABLE_NOW] Run final direct artifact/code/task comparison (provider-native Analyze/Converge remains `NOT_RUN` when unavailable), verify T001-T181 unique IDs and no Phase 7/Spec 004/scope creep, create `checklists/final-handle-trust-review-checkpoint.md`, and record remaining external blockers.
- [x] T181 [RUNNABLE_NOW] Run `git diff --check`, inspect for secrets/private keys/real policy/certificates/manifests/raw descriptors/SIDs/temporary fixtures/bin/obj/unrelated work, commit `fix(feature-003): close final handle trust review`, push only `fix/003-final-handle-trust-review`, and stop without merge, force-push, tag, release, Phase 7, or Spec 004.
