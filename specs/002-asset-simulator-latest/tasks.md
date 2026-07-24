# Tasks: Asset Simulator Latest

**Input**: `spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`,
`CONTEXT.md`, accepted ADRs, the R0 implementation, and DOC-04 through DOC-08.

**Scope**: R1 / VS-01 only. These tasks implement 68 functional requirements, five user stories and
nine success criteria through the ten implementation phases defined in `plan.md`.

**Execution rule**: Run one phase per `/speckit.implement` invocation. At every phase checkpoint,
record PASS, FAIL and BLOCKED evidence, complete Standards and Spec reviews, and stop for approval
before the next phase. Every checkpoint executes
`.\scripts\harness.ps1 -Mode Fast -Feature 002-asset-simulator-latest` plus the exact
phase-specific test commands named by that phase; Phase 10 also executes Full mode.

**Test rule**: Executable behavior follows red -> green -> refactor -> review -> checkpoint.
PostgreSQL behavior uses approved PostgreSQL only; SQLite, EF Core InMemory, Testcontainers and
substitute databases are prohibited.

**Task format**: `[ID] [P?] [Story?] [environment] action; References; Depends; Verify`.

## Phase 0: Pre-implementation governance and environment gates

**Goal**: Establish current source precedence, amend the historical R0-only governance language,
record blockers truthfully, and pass `/speckit.analyze` before source implementation.

- [ ] T001 [RUNNABLE_NOW] Update DOC-05 to v0.2, DOC-07 to v0.2 and add DOC-08 v0.1 in `docs/source-register.md`; References: plan Constitution gate, DOC-05/07/08; Depends: none; Verify: `rg -n "DOC-05.*v0.2|DOC-07.*v0.2|DOC-08.*v0.1" docs/source-register.md` returns all three registrations.
- [ ] T002 [RUNNABLE_NOW] Draft the governed active-feature/release-lifecycle amendment and impact list in `docs/decision-log.md`, preserving product boundary, modular monolith, test-first evidence, restricted execution, PostgreSQL-only, no-Docker/no-public-download, operability and traceability; References: constitution I-VI and Governance, plan Constitution gate; Depends: T001; Verify: review records affected templates, ADRs, active feature and approval owners.
- [ ] T003 [REQUIRES_COMPANY_APPROVAL] Amend `.specify/memory/constitution.md` to replace R0-only/feature-001 implementation wording with the active-feature release lifecycle, bump the semantic version and update the Sync Impact Report without weakening principles; References: T002, constitution Governance; Depends: T002 and Product Owner/Tech Lead governance approval; Verify: `rg -n "002-asset-simulator-latest|Version|Sync Impact Report" .specify/memory/constitution.md` and a diff review show the approved scope only.
- [ ] T004 [P] [RUNNABLE_NOW] Synchronize constitution-dependent guidance in `.specify/templates/plan-template.md`, `.specify/templates/tasks-template.md` and `docs/repository-harness.md` only where the approved amendment requires it; References: T003 Sync Impact Report; Depends: T003; Verify: `git diff --check` and constitution/template terminology agree.
- [ ] T005 [P] [RUNNABLE_NOW] Record installed .NET/Node/package-cache and non-container restrictions in `docs/environment-inventory.md`; References: ADR-002, ADR-016, constitution V; Depends: none; Verify: inventory contains exact commands and no public restore/install action.
- [ ] T006 [BLOCKED_BY_ENVIRONMENT] Record approved PostgreSQL host/profile and `psql` absence or availability in `docs/database-access-request.md` and `docs/blocker-report.md`; References: ADR-003, ADR-015, plan Technical context; Depends: none; Verify: `Get-Command psql -ErrorAction SilentlyContinue` evidence is classified, never converted to PASS when absent.
- [ ] T007 [BLOCKED_BY_PACKAGE_POLICY] Record approved Npgsql/EF Core source and locked-version availability in `docs/blocker-report.md`; References: ADR-002, ADR-016; Depends: T005; Verify: configured package sources/cache are inspected without contacting a public source.
- [ ] T008 [P] [BLOCKED_BY_ENVIRONMENT] Record the pre-provisioned Data Protection directory decision and writable-service-account evidence in `docs/blocker-report.md`; References: plan P-014, `contracts/iam-authorization.md`; Depends: none; Verify: evidence contains no key material and unavailable storage remains BLOCKED_BY_ENVIRONMENT.
- [ ] T009 [P] [REQUIRES_COMPANY_APPROVAL] Record company CI runner/template and target-host approvals in `docs/ci-readiness.md` and `docs/blocker-report.md`; References: ADR-016, constitution V; Depends: none; Verify: no public action, container workflow or unapproved runner is proposed.
- [ ] T010 [RUNNABLE_NOW] Run `/speckit.analyze` against `specs/002-asset-simulator-latest/spec.md`, `plan.md` and `tasks.md`, resolve every Critical conflict in `tasks.md`, and record the result in `specs/002-asset-simulator-latest/checklists/analysis.md`; References: repository workflow step 7; Depends: T001-T009; Verify: analysis reports 0 unresolved Critical conflicts.
- [ ] T011 [RUNNABLE_NOW] Execute the governance checkpoint and stop in `specs/002-asset-simulator-latest/checklists/phase-00-governance.md`; References: constitution, 68/5/9 scope; Depends: T003 and T010; Verify: `.\scripts\harness.ps1 -Mode Fast -Feature 002-asset-simulator-latest`, architecture boundary, Standards review, Spec review, code-review checkpoint and blocker classifications are recorded before Phase 1.

**Hard gate**: No application-source implementation task below may start until T003 and T010 are
complete. Test-source tasks may be prepared, but green implementation is forbidden before this gate.

## Phase 1: Minimal IAM and bootstrap

**Stories**: US4 authorization foundation; US1 Administrator bootstrap; US5 authentication/audit.

**Independent test**: Administrator can authenticate and create the root Site; scoped identities are
resolved server-side; an Engineer without scope cannot create a root Site; logout, disable and
revoke-all invalidate the required sessions.

- [ ] T012 [P] [US4] [RUNNABLE_NOW] Add failing IAM domain tests for User Account status, five base roles, User Role, User Scope, Capability, User Capability and User Session in `tests/Unit/IAM/IamDomainTests.cs`; References: FR-IAM-001..008, `data-model.md`; Depends: T011; Verify: `dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj --no-restore` fails for missing IAM behavior.
- [ ] T013 [P] [US4] [RUNNABLE_NOW] Add failing authorization policy tests for Administrator global access, Engineer Site scope, Operator/Manager/Viewer reads and out-of-scope NOT_FOUND in `tests/Unit/IAM/AuthorizationPolicyTests.cs`; References: US4, FR-028..034, `contracts/iam-authorization.md`; Depends: T011; Verify: unit command fails with each named denial scenario.
- [ ] T014 [US4] [RUNNABLE_NOW] Capture Phase 1 red evidence, including exact failing assertions, in `specs/002-asset-simulator-latest/checklists/phase-01-red.md`; References: constitution IV; Depends: T012-T013; Verify: evidence includes command, exit code and failures, not a claimed PASS.
- [ ] T015 [P] [US4] [RUNNABLE_NOW] Implement IAM domain records, status transitions, five-role constants and `AUDIT_READ` capability in `src/Modules/IAM/Domain/IamModel.cs`; References: FR-IAM-001..005, plan P-017; Depends: T003, T010, T014; Verify: IAM domain tests compile and pass.
- [ ] T016 [P] [US4] [RUNNABLE_NOW] Implement `ICallerContext`, principal resolution, `IAuthorizationDecision` and scope-safe NOT_FOUND policy in `src/Modules/IAM/Application/Authorization.cs`; References: FR-028..034, `contracts/iam-authorization.md`; Depends: T014-T015; Verify: authorization policy tests pass without UI claims acting as authority.
- [ ] T017 [P] [US1] [RUNNABLE_NOW] Implement `IActiveUserEligibility` for Data Owner Active/scope checks in `src/Modules/IAM/Contracts/EligibilityContracts.cs`; References: FR-DO-001..003, plan synchronous contracts; Depends: T015; Verify: deterministic contract tests in `tests/Unit/IAM/ActiveUserEligibilityTests.cs` pass.
- [ ] T018 [P] [US4] [RUNNABLE_NOW] Add failing session tests for 256-bit opaque tokens, SHA-256 hash-only storage, multiple sessions, idle/absolute expiry, logout, Disabled user and revoke-all in `tests/Unit/IAM/SessionPolicyTests.cs`; References: plan P-014, IAM Session table; Depends: T011; Verify: unit command fails before session implementation.
- [ ] T019 [US4] [RUNNABLE_NOW] Implement token generation, hashing, session validation and invalidation policies in `src/Modules/IAM/Application/SessionManager.cs`; References: FR-IAM-001..008, plan P-014; Depends: T003, T010, T018; Verify: T018 tests pass and no raw token is persisted or logged.
- [ ] T020 [P] [US4] [RUNNABLE_NOW] Add failing login/logout/antiforgery/`/me` contract tests in `tests/Unit/Api/AuthContractTests.cs`; References: `contracts/iam-authorization.md`; Depends: T016 and T018; Verify: unit command records red endpoint-contract evidence.
- [ ] T021 [US4] [RUNNABLE_NOW] Implement ASP.NET Core PasswordHasher authentication, cookie session, antiforgery and `/api/v1/auth/*` plus `/api/v1/me` endpoints in `src/Api/AuthEndpoints.cs`; References: FR-IAM-001..008, plan P-014/P-018; Depends: T003, T010, T016 and T019-T020; Verify: contract tests pass using installed framework packages only.
- [ ] T022 [P] [US4] [RUNNABLE_NOW] Configure non-enumerating login errors and framework rate limiting at five failures per 15 seconds in `src/Api/AuthSecurityOptions.cs`; References: IAM contract Session defaults; Depends: T020; Verify: `tests/Unit/Api/AuthSecurityPolicyTests.cs` passes for threshold and redaction.
- [ ] T023 [P] [US4] [BLOCKED_BY_ENVIRONMENT] Configure pre-provisioned Data Protection directory/DPAPI behavior and explicit blocked readiness in `src/Api/DataProtectionConfiguration.cs`; References: plan P-014, T008; Depends: T008 and T021; Verify: unavailable directory produces BLOCKED_BY_ENVIRONMENT without elevation or key creation in the repository.
- [ ] T024 [US1] [RUNNABLE_NOW] Implement deterministic bootstrap Administrator and four non-scoped POC user definitions in `src/Modules/IAM/Application/PocIdentityDefinitions.cs`; References: plan P-019, research Bootstrap split; Depends: T015 and T019; Verify: `tests/Unit/IAM/PocIdentityDefinitionTests.cs` proves exactly five users and no pre-Site scope.
- [ ] T025 [US1] [RUNNABLE_NOW] Implement the idempotent post-Site scope/capability fixture command against an `ISiteScopeTarget` port in `src/Modules/IAM/Application/PostSitePocFixture.cs`; References: FR-IAM-006, `contracts/iam-authorization.md`; Depends: T016 and T024; Verify: deterministic port test rerun produces no duplicate scope/capability assignment, with Phase 3 wiring deferred explicitly to T054.
- [ ] T026 [US4] [RUNNABLE_NOW] Create ordered IAM migration source `database/migrations/0002_iam_foundation.sql` for accounts, roles, capabilities, scopes and sessions without credentials; References: data model, migration design 0002; Depends: T015-T019; Verify: SQL review confirms owner schema, constraints, indexes and no plaintext secret.
- [ ] T027 [US4] [REQUIRES_APPROVED_POSTGRESQL] Add IAM PostgreSQL migration/session repository tests in `tests/Integration/IAM/IamPersistenceTests.cs`; References: ADR-003/015, 0002; Depends: T006-T007 and T026; Verify: approved PostgreSQL run proves hashing, uniqueness, revocation and rollback; otherwise record BLOCKED.
- [ ] T028 [US4] [REQUIRES_APPROVED_POSTGRESQL] Execute 0002 through the approved migration harness and record clean/N-1/rollback evidence in `specs/002-asset-simulator-latest/checklists/migration-0002.md`; References: ADR-015; Depends: T027; Verify: approved PostgreSQL evidence only.
- [ ] T029 [US4] [RUNNABLE_NOW] Refactor IAM so callers use the public contracts rather than internal types and add dependency checks in `tests/Verification/architecture.tests.ps1`; References: constitution III, ADR-007; Depends: T015-T025; Verify: Fast architecture checks pass.
- [ ] T030 [US1] [RUNNABLE_NOW] Complete the Phase 1 review/checkpoint and stop in `specs/002-asset-simulator-latest/checklists/phase-01-iam.md`; References: US1/US4/US5 independent tests; Depends: T014-T029; Verify: unit/contract results, blocked evidence, architecture boundary, Standards/Spec reviews and code-review checkpoint are recorded before Phase 2.

## Phase 2: Catalog primitives

**Stories**: US1 Metric/Unit readiness; US2 Simulator Data Source and Source Mapping.

**Independent test**: Compatible Metric/Unit seeds are idempotent; Source/Mapping lifecycle and
overlap/delete policies work without activating a Point.

- [ ] T031 [P] [US1] [RUNNABLE_NOW] Add failing Metric, Unit and compatibility domain tests in `tests/Unit/Catalog/MetricUnitTests.cs`; References: FR-CAT-001..004; Depends: T030; Verify: unit command fails for compatibility/status invariants.
- [ ] T032 [P] [US2] [RUNNABLE_NOW] Add failing Data Source and Source Mapping lifecycle/effective-period tests in `tests/Unit/Catalog/SourceMappingTests.cs`; References: FR-DS-001..004, US2; Depends: T030; Verify: red evidence covers Draft/Active/Inactive/Superseded and half-open periods.
- [ ] T033 [P] [US2] [RUNNABLE_NOW] Add failing overlap and Draft-unused/dependent-history deletion tests in `tests/Unit/Catalog/MappingConflictTests.cs`; References: plan P-008/P-010; Depends: T030; Verify: overlapping Active periods and dependency deletion fail as specified.
- [ ] T034 [US1] [RUNNABLE_NOW] Record Phase 2 red evidence in `specs/002-asset-simulator-latest/checklists/phase-02-red.md`; References: constitution IV; Depends: T031-T033; Verify: exact command/exit/failures recorded.
- [ ] T035 [P] [US1] [RUNNABLE_NOW] Implement Metric, Unit and compatibility domain types in `src/Modules/Catalog/Domain/MetricUnitModel.cs`; References: FR-CAT-001..004, `data-model.md`; Depends: T003, T010, T034; Verify: T031 passes.
- [ ] T036 [P] [US2] [RUNNABLE_NOW] Implement Data Source lifecycle and safe credential-reference handling in `src/Modules/Catalog/Domain/DataSource.cs`; References: FR-DS-001..004, `contracts/catalog.md`; Depends: T034; Verify: lifecycle tests pass and secrets never appear in audit snapshots.
- [ ] T037 [US2] [RUNNABLE_NOW] Implement Source Mapping lifecycle, effective-period overlap decision and conditional deletion policy in `src/Modules/Catalog/Domain/SourcePointMapping.cs`; References: plan P-008/P-010; Depends: T035-T036; Verify: T032-T033 pass.
- [ ] T038 [P] [US1] [RUNNABLE_NOW] Implement idempotent Electric Power/kW and Electrical Energy/kWh definitions in `src/Modules/Catalog/Application/PocCatalogDefinitions.cs`; References: plan P-019, research seed split; Depends: T035; Verify: `tests/Unit/Catalog/PocCatalogDefinitionTests.cs` proves stable IDs and rerun idempotency.
- [ ] T039 [P] [US1] [RUNNABLE_NOW] Define `IMetricUnitCompatibility` and Catalog query DTOs in `src/Modules/Catalog/Contracts/MetricUnitContracts.cs`; References: plan synchronous contracts; Depends: T035; Verify: contract surface exposes facts, not Catalog internals.
- [ ] T040 [P] [US2] [RUNNABLE_NOW] Define Source/Mapping snapshot, readiness and dependency ports in `src/Modules/Catalog/Contracts/SourceMappingContracts.cs`; References: `contracts/catalog.md`, ADR-007; Depends: T036-T037; Verify: contract tests use a deterministic Organization adapter.
- [ ] T041 [US2] [RUNNABLE_NOW] Add authorization and outbox/audit event construction for Source/Mapping changes in `src/Modules/Catalog/Application/SourceMappingCommands.cs`; References: US5, plan asynchronous events; Depends: T016, T037, T040; Verify: `tests/Unit/Catalog/SourceMappingCommandTests.cs` passes scope and redaction cases.
- [ ] T042 [US1] [RUNNABLE_NOW] Create `database/migrations/0003_catalog_foundation.sql` for Metric, Unit, compatibility and Data Source; References: migration design 0003; Depends: T035-T038; Verify: SQL review confirms Catalog ownership and idempotent seed support.
- [ ] T043 [US2] [REQUIRES_APPROVED_POSTGRESQL] Add PostgreSQL compatibility, lifecycle and dependency tests in `tests/Integration/Catalog/CatalogPersistenceTests.cs`; References: 0003, ADR-003; Depends: T006-T007 and T042; Verify: approved PostgreSQL only or BLOCKED evidence.
- [ ] T044 [US2] [RUNNABLE_NOW] Refactor Catalog module depth and verify public-contract-only dependencies in `tests/Verification/architecture.tests.ps1`; References: constitution III, codebase-design; Depends: T035-T041; Verify: Fast architecture check passes.
- [ ] T045 [US2] [RUNNABLE_NOW] Complete the Phase 2 review/checkpoint and stop in `specs/002-asset-simulator-latest/checklists/phase-02-catalog.md`; References: US1/US2 independent checkpoint; Depends: T034-T044; Verify: red/green/refactor, architecture boundary, Standards/Spec reviews, code-review checkpoint and DB blocker evidence are recorded before Phase 3.

## Phase 3: Draft Organization hierarchy

**Story**: US1 hierarchy configuration; US5 lifecycle audit.

**Independent test**: Administrator creates/activates Site and assigns scope; Engineer creates Draft
Area, Asset and Point and activates valid parents top-down; invalid transitions, duplicates and
decommission dependencies are rejected without cascade.

- [ ] T046 [P] [US1] [RUNNABLE_NOW] Add failing Site/Area/Asset/Measurement Point creation, code-scope and Draft-child tests in `tests/Unit/Organization/HierarchyDomainTests.cs`; References: FR-001..007; Depends: T045; Verify: unit command fails for missing hierarchy model.
- [ ] T047 [P] [US1] [RUNNABLE_NOW] Add failing lifecycle matrix tests for every valid/invalid activation, inactivation and terminal decommission transition in `tests/Unit/Organization/LifecycleTests.cs`; References: FR-001..007, FR-DC-001..005; Depends: T045; Verify: red output names every transition.
- [ ] T048 [P] [US1] [RUNNABLE_NOW] Add failing interval, Data Owner reference and Point-code non-reuse tests in `tests/Unit/Organization/MeasurementPointTests.cs`; References: FR-004..007, FR-AP-001..005, FR-DO-001..003; Depends: T045; Verify: red output covers `expected_interval_seconds`, `no_data_after_seconds` and owner requirements.
- [ ] T049 [P] [US1] [RUNNABLE_NOW] Add failing Asset Active-child decommission/no-cascade tests in `tests/Unit/Organization/DecommissionTests.cs`; References: SC-009, plan P-009; Depends: T045; Verify: red evidence proves atomic `ACTIVE_CHILD_POINT`.
- [ ] T050 [US1] [RUNNABLE_NOW] Record Phase 3 red evidence in `specs/002-asset-simulator-latest/checklists/phase-03-red.md`; References: constitution IV; Depends: T046-T049; Verify: exact failing command and assertions recorded.
- [ ] T051 [P] [US1] [RUNNABLE_NOW] Implement Site, Area, Asset and Measurement Point aggregates/value objects in `src/Modules/Organization/Domain/Hierarchy.cs`; References: FR-001..007, `data-model.md`; Depends: T003, T010, T050; Verify: T046 and T048 pass.
- [ ] T052 [US1] [RUNNABLE_NOW] Implement lifecycle state machines, top-down activation, terminal decommission and no-cascade child guard in `src/Modules/Organization/Domain/LifecyclePolicy.cs`; References: FR-DC-001..005, plan P-009; Depends: T051; Verify: T047 and T049 pass.
- [ ] T053 [P] [US1] [RUNNABLE_NOW] Define hierarchy, Point eligibility and decommission dependency contracts in `src/Modules/Organization/Contracts/OrganizationContracts.cs`; References: plan synchronous contracts; Depends: T051-T052; Verify: interface exposes versioned snapshots and specific failure codes.
- [ ] T054 [US1] [RUNNABLE_NOW] Implement Administrator root-Site and scoped Engineer hierarchy commands with optimistic concurrency in `src/Modules/Organization/Application/HierarchyCommands.cs`; References: US1, FR-028..034; Depends: T016, T051-T053; Verify: `tests/Unit/Organization/HierarchyCommandTests.cs` passes role/scope/version cases.
- [ ] T055 [P] [US1] [RUNNABLE_NOW] Implement scope-filtered hierarchy queries in `src/Modules/Organization/Application/HierarchyQueries.cs`; References: US1/US4; Depends: T016, T051; Verify: out-of-scope lookup is indistinguishable NOT_FOUND.
- [ ] T056 [P] [US5] [RUNNABLE_NOW] Implement lifecycle-history and immutable Organization event payloads in `src/Modules/Organization/Application/OrganizationEvents.cs`; References: FR-035..039, US5; Depends: T051-T054; Verify: before/after snapshots omit secrets and retain actor/correlation.
- [ ] T057 [US1] [RUNNABLE_NOW] Create `database/migrations/0004_organization_hierarchy.sql` for hierarchy, Point and lifecycle-history constraints/indexes; References: migration design 0004; Depends: T051-T056; Verify: SQL review proves scoped uniqueness and Point code non-reuse.
- [ ] T058 [US1] [REQUIRES_APPROVED_POSTGRESQL] Add Organization PostgreSQL uniqueness/concurrency/lifecycle tests in `tests/Integration/Organization/OrganizationPersistenceTests.cs`; References: 0004; Depends: T006-T007 and T057; Verify: approved PostgreSQL transaction evidence or BLOCKED.
- [ ] T059 [US1] [RUNNABLE_NOW] Refactor Organization commands behind public contracts and extend module-boundary checks in `tests/Verification/architecture.tests.ps1`; References: ADR-007; Depends: T051-T056; Verify: Fast architecture check passes.
- [ ] T060 [US1] [RUNNABLE_NOW] Complete the Phase 3 review/checkpoint and stop in `specs/002-asset-simulator-latest/checklists/phase-03-organization.md`; References: US1 independent test, SC-009; Depends: T050-T059; Verify: domain/application checks, architecture boundary, Standards/Spec reviews, code-review checkpoint and DB blocker evidence are recorded before Phase 4.

## Phase 4: Simulator configuration and Mapping

**Story**: US2 configuration; US1 Point prerequisites; US5 audit.

**Independent test**: Create immutable Constant/Normal configuration and one effective Active
Mapping for a Draft Point; it is configuration-ready but produces nothing.

- [ ] T061 [P] [US2] [RUNNABLE_NOW] Add failing configuration-head/version and Constant/Normal validation tests in `tests/Unit/Acquisition/SimulatorConfigurationTests.cs`; References: FR-008..016, plan P-011/P-012; Depends: T060; Verify: unit command fails before configuration implementation.
- [ ] T062 [P] [US2] [RUNNABLE_NOW] Add failing Draft-Point Mapping/readiness and overlap contract tests in `tests/Unit/Catalog/SimulatorMappingTests.cs`; References: plan P-004/P-010; Depends: T060; Verify: red output proves Active Mapping does not imply production.
- [ ] T063 [US2] [RUNNABLE_NOW] Record Phase 4 red evidence in `specs/002-asset-simulator-latest/checklists/phase-04-red.md`; References: constitution IV; Depends: T061-T062; Verify: exact failures recorded.
- [ ] T064 [P] [US2] [RUNNABLE_NOW] Implement Simulator configuration aggregate head and immutable versions in `src/Modules/Acquisition/Domain/SimulatorConfiguration.cs`; References: plan P-011; Depends: T003, T010, T063; Verify: T061 version tests pass.
- [ ] T065 [P] [US2] [RUNNABLE_NOW] Implement Constant/Normal parameter and algorithm-identity validation without generation in `src/Modules/Acquisition/Domain/SimulatorConfigurationPolicy.cs`; References: `contracts/simulator.md`; Depends: T064; Verify: validation tests pass and no Worker production code exists in this phase.
- [ ] T066 [US2] [RUNNABLE_NOW] Implement Catalog-to-Organization readiness adapter and Draft-Point Mapping commands in `src/Modules/Catalog/Application/SimulatorMappingCommands.cs`; References: plan P-004/P-010, ADR-007; Depends: T040, T053, T062; Verify: deterministic adapter tests pass.
- [ ] T067 [P] [US2] [RUNNABLE_NOW] Define configuration/version and Simulator Mapping API contracts in `src/Modules/Acquisition/Contracts/SimulatorConfigurationContracts.cs`; References: `contracts/simulator.md`, `contracts/catalog.md`; Depends: T064-T066; Verify: contract snapshots include configuration and Mapping versions.
- [ ] T068 [P] [US5] [RUNNABLE_NOW] Add configuration/Mapping event and audit payload construction in `src/Modules/Acquisition/Application/SimulatorConfigurationEvents.cs`; References: US5, FR-035..039; Depends: T064-T067; Verify: unit tests prove immutable version/actor/correlation snapshots.
- [ ] T069 [US2] [RUNNABLE_NOW] Create `database/migrations/0005_acquisition_configuration.sql` for head/immutable versions; References: migration design 0005; Depends: T064-T065; Verify: SQL review forbids update/delete of referenced versions.
- [ ] T070 [US2] [RUNNABLE_NOW] Create `database/migrations/0006_catalog_source_mapping.sql` for effective periods and overlap protection after Point exists; References: migration design 0006; Depends: T057 and T066; Verify: ordered-source review confirms Catalog ownership and no premature Point FK creation.
- [ ] T071 [US2] [REQUIRES_APPROVED_POSTGRESQL] Add PostgreSQL configuration immutability and Mapping exclusion/unique-race tests in `tests/Integration/Acquisition/ConfigurationMappingPersistenceTests.cs`; References: 0005/0006; Depends: T006-T007, T069-T070; Verify: approved PostgreSQL only or BLOCKED.
- [ ] T072 [US2] [RUNNABLE_NOW] Refactor configuration and Mapping seams and verify no Acquisition ownership of Mapping in `tests/Verification/architecture.tests.ps1`; References: ADR-007, research Catalog ownership; Depends: T064-T068; Verify: Fast architecture check passes.
- [ ] T073 [US2] [RUNNABLE_NOW] Complete the Phase 4 review/checkpoint and stop in `specs/002-asset-simulator-latest/checklists/phase-04-configuration-mapping.md`; References: US2 independent test; Depends: T063-T072; Verify: red/green/refactor, architecture boundary, Standards/Spec reviews, code-review checkpoint and DB blocker evidence are recorded before Phase 5.

## Phase 5: Measurement Point activation

**Story**: US1 top-down activation.

**Independent test**: Activation succeeds only after IAM -> Organization -> Catalog checks in one
REPEATABLE READ transaction; each prerequisite failure rolls back with its specific code.

- [ ] T074 [P] [US1] [RUNNABLE_NOW] Add failing Point-activation application tests for owner, ancestors, intervals, Metric/Unit and exactly-one Mapping in `tests/Unit/Organization/PointActivationTests.cs`; References: FR-AP-001..005, FR-DO-001..003; Depends: T073; Verify: unit command fails for every prerequisite.
- [ ] T075 [P] [US1] [REQUIRES_APPROVED_POSTGRESQL] Add failing concurrent activation, owner-version mismatch and rollback tests in `tests/Integration/Organization/PointActivationTransactionTests.cs`; References: plan P-016, `contracts/README.md`; Depends: T006-T007 and T073; Verify: approved PostgreSQL red evidence or BLOCKED.
- [ ] T076 [US1] [RUNNABLE_NOW] Record runnable and blocked Phase 5 red evidence in `specs/002-asset-simulator-latest/checklists/phase-05-red.md`; References: constitution IV; Depends: T074-T075; Verify: domain failures and PostgreSQL blocker are distinguished.
- [ ] T077 [US1] [RUNNABLE_NOW] Implement Point activation orchestration with provider-owned IAM/Organization/Catalog adapters in `src/Modules/Organization/Application/ActivateMeasurementPoint.cs`; References: FR-AP-001..005, plan P-016; Depends: T003, T010, T017, T039-T040, T053 and T076; Verify: T074 passes.
- [ ] T078 [US1] [BLOCKED_BY_PACKAGE_POLICY] Implement the host-coordinated REPEATABLE READ unit-of-work adapter in `src/BuildingBlocks/Persistence/PostgresUnitOfWork.cs`; References: `contracts/README.md`; Depends: T007, T077; Verify: builds only with approved locked PostgreSQL provider.
- [ ] T079 [US1] [REQUIRES_APPROVED_POSTGRESQL] Implement and verify `SELECT FOR UPDATE` order IAM -> Organization -> Catalog -> Integration plus bounded conflict retries in `tests/Integration/Organization/PointActivationTransactionTests.cs`; References: plan P-016; Depends: T075, T078; Verify: concurrent PostgreSQL tests prove order, rollback and `TRANSIENT_DATABASE_CONFLICT`.
- [ ] T080 [US1] [RUNNABLE_NOW] Implement specific activation prerequisite errors and owner-version rollback mapping in `src/Modules/Organization/Application/PointActivationErrors.cs`; References: quickstart step 15, P-016; Depends: T077; Verify: T074 asserts each safe error code.
- [ ] T081 [P] [US5] [RUNNABLE_NOW] Implement atomic Point-activated outbox event construction in `src/Modules/Organization/Application/PointActivationEvents.cs`; References: FR-035..039, ADR-005; Depends: T077; Verify: unit test proves event created only for successful activation.
- [ ] T082 [US1] [RUNNABLE_NOW] Refactor activation orchestration to keep provider facts behind contracts and extend lock-order architecture checks in `tests/Verification/architecture.tests.ps1`; References: constitution III; Depends: T077-T081; Verify: Fast architecture check passes.
- [ ] T083 [US1] [RUNNABLE_NOW] Complete the Phase 5 review/checkpoint and stop in `specs/002-asset-simulator-latest/checklists/phase-05-point-activation.md`; References: US1 independent activation test; Depends: T076-T082; Verify: positive/negative/concurrent evidence, architecture boundary, Standards/Spec reviews, code-review checkpoint and blockers are recorded before Phase 6.

## Phase 6: Simulator Run and Worker

**Story**: US2 Simulator operation; US5 control audit.

**Independent test**: Literal vectors pass before Worker connection; Start/Pause/Resume/Stop,
zero-based sequence, durable attempt reservation and both crash-recovery points behave exactly as
`contracts/simulator.md`.

- [ ] T084 [US2] [RUNNABLE_NOW] Freeze the sole algorithm reference by adding a contract checksum/reference assertion in `tests/Unit/Acquisition/SimulatorContractReferenceTests.cs`; References: `contracts/simulator.md`; Depends: T083; Verify: test points to the normative contract and contains no alternate algorithm prose.
- [ ] T085 [P] [US2] [RUNNABLE_NOW] Add failing literal vector tests for Constant seq 0/output 12.5000/next 1 and Normal seq 0/output 11.6519/next 1 in `tests/Unit/Acquisition/SimulatorGoldenVectorTests.cs`; References: Simulator golden vectors; Depends: T084; Verify: unit command fails before generator implementation.
- [ ] T086 [P] [US2] [RUNNABLE_NOW] Add failing persisted-state restart vector test for Normal seq 1/output 17.9149/next 2 in `tests/Unit/Acquisition/SimulatorRestartVectorTests.cs`; References: Simulator vector 3; Depends: T084; Verify: red evidence includes exact state hex/spare.
- [ ] T087 [P] [US2] [RUNNABLE_NOW] Add failing sequence/Constant/no-PRNG and Pending-authoritative-retry tests in `tests/Unit/Acquisition/ProductionAttemptTests.cs`; References: plan P-007/P-015; Depends: T084; Verify: red assertions cover no generator call, no state/sequence/Generated change on retry.
- [ ] T088 [P] [US2] [RUNNABLE_NOW] Add failing Run lifecycle, pinned snapshot, owner-change and no-Mapping-replacement tests in `tests/Unit/Acquisition/SimulatorRunTests.cs`; References: US2, plan Phase 6; Depends: T084; Verify: red output covers Start/Pause/Resume/Stop/restart.
- [ ] T089 [US2] [RUNNABLE_NOW] Record Phase 6 red evidence in `specs/002-asset-simulator-latest/checklists/phase-06-red.md`; References: constitution IV; Depends: T085-T088; Verify: all literal inputs/outputs and failing assertions recorded.
- [ ] T090 [P] [US2] [RUNNABLE_NOW] Implement unsigned overflow helpers, UTF-8 canonical material and FNV-1a initialization in `src/Modules/Acquisition/Domain/DeterministicGeneratorInitialization.cs`; References: Simulator normative pseudocode; Depends: T003, T010, T089; Verify: initialization state equals contract hex.
- [ ] T091 [P] [US2] [RUNNABLE_NOW] Implement exact PCG transition/output and 25-byte little-endian serializer in `src/Modules/Acquisition/Domain/PcgState.cs`; References: Simulator PRNG/state serialization; Depends: T089; Verify: state/draw assertions in T085-T086 pass.
- [ ] T092 [US2] [RUNNABLE_NOW] Implement Box-Muller pair/spare, ties-to-even rounding and deterministic round-then-clamp in `src/Modules/Acquisition/Domain/DeterministicSimulatorGenerator.cs`; References: Simulator Normal conversion; Depends: T090-T091; Verify: all three literal vectors pass before Worker integration.
- [ ] T093 [P] [US2] [RUNNABLE_NOW] Implement UUIDv5 Measurement identity derivation in `src/Modules/Acquisition/Domain/SimulatorMeasurementIdentity.cs`; References: plan P-013, fixed namespace; Depends: T089; Verify: `tests/Unit/Acquisition/MeasurementIdentityTests.cs` passes canonical tuple/collision cases.
- [ ] T094 [US2] [RUNNABLE_NOW] Implement Run lifecycle, new-Start/reset versus Resume/continue and pinned Run-Point snapshots in `src/Modules/Acquisition/Domain/SimulatorRun.cs`; References: plan P-007/P-011; Depends: T092-T093; Verify: T088 lifecycle/snapshot tests pass.
- [ ] T095 [US2] [RUNNABLE_NOW] Implement new-slot reservation and Pending authoritative retry orchestration in `src/Modules/Acquisition/Application/ProduceSimulatorMeasurement.cs`; References: plan P-015, Simulator checkpoint; Depends: T087, T092-T094; Verify: T087 passes exactly-once sequence/Generated behavior.
- [ ] T096 [P] [US2] [RUNNABLE_NOW] Implement Run control application contracts/handlers in `src/Modules/Acquisition/Application/SimulatorRunCommands.cs`; References: US2 acceptance scenarios; Depends: T094; Verify: command tests pass transition/idempotency/version cases.
- [ ] T097 [P] [US5] [RUNNABLE_NOW] Implement Simulator control and stopped-on-owner-change event payloads in `src/Modules/Acquisition/Application/SimulatorRunEvents.cs`; References: US5, owner-state contract; Depends: T094-T096; Verify: audit event tests preserve actor/config/time/correlation.
- [ ] T098 [US2] [RUNNABLE_NOW] Create `database/migrations/0007_acquisition_run.sql` for Run, Run-Point state, 25-byte PRNG state, production attempts and leases; References: migration design 0007; Depends: T090-T097; Verify: SQL review proves sequence/attempt uniqueness and immutable Pending payload.
- [ ] T099 [US2] [REQUIRES_APPROVED_POSTGRESQL] Add lease, reservation, crash-point and exactly-once counter PostgreSQL tests in `tests/Integration/Acquisition/SimulatorRunPersistenceTests.cs`; References: 0007, Simulator crash recovery; Depends: T006-T007 and T098; Verify: approved PostgreSQL only or BLOCKED.
- [ ] T100 [US2] [BLOCKED_BY_PACKAGE_POLICY] Implement Acquisition PostgreSQL repositories and lease adapter in `src/Modules/Acquisition/Infrastructure/PostgresSimulatorStore.cs`; References: 0007, ADR-005; Depends: T007, T098; Verify: builds only from approved package source and passes T099 when DB available.
- [ ] T101 [US2] [RUNNABLE_NOW] Implement restart-safe Worker scheduling and existing-Pending-first dispatch against the T095 Acquisition persistence port in `src/Worker/SimulatorWorker.cs`; References: US2 restart, Simulator checkpoint; Depends: T092-T096; Verify: deterministic Worker tests prove no generator call on Pending retry.
- [ ] T102 [US2] [RUNNABLE_NOW] Refactor generator, Run and Worker seams and verify API/Worker/module dependency direction in `tests/Verification/architecture.tests.ps1`; References: ADR-001/004/007; Depends: T090-T101; Verify: Fast architecture check passes and literal vectors remain green.
- [ ] T103 [US2] [RUNNABLE_NOW] Complete the Phase 6 review/checkpoint and stop in `specs/002-asset-simulator-latest/checklists/phase-06-simulator-worker.md`; References: US2 independent test, SC-002; Depends: T089-T102; Verify: vectors, restart/counter evidence, architecture boundary, Standards/Spec reviews, code-review checkpoint and blockers are recorded before Phase 7.

## Phase 7: Canonical Telemetry

**Stories**: US2 ingestion result; US3 accepted Measurement/Latest input.

**Independent test**: Valid trusted identity receives one stable Accepted or Rejected terminal
result; exact Duplicate replay survives crash; same ID/different fingerprint conflicts.

- [ ] T104 [US2] [RUNNABLE_NOW] Freeze the request-fingerprint typed fields, order, UUID normalization, float64/canonical numeric representation, timestamp normalization, UTF-8 encoding and excluded retry metadata in `src/Modules/Telemetry/Contracts/MeasurementFingerprintV1.cs`; References: `contracts/telemetry.md`; Depends: T103; Verify: contract review has no unspecified field or encoding.
- [ ] T105 [P] [US2] [RUNNABLE_NOW] Add failing fingerprint tests for identical payload, excluded transport-only retry metadata and changed identity/value in `tests/Unit/Telemetry/MeasurementFingerprintTests.cs`; References: T104, Telemetry contract; Depends: T104; Verify: unit command fails before fingerprint implementation.
- [ ] T106 [P] [US2] [RUNNABLE_NOW] Add failing trusted-producer, UUID tuple recomputation, malformed-ID and fingerprint-conflict tests in `tests/Unit/Telemetry/TelemetryIdentityTests.cs`; References: FR-017..021; Depends: T104; Verify: red output distinguishes pre-reservation errors from terminal Rejected.
- [ ] T107 [P] [US2] [RUNNABLE_NOW] Add failing Accepted/Rejected terminal registry and exact Duplicate replay tests in `tests/Unit/Telemetry/TerminalResultTests.cs`; References: plan P-015A, Telemetry Result; Depends: T104; Verify: red output proves Rejected needs no raw Measurement.
- [ ] T108 [P] [US3] [RUNNABLE_NOW] Add failing quality/Latest eligibility tests for Good, Uncertain, Bad, `VALUE_OUT_OF_RANGE` and `SOURCE_TIMESTAMP_FUTURE` in `tests/Unit/Telemetry/QualityPolicyTests.cs`; References: P-001/P-002; Depends: T104; Verify: red assertions match contract.
- [ ] T109 [US2] [RUNNABLE_NOW] Record Phase 7 red evidence in `specs/002-asset-simulator-latest/checklists/phase-07-red.md`; References: constitution IV; Depends: T105-T108; Verify: exact failures recorded.
- [ ] T110 [P] [US2] [RUNNABLE_NOW] Implement SHA-256 request fingerprint V1 in `src/Modules/Telemetry/Domain/MeasurementFingerprint.cs`; References: T104; Depends: T003, T010, T105 and frozen contract; Verify: T105 passes.
- [ ] T111 [P] [US2] [RUNNABLE_NOW] Implement producer/identity validation and UUID tuple recomputation in `src/Modules/Telemetry/Application/TelemetryIdentityValidator.cs`; References: Telemetry validation steps 1-4; Depends: T093, T106; Verify: T106 passes.
- [ ] T112 [P] [US3] [RUNNABLE_NOW] Implement Data Quality classification and Latest eligibility in `src/Modules/Telemetry/Domain/MeasurementQualityPolicy.cs`; References: P-001/P-002; Depends: T108; Verify: T108 passes.
- [ ] T113 [US2] [RUNNABLE_NOW] Implement immutable terminal-result and Duplicate/idempotency decision model in `src/Modules/Telemetry/Domain/MeasurementTerminalResult.cs`; References: plan P-015A; Depends: T110-T112; Verify: T107 domain tests pass.
- [ ] T114 [US2] [RUNNABLE_NOW] Implement canonical ingestion orchestration and Organization -> Catalog -> Telemetry -> Integration provider flow in `src/Modules/Telemetry/Application/IngestMeasurement.cs`; References: Telemetry validation steps, P-016; Depends: T039-T040, T053, T111-T113; Verify: application tests pass Accepted, Rejected, Duplicate and conflict paths.
- [ ] T115 [US2] [RUNNABLE_NOW] Create `database/migrations/0008_telemetry_measurement.sql` for terminal registry/fingerprint constraints and Accepted-only raw Measurement partitions/indexes; References: migration design 0008; Depends: T104, T113-T114; Verify: SQL review enforces Accepted/Rejected check constraints and Run+Point+sequence uniqueness.
- [ ] T116 [US2] [REQUIRES_APPROVED_POSTGRESQL] Add atomic Accepted/raw, Rejected/no-raw and immutable Duplicate PostgreSQL tests in `tests/Integration/Telemetry/TerminalRegistryPersistenceTests.cs`; References: 0008; Depends: T006-T007 and T115; Verify: approved PostgreSQL only or BLOCKED.
- [ ] T117 [US2] [REQUIRES_APPROVED_POSTGRESQL] Add unique-race tests that roll back the aborted transaction and reload the winner in a new bounded transaction in `tests/Integration/Telemetry/TelemetryUniqueRaceTests.cs`; References: user race rule, Telemetry step 9; Depends: T116; Verify: no query runs inside an aborted transaction.
- [ ] T118 [US2] [BLOCKED_BY_PACKAGE_POLICY] Implement Telemetry terminal-registry/raw repository adapter in `src/Modules/Telemetry/Infrastructure/PostgresTelemetryStore.cs`; References: 0008, ADR-003; Depends: T007, T115; Verify: approved-provider build and T116-T117 when DB available.
- [ ] T119 [US2] [RUNNABLE_NOW] Implement stable Acquisition Pending finalization and exactly-once Accepted/Rejected counter application in `src/Modules/Acquisition/Application/FinalizeProductionAttempt.cs`; References: Telemetry Acquisition finalization; Depends: T095, T113-T114; Verify: `tests/Unit/Acquisition/ProductionAttemptFinalizationTests.cs` passes same-result no-op/different-result conflict.
- [ ] T120 [P] [US5] [RUNNABLE_NOW] Implement MeasurementAccepted/Rejected and PointLatestAdvanced outbox payloads in `src/Modules/Telemetry/Application/TelemetryEvents.cs`; References: Telemetry Events, ADR-005; Depends: T112-T114; Verify: event tests preserve IDs, quality/reason and lineage.
- [ ] T121 [US2] [RUNNABLE_NOW] Refactor Telemetry registry/ingestion behind public contracts and extend schema-ownership/lock-order checks in `tests/Verification/architecture.tests.ps1`; References: ADR-007; Depends: T110-T120; Verify: Fast architecture check passes.
- [ ] T122 [US2] [RUNNABLE_NOW] Complete the Phase 7 review/checkpoint and stop in `specs/002-asset-simulator-latest/checklists/phase-07-telemetry.md`; References: US2/US3 checkpoint; Depends: T109-T121; Verify: fingerprint, terminal-result/race evidence, architecture boundary, Standards/Spec reviews, code-review checkpoint and blockers are recorded before Phase 8.

## Phase 8: Latest and Source Health

**Story**: US3 observe latest Measurement and source health.

**Independent test**: Latest never regresses; Stale/No Data are derived without synthetic zero;
restart-safe evaluation emits events only on real transitions and recovers after accepted data.

- [ ] T123 [P] [US3] [RUNNABLE_NOW] Add failing Latest tuple-order and out-of-order-history tests in `tests/Unit/Telemetry/LatestOrderingTests.cs`; References: P-003, FR-022..027; Depends: T122; Verify: red output covers timestamp, sequence, processing time and Measurement ID.
- [ ] T124 [P] [US3] [RUNNABLE_NOW] Add failing Online/Stale/NoData/Suspended/Decommissioned precedence/default tests in `tests/Unit/Telemetry/SourceHealthTests.cs`; References: P-006, defaults 60/300; Depends: T122; Verify: red output proves No Data is not zero.
- [ ] T125 [P] [US3] [RUNNABLE_NOW] Add failing scheduled restart/reconciliation/real-transition-only event tests in `tests/Unit/Operations/SourceHealthJobTests.cs`; References: US3 restart/recovery; Depends: T122; Verify: red output covers persisted due time and recovery.
- [ ] T126 [US3] [RUNNABLE_NOW] Record Phase 8 red evidence in `specs/002-asset-simulator-latest/checklists/phase-08-red.md`; References: constitution IV; Depends: T123-T125; Verify: exact failures recorded.
- [ ] T127 [P] [US3] [RUNNABLE_NOW] Implement Latest ordering tuple and compare-and-set decision in `src/Modules/Telemetry/Domain/PointLatestPolicy.cs`; References: P-003; Depends: T003, T010, T123, T126; Verify: T123 passes.
- [ ] T128 [P] [US3] [RUNNABLE_NOW] Implement Source Health derivation/defaults/administrative precedence in `src/Modules/Telemetry/Domain/PointSourceHealthPolicy.cs`; References: P-006; Depends: T124, T126; Verify: T124 passes.
- [ ] T129 [US3] [RUNNABLE_NOW] Implement durable scheduled health evaluation and restart reconciliation in `src/Modules/Operations/Application/EvaluatePointSourceHealth.cs`; References: ADR-005, US3; Depends: T125, T128 and existing `operations.job`; Verify: T125 passes and repeated evaluation emits no duplicate event.
- [ ] T130 [P] [US3] [RUNNABLE_NOW] Implement source-health recovery on new Accepted Measurement in `src/Modules/Telemetry/Application/RecoverPointSourceHealth.cs`; References: US3 scenario 4; Depends: T112, T128; Verify: recovery test returns Online and advances Latest only when eligible.
- [ ] T131 [US3] [RUNNABLE_NOW] Create `database/migrations/0009_telemetry_latest_status.sql` for `point_latest`, `point_source_status` and due-time indexes; References: migration design 0009; Depends: T127-T130; Verify: SQL review keeps prior Latest distinct from current NoData.
- [ ] T132 [US3] [REQUIRES_APPROVED_POSTGRESQL] Add Latest CAS/concurrency and scheduler restart PostgreSQL tests in `tests/Integration/Telemetry/LatestHealthPersistenceTests.cs`; References: 0009; Depends: T006-T007 and T131; Verify: approved PostgreSQL only or BLOCKED.
- [ ] T133 [US3] [RUNNABLE_NOW] Refactor Latest/Health seams and verify Telemetry versus Operations ownership in `tests/Verification/architecture.tests.ps1`; References: ADR-005/007; Depends: T127-T132; Verify: Fast architecture check passes.
- [ ] T134 [US3] [RUNNABLE_NOW] Complete the Phase 8 review/checkpoint and stop in `specs/002-asset-simulator-latest/checklists/phase-08-latest-health.md`; References: US3 independent test, SC-003/004; Depends: T126-T133; Verify: ordering/health/restart evidence, architecture boundary, Standards/Spec reviews, code-review checkpoint and blockers are recorded before Phase 9.

## Phase 9: API and Web integration

**Stories**: US1-US5 through the Industrial Operations Console.

**Independent test**: The Administrator bootstrap and scoped Engineer journey configures and starts
the Simulator; Operator sees Latest/Health; authorized reviewer sees immutable Audit; all states are
scope-safe, responsive and keyboard-usable.

- [ ] T135 [P] [US1] [RUNNABLE_NOW] Add failing HTTP contract tests for hierarchy, Catalog, Mapping, configuration and Point activation routes in `tests/Unit/Api/ConfigurationEndpointTests.cs`; References: `contracts/organization.md`, `catalog.md`, `simulator.md`; Depends: T134; Verify: unit command records red route/result contracts.
- [ ] T136 [P] [US2] [RUNNABLE_NOW] Add failing HTTP contract tests for Simulator Start/Pause/Resume/Stop/status in `tests/Unit/Api/SimulatorEndpointTests.cs`; References: US2, P-018; Depends: T134; Verify: red output covers Idempotency-Key/If-Match semantics.
- [ ] T137 [P] [US3] [RUNNABLE_NOW] Add failing HTTP contract tests for Latest and Source Health queries in `tests/Unit/Api/TelemetryQueryEndpointTests.cs`; References: Telemetry Query surface; Depends: T134; Verify: red output includes quality/timestamps/NoData fields.
- [ ] T138 [P] [US5] [RUNNABLE_NOW] Add failing Audit query/capability/redaction tests in `tests/Unit/Api/AuditEndpointTests.cs`; References: US5, AUDIT_READ; Depends: T134; Verify: red output proves immutable scoped audit.
- [ ] T139 [US1] [RUNNABLE_NOW] Record API/Web red evidence in `specs/002-asset-simulator-latest/checklists/phase-09-red.md`; References: constitution IV; Depends: T135-T138; Verify: exact failures and unavailable frontend-test packages are classified.
- [ ] T140 [P] [US1] [RUNNABLE_NOW] Implement hierarchy/Catalog/configuration/activation endpoints in `src/Api/ConfigurationEndpoints.cs`; References: US1, contracts, P-018; Depends: T054-T055, T066-T067, T077 and T135; Verify: T135 passes with server-side authorization.
- [ ] T141 [P] [US2] [RUNNABLE_NOW] Implement Simulator run endpoints in `src/Api/SimulatorEndpoints.cs`; References: US2, Simulator HTTP surface; Depends: T096 and T136; Verify: T136 passes.
- [ ] T142 [P] [US3] [RUNNABLE_NOW] Implement Latest/Source Health endpoints in `src/Api/TelemetryQueryEndpoints.cs`; References: US3; Depends: T127-T130 and T137; Verify: T137 passes and NoData never serializes as numeric zero.
- [ ] T143 [P] [US5] [RUNNABLE_NOW] Implement capability-gated Audit query endpoint in `src/Api/AuditEndpoints.cs`; References: US5, FR-035..039; Depends: T016, T056, T097, T120 and T138; Verify: T138 passes safe scope/redaction cases.
- [ ] T144 [US5] [RUNNABLE_NOW] Create `database/migrations/0010_audit_event.sql` for append-only Audit storage/indexes/permissions; References: migration design 0010; Depends: T056, T097, T120, T143; Verify: SQL review prevents update/delete and stores safe snapshots only.
- [ ] T145 [US5] [REQUIRES_APPROVED_POSTGRESQL] Add append-only/scoped Audit PostgreSQL tests in `tests/Integration/Audit/AuditPersistenceTests.cs`; References: 0010; Depends: T006-T007 and T144; Verify: approved PostgreSQL only or BLOCKED.
- [ ] T146 [P] [US1] [RUNNABLE_NOW] Build the Industrial Light application shell, auth/session/scope context and safe route guards in `src/Web/src/app/AppShell.tsx`; References: DOC-08 UX-D01/D03, US1/US4; Depends: T021, T140; Verify: `npm --prefix src/Web run lint` and `npm --prefix src/Web run build`.
- [ ] T147 [P] [US1] [RUNNABLE_NOW] Implement Site/Area/Asset/Point configuration pages and Metric/Unit/Mapping forms in `src/Web/src/features/configuration/ConfigurationRoutes.tsx`; References: US1, DOC-08 section 19; Depends: T140 and T146; Verify: lint/build plus observable loading/empty/validation/conflict states.
- [ ] T148 [P] [US2] [RUNNABLE_NOW] Implement Simulator configuration/version and Start/Pause/Resume/Stop workspace in `src/Web/src/features/simulator/SimulatorRoute.tsx`; References: US2, DOC-08 section 22; Depends: T141 and T146; Verify: lint/build and UI shows run status/counters/errors without pretending to be equipment.
- [ ] T149 [P] [US3] [RUNNABLE_NOW] Implement Point Latest/Source Health view with value, unit, source/received timestamps, quality/reason and previous-Latest-versus-NoData presentation in `src/Web/src/features/telemetry/PointCurrentRoute.tsx`; References: US3, DOC-08 section 14/18; Depends: T142 and T146; Verify: lint/build and no history chart/aggregate is added.
- [ ] T150 [P] [US5] [RUNNABLE_NOW] Implement read-only AuditReview route/diff/redaction states in `src/Web/src/features/audit/AuditRoute.tsx`; References: US5, DOC-08 section 23; Depends: T143 and T146; Verify: lint/build and unauthorized action is absent while direct-route denial stays safe.
- [ ] T151 [P] [US1] [RUNNABLE_NOW] Implement reusable loading, skeleton, empty, blocked, forbidden/not-found, validation, conflict and transient-database states in `src/Web/src/components/feedback/FeedbackStates.tsx`; References: DOC-08 sections 13/25; Depends: T146; Verify: lint/build and each named state has text, not color alone.
- [ ] T152 [US1] [RUNNABLE_NOW] Implement desktop-first responsive layout, visible keyboard focus, skip link, labels/error associations and quality text/icon semantics in `src/Web/src/App.css`; References: DOC-08 sections 10/12/14; Depends: T146-T151; Verify: lint/build plus documented keyboard/zoom/contrast review; no dark-mode commitment.
- [ ] T153 [P] [US4] [BLOCKED_BY_PACKAGE_POLICY] Add frontend behavior tests for auth, scope-safe routing and error states in `src/Web/src/test/auth-scope.test.tsx`; References: US4, DOC-08 accessibility; Depends: approved locked test packages and T146-T152; Verify: run only the approved test command or record BLOCKED_BY_PACKAGE_POLICY.
- [ ] T154 [US1] [RUNNABLE_NOW] Refactor API endpoint registration and Web feature ownership, then extend architecture checks in `tests/Verification/architecture.tests.ps1`; References: ADR-001/004/007; Depends: T140-T153; Verify: Fast architecture, Web lint and Web build pass.
- [ ] T155 [US1] [RUNNABLE_NOW] Complete the Phase 9 review/checkpoint and stop in `specs/002-asset-simulator-latest/checklists/phase-09-api-web.md`; References: SC-001..009 UI/API portions; Depends: T139-T154; Verify: API/Web evidence, accessibility and architecture reviews, Standards/Spec reviews, code-review checkpoint and blockers are recorded before Phase 10.

## Phase 10: Acceptance hardening and release evidence

**Stories**: US1-US5 complete acceptance and traceability.

**Independent test**: Every FR, story and success criterion maps to executable evidence or an honest
blocker; restricted-environment Full harness remains non-passing while mandatory blockers remain.

- [ ] T156 [P] [US4] [RUNNABLE_NOW] Add role/scope negative and object-ID enumeration tests in `tests/Unit/Acceptance/AuthorizationNegativeTests.cs`; References: US4, SC-005; Depends: T155; Verify: unit command passes safe 401/403/404 behavior without target leakage.
- [ ] T157 [P] [US1] [RUNNABLE_NOW] Add lifecycle/decommission and audit-completeness acceptance tests in `tests/Unit/Acceptance/LifecycleAuditTests.cs`; References: US1/US5, SC-006/008/009; Depends: T155; Verify: unit command covers no cascade and terminal state.
- [ ] T158 [P] [US2] [REQUIRES_APPROVED_POSTGRESQL] Add concurrent Mapping activation, Point activation and Simulator Start-versus-decommission tests in `tests/Integration/Acceptance/ConfigurationRaceTests.cs`; References: SC-007/009, P-016; Depends: approved PostgreSQL and T155; Verify: real PostgreSQL evidence or BLOCKED.
- [ ] T159 [P] [US2] [REQUIRES_APPROVED_POSTGRESQL] Add production-attempt crash-before/after-Telemetry and Accepted/Rejected Duplicate replay tests in `tests/Integration/Acceptance/SimulatorCrashRecoveryTests.cs`; References: Simulator crash recovery, Telemetry terminal result; Depends: approved PostgreSQL and T155; Verify: exact counters/payload replay or BLOCKED.
- [ ] T160 [P] [US3] [REQUIRES_APPROVED_POSTGRESQL] Add Latest concurrency and health-scheduler restart/recovery tests in `tests/Integration/Acceptance/LatestHealthRaceTests.cs`; References: US3, SC-003/004; Depends: approved PostgreSQL and T155; Verify: real PostgreSQL evidence or BLOCKED.
- [ ] T161 [P] [RUNNABLE_NOW] Extend correlation-ID and structured-log verification across API/Worker/module events in `tests/Verification/observability.tests.ps1`; References: ADR-011, constitution VI; Depends: T155; Verify: script proves safe correlation propagation and no credential values.
- [ ] T162 [P] [BLOCKED_BY_ENVIRONMENT] Add database-unavailable readiness and explicit failure-state smoke checks in `tests/Verification/database-readiness.tests.ps1`; References: ADR-011, quickstart classification; Depends: T006 and T155; Verify: missing database reports blocked/unready, never healthy PASS.
- [ ] T163 [RUNNABLE_NOW] Create `database/migrations/0011_r1_infrastructure_expand.sql` only if reviewed VS-01 evidence proves additive columns are required; otherwise record a no-change decision in `specs/002-asset-simulator-latest/checklists/migration-0011.md`; References: R0 `0001_r0_foundation.sql`; Depends: T144; Verify: never recreates `integration.outbox_event`, `integration.inbox_message` or `operations.job`.
- [ ] T164 [RUNNABLE_NOW] Create `database/migrations/0012_r1_idempotent_seeds.sql` for fixed roles/capability/users and Catalog definitions without pre-Site scope, credentials or cross-schema writes; References: plan P-019; Depends: T163 and schema migrations 0002-0010; Verify: SQL review proves deterministic IDs and rerun idempotency.
- [ ] T165 [RUNNABLE_NOW] Create `database/migrations/0013_r1_validation_reconciliation.sql` for owner-reference, registry/raw, Latest/status and migration validation queries; References: migration design 0013; Depends: T164; Verify: queries are read-only/reconciling and preserve module ownership.
- [ ] T166 [REQUIRES_APPROVED_POSTGRESQL] Execute ordered migrations 0001-0013 on clean and N-1 approved PostgreSQL and record forward-fix/rollback evidence in `specs/002-asset-simulator-latest/checklists/migrations-full.md`; References: ADR-015; Depends: T006-T007, T026, T042, T057, T069-T070, T098, T115, T131, T144, T163-T165; Verify: checksums/order/constraints pass or remain BLOCKED.
- [ ] T167 [RUNNABLE_NOW] Build the 68-FR requirement-to-test matrix in `specs/002-asset-simulator-latest/checklists/requirements-traceability.md`; References: spec FR index, plan Traceability; Depends: T156-T166; Verify: script/count review shows every unique FR exactly once with evidence classification.
- [ ] T168 [RUNNABLE_NOW] Build the five-story and nine-success-criterion evidence matrix in `specs/002-asset-simulator-latest/checklists/acceptance-traceability.md`; References: US1-US5, SC-001..009; Depends: T156-T167; Verify: current authoritative counts are 5/5 stories and 9/9 success criteria.
- [ ] T169 [RUNNABLE_NOW] Run the complete quickstart journey and record each observable PASS/FAIL/BLOCKED result in `specs/002-asset-simulator-latest/checklists/quickstart-evidence.md`; References: `quickstart.md`; Depends: T167-T168; Verify: no blocked database/package/environment step is called PASS.
- [ ] T170 [RUNNABLE_NOW] Run Standards and Spec-compliance code reviews and record all findings in `docs/code-review.md`; References: repository workflow step 9; Depends: T169; Verify: every Critical/High finding is fixed or completion remains stopped.
- [ ] T171 [RUNNABLE_NOW] Run `.\scripts\harness.ps1 -Mode Fast -Feature 002-asset-simulator-latest` and record `verification-results.json` summary in `specs/002-asset-simulator-latest/checklists/phase-10-fast.md`; References: repository harness; Depends: T170; Verify: exact exit/output recorded.
- [ ] T172 [REQUIRES_APPROVED_POSTGRESQL] Run `.\scripts\harness.ps1 -Mode Full -Feature 002-asset-simulator-latest` and record all PASS/FAIL/BLOCKED classifications in `specs/002-asset-simulator-latest/checklists/phase-10-full.md`; References: repository harness; Depends: T166 and T171; Verify: Full is not described as passing while PostgreSQL/package/CI/environment blockers remain.
- [ ] T173 [REQUIRES_COMPANY_APPROVAL] Validate company CI/release evidence in `docs/ci-readiness.md`; References: ADR-016; Depends: T009 and T171-T172; Verify: only approved company runner/template evidence can pass.
- [ ] T174 [RUNNABLE_NOW] Complete final architecture-boundary and restricted-environment checks in `tests/Verification/architecture.tests.ps1` and `tests/Verification/repository-policy.tests.ps1`; References: constitution I/III/V; Depends: T170-T173; Verify: commands prove no write-back, Modbus, public download, container, alternate DB or cross-schema write.
- [ ] T175 [RUNNABLE_NOW] Complete the Phase 10 checkpoint and stop in `specs/002-asset-simulator-latest/checklists/phase-10-acceptance.md`; References: 68 FRs, 5 stories, 9 criteria; Depends: T167-T174; Verify: architecture boundary, Standards/Spec reviews and code-review checkpoint are recorded, unresolved task dependencies are zero, and all non-passing evidence is explicit.

## Dependencies and execution order

```text
Phase 0 governance
  -> Phase 1 IAM/bootstrap
  -> Phase 2 Catalog
  -> Phase 3 Organization
  -> Phase 4 Simulator configuration/Mapping
  -> Phase 5 Point activation
  -> Phase 6 Simulator Run/Worker
  -> Phase 7 Telemetry
  -> Phase 8 Latest/Health
  -> Phase 9 API/Web
  -> Phase 10 Acceptance hardening
```

- T003 and T010 are hard prerequisites for every green application-source implementation task.
- Migration source order is 0002 (T026), 0003 (T042), 0004 (T057), 0005/0006 (T069/T070),
  0007 (T098), 0008 (T115), 0009 (T131), 0010 (T144), then 0011-0013 (T163-T165).
- PostgreSQL execution depends on T006/T007 and never uses a substitute database.
- Phase checkpoints T011, T030, T045, T060, T073, T083, T103, T122, T134, T155 and T175 are
  explicit stops; the next phase does not begin automatically.

## User-story dependencies and independent tests

| Story | Primary phases | Dependency | Independent completion criterion |
|---|---|---|---|
| US1 Configure hierarchy | 1-5, 9-10 | Governance, IAM, Catalog | Admin creates Site/scope; Engineer creates and activates valid hierarchy; invalid prerequisites fail |
| US2 Operate Simulator | 2, 4, 6-7, 9-10 | Active Point from US1 | Literal generator vectors pass; run controls and crash-safe ingestion work |
| US3 Observe Latest/Health | 7-10 | Accepted Telemetry from US2 | Scoped latest/quality/timestamps and Online/Stale/NoData/recovery are correct |
| US4 Enforce scope | 1, 3, 9-10 | IAM foundation | Every command/query is server-authorized and out-of-scope IDs leak nothing |
| US5 Audit trail | 1-10 | Owner events + Audit storage | Authorized reviewer sees immutable, redacted configuration/control evidence |

## Parallel opportunities

- Phase 0: T004, T005, T008 and T009 touch independent governance/evidence files.
- Phase 1: domain, authorization and session red tests (T012, T013, T018, T020) are independent;
  green work converges before persistence/API integration.
- Phases 2-9: tasks marked `[P]` touch different files and do not depend on an incomplete mutation
  of the same seam. Migration files and shared architecture checks are deliberately serial.
- Phase 10: negative, lifecycle, concurrency, crash, health and observability checks can be authored
  independently, but evidence matrices wait for all results.

### Parallel example: US2 literal generator work

```text
T085 tests Constant and first Normal vectors.
T086 tests persisted-state restart vector.
T087 tests sequence and Pending retry semantics.
T088 tests Run lifecycle and pinned snapshots.
After the common red checkpoint T089, T090 and T091 may implement independent initialization/state
files; T092 integrates them before any Worker production task begins.
```

### Parallel example: US3 observation work

```text
T123 tests Latest ordering.
T124 tests Source Health derivation.
T125 tests durable scheduled evaluation.
After T126, T127 and T128 may proceed in separate domain files; T129 integrates scheduling.
```

## Requirement coverage

| Requirement group | Count | Primary task ranges |
|---|---:|---|
| FR-001..FR-007, FR-DC-001..005 | 12 | T046-T060, T157 |
| FR-008..FR-016 | 9 | T061-T073, T084-T103 |
| FR-017..FR-021 | 5 | T104-T122 |
| FR-022..FR-027 | 6 | T123-T134 |
| FR-028..FR-039 | 12 | T012-T030, T135-T155, T156-T157 |
| FR-AP-001..005, FR-DO-001..003 | 8 | T048, T074-T083 |
| FR-IAM-001..008 | 8 | T012-T030 |
| FR-DS-001..004, FR-CAT-001..004 | 8 | T031-T045 |
| **Total** | **68** | **T012-T175** |

## Implementation strategy

1. Complete Phase 0 and pass `/speckit.analyze`; this is the implementation gate.
2. Implement exactly one phase using red -> green -> refactor -> review -> checkpoint.
3. Stop at the phase checkpoint and review evidence before invoking `/speckit.implement` again.
4. The smallest demonstrable foundation is Phases 1-5 (US1 hierarchy activation). It is not a
   release until Phases 6-10 and all mandatory evidence are complete.
5. `/speckit.implement` is **not ready now**: T003 constitution amendment, T010 analysis and
   environment/package/PostgreSQL gates remain prerequisites.

## Task-list completion status

- Functional requirements represented: **68/68**
- User stories represented: **5/5**
- Success criteria represented: **9/9**
- Unresolved task dependencies: **0** (all prerequisites are explicit)
- Ready for `/speckit.analyze`: **YES**
- Ready for `/speckit.implement`: **NO**
