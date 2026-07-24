# Tasks: Asset Simulator Latest

**Authoritative inputs**: `spec.md` -> `plan.md` -> `research.md` -> `data-model.md` ->
`contracts/` -> `quickstart.md` -> `CONTEXT.md` -> ADRs -> R0 repository -> DOC-04..DOC-08.

**Scope**: R1 / VS-01; 68 functional requirements, five user stories, nine success criteria,
Phase 0 governance plus ten implementation phases. Rules, Alerts, Reports, external REST/CSV,
Modbus, Edge Collector, write-back, and AI/ML remain excluded.

**Execution**: one phase per implementation invocation. Red tests precede green behavior.
PostgreSQL means approved PostgreSQL only; no substitute database or container. Every phase ends
with review, an evidence checkpoint, and a stop.

**Evidence states**: PASS = executed and verified; FAIL = executed and failed; BLOCKED = runnable
artifact produced where possible and exact external dependency/check ID/blocker ID/evidence
recorded, never passing; NOT_RUN = unattempted. A phase may progress incompletely only when every
RUNNABLE_NOW task passed, blockers are external/classified, no runnable dependent needs blocked
behavior, and the checkpoint says capability incomplete. FAIL or runnable NOT_RUN stops
progression. Full/release never passes with mandatory BLOCKED/NOT_RUN evidence.

## Phase 0: Governance and environment evidence

**Goal**: correct source precedence, record external evidence, analyze this repaired graph, then
process the governed constitution change. No green application-source task may begin until analysis
is clean, the approved amendment is applied, dependent guidance is synchronized, and final T012
permits progression.

- [ ] T001 [RUNNABLE_NOW] Correct DOC-05 to v0.2, DOC-07 to v0.2, and add DOC-08 v0.1 in `docs/source-register.md`; References: authoritative-input order, DOC-05/07/08; Depends: none; Verify: `rg -n "DOC-05.*v0.2|DOC-07.*v0.2|DOC-08.*v0.1" docs/source-register.md` finds all entries, expected PASS or FAIL.
- [ ] T002 [RUNNABLE_NOW] Record approved PostgreSQL endpoint/profile/access status in `docs/blocker-report.md`; References: ADR-003, ADR-015, plan environment classification; Depends: none; Verify: the documentation check itself is PASS when performed with exact evidence; PostgreSQL capability is separately PASS or BLOCKED_BY_DATABASE_ACCESS, with no substitute database.
- [ ] T003 [RUNNABLE_NOW] Record locked Npgsql/EF package-source/cache availability in `docs/blocker-report.md`; References: ADR-002, ADR-016; Depends: none; Verify: the documentation check itself is PASS when performed with exact evidence; package capability is separately PASS or BLOCKED_BY_PACKAGE_POLICY without public restore.
- [ ] T004 [RUNNABLE_NOW] Record `psql`, migration-runner, API/Worker smoke-tool availability in `docs/blocker-report.md`; References: repository harness; Depends: none; Verify: the documentation check itself is PASS when performed with exact evidence; each missing tool is separately PASS or BLOCKED_BY_MISSING_TOOL.
- [ ] T005 [RUNNABLE_NOW] Record Data Protection provisioning, company CI runner/template, and target-host approvals in `docs/blocker-report.md`; References: P-014, ADR-016, IAM contract; Depends: none; Verify: the documentation check itself is PASS when performed with exact evidence; each unavailable approval/capability is separately PASS or BLOCKED_BY_COMPANY_APPROVAL without key material or unapproved runner.
- [ ] T006 [RUNNABLE_NOW] Run `/speckit.analyze` on `specs/002-asset-simulator-latest/spec.md`, `plan.md`, and repaired `tasks.md`, recording findings in `specs/002-asset-simulator-latest/checklists/analysis.md`; References: repository workflow step 7; Depends: T001; Verify: command executes without depending on T002-T005, expected PASS when report is produced or FAIL on tool/content error.
- [ ] T007 [RUNNABLE_NOW] Resolve all Critical and High analysis findings only in `specs/002-asset-simulator-latest/tasks.md`; References: T006 findings; Depends: T006; Verify: each finding maps to an edited task/dependency or documented non-conflict, expected PASS or FAIL.
- [ ] T008 [RUNNABLE_NOW] Re-run `/speckit.analyze` and record zero unresolved Critical/High findings in `specs/002-asset-simulator-latest/checklists/analysis.md`; References: repository workflow gate; Depends: T007; Verify: zero Critical/High yields PASS, any remaining finding yields FAIL.
- [ ] T009 [RUNNABLE_NOW] Draft the active-feature/release-lifecycle constitution amendment and Sync Impact Report in `docs/decision-log.md`; References: constitution Governance, plan Constitution gate; Depends: T008; Verify: draft preserves principles/product boundary and lists affected templates/guidance, expected PASS or FAIL.
- [ ] T010 [BLOCKED_BY_COMPANY_APPROVAL] Obtain governed approval and apply the approved semantic-versioned amendment in `.specify/memory/constitution.md`; References: T009, constitution Governance; Depends: T009; Verify: approved diff yields PASS; absent approval is BLOCKED and no amendment is applied.
- [ ] T011 [RUNNABLE_NOW] Synchronize only Sync Impact Report-identified guidance in `.specify/templates/plan-template.md`, `.specify/templates/tasks-template.md`, and `docs/repository-harness.md`; References: T009-T010; Depends: T010; Verify: `git diff --check` and terminology review yield PASS/FAIL; if T010 is BLOCKED this remains NOT_RUN.
- [ ] T012 [RUNNABLE_NOW] Record the final Phase 0 review/checkpoint and stop in `specs/002-asset-simulator-latest/checklists/phase-00-governance.md`; References: Phase 0 gate, evidence semantics; Depends: T008,T009,T010,T011; Verify: record runnable PASS, FAIL, each BLOCKED classification, NOT_RUN, capability status, progression decision, and release blocker; T012 is NOT_RUN when T010 is BLOCKED_BY_COMPANY_APPROVAL or T011 is unavailable, progression remains NO, and T012 permits YES only when T008/T009/T010/T011 all pass.

## Phase 1: Minimal IAM and bootstrap

**Stories**: US1, US4, US5. **Independent test**: local identities, sessions, authorization,
Data Owner eligibility, deterministic bootstrap definitions, and no pre-Site scope work without a
database adapter.

- [ ] T013 [P] [US4] [RUNNABLE_NOW] Add failing User/Role/Scope/Capability/Session and Active User/Data Owner eligibility tests in `tests/Unit/IAM/IamDomainTests.cs`; References: FR-IAM-001..005, FR-DO-001..003; Depends: T008; Verify: expected FAIL before green and PASS after implementation.
- [ ] T014 [P] [US4] [RUNNABLE_NOW] Add failing Administrator-global, scoped-role, server-principal, and out-of-scope NOT_FOUND authorization tests in `tests/Unit/IAM/AuthorizationPolicyTests.cs`; References: FR-028..034, FR-IAM-004/005; Depends: T008; Verify: expected FAIL before implementation and PASS after.
- [ ] T015 [P] [US4] [RUNNABLE_NOW] Add failing authentication rate-limit and non-enumerating-error tests in `tests/Unit/Api/AuthSecurityPolicyTests.cs`; References: IAM contract; Depends: T008; Verify: expected FAIL for five-per-15-second/redaction behavior before green.
- [ ] T016 [P] [US1] [RUNNABLE_NOW] Add failing deterministic five-user/no-pre-Site-scope and post-Site fixture tests in `tests/Unit/IAM/PocIdentityFixtureTests.cs`; References: FR-IAM-006, P-019; Depends: T008; Verify: expected FAIL before definitions/fixture, later PASS.
- [ ] T017 [P] [US4] [RUNNABLE_NOW] Add failing session hashing, expiry, Disabled-user invalidation, logout, multiple-session, and revoke-all tests in `tests/Unit/IAM/SessionPolicyTests.cs`; References: P-014, IAM contract; Depends: T008; Verify: expected FAIL before green and no raw token in evidence.
- [ ] T018 [US4] [RUNNABLE_NOW] Capture Phase 1 red command/exit/assertion evidence in `specs/002-asset-simulator-latest/checklists/phase-01-red.md`; References: constitution test-first principle; Depends: T013,T014,T015,T016,T017; Verify: missing behavior is FAIL evidence, never PASS.
- [ ] T019 [US4] [RUNNABLE_NOW] Define `IIamCommandRepository` in `src/Modules/IAM/Contracts/IamPersistenceContracts.cs`; References: persistence-adapters contract; Depends: T012,T018; Verify: contract exposes users/roles/scopes/capabilities writes and transaction handle without SQL details, expected PASS/FAIL compile.
- [ ] T020 [US4] [RUNNABLE_NOW] Define `IIamPrincipalSessionRepository` in `src/Modules/IAM/Contracts/IamSessionContracts.cs`; References: persistence-adapters and IAM contracts; Depends: T012,T018; Verify: lookup/revoke/revoke-all surfaces compile, expected PASS/FAIL.
- [ ] T021 [P] [US4] [RUNNABLE_NOW] Implement deterministic IAM repository fakes in `tests/Unit/Fakes/FakeIamRepositories.cs`; References: T019-T020; Depends: T019,T020; Verify: fake contract tests pass with deterministic optimistic versions.
- [ ] T022 [US4] [RUNNABLE_NOW] Implement IAM domain, Active User/Data Owner eligibility, and five-role/capability policy in `src/Modules/IAM/Domain/IamModel.cs`; References: FR-IAM-001..005, FR-DO-001..003, P-017; Depends: T011,T018,T021; Verify: T013 passes, expected PASS/FAIL.
- [ ] T023 [US4] [RUNNABLE_NOW] Implement server-side caller resolution and authorization decisions in `src/Modules/IAM/Application/Authorization.cs`; References: FR-028..034, FR-IAM-004/005; Depends: T011,T014,T022; Verify: T014 passes with no client claims as authority for PASS or reports FAIL.
- [ ] T024 [US4] [RUNNABLE_NOW] Implement opaque-token session/authentication policy and revoke-all in `src/Modules/IAM/Application/SessionManager.cs`; References: P-014, IAM contract; Depends: T011,T017,T020,T022; Verify: T017 passes and persisted/logged values exclude raw tokens for PASS or reports FAIL.
- [ ] T025 [US4] [RUNNABLE_NOW] Implement non-enumerating authentication and framework rate-limit configuration in `src/Api/AuthSecurityOptions.cs`; References: IAM contract; Depends: T011,T015,T024; Verify: T015 passes, expected PASS/FAIL.
- [ ] T026 [US1] [RUNNABLE_NOW] Implement deterministic POC identity definitions and idempotent post-Site fixture command in `src/Modules/IAM/Application/PocIdentityFixture.cs`; References: FR-IAM-006, P-019; Depends: T011,T016,T019,T022,T023; Verify: T016 passes and no scope references a nonexistent Site for PASS or reports FAIL.
- [ ] T027 [US4] [RUNNABLE_NOW] Create reviewed migration source `database/migrations/0002_iam_foundation.sql`; References: migration order 0002, data model; Depends: T019,T020,T022,T024; Verify: SQL review covers users/roles/scopes/capabilities/sessions, indexes, no credentials; expected PASS/FAIL.
- [ ] T028 [US4] [RUNNABLE_NOW] Add PostgreSQL IAM adapter integration-test source in `tests/Integration/IAM/IamRepositoryTests.cs`; References: persistence-adapters contract, 0002; Depends: T019,T020,T027; Verify: test project/source compiles when approved packages exist; source review PASS/FAIL.
- [ ] T029 [US4] [BLOCKED_BY_PACKAGE_POLICY] Implement the Integration-tested PostgreSQL IAM adapters in `src/Modules/IAM/Infrastructure/PostgresIamRepositories.cs`; References: T019-T020,T028; Depends: T027,T028; Verify: approved locked packages compile and tests can run for PASS/FAIL, otherwise BLOCKED.
- [ ] T030 [US4] [BLOCKED_BY_PACKAGE_POLICY] Register IAM PostgreSQL adapters in `src/Api/Program.cs` and add required project references in `src/Api/IUMP.Api.csproj`; References: persistence-adapters contract; Depends: T029; Verify: API build and reachability yield PASS/FAIL, otherwise BLOCKED.
- [ ] T031 [US4] [BLOCKED_BY_DATABASE_ACCESS] Execute 0002 and IAM repository tests against approved PostgreSQL, recording `specs/002-asset-simulator-latest/checklists/migration-0002.md`; References: ADR-003/015; Depends: T027,T028,T029,T030; Verify: uniqueness, session hash lookup, revocation, rollback+outbox PASS/FAIL or exact BLOCKED.
- [ ] T032 [US4] [RUNNABLE_NOW] Add failing auth/login/logout/antiforgery/`/me` endpoint tests in `tests/Unit/Api/AuthEndpointTests.cs`; References: IAM HTTP contract; Depends: T024,T025; Verify: endpoint group is red before implementation and later PASS.
- [ ] T033 [US4] [RUNNABLE_NOW] Implement login/logout/antiforgery/`/me` endpoints without command-idempotency registry in `src/Api/AuthEndpoints.cs`; References: P-018, IAM HTTP contract; Depends: T011,T032; Verify: T032 passes and login/logout/query never invoke `ICommandIdempotencyStore` for PASS or reports FAIL.
- [ ] T034 [US4] [BLOCKED_BY_COMPANY_APPROVAL] Configure approved Data Protection/DPAPI storage in `src/Api/DataProtectionConfiguration.cs`; References: P-014,T005; Depends: T033; Verify: provisioned path behavior PASS/FAIL; absent approval BLOCKED without elevation/key disclosure.
- [ ] T035 [US4] [RUNNABLE_NOW] Refactor IAM seams and extend ownership checks in `tests/Verification/architecture.tests.ps1`; References: ADR-007, persistence-adapters; Depends: T022,T023,T024,T026,T033; Verify: Fast architecture PASS/FAIL and no consumer writes `iam`.
- [ ] T036 [US1] [RUNNABLE_NOW] Run Standards and Spec reviews for Phase 1 in `specs/002-asset-simulator-latest/checklists/phase-01-review.md`; References: US1/US4/US5; Depends: T035; Verify: Critical/High count zero for PASS, otherwise FAIL.
- [ ] T037 [US1] [RUNNABLE_NOW] Record Phase 1 checkpoint and stop in `specs/002-asset-simulator-latest/checklists/phase-01-iam.md`; References: Phase 1 independent test; Depends: T036; Verify: counts PASS/FAIL/BLOCKED by class/NOT_RUN, capability completeness, progression and release blockers; blocked T029-T031/T034 never count PASS.

## Phase 2: Catalog primitives

**Stories**: US1, US2, US4, US5. **Independent test**: Metric/Unit compatibility and Source/Mapping
lifecycle, authorization, events, seeds, overlap, and deletion policy pass with a deterministic fake.

- [ ] T038 [P] [US1] [RUNNABLE_NOW] Add failing Metric/Unit/compatibility and deterministic seed tests in `tests/Unit/Catalog/MetricUnitTests.cs`; References: FR-CAT-001..004; Depends: T037; Verify: expected FAIL before green, later PASS.
- [ ] T039 [P] [US2] [RUNNABLE_NOW] Add failing Source/Mapping lifecycle, overlap, and dependency-delete tests in `tests/Unit/Catalog/SourceMappingTests.cs`; References: FR-DS-001..004, SC-007/008; Depends: T037; Verify: expected FAIL before green, later PASS.
- [ ] T040 [P] [US4] [RUNNABLE_NOW] Add failing Catalog command authorization and owner-event payload/redaction tests in `tests/Unit/Catalog/CatalogCommandTests.cs`; References: FR-028/031/035/036, Catalog events; Depends: T037; Verify: expected FAIL before commands/events.
- [ ] T041 [US1] [RUNNABLE_NOW] Capture Phase 2 red evidence in `specs/002-asset-simulator-latest/checklists/phase-02-red.md`; References: test-first; Depends: T038,T039,T040; Verify: expected red FAIL evidence recorded.
- [ ] T042 [US1] [RUNNABLE_NOW] Define `ICatalogCommandRepository` in `src/Modules/Catalog/Contracts/CatalogPersistenceContracts.cs`; References: persistence-adapters; Depends: T041; Verify: contract covers Metric/Unit/Source/Mapping ownership and compiles for PASS or reports FAIL.
- [ ] T043 [US1] [RUNNABLE_NOW] Define `ICatalogEligibilityQueryRepository` and public eligibility snapshots in `src/Modules/Catalog/Contracts/CatalogEligibilityContracts.cs`; References: Catalog contract; Depends: T041; Verify: versioned fact surface compiles without leaking internals for PASS or reports FAIL.
- [ ] T044 [P] [US1] [RUNNABLE_NOW] Implement deterministic Catalog repository fakes in `tests/Unit/Fakes/FakeCatalogRepositories.cs`; References: T042-T043; Depends: T042,T043; Verify: fake tests PASS/FAIL for uniqueness/overlap/dependencies.
- [ ] T045 [US1] [RUNNABLE_NOW] Implement Metric/Unit/compatibility domain and seed definitions in `src/Modules/Catalog/Domain/MetricUnitModel.cs`; References: FR-CAT-001..004; Depends: T011,T038,T044; Verify: T038 passes for PASS or reports FAIL.
- [ ] T046 [US2] [RUNNABLE_NOW] Implement Source/Mapping lifecycle, effective-period overlap, and deletion decisions in `src/Modules/Catalog/Domain/SourceMappingModel.cs`; References: FR-DS-001..004, P-008/P-010; Depends: T011,T039,T044,T045; Verify: T039 passes for PASS or reports FAIL.
- [ ] T047 [US2] [RUNNABLE_NOW] Implement authorized Catalog commands and safe owner events in `src/Modules/Catalog/Application/CatalogCommands.cs`; References: FR-028/031/035/036, P-021; Depends: T011,T040,T042,T043,T046; Verify: T040 passes and payload construction is not claimed as Audit persistence for PASS or reports FAIL.
- [ ] T048 [US1] [RUNNABLE_NOW] Create reviewed `database/migrations/0003_catalog_foundation.sql`; References: migration order 0003; Depends: T042,T045,T046; Verify: SQL covers Metric/Unit/compatibility/Source ownership and indexes, expected PASS/FAIL.
- [ ] T049 [US2] [RUNNABLE_NOW] Add Catalog PostgreSQL adapter test source in `tests/Integration/Catalog/CatalogRepositoryTests.cs`; References: persistence-adapters, 0003; Depends: T042,T043,T048; Verify: source review/compile PASS/FAIL when packages available.
- [ ] T050 [US2] [BLOCKED_BY_PACKAGE_POLICY] Implement PostgreSQL Catalog adapters in `src/Modules/Catalog/Infrastructure/PostgresCatalogRepositories.cs`; References: T042-T043,T049; Depends: T048,T049; Verify: approved locked-package build/test PASS/FAIL or BLOCKED.
- [ ] T051 [US2] [BLOCKED_BY_PACKAGE_POLICY] Register Catalog adapters in `src/Api/Program.cs`, `src/Worker/Program.cs`, `src/Api/IUMP.Api.csproj`, and `src/Worker/IUMP.Worker.csproj`; References: persistence-adapters; Depends: T050; Verify: both hosts compile/reach adapters PASS/FAIL or BLOCKED.
- [ ] T052 [US2] [BLOCKED_BY_DATABASE_ACCESS] Execute 0003 and Catalog PostgreSQL tests, recording `specs/002-asset-simulator-latest/checklists/migration-0003.md`; References: ADR-003/015; Depends: T048,T049,T050,T051; Verify: constraints/overlap/dependency query/rollback+outbox PASS/FAIL or BLOCKED.
- [ ] T053 [US2] [RUNNABLE_NOW] Extend architecture ownership checks for Catalog in `tests/Verification/architecture.tests.ps1`; References: ADR-007; Depends: T045,T046,T047; Verify: Fast PASS/FAIL and no cross-schema write.
- [ ] T054 [US2] [RUNNABLE_NOW] Run Phase 2 Standards/Spec review in `specs/002-asset-simulator-latest/checklists/phase-02-review.md`; References: US1/US2; Depends: T053; Verify: zero Critical/High PASS, otherwise FAIL.
- [ ] T055 [US2] [RUNNABLE_NOW] Record Phase 2 checkpoint and stop in `specs/002-asset-simulator-latest/checklists/phase-02-catalog.md`; References: Phase 2 independent test; Depends: T054; Verify: runnable PASS count, FAIL count, BLOCKED count by classification, NOT_RUN count, capability status, progression decision, and release blocker are recorded for PASS or malformed evidence reports FAIL.

## Phase 3: Draft Organization hierarchy

**Stories**: US1, US4, US5. **Independent test**: authorized hierarchy commands and scope-filtered
queries enforce lifecycle, code, interval, no-cascade, terminal decommission, and post-Site fixture.

- [ ] T056 [P] [US1] [RUNNABLE_NOW] Add failing hierarchy/lifecycle/code/interval tests in `tests/Unit/Organization/HierarchyDomainTests.cs`; References: FR-001..007, FR-AP-001/002; Depends: T055; Verify: expected FAIL before green.
- [ ] T057 [P] [US1] [RUNNABLE_NOW] Add failing Asset/Point decommission, no-cascade, terminal-state tests in `tests/Unit/Organization/DecommissionTests.cs`; References: FR-DC-001..005, SC-009; Depends: T055; Verify: expected FAIL before green.
- [ ] T058 [P] [US4] [RUNNABLE_NOW] Add failing hierarchy command authorization and owner-event tests in `tests/Unit/Organization/HierarchyCommandTests.cs`; References: FR-028..034, FR-035; Depends: T055; Verify: expected FAIL before commands/events.
- [ ] T059 [P] [US4] [RUNNABLE_NOW] Add failing scope-filtered hierarchy query tests in `tests/Unit/Organization/HierarchyQueryTests.cs`; References: FR-029..034, SC-005; Depends: T055; Verify: expected FAIL and no out-of-scope counts/data.
- [ ] T060 [P] [US1] [RUNNABLE_NOW] Add failing post-Site scope/capability fixture wiring tests in `tests/Unit/Organization/PostSiteFixtureTests.cs`; References: FR-IAM-006, P-019; Depends: T055; Verify: expected FAIL before wiring.
- [ ] T061 [US1] [RUNNABLE_NOW] Capture Phase 3 red evidence in `specs/002-asset-simulator-latest/checklists/phase-03-red.md`; References: test-first; Depends: T056,T057,T058,T059,T060; Verify: red FAIL evidence recorded.
- [ ] T062 [US1] [RUNNABLE_NOW] Define `IOrganizationCommandRepository` in `src/Modules/Organization/Contracts/OrganizationPersistenceContracts.cs`; References: persistence-adapters; Depends: T061; Verify: Site/Area/Asset/Point/history/locking surface compiles for PASS or reports FAIL.
- [ ] T063 [US1] [RUNNABLE_NOW] Define `IOrganizationQueryRepository` and eligibility snapshots in `src/Modules/Organization/Contracts/OrganizationQueryContracts.cs`; References: Organization contract; Depends: T061; Verify: scope/filter/version surface compiles for PASS or reports FAIL.
- [ ] T064 [P] [US1] [RUNNABLE_NOW] Implement deterministic Organization repository fakes in `tests/Unit/Fakes/FakeOrganizationRepositories.cs`; References: T062-T063; Depends: T062,T063; Verify: deterministic hierarchy/lock tests PASS/FAIL.
- [ ] T065 [US1] [RUNNABLE_NOW] Implement hierarchy aggregates, lifecycle, code/interval rules in `src/Modules/Organization/Domain/Hierarchy.cs`; References: FR-001..007, FR-AP-001/002; Depends: T011,T056,T064; Verify: T056 passes for PASS or reports FAIL.
- [ ] T066 [US1] [RUNNABLE_NOW] Implement terminal decommission and no-cascade decisions in `src/Modules/Organization/Domain/DecommissionPolicy.cs`; References: FR-DC-001..005; Depends: T011,T057,T065; Verify: T057 passes for PASS or reports FAIL.
- [ ] T067 [US1] [RUNNABLE_NOW] Implement authorized hierarchy commands and versioned owner events in `src/Modules/Organization/Application/HierarchyCommands.cs`; References: FR-028/031/035, P-021; Depends: T011,T058,T062,T063,T065,T066; Verify: T058 passes for PASS or reports FAIL.
- [ ] T068 [US4] [RUNNABLE_NOW] Implement scope-filtered hierarchy queries in `src/Modules/Organization/Application/HierarchyQueries.cs`; References: FR-029..034; Depends: T011,T059,T063,T065; Verify: T059 passes with filtering before paging for PASS or reports FAIL.
- [ ] T069 [US1] [RUNNABLE_NOW] Wire the IAM post-Site fixture application service through Organization public contracts in `src/Modules/IAM/Application/PostSiteFixtureOrganizationAdapter.cs`; References: FR-IAM-006,P-019; Depends: T011,T026,T060,T063,T067; Verify: T060 passes and no HTTP endpoint or direct cross-schema SQL is introduced before Phase 9 for PASS or reports FAIL.
- [ ] T070 [US1] [RUNNABLE_NOW] Create reviewed `database/migrations/0004_organization_hierarchy.sql`; References: migration order 0004; Depends: T062,T063,T065,T066; Verify: hierarchy/history/index SQL review PASS/FAIL.
- [ ] T071 [US1] [RUNNABLE_NOW] Add Organization PostgreSQL adapter test source in `tests/Integration/Organization/OrganizationRepositoryTests.cs`; References: persistence-adapters,0004; Depends: T062,T063,T070; Verify: source review/compile PASS/FAIL when packages exist.
- [ ] T072 [US1] [BLOCKED_BY_PACKAGE_POLICY] Implement PostgreSQL Organization adapters in `src/Modules/Organization/Infrastructure/PostgresOrganizationRepositories.cs`; References: T062-T063,T071; Depends: T070,T071; Verify: approved package build/test PASS/FAIL or BLOCKED.
- [ ] T073 [US1] [BLOCKED_BY_PACKAGE_POLICY] Register Organization adapters in `src/Api/Program.cs`, `src/Worker/Program.cs`, `src/Api/IUMP.Api.csproj`, and `src/Worker/IUMP.Worker.csproj`; References: persistence-adapters; Depends: T072; Verify: host build/reachability PASS/FAIL or BLOCKED.
- [ ] T074 [US1] [BLOCKED_BY_DATABASE_ACCESS] Execute 0004 and Organization PostgreSQL tests, recording `specs/002-asset-simulator-latest/checklists/migration-0004.md`; References: ADR-003/015; Depends: T070,T071,T072,T073; Verify: uniqueness, locks, concurrent decommission, rollback+outbox PASS/FAIL or BLOCKED.
- [ ] T075 [US1] [RUNNABLE_NOW] Extend Organization ownership checks in `tests/Verification/architecture.tests.ps1`; References: ADR-007; Depends: T065,T066,T067,T068,T069; Verify: Fast PASS/FAIL and provider-only writes.
- [ ] T076 [US1] [RUNNABLE_NOW] Run Phase 3 Standards/Spec review in `specs/002-asset-simulator-latest/checklists/phase-03-review.md`; References: US1/US4/US5; Depends: T075; Verify: zero Critical/High PASS, otherwise FAIL.
- [ ] T077 [US1] [RUNNABLE_NOW] Record Phase 3 checkpoint and stop in `specs/002-asset-simulator-latest/checklists/phase-03-organization.md`; References: SC-001/005/009; Depends: T076; Verify: runnable PASS count, FAIL count, BLOCKED count by classification, NOT_RUN count, capability status, progression decision, and release blocker are recorded for PASS or malformed evidence reports FAIL.

## Phase 4: Simulator configuration and Mapping readiness

**Stories**: US1, US2, US5. **Independent test**: immutable configuration versions and Mapping
readiness support a Draft non-producing Point and emit safe owner events.

- [ ] T078 [P] [US2] [RUNNABLE_NOW] Add failing immutable configuration/head/version tests in `tests/Unit/Acquisition/ConfigurationTests.cs`; References: FR-008,P-011; Depends: T077; Verify: expected FAIL before green.
- [ ] T079 [P] [US2] [RUNNABLE_NOW] Add failing configuration command authorization/event tests in `tests/Unit/Acquisition/ConfigurationCommandTests.cs`; References: FR-028/031/037/038, P-021; Depends: T077; Verify: expected FAIL before green.
- [ ] T080 [P] [US2] [RUNNABLE_NOW] Add failing Mapping readiness/real Organization adapter tests in `tests/Unit/Catalog/MappingReadinessTests.cs`; References: FR-014..016, SC-007; Depends: T077; Verify: expected FAIL before integration.
- [ ] T081 [US2] [RUNNABLE_NOW] Capture Phase 4 red evidence in `specs/002-asset-simulator-latest/checklists/phase-04-red.md`; References: test-first; Depends: T078,T079,T080; Verify: expected red FAIL evidence.
- [ ] T082 [US2] [RUNNABLE_NOW] Define Acquisition configuration repository port in `src/Modules/Acquisition/Contracts/ConfigurationPersistenceContracts.cs`; References: persistence-adapters; Depends: T081; Verify: immutable head/version surface compiles for PASS or reports FAIL.
- [ ] T083 [US2] [RUNNABLE_NOW] Implement deterministic configuration repository fake in `tests/Unit/Fakes/FakeAcquisitionConfigurationRepository.cs`; References: T082; Depends: T082; Verify: deterministic version tests PASS/FAIL.
- [ ] T084 [US2] [RUNNABLE_NOW] Implement immutable configuration domain/application commands and owner events in `src/Modules/Acquisition/Application/SimulatorConfiguration.cs`; References: FR-008,P-011,P-021; Depends: T011,T078,T079,T083; Verify: T078-T079 pass.
- [ ] T085 [US2] [RUNNABLE_NOW] Implement real Organization readiness adapter for Catalog Mapping activation in `src/Modules/Catalog/Application/OrganizationPointReadinessAdapter.cs`; References: Catalog contract; Depends: T011,T063,T080; Verify: T080 passes and Draft Mapping remains non-producing for PASS or reports FAIL.
- [ ] T086 [US2] [RUNNABLE_NOW] Create reviewed `database/migrations/0005_acquisition_configuration.sql`; References: migration order 0005; Depends: T082,T084; Verify: immutable version constraints/indexes review PASS/FAIL.
- [ ] T087 [US2] [RUNNABLE_NOW] Create reviewed `database/migrations/0006_catalog_source_mapping.sql`; References: migration order 0006; Depends: T046,T070,T085; Verify: half-open overlap/exclusion and dependency order review PASS/FAIL.
- [ ] T088 [US2] [RUNNABLE_NOW] Add configuration PostgreSQL adapter test source in `tests/Integration/Acquisition/ConfigurationRepositoryTests.cs`; References: persistence-adapters,0005; Depends: T082,T086; Verify: source review/compile PASS/FAIL.
- [ ] T089 [US2] [BLOCKED_BY_PACKAGE_POLICY] Implement/register PostgreSQL configuration adapter in `src/Modules/Acquisition/Infrastructure/PostgresConfigurationRepository.cs`, `src/Api/Program.cs`, and `src/Api/IUMP.Api.csproj`; References: T082,T088; Depends: T086,T088; Verify: approved package build/test PASS/FAIL or BLOCKED.
- [ ] T090 [US2] [BLOCKED_BY_DATABASE_ACCESS] Execute 0005/0006 and configuration/Mapping readiness tests, recording `specs/002-asset-simulator-latest/checklists/migrations-0005-0006.md`; References: ADR-015; Depends: T086,T087,T088,T089,T052,T074; Verify: clean/order/immutability/SC-007 PASS/FAIL or BLOCKED.
- [ ] T091 [US2] [RUNNABLE_NOW] Extend Acquisition/Catalog readiness ownership checks in `tests/Verification/architecture.tests.ps1`; References: ADR-007; Depends: T084,T085; Verify: Fast PASS/FAIL.
- [ ] T092 [US2] [RUNNABLE_NOW] Run Phase 4 Standards/Spec review in `specs/002-asset-simulator-latest/checklists/phase-04-review.md`; References: US1/US2/US5; Depends: T091; Verify: zero Critical/High PASS, otherwise FAIL.
- [ ] T093 [US2] [RUNNABLE_NOW] Record Phase 4 checkpoint and stop in `specs/002-asset-simulator-latest/checklists/phase-04-configuration.md`; References: Phase 4 independent test; Depends: T092; Verify: runnable PASS count, FAIL count, BLOCKED count by classification, NOT_RUN count, capability status, progression decision, and release blocker are recorded for PASS or malformed evidence reports FAIL.

## Phase 5: Point activation and shared transaction

**Stories**: US1, US4, US5. **Independent test**: Point activation validates IAM, Organization, and
Catalog facts inside the global lock order and atomically stages an owner event/outbox write.

- [ ] T094 [P] [US1] [RUNNABLE_NOW] Add failing Point activation prerequisite/error tests in `tests/Unit/Organization/PointActivationTests.cs`; References: FR-005,FR-AP-003..005,FR-DO-001..003; Depends: T093; Verify: expected FAIL before green.
- [ ] T095 [P] [US1] [RUNNABLE_NOW] Add failing global lock-order/host-transaction/rollback tests in `tests/Unit/Organization/PointActivationTransactionTests.cs`; References: P-016; Depends: T093; Verify: expected FAIL before coordinator.
- [ ] T096 [P] [US5] [RUNNABLE_NOW] Add failing owner-event envelope/correlation/causation tests in `tests/Unit/Integration/OwnerEventEnvelopeTests.cs`; References: P-021, Integration contract; Depends: T093; Verify: expected FAIL before envelope/outbox port.
- [ ] T097 [US1] [RUNNABLE_NOW] Capture Phase 5 red evidence in `specs/002-asset-simulator-latest/checklists/phase-05-red.md`; References: test-first; Depends: T094,T095,T096; Verify: expected FAIL evidence.
- [ ] T098 [US5] [RUNNABLE_NOW] Define event envelope and `ITransactionalOutboxWriter` in `src/Modules/Integration/Contracts/OutboxContracts.cs`; References: Integration contract; Depends: T097; Verify: versioned envelope/transaction port compiles for PASS or reports FAIL.
- [ ] T099 [US5] [RUNNABLE_NOW] Implement deterministic transactional outbox fake in `tests/Unit/Fakes/FakeTransactionalOutboxWriter.cs`; References: T098; Depends: T098; Verify: rollback/one-event tests PASS/FAIL.
- [ ] T100 [US1] [RUNNABLE_NOW] Implement host transaction coordinator and global lock-order contract in `src/BuildingBlocks/Persistence/HostTransactionCoordinator.cs`; References: P-016; Depends: T011,T095,T099; Verify: T095 passes with Integration last for PASS or reports FAIL.
- [ ] T101 [US1] [RUNNABLE_NOW] Implement Point activation orchestration in `src/Modules/Organization/Application/ActivateMeasurementPoint.cs`; References: FR-005,FR-AP-003..005,FR-DO-001..003; Depends: T011,T094,T100,T022,T043,T063; Verify: T094 passes with specific failures for PASS or reports FAIL.
- [ ] T102 [US5] [RUNNABLE_NOW] Implement safe owner-event envelope creation for activation in `src/Modules/Organization/Application/OrganizationEvents.cs`; References: FR-035/038,P-021; Depends: T011,T096,T098,T101; Verify: T096 passes and event construction is not Audit completion for PASS or reports FAIL.
- [ ] T103 [US1] [RUNNABLE_NOW] Add PostgreSQL Point activation transaction-test source in `tests/Integration/Organization/PointActivationTransactionTests.cs`; References: P-016; Depends: T100,T101,T102; Verify: test source review/compile PASS/FAIL.
- [ ] T104 [US1] [BLOCKED_BY_DATABASE_ACCESS] Execute Point activation concurrency/rollback/outbox tests on approved PostgreSQL and record `specs/002-asset-simulator-latest/checklists/phase-05-postgresql.md`; References: P-016; Depends: T103,T029,T050,T072; Verify: one atomic commit and lock order PASS/FAIL or BLOCKED.
- [ ] T105 [US1] [RUNNABLE_NOW] Extend host transaction/ownership checks in `tests/Verification/architecture.tests.ps1`; References: P-022,ADR-007; Depends: T100,T101,T102; Verify: Fast PASS/FAIL and Integration last/no cross-schema SQL.
- [ ] T106 [US1] [RUNNABLE_NOW] Run Phase 5 Standards/Spec review in `specs/002-asset-simulator-latest/checklists/phase-05-review.md`; References: US1/US4/US5; Depends: T105; Verify: zero Critical/High PASS, otherwise FAIL.
- [ ] T107 [US1] [RUNNABLE_NOW] Record Phase 5 checkpoint and stop in `specs/002-asset-simulator-latest/checklists/phase-05-activation.md`; References: Phase 5 independent test; Depends: T106; Verify: runnable PASS count, FAIL count, BLOCKED count by classification, NOT_RUN count, capability status, progression decision, and release blocker are recorded for PASS or malformed evidence reports FAIL; T104 remains non-passing when BLOCKED.

## Phase 6: Simulator Run and Worker production

**Stories**: US2, US5. **Independent test**: literal generator vectors, Run controls, pinned state,
leases, existing-Pending-first dispatch, and attempt reservation/finalization are deterministic.

- [ ] T108 [P] [US2] [RUNNABLE_NOW] Add failing literal Constant/Normal/restart vector tests in `tests/Unit/Acquisition/DeterministicGeneratorVectorTests.cs`; References: P-012, simulator contract; Depends: T107; Verify: expected FAIL using literal outputs/state only.
- [ ] T109 [P] [US2] [RUNNABLE_NOW] Add failing Measurement UUIDv5 identity/canonical tuple tests in `tests/Unit/Acquisition/MeasurementIdentityTests.cs`; References: P-013,FR-017/018; Depends: T107; Verify: expected FAIL before identity implementation.
- [ ] T110 [P] [US2] [RUNNABLE_NOW] Add failing Run control/pinned Run-Point/restart tests in `tests/Unit/Acquisition/RunControlTests.cs`; References: FR-009/010/012/013,P-007; Depends: T107; Verify: expected FAIL before green.
- [ ] T111 [P] [US2] [RUNNABLE_NOW] Add failing existing-Pending-first Worker dispatch tests in `tests/Unit/Worker/ProductionDispatchTests.cs`; References: P-015; Depends: T107; Verify: expected FAIL proving generator/cursor/counter untouched on retry.
- [ ] T112 [P] [US2] [RUNNABLE_NOW] Add failing production-attempt reserve/finalize/idempotent-counter tests in `tests/Unit/Acquisition/ProductionAttemptTests.cs`; References: P-015, telemetry finalization contract; Depends: T107; Verify: expected FAIL before green.
- [ ] T113 [P] [US5] [RUNNABLE_NOW] Add failing Run/configuration owner-event tests in `tests/Unit/Acquisition/AcquisitionEventTests.cs`; References: FR-037/038,P-021; Depends: T107; Verify: expected FAIL before event implementation.
- [ ] T114 [US2] [RUNNABLE_NOW] Capture Phase 6 red evidence in `specs/002-asset-simulator-latest/checklists/phase-06-red.md`; References: test-first; Depends: T108,T109,T110,T111,T112,T113; Verify: expected red FAIL evidence.
- [ ] T115 [US2] [RUNNABLE_NOW] Define `IAcquisitionRunRepository` in `src/Modules/Acquisition/Contracts/RunPersistenceContracts.cs`; References: persistence-adapters; Depends: T114; Verify: Run/Run-Point/lease contract compiles for PASS or reports FAIL.
- [ ] T116 [US2] [RUNNABLE_NOW] Define `ISimulatorProductionAttemptRepository` in `src/Modules/Acquisition/Contracts/ProductionAttemptContracts.cs`; References: persistence-adapters; Depends: T114; Verify: Pending/load/finalize contract compiles for PASS or reports FAIL.
- [ ] T117 [P] [US2] [RUNNABLE_NOW] Implement deterministic Acquisition Run/attempt fakes in `tests/Unit/Fakes/FakeAcquisitionRunRepositories.cs`; References: T115-T116; Depends: T115,T116; Verify: lease/reserve/finalize fake tests PASS/FAIL.
- [ ] T118 [US2] [RUNNABLE_NOW] Implement IUMP-DETERMINISTIC-V1 and state serialization in `src/Modules/Acquisition/Domain/DeterministicGenerator.cs`; References: P-012; Depends: T011,T108; Verify: T108 passes exactly for PASS or reports FAIL.
- [ ] T119 [US2] [RUNNABLE_NOW] Implement stable Measurement identity derivation in `src/Modules/Acquisition/Domain/MeasurementIdentity.cs`; References: P-013,FR-017/018; Depends: T011,T109; Verify: T109 passes for PASS or reports FAIL.
- [ ] T120 [US2] [RUNNABLE_NOW] Implement Run controls, pinned Run-Point state, and owner events in `src/Modules/Acquisition/Application/RunCommands.cs`; References: FR-009/010/012/013,P-007,P-021; Depends: T011,T110,T113,T115,T117,T118,T119; Verify: T110/T113 pass.
- [ ] T121 [US2] [RUNNABLE_NOW] Implement production-attempt reservation/finalization service in `src/Modules/Acquisition/Application/ProductionAttemptService.cs`; References: P-015; Depends: T011,T112,T116,T117,T120; Verify: T112 passes with one final counter for PASS or reports FAIL.
- [ ] T122 [US2] [RUNNABLE_NOW] Implement existing-Pending-first Worker production loop in `src/Worker/SimulatorProductionWorker.cs`; References: P-015; Depends: T011,T111,T121; Verify: T111 passes for PASS or reports FAIL.
- [ ] T123 [US2] [RUNNABLE_NOW] Create reviewed `database/migrations/0007_acquisition_run.sql`; References: migration order 0007; Depends: T115,T116,T120,T121; Verify: Run/state/attempt/lease/unique identity SQL review PASS/FAIL.
- [ ] T124 [US2] [RUNNABLE_NOW] Add Acquisition PostgreSQL adapter test source in `tests/Integration/Acquisition/RunAttemptRepositoryTests.cs`; References: persistence-adapters,0007; Depends: T115,T116,T123; Verify: source review/compile PASS/FAIL.
- [ ] T125 [US2] [BLOCKED_BY_PACKAGE_POLICY] Implement PostgreSQL Run/attempt adapters in `src/Modules/Acquisition/Infrastructure/PostgresRunRepositories.cs`; References: T115-T116,T124; Depends: T123,T124; Verify: approved package build/test PASS/FAIL or BLOCKED.
- [ ] T126 [US2] [BLOCKED_BY_PACKAGE_POLICY] Register Acquisition adapters/Worker services in `src/Api/Program.cs`, `src/Worker/Program.cs`, `src/Api/IUMP.Api.csproj`, and `src/Worker/IUMP.Worker.csproj`; References: persistence-adapters; Depends: T089,T125,T122; Verify: both hosts build/reach services PASS/FAIL or BLOCKED.
- [ ] T127 [US2] [BLOCKED_BY_DATABASE_ACCESS] Execute 0007 and Run/attempt transaction/concurrency tests, recording `specs/002-asset-simulator-latest/checklists/migration-0007.md`; References: ADR-015; Depends: T123,T124,T125,T126; Verify: unique slot, lease reclaim, cursor/PRNG/counter atomicity PASS/FAIL or BLOCKED.
- [ ] T128 [US2] [RUNNABLE_NOW] Extend Acquisition/Worker ownership checks in `tests/Verification/architecture.tests.ps1`; References: P-022; Depends: T118,T119,T120,T121,T122; Verify: Fast PASS/FAIL.
- [ ] T129 [US2] [RUNNABLE_NOW] Run Phase 6 Standards/Spec review in `specs/002-asset-simulator-latest/checklists/phase-06-review.md`; References: US2/US5; Depends: T128; Verify: zero Critical/High PASS, otherwise FAIL.
- [ ] T130 [US2] [RUNNABLE_NOW] Record Phase 6 checkpoint and stop in `specs/002-asset-simulator-latest/checklists/phase-06-simulator.md`; References: Phase 6 independent test; Depends: T129; Verify: runnable PASS count, FAIL count, BLOCKED count by classification, NOT_RUN count, capability status, progression decision, and release blocker are recorded for PASS or malformed evidence reports FAIL.

## Phase 7: Canonical Telemetry ingestion

**Stories**: US2, US3, US5. **Independent test**: trusted canonical ingestion commits stable
Accepted/Rejected terminal results, exact Duplicate replay, raw Accepted data, and attempt
finalization without identity confusion.

- [ ] T131 [P] [US2] [RUNNABLE_NOW] Add failing terminal registry/fingerprint/exact replay tests in `tests/Unit/Telemetry/MeasurementIdentityRegistryTests.cs`; References: FR-017/018,P-015A; Depends: T130; Verify: expected FAIL before green.
- [ ] T132 [P] [US2] [RUNNABLE_NOW] Add failing Telemetry ingestion orchestration/validation/quality tests in `tests/Unit/Telemetry/IngestionOrchestrationTests.cs`; References: FR-019..021; Depends: T130; Verify: expected FAIL before orchestration.
- [ ] T133 [P] [US2] [RUNNABLE_NOW] Add failing Accepted+raw, Rejected-without-raw, and transaction/outbox tests in `tests/Unit/Telemetry/IngestionPersistenceContractTests.cs`; References: P-015A; Depends: T130; Verify: expected FAIL before repository orchestration.
- [ ] T134 [P] [US2] [RUNNABLE_NOW] Add failing Acquisition finalization-on-Accepted/Rejected/Duplicate tests in `tests/Unit/Acquisition/TelemetryFinalizationTests.cs`; References: telemetry finalization contract; Depends: T130; Verify: expected FAIL before integration.
- [ ] T135 [P] [US5] [RUNNABLE_NOW] Add failing Measurement owner-event payload tests in `tests/Unit/Telemetry/TelemetryEventTests.cs`; References: P-021,Telemetry events; Depends: T130; Verify: expected FAIL before events.
- [ ] T136 [US2] [RUNNABLE_NOW] Capture Phase 7 red evidence in `specs/002-asset-simulator-latest/checklists/phase-07-red.md`; References: test-first; Depends: T131,T132,T133,T134,T135; Verify: expected red FAIL evidence.
- [ ] T137 [US2] [RUNNABLE_NOW] Define `ITelemetryIngestionRepository` in `src/Modules/Telemetry/Contracts/TelemetryPersistenceContracts.cs`; References: persistence-adapters; Depends: T136; Verify: identity/raw/transaction result port compiles for PASS or reports FAIL.
- [ ] T138 [US3] [RUNNABLE_NOW] Define `ILatestProjectionRepository`, `ISourceHealthRepository`, and `ITelemetryQueryRepository` in `src/Modules/Telemetry/Contracts/TelemetryProjectionContracts.cs`; References: persistence-adapters; Depends: T136; Verify: projection/query ports compile for PASS or reports FAIL.
- [ ] T139 [P] [US2] [RUNNABLE_NOW] Implement deterministic Telemetry repository fakes in `tests/Unit/Fakes/FakeTelemetryRepositories.cs`; References: T137-T138; Depends: T137,T138; Verify: identity/raw/latest/status fake tests PASS/FAIL.
- [ ] T140 [US2] [RUNNABLE_NOW] Implement terminal identity/fingerprint/result domain in `src/Modules/Telemetry/Domain/MeasurementIdentityResult.cs`; References: P-015A; Depends: T011,T131,T139; Verify: T131 passes and differs from API command idempotency for PASS or reports FAIL.
- [ ] T141 [US2] [RUNNABLE_NOW] Implement canonical ingestion orchestration and quality policy in `src/Modules/Telemetry/Application/IngestMeasurement.cs`; References: FR-019..021,P-001/P-002; Depends: T011,T132,T137,T139,T140; Verify: T132 passes for PASS or reports FAIL.
- [ ] T142 [US2] [RUNNABLE_NOW] Implement accepted/rejected transaction and safe owner events in `src/Modules/Telemetry/Application/TelemetryPersistenceService.cs`; References: P-015A,P-021; Depends: T011,T133,T135,T098,T141; Verify: T133/T135 pass.
- [ ] T143 [US2] [RUNNABLE_NOW] Integrate Acquisition attempt finalization with stable Telemetry result in `src/Modules/Acquisition/Application/FinalizeTelemetryAttempt.cs`; References: P-015,Telemetry contract; Depends: T011,T134,T121,T142; Verify: T134 passes and Duplicate is not a counter for PASS or reports FAIL.
- [ ] T144 [US2] [RUNNABLE_NOW] Create reviewed `database/migrations/0008_telemetry_measurement.sql`; References: migration order 0008; Depends: T137,T140,T142; Verify: identity/raw constraints/immutability SQL review PASS/FAIL.
- [ ] T145 [US2] [RUNNABLE_NOW] Add Telemetry PostgreSQL adapter test source in `tests/Integration/Telemetry/TelemetryIngestionRepositoryTests.cs`; References: persistence-adapters,0008; Depends: T137,T138,T144; Verify: source review/compile PASS/FAIL.
- [ ] T146 [US2] [BLOCKED_BY_PACKAGE_POLICY] Implement PostgreSQL Telemetry ingestion adapter in `src/Modules/Telemetry/Infrastructure/PostgresTelemetryRepositories.cs`; References: T137-T138,T145; Depends: T144,T145; Verify: approved package build/test PASS/FAIL or BLOCKED.
- [ ] T147 [US2] [BLOCKED_BY_PACKAGE_POLICY] Register Telemetry adapters in `src/Api/Program.cs`, `src/Worker/Program.cs`, `src/Api/IUMP.Api.csproj`, and `src/Worker/IUMP.Worker.csproj`; References: persistence-adapters; Depends: T146; Verify: host build/reachability PASS/FAIL or BLOCKED.
- [ ] T148 [US2] [BLOCKED_BY_DATABASE_ACCESS] Execute 0008 and Telemetry transaction/concurrency tests, recording `specs/002-asset-simulator-latest/checklists/migration-0008.md`; References: ADR-015; Depends: T144,T145,T146,T147; Verify: terminal uniqueness, Accepted+raw atomicity, Rejected-without-raw, replay/conflict PASS/FAIL or BLOCKED.
- [ ] T149 [US2] [RUNNABLE_NOW] Extend Telemetry ownership/identity checks in `tests/Verification/architecture.tests.ps1`; References: P-022; Depends: T140,T141,T142,T143; Verify: Fast PASS/FAIL and no identity mechanism conflation.
- [ ] T150 [US2] [RUNNABLE_NOW] Run Phase 7 Standards/Spec review in `specs/002-asset-simulator-latest/checklists/phase-07-review.md`; References: US2/US3/US5; Depends: T149; Verify: zero Critical/High PASS, otherwise FAIL.
- [ ] T151 [US2] [RUNNABLE_NOW] Record Phase 7 checkpoint and stop in `specs/002-asset-simulator-latest/checklists/phase-07-telemetry.md`; References: FR-017..021; Depends: T150; Verify: runnable PASS count, FAIL count, BLOCKED count by classification, NOT_RUN count, capability status, progression decision, and release blocker are recorded for PASS or malformed evidence reports FAIL.

## Phase 8: Latest, Source Health, and Operations scheduling

**Stories**: US3, US5. **Independent test**: Latest never regresses, No Data is not zero, Source
Health recovers, and durable scheduling/leases/retries reconcile idempotently.

- [ ] T152 [P] [US3] [RUNNABLE_NOW] Add failing Latest eligibility/order/tie/concurrency decision tests in `tests/Unit/Telemetry/PointLatestTests.cs`; References: FR-022/023,P-001/P-003; Depends: T151; Verify: expected FAIL before green.
- [ ] T153 [P] [US3] [RUNNABLE_NOW] Add failing Source Health precedence/threshold/recovery tests in `tests/Unit/Telemetry/SourceHealthTests.cs`; References: FR-024..027,P-006,SC-004; Depends: T151; Verify: expected FAIL before green.
- [ ] T154 [P] [US3] [RUNNABLE_NOW] Add failing durable job schedule/claim/lease/retry/reconciliation tests in `tests/Unit/Operations/DurableJobTests.cs`; References: Operations contract; Depends: T151; Verify: expected FAIL before ports/green.
- [ ] T155 [US3] [RUNNABLE_NOW] Capture Phase 8 red evidence in `specs/002-asset-simulator-latest/checklists/phase-08-red.md`; References: test-first; Depends: T152,T153,T154; Verify: expected red FAIL evidence.
- [ ] T156 [US3] [RUNNABLE_NOW] Define `IDurableJobScheduler` in `src/Modules/Operations/Contracts/DurableJobContracts.cs`; References: persistence-adapters; Depends: T155; Verify: safe payload/idempotency scheduling port compiles for PASS or reports FAIL.
- [ ] T157 [US3] [RUNNABLE_NOW] Define `IJobClaimRepository` in `src/Modules/Operations/Contracts/JobClaimContracts.cs`; References: Operations contract; Depends: T155; Verify: claim/renew/complete/reschedule/Failed port compiles for PASS or reports FAIL.
- [ ] T158 [P] [US3] [RUNNABLE_NOW] Implement deterministic Operations job fakes in `tests/Unit/Fakes/FakeOperationsRepositories.cs`; References: T156-T157; Depends: T156,T157; Verify: deterministic lease/retry tests PASS/FAIL.
- [ ] T159 [US3] [RUNNABLE_NOW] Implement Latest policy/service in `src/Modules/Telemetry/Application/PointLatestService.cs`; References: FR-022/023,P-001/P-003; Depends: T011,T138,T139,T152; Verify: T152 passes for PASS or reports FAIL.
- [ ] T160 [US3] [RUNNABLE_NOW] Implement Source Health evaluation/recovery service in `src/Modules/Telemetry/Application/SourceHealthService.cs`; References: FR-024..027,P-006; Depends: T011,T138,T139,T153,T159; Verify: T153 passes and No Data is never Measurement/zero for PASS or reports FAIL.
- [ ] T161 [US3] [RUNNABLE_NOW] Implement durable health scheduling/reconciliation application service in `src/Modules/Operations/Application/SourceHealthJobs.cs`; References: Operations contract; Depends: T011,T154,T156,T157,T158,T160; Verify: T154 passes for 30-second leases/idempotent reconciliation for PASS or reports FAIL.
- [ ] T162 [US3] [RUNNABLE_NOW] Create reviewed `database/migrations/0009_telemetry_latest_status.sql`; References: migration order 0009; Depends: T138,T159,T160; Verify: Latest/status/index SQL review PASS/FAIL.
- [ ] T163 [US3] [RUNNABLE_NOW] Review the existing Operations migration source and add PostgreSQL job-adapter test source in `database/migrations/0001_r0_foundation.sql` and `tests/Integration/Operations/OperationsJobRepositoryTests.cs`; References: persistence-adapters,R0 job; Depends: T156,T157; Verify: source review/compile PASS/FAIL, existing job schema is sufficient, and no new/recreated job table is proposed.
- [ ] T164 [US3] [BLOCKED_BY_PACKAGE_POLICY] Implement PostgreSQL Operations job adapter in `src/Modules/Operations/Infrastructure/PostgresJobRepositories.cs`; References: T156-T157,T163; Depends: T163; Verify: approved package build/test PASS/FAIL or BLOCKED.
- [ ] T165 [US3] [BLOCKED_BY_PACKAGE_POLICY] Register Operations job adapter/health jobs in `src/Worker/Program.cs` and `src/Worker/IUMP.Worker.csproj`; References: persistence-adapters; Depends: T161,T164; Verify: Worker build/reachability PASS/FAIL or BLOCKED.
- [ ] T166 [US3] [BLOCKED_BY_DATABASE_ACCESS] Execute 0009, Latest concurrency, job lease/retry/recovery tests, recording `specs/002-asset-simulator-latest/checklists/migration-0009.md`; References: ADR-015; Depends: T162,T163,T164,T165,T146; Verify: Latest CAS/no regression, SKIP LOCKED, lease reclaim, retry PASS/FAIL or BLOCKED.
- [ ] T167 [US3] [RUNNABLE_NOW] Extend Telemetry/Operations ownership checks in `tests/Verification/architecture.tests.ps1`; References: P-022; Depends: T159,T160,T161; Verify: Fast PASS/FAIL.
- [ ] T168 [US3] [RUNNABLE_NOW] Run Phase 8 Standards/Spec review in `specs/002-asset-simulator-latest/checklists/phase-08-review.md`; References: US3/US5; Depends: T167; Verify: zero Critical/High PASS, otherwise FAIL.
- [ ] T169 [US3] [RUNNABLE_NOW] Record Phase 8 checkpoint and stop in `specs/002-asset-simulator-latest/checklists/phase-08-latest-health.md`; References: SC-003/004; Depends: T168; Verify: runnable PASS count, FAIL count, BLOCKED count by classification, NOT_RUN count, capability status, progression decision, and release blocker are recorded for PASS or malformed evidence reports FAIL.

## Phase 9: API command idempotency, delivery runtime, Audit, API, and Web

**Stories**: US1-US5. **Independent test**: all mutation endpoints use one durable command executor;
outbox/inbox/dispatcher/Audit completes the mandatory path; authorized queries and Web flows remain
scope-safe.

### Phase 9 red contracts

- [ ] T170 [P] [US4] [RUNNABLE_NOW] Add failing operation-code/fingerprint V1 tests in `tests/Unit/Integration/CommandFingerprintTests.cs`; References: P-020,Integration contract; Depends: T169; Verify: expected FAIL before green covers length-prefix UTF-8, UUID/string/integer/decimal/timestamp normalization, If-Match inclusion/exclusions, stable/changed hashes, and secrets.
- [ ] T171 [P] [US4] [RUNNABLE_NOW] Add failing Pending/Completed/result/lease/retention/error tests in `tests/Unit/Integration/CommandIdempotencyDomainTests.cs`; References: P-020; Depends: T169; Verify: expected FAIL for in-progress/conflict/24-hour policy before green.
- [ ] T172 [P] [US4] [RUNNABLE_NOW] Add failing idempotent-command executor tests in `tests/Unit/Integration/IdempotentCommandExecutorTests.cs`; References: P-020; Depends: T169; Verify: expected FAIL before green covers same/different key, live/expired Pending, crash boundaries, one mutation/outbox, exact response replay, login/logout/query exclusion.
- [ ] T173 [P] [US5] [RUNNABLE_NOW] Add failing outbox/inbox envelope/hash/claim/lease/retry/Failed tests in `tests/Unit/Integration/DeliveryRepositoryContractTests.cs`; References: P-021,Integration contract; Depends: T169; Verify: expected FAIL before ports/runtime.
- [ ] T174 [P] [US5] [RUNNABLE_NOW] Add failing dispatcher/consumer-resolution/restart/correlation tests in `tests/Unit/Worker/OutboxDispatcherTests.cs`; References: audit-events contract; Depends: T169; Verify: expected FAIL for at-least-once/all-required-consumers before green.
- [ ] T175 [P] [US5] [RUNNABLE_NOW] Add failing Audit event consumption/schema/redaction/append-idempotency tests in `tests/Unit/Audit/AuditConsumerTests.cs`; References: FR-035..039; Depends: T169; Verify: expected FAIL before consumer/repository.
- [ ] T176 [P] [US5] [RUNNABLE_NOW] Add failing Audit filtered query/global/scope/keyset tests in `tests/Unit/Audit/AuditQueryTests.cs`; References: FR-028/029/035..039; Depends: T169; Verify: expected FAIL before query service.
- [ ] T177 [P] [US5] [RUNNABLE_NOW] Add failing Operations delivery-reconciliation/replay/poison tests in `tests/Unit/Operations/AuditDeliveryJobsTests.cs`; References: Operations/audit contracts; Depends: T169; Verify: expected FAIL before green covers 30-second leases, 250ms/1s/2s/5s/30s-capped retry, 10 attempts, backlog, expired leases, Published-without-Audit, and identity-preserving replay.
- [ ] T178 [P] [US1] [RUNNABLE_NOW] Add failing IAM admin/post-Site fixture and hierarchy/Catalog/configuration mutation/query endpoint-group tests in `tests/Unit/Api/ConfigurationEndpointTests.cs`; References: IAM/Organization/Catalog/Simulator HTTP contracts; Depends: T169; Verify: expected FAIL and every mutation expects common executor.
- [ ] T179 [P] [US2] [RUNNABLE_NOW] Add failing Simulator mutation/query endpoint-group tests in `tests/Unit/Api/SimulatorEndpointTests.cs`; References: FR-009,P-018/P-020; Depends: T169; Verify: expected FAIL and mutations require executor.
- [ ] T180 [P] [US3] [RUNNABLE_NOW] Add failing Telemetry query endpoint-group tests in `tests/Unit/Api/TelemetryQueryEndpointTests.cs`; References: FR-023/027; Depends: T169; Verify: expected FAIL with scope-safe No Data representation.
- [ ] T181 [P] [US5] [RUNNABLE_NOW] Add failing Audit endpoint-group tests in `tests/Unit/Api/AuditEndpointTests.cs`; References: FR-035..039; Depends: T169; Verify: expected FAIL for Admin global or AUDIT_READ+scope.
- [ ] T182 [US1] [RUNNABLE_NOW] Capture all Phase 9 red evidence in `specs/002-asset-simulator-latest/checklists/phase-09-red.md`; References: test-first; Depends: T170,T171,T172,T173,T174,T175,T176,T177,T178,T179,T180,T181; Verify: exact expected FAIL evidence recorded before green.

### Integration command idempotency and delivery

- [ ] T183 [US5] [RUNNABLE_NOW] Create the ordered initial Audit migration source in `database/migrations/0010_audit_event.sql`; References: migration order 0010, audit contract; Depends: T182; Verify: initial unique source-event/append-only/index design review yields PASS or FAIL and precedes 0011 source.
- [ ] T184 [US4] [RUNNABLE_NOW] Implement command identity/result/state/policy, stable operation-code registry, and typed canonical fingerprint V1 in `src/Modules/Integration/Domain/CommandIdempotency.cs` and `src/Modules/Integration/Application/CommandFingerprintV1.cs`; References: P-020,Integration/README contracts; Depends: T011,T170,T171; Verify: T170-T171 pass and excluded security metadata has no effect.
- [ ] T185 [US4] [RUNNABLE_NOW] Define `ICommandIdempotencyStore` in `src/Modules/Integration/Contracts/CommandIdempotencyContracts.cs`; References: persistence-adapters; Depends: T182; Verify: register/read/reclaim/complete contract compiles for PASS or reports FAIL.
- [ ] T186 [US4] [RUNNABLE_NOW] Implement deterministic command-idempotency fake in `tests/Unit/Fakes/FakeCommandIdempotencyStore.cs`; References: T185; Depends: T185; Verify: concurrency/version tests PASS/FAIL without Audit migration, Integration migration, PostgreSQL, or package dependencies.
- [ ] T187 [US4] [RUNNABLE_NOW] Implement common idempotent-command executor/orchestrator in `src/Api/Infrastructure/IdempotentCommandExecutor.cs`; References: P-020; Depends: T011,T172,T184,T185,T186,T098,T100; Verify: T172 passes for exact replay/conflict/Pending/crash/one mutation+outbox for PASS or reports FAIL.
- [ ] T188 [US5] [RUNNABLE_NOW] Define `IOutboxClaimRepository` and `IInboxDeduplicationRepository` in `src/Modules/Integration/Contracts/DeliveryPersistenceContracts.cs`; References: Integration contract; Depends: T182; Verify: claim/renew/retry/complete/Failed/hash surface compiles for PASS or reports FAIL.
- [ ] T189 [US5] [RUNNABLE_NOW] Implement deterministic outbox/inbox fakes and consumer registry in `tests/Unit/Fakes/FakeIntegrationDeliveryRepositories.cs`; References: T188; Depends: T173,T188; Verify: T173 passes for deterministic repository transitions for PASS or reports FAIL.
- [ ] T190 [US4] [RUNNABLE_NOW] Create required additive `database/migrations/0011_r1_infrastructure_expand.sql`; References: migration design 0011; Depends: T183,T185,T188; Verify: creates `integration.command_idempotency`, adds nullable inbox recovery fields/indexes, and does not recreate outbox/inbox/job; SQL review PASS/FAIL.
- [ ] T191 [US4] [RUNNABLE_NOW] Add Integration PostgreSQL command/outbox/inbox adapter test source in `tests/Integration/Integration/IntegrationRepositoryTests.cs`; References: persistence-adapters,0011; Depends: T185,T188,T190; Verify: source covers constraints/reclaim/`FOR UPDATE SKIP LOCKED`/hash/retry and compiles for PASS or reports FAIL when packages exist.
- [ ] T192 [US4] [BLOCKED_BY_PACKAGE_POLICY] Implement PostgreSQL command-idempotency/outbox/inbox adapters in `src/Modules/Integration/Infrastructure/PostgresIntegrationRepositories.cs`; References: T185,T188,T191; Depends: T190,T191; Verify: approved package build/test PASS/FAIL or BLOCKED.
- [ ] T193 [US4] [BLOCKED_BY_PACKAGE_POLICY] Register Integration adapters and common executor in `src/Api/Program.cs`, `src/Worker/Program.cs`, `src/Api/IUMP.Api.csproj`, and `src/Worker/IUMP.Worker.csproj`; References: persistence-adapters; Depends: T187,T192; Verify: both composition roots resolve public ports PASS/FAIL or BLOCKED.

### Worker dispatcher, Audit, and Operations delivery

- [ ] T194 [US5] [RUNNABLE_NOW] Implement required-consumer registry/resolution in `src/Worker/Integration/RequiredConsumerRegistry.cs`; References: audit-events contract; Depends: T011,T174,T189; Verify: T174 consumer-resolution cases pass.
- [ ] T195 [US5] [RUNNABLE_NOW] Implement `OutboxDispatcherWorker` in `src/Worker/Integration/OutboxDispatcherWorker.cs`; References: P-021; Depends: T011,T174,T188,T194; Verify: T174 passes for lease/restart/at-least-once/Published-after-all/correlation for PASS or reports FAIL.
- [ ] T196 [US5] [RUNNABLE_NOW] Define `IAuditEventConsumer`, `IAuditAppendRepository`, and `IAuditQueryRepository` in `src/Modules/Audit/Contracts/AuditContracts.cs`; References: persistence-adapters,audit contract; Depends: T182; Verify: consumer/append/query ports compile for PASS or report FAIL.
- [ ] T197 [US5] [RUNNABLE_NOW] Implement deterministic Audit repository fakes in `tests/Unit/Fakes/FakeAuditRepositories.cs`; References: T196; Depends: T196; Verify: unique source event/filter/keyset fake tests PASS/FAIL.
- [ ] T198 [US5] [RUNNABLE_NOW] Implement Audit event consumer schema validation/mapping/redaction in `src/Modules/Audit/Application/AuditEventConsumer.cs`; References: FR-035..039; Depends: T011,T175,T196,T197; Verify: T175 passes and payload construction alone is not completion for PASS or reports FAIL.
- [ ] T199 [US5] [RUNNABLE_NOW] Implement Audit query service authorization/filtering in `src/Modules/Audit/Application/AuditQueryService.cs`; References: FR-028/029/035..039; Depends: T011,T176,T196,T197,T023; Verify: T176 passes for Admin global, AUDIT_READ+scope, unscoped restriction for PASS or reports FAIL.
- [ ] T200 [US5] [RUNNABLE_NOW] Finalize and review `database/migrations/0010_audit_event.sql` against the implemented Audit ports/consumer without changing its migration order; References: migration order 0010; Depends: T183,T196,T198; Verify: unique `source_event_id`, append-only grants/indexes/no restrictive target FK review PASS/FAIL.
- [ ] T201 [US5] [RUNNABLE_NOW] Add Audit PostgreSQL adapter test source in `tests/Integration/Audit/AuditRepositoryTests.cs`; References: persistence-adapters,0010; Depends: T196,T200; Verify: source covers append-if-absent/immutable/query scope and compiles for PASS or reports FAIL when packages exist.
- [ ] T202 [US5] [BLOCKED_BY_PACKAGE_POLICY] Implement PostgreSQL Audit append/query adapters in `src/Modules/Audit/Infrastructure/PostgresAuditRepositories.cs`; References: T196,T201; Depends: T200,T201; Verify: approved package build/test PASS/FAIL or BLOCKED.
- [ ] T203 [US5] [RUNNABLE_NOW] Implement Audit append plus inbox-completion host transaction in `src/Worker/Integration/AuditDeliveryHandler.cs`; References: P-021,persistence-adapters; Depends: T011,T175,T188,T195,T198; Verify: unit crash tests show Audit first, Integration last, and at most one row for PASS or report FAIL.
- [ ] T204 [US5] [RUNNABLE_NOW] Implement dispatcher/reconciliation wakeups, 30-second leases, specified capped retry schedule, 10-attempt poison/backlog policy, and operator replay in `src/Modules/Operations/Application/AuditDeliveryJobs.cs`; References: Operations contract; Depends: T011,T177,T156,T157,T161,T195; Verify: T177 passes with original event/correlation/causation retained for PASS or reports FAIL.
- [ ] T205 [US5] [BLOCKED_BY_PACKAGE_POLICY] Register Audit consumer/repositories, delivery handler, dispatcher, and Operations delivery jobs in `src/Api/Program.cs`, `src/Worker/Program.cs`, `src/Api/IUMP.Api.csproj`, and `src/Worker/IUMP.Worker.csproj`; References: persistence-adapters; Depends: T193,T198,T199,T202,T203,T204; Verify: approved package builds resolve API query and Worker runtime for PASS/FAIL, otherwise BLOCKED.
- [ ] T206 [US5] [BLOCKED_BY_DATABASE_ACCESS] Execute 0010/0011 Integration/Audit/Operations tests on approved PostgreSQL and record `specs/002-asset-simulator-latest/checklists/migrations-0010-0011.md`; References: ADR-015; Depends: T190,T191,T192,T193,T200,T201,T202,T203,T204,T205; Verify: constraints, SKIP LOCKED, leases, atomics, poison/replay PASS/FAIL or BLOCKED.

### API endpoint groups and Web

- [ ] T207 [US1] [RUNNABLE_NOW] Implement IAM admin/post-Site fixture and hierarchy/Catalog/configuration/activation mutation/query endpoints in `src/Api/ConfigurationEndpoints.cs`; References: US1,FR-028,FR-IAM-006,P-018/P-020; Depends: T011,T178,T187,T026,T069,T067,T068,T047,T084,T101; Verify: T178 passes and every mutation calls common executor for PASS or reports FAIL.
- [ ] T208 [US2] [RUNNABLE_NOW] Implement Simulator mutation/query endpoints in `src/Api/SimulatorEndpoints.cs`; References: US2,FR-009,P-018/P-020; Depends: T011,T179,T187,T120; Verify: T179 passes and every mutation calls common executor for PASS or reports FAIL.
- [ ] T209 [US3] [RUNNABLE_NOW] Implement Latest/Source Health query endpoints in `src/Api/TelemetryQueryEndpoints.cs`; References: US3,FR-023/027; Depends: T011,T180,T138,T159,T160; Verify: T180 passes and query does not use command registry for PASS or reports FAIL.
- [ ] T210 [US5] [RUNNABLE_NOW] Implement Audit query endpoint in `src/Api/AuditEndpoints.cs`; References: FR-035..039; Depends: T011,T181,T199; Verify: T181 passes and endpoint is not treated complete without consumer/runtime for PASS or reports FAIL.
- [ ] T211 [US1] [RUNNABLE_NOW] Add failing Web shell/auth/scope/feedback tests in `src/Web/src/test/app-shell.test.tsx`; References: DOC-08,US1/US4; Depends: T182; Verify: approved existing frontend test command yields expected FAIL before UI or BLOCKED_BY_PACKAGE_POLICY is recorded separately in T218.
- [ ] T212 [US1] [RUNNABLE_NOW] Implement Web application shell/auth/scope/feedback states in `src/Web/src/app/AppShell.tsx`; References: DOC-08; Depends: T011,T211,T033,T207; Verify: lint/build PASS/FAIL and red tests pass when executable.
- [ ] T213 [P] [US1] [RUNNABLE_NOW] Implement hierarchy/configuration UI in `src/Web/src/features/configuration/ConfigurationRoutes.tsx`; References: US1,DOC-08; Depends: T212,T207; Verify: lint/build PASS/FAIL with loading/empty/validation/conflict states.
- [ ] T214 [P] [US2] [RUNNABLE_NOW] Implement Simulator UI in `src/Web/src/features/simulator/SimulatorRoute.tsx`; References: US2,DOC-08; Depends: T212,T208; Verify: lint/build PASS/FAIL with Run controls/status/counters.
- [ ] T215 [P] [US3] [RUNNABLE_NOW] Implement Latest/Health UI in `src/Web/src/features/telemetry/PointCurrentRoute.tsx`; References: US3,DOC-08; Depends: T212,T209; Verify: lint/build PASS/FAIL and No Data is textual/nonzero.
- [ ] T216 [P] [US5] [RUNNABLE_NOW] Implement read-only Audit UI in `src/Web/src/features/audit/AuditRoute.tsx`; References: US5,DOC-08; Depends: T212,T210; Verify: lint/build PASS/FAIL with scope/redaction/keyset states.
- [ ] T217 [US1] [RUNNABLE_NOW] Implement responsive keyboard/accessibility styles in `src/Web/src/App.css`; References: DOC-08; Depends: T213,T214,T215,T216; Verify: lint/build plus keyboard/zoom/text-not-color review PASS/FAIL.
- [ ] T218 [US4] [BLOCKED_BY_PACKAGE_POLICY] Execute approved frontend behavior tests for `src/Web/src/test/`; References: DOC-08,US4; Depends: T211,T212,T213,T214,T215,T216,T217; Verify: approved locked test packages yield PASS/FAIL, otherwise BLOCKED.

### Phase 9 verification and checkpoint

- [ ] T219 [US4] [BLOCKED_BY_DATABASE_ACCESS] Execute command-idempotency API integration tests in `tests/Integration/Integration/CommandIdempotencyApiTests.cs`; References: P-020; Depends: T187,T190,T192,T193,T207,T208; Verify: exact status/body/Location/ETag/original correlation replay, conflict, concurrent duplicate, live/expired Pending, both crashes, one mutation/outbox, no secrets, 24-hour cleanup PASS/FAIL or BLOCKED.
- [ ] T220 [US5] [BLOCKED_BY_DATABASE_ACCESS] Execute end-to-end Audit delivery tests in `tests/Integration/Audit/AuditDeliveryTests.cs`; References: P-021,SC-006; Depends: T195,T198,T199,T202,T203,T204,T205,T206,T210; Verify: owner+outbox atomic, append+inbox atomic, both crashes, dedup/hash conflict, failed exhaustion, replay IDs, five-second visibility PASS/FAIL or BLOCKED.
- [ ] T221 [US1] [RUNNABLE_NOW] Refactor API/Worker/module seams and extend project-reference/ownership checks in `tests/Verification/architecture.tests.ps1`; References: P-022,ADR-007; Depends: T187,T195,T198,T199,T203,T204,T207,T208,T209,T210,T217; Verify: Fast PASS/FAIL for runnable source ownership/no cross-schema writes; blocked composition reachability remains explicitly incomplete under T205.
- [ ] T222 [US1] [RUNNABLE_NOW] Run Phase 9 Standards/Spec review in `specs/002-asset-simulator-latest/checklists/phase-09-review.md`; References: US1-US5; Depends: T221; Verify: zero Critical/High PASS, otherwise FAIL.
- [ ] T223 [US1] [RUNNABLE_NOW] Record Phase 9 checkpoint and stop in `specs/002-asset-simulator-latest/checklists/phase-09-api-audit-web.md`; References: SC-001..009; Depends: T222; Verify: runnable PASS count, FAIL count, BLOCKED count by classification, NOT_RUN count, capability status, progression decision, and release blocker are recorded for PASS or malformed evidence reports FAIL; blocked runtime/E2E never counts PASS.

## Phase 10: Acceptance hardening and release evidence

**Stories**: US1-US5. **Independent test**: all requirements and criteria map to implementation and
fresh evidence, including timed SC-001 and SC-002 journeys; mandatory blocked gates keep
Full/release non-passing.

- [ ] T224 [P] [US4] [RUNNABLE_NOW] Add role/scope/enumeration acceptance tests in `tests/Unit/Acceptance/AuthorizationNegativeTests.cs`; References: FR-028,FR-IAM-004/005,SC-005; Depends: T223; Verify: tests PASS/FAIL for safe 401/403/404.
- [ ] T225 [P] [US1] [RUNNABLE_NOW] Add lifecycle/deletion/decommission acceptance tests in `tests/Unit/Acceptance/LifecycleAcceptanceTests.cs`; References: SC-008/009; Depends: T223; Verify: tests PASS/FAIL for dependency rule/no cascade/audit-only deletion.
- [ ] T226 [P] [US2] [RUNNABLE_NOW] Add Mapping/activation/start race-test source in `tests/Integration/Acceptance/ConfigurationRaceTests.cs`; References: SC-007/009,P-016; Depends: T223; Verify: source review/compile PASS/FAIL.
- [ ] T227 [P] [US2] [RUNNABLE_NOW] Add Simulator/Telemetry crash-test source in `tests/Integration/Acceptance/SimulatorCrashRecoveryTests.cs`; References: P-015/P-015A,FR-018; Depends: T223; Verify: source review/compile PASS/FAIL.
- [ ] T228 [P] [US3] [RUNNABLE_NOW] Add Latest/Health race/restart-test source in `tests/Integration/Acceptance/LatestHealthRaceTests.cs`; References: SC-003/004; Depends: T223; Verify: source review/compile PASS/FAIL.
- [ ] T229 [P] [US5] [RUNNABLE_NOW] Add Audit/idempotency E2E acceptance-test source in `tests/Integration/Acceptance/AuditIdempotencyE2ETests.cs`; References: SC-006,P-020/P-021; Depends: T223; Verify: source review/compile PASS/FAIL.
- [ ] T230 [P] [US1] [RUNNABLE_NOW] Add deterministic timed-journey acceptance harness/source in `tests/Integration/Acceptance/TimedJourneyAcceptanceTests.cs`; References: SC-001/002; Depends: T223; Verify: source review/compile PASS/FAIL and captures a clean POC foundation with no root Site, authenticated Administrator creation of the Site and Engineer scope, Engineer Draft Area/Asset/Point/Source/configuration/Mapping preparation, top-down activation, SC-001 stop when the configured hierarchy is operational, and SC-002 stop when the first Accepted Measurement is visible through the supported Latest/API/UI journey.
- [ ] T231 [RUNNABLE_NOW] Create reviewed deterministic `database/migrations/0012_r1_idempotent_seeds.sql`; References: migration order 0012,P-019; Depends: T190,T200,T027,T048,T070,T086,T087,T123,T144,T162; Verify: fixed IDs/no credentials/no pre-Site scope/rerun review PASS/FAIL.
- [ ] T232 [RUNNABLE_NOW] Create reviewed `database/migrations/0013_r1_validation_reconciliation.sql`; References: migration order 0013; Depends: T231; Verify: read-only/reconciling owner/registry/Latest/Audit/delivery checks review PASS/FAIL.
- [ ] T233 [BLOCKED_BY_DATABASE_ACCESS] Execute ordered 0001-0013 clean/N-1 migrations and record `specs/002-asset-simulator-latest/checklists/migrations-full.md`; References: ADR-015; Depends: T232,T031,T052,T074,T090,T127,T148,T166,T206; Verify: checksum/order/constraints/forward-fix PASS/FAIL or BLOCKED.
- [ ] T234 [P] [BLOCKED_BY_MISSING_TOOL] Execute API/Worker/Web/quickstart smoke with required local tools and record `specs/002-asset-simulator-latest/checklists/quickstart-evidence.md`; References: quickstart; Depends: T223; Verify: observable PASS/FAIL or exact missing-tool BLOCKED.
- [ ] T235 [US1] [BLOCKED_BY_DATABASE_ACCESS] Execute the timed SC-001/SC-002 journeys and record `specs/002-asset-simulator-latest/checklists/sc-001-sc-002-timed-journeys.md`; References: SC-001/002,quickstart,T230,T233,T234,T005,T034; Depends: T230,T233,T234,T005,T034; Verify: execute only after the timed harness, reviewed 0012/0013 sources, ordered full migration evidence, quickstart smoke evidence, and approved Data Protection/runtime provisioning are PASS or truthfully BLOCKED; start SC-001 before root-Site creation and SC-002 at the successful Point activation result, record start/end/elapsed times, execute SC-001 without consulting documentation and require <=5 minutes, require SC-002 activation-to-first-Accepted-Measurement visibility <=2 minutes, and record PASS/FAIL/BLOCKED/NOT_RUN exactly; any prerequisite blocker is BLOCKED and never a timing PASS.
- [ ] T236 [P] [BLOCKED_BY_DATABASE_ACCESS] Execute T226-T229 race/crash/E2E suites on approved PostgreSQL; References: SC-003/004/006/007/009; Depends: T226,T227,T228,T229,T233; Verify: suite PASS/FAIL or exact BLOCKED, never substitute DB.
- [ ] T237 [P] [RUNNABLE_NOW] Extend correlation/causation/log-redaction verification in `tests/Verification/observability.tests.ps1`; References: ADR-011,P-021; Depends: T223; Verify: script PASS/FAIL and no secrets.
- [ ] T238 [RUNNABLE_NOW] Build 68-FR implementation-and-evidence traceability in `specs/002-asset-simulator-latest/checklists/requirements-traceability.md`; References: spec FR index,SC-001/002 timed task T235 state; Depends: T224,T225,T226,T227,T228,T229,T230,T231,T232,T237; Verify: 68/68 unique FRs each map to green task and evidence task; timed evidence state is recorded without requiring T235 to pass or complete, PASS/FAIL.
- [ ] T239 [RUNNABLE_NOW] Build five-story/nine-criterion traceability in `specs/002-asset-simulator-latest/checklists/acceptance-traceability.md`; References: US1-US5,SC-001..009,T235; Depends: T238; Verify: 5/5 and 9/9 with implementation/evidence mappings; record T235 as PASS, FAIL, BLOCKED, or NOT_RUN without requiring it to pass or complete, PASS/FAIL.
- [ ] T240 [RUNNABLE_NOW] Run Standards and Spec-compliance reviews and resolve every Critical/High finding in `docs/code-review.md`; References: repository workflow step 9; Depends: T239; Verify: zero unresolved Critical/High PASS, otherwise FAIL.
- [ ] T241 [RUNNABLE_NOW] Run `.\scripts\harness.ps1 -Mode Fast -Feature 002-asset-simulator-latest` and record `specs/002-asset-simulator-latest/checklists/phase-10-fast.md`; References: repository harness; Depends: T240; Verify: fresh command exit/output yields PASS or FAIL.
- [ ] T242 [BLOCKED_BY_DATABASE_ACCESS] Run Full harness database checks and record `specs/002-asset-simulator-latest/checklists/phase-10-full.md`; References: repository harness; Depends: T233,T236,T241; Verify: PASS/FAIL with approved DB or BLOCKED; Full remains non-passing while mandatory blocker exists.
- [ ] T243 [BLOCKED_BY_PACKAGE_POLICY] Run Full harness package-dependent checks in `specs/002-asset-simulator-latest/checklists/phase-10-full.md`; References: repository harness; Depends: T241; Verify: PASS/FAIL with approved packages or BLOCKED.
- [ ] T244 [BLOCKED_BY_MISSING_TOOL] Run Full harness tool-dependent smoke checks in `specs/002-asset-simulator-latest/checklists/phase-10-full.md`; References: repository harness; Depends: T241; Verify: PASS/FAIL with tools or BLOCKED.
- [ ] T245 [BLOCKED_BY_COMPANY_APPROVAL] Execute approved company CI/release evidence and update `docs/ci-readiness.md`; References: ADR-016; Depends: T241; Verify: approved runner evidence PASS/FAIL or BLOCKED; no public/container substitute.
- [ ] T246 [RUNNABLE_NOW] Run final architecture/repository-policy verification in `tests/Verification/architecture.tests.ps1` and `tests/Verification/repository-policy.tests.ps1`; References: constitution I/III/V; Depends: T240,T241; Verify: PASS/FAIL for ownership and excluded scope.
- [ ] T247 [RUNNABLE_NOW] Record Phase 10 final checkpoint and stop in `specs/002-asset-simulator-latest/checklists/phase-10-acceptance.md`; References: 68 FRs,5 stories,9 criteria,T235; Depends: T238,T239,T240,T241,T246; Verify: record PASS/FAIL/BLOCKED/NOT_RUN for every capability, explicitly inspect and record timed T235 state; PASS contributes SC-001/SC-002 evidence, BLOCKED or NOT_RUN keeps release readiness NO, FAIL blocks progression and release, and any mandatory blocker keeps release NO.

## Dependencies and execution order

```text
Phase 0 governance
  -> Phase 1 IAM/bootstrap
  -> Phase 2 Catalog
  -> Phase 3 Organization
  -> Phase 4 configuration/Mapping
  -> Phase 5 Point activation/host transaction
  -> Phase 6 Simulator Run/Worker
  -> Phase 7 Telemetry
  -> Phase 8 Latest/Health/Operations
  -> Phase 9 idempotency/delivery/Audit/API/Web
  -> Phase 10 acceptance/release evidence
```

- Green application-source tasks require final T012; red-test source may be authored after T008.
  T012 is the single final Phase 0 implementation gate: it permits progression only after T008,
  T009, T010, and T011 all pass. If T010 is blocked, T012 is NOT_RUN and green implementation is
  forbidden.
- Migration source order is T027 -> T048 -> T070 -> T086 -> T087 -> T123 -> T144 -> T162 ->
  T183/T200 -> T190 -> T231 -> T232, corresponding to 0002..0013.
- Checkpoint stops are T012,T037,T055,T077,T093,T107,T130,T151,T169,T223,T247.
- Blocked execution tasks are evidence leaves, not prerequisites for runnable reviews/checkpoints;
  checkpoints inspect their state and mark capability incomplete.

## User-story dependencies and independent tests

| Story | Primary phases | Dependency | Independent criterion |
|---|---|---|---|
| US1 Configure hierarchy | 1-5,9-10 | governance,IAM,Catalog | Admin bootstraps Site/scope; Engineer configures/activates valid hierarchy |
| US2 Operate Simulator | 2,4,6-7,9-10 | active Point | literal vectors, controls, Pending-first crash-safe ingestion |
| US3 Observe Latest/Health | 7-10 | Accepted Telemetry | scoped current value/quality/timestamps and correct health/recovery |
| US4 Enforce scope | 1,3,9-10 | IAM | every command/query is server-authorized with no enumeration |
| US5 Audit trail | 1-10 | owner events and delivery runtime | immutable redacted evidence reaches authorized query within five seconds |

## Parallel execution examples

- Phase 1 red tests T013-T017 are parallel; green converges after T018.
- Phase 6 red tests T108-T113 are parallel; generator/identity work T118-T119 uses separate files.
- Phase 9 red tests T170-T181 are parallel. Web feature tasks T213-T216 are parallel after T212.
- Phase 10 source tasks T224-T230 and observability T237 are parallel; blocked executions remain
  separate from runnable source creation. T235 is the separately classified timed-journey execution
  gate after migration and quickstart prerequisites; it cannot report timing PASS when prerequisites
  are blocked.

## Requirement and evidence coverage

| Requirement group | Count | Implementation tasks | Verification/evidence tasks |
|---|---:|---|---|
| FR-001..007, FR-AP-001..005, FR-DC-001..005 | 17 | T065-T069,T100-T102 | T056-T060,T094-T096,T225,T226 |
| FR-008..016 | 9 | T084-T085,T118-T122 | T078-T080,T108-T113,T226-T227 |
| FR-017..021 | 5 | T119,T140-T143 | T109,T131-T135,T227 |
| FR-022..027 | 6 | T159-T161 | T152-T154,T228 |
| FR-028..039 | 12 | T023,T047,T067-T068,T102,T187,T195,T198-T210 | T014,T040,T058-T059,T096,T172-T181,T219-T220,T224,T229 |
| FR-IAM-001..008 | 8 | T022-T026,T033 | T013-T017,T032 |
| FR-DS-001..004, FR-CAT-001..004 | 8 | T045-T047 | T038-T040,T225-T226 |
| FR-DO-001..003 | 3 | T022,T101 | T013,T094 |
| **Total** | **68** | **mapped** | **mapped** |

Specific coverage: FR-018 T109/T119/T131/T140/T148/T227; FR-028 and FR-IAM-004/005
T014/T023/T178-T181/T207-T210/T224; FR-035..039 T096/T102/T113/T135/T175-T176/T198-T210/T220;
SC-001 T230/T235; SC-002 T230/T235; SC-006 T174-T177/T195-T205/T220/T229; SC-007
T039/T080/T087/T090/T226/T236; SC-008 T039/T046/T225; SC-009 T057/T066/T225/T226/T236.

## Implementation strategy and readiness

1. Complete analysis before constitution work; do not make analysis depend on external approvals.
2. Apply red -> evidence -> minimal green -> refactor -> architecture verification -> Standards/Spec
   review -> checkpoint for each phase.
3. The smallest demonstrable increment is US1 through Phase 5; it is not a release.
4. Current coverage: FR 68/68, stories 5/5, success criteria 9/9.
5. Invalid dependencies: 0; dependency cycles: 0; unresolved dependencies: 0.
6. Ready for `/speckit.analyze`: YES.
7. Ready for constitution amendment: NO, until T006-T008 analysis is clean.
8. Ready for `/speckit.implement`: NO, until Phase 0 governance gate permits progression.
