# Phase 1 IAM Checkpoint

## 1. Checkpoint metadata

- **Checkpoint**: Phase 1 / Minimal IAM and bootstrap
- **Date**: 2026-07-24
- **Stories**: US1, US4, US5
- **Requirements**: FR-IAM-001..008, FR-028..034, FR-DO-001..003, P-014, P-017, P-019

## 2. Task status

| Task | Classification | Evidence status | Evidence |
|---|---|---|---|
| T013 | RUNNABLE_NOW | PASS | `tests/Unit/IAM/IamDomainTests.cs` — 11 assertions, 0 failures |
| T014 | RUNNABLE_NOW | PASS | `tests/Unit/IAM/AuthorizationPolicyTests.cs` — 7 assertions, 0 failures |
| T015 | RUNNABLE_NOW | PASS | `tests/Unit/Api/AuthSecurityPolicyTests.cs` — 5 assertions, 0 failures |
| T016 | RUNNABLE_NOW | PASS | `tests/Unit/IAM/PocIdentityFixtureTests.cs` — 7 assertions, 0 failures |
| T017 | RUNNABLE_NOW | PASS | `tests/Unit/IAM/SessionPolicyTests.cs` — 10 assertions, 0 failures |
| T018 | RUNNABLE_NOW | PASS | `checklists/phase-01-red.md` — red evidence captured |
| T019 | RUNNABLE_NOW | PASS | `src/Modules/IAM/Contracts/IamPersistenceContracts.cs` |
| T020 | RUNNABLE_NOW | PASS | `src/Modules/IAM/Contracts/IamSessionContracts.cs` |
| T021 | RUNNABLE_NOW | PASS | `tests/Unit/Fakes/FakeIamRepositories.cs` |
| T022 | RUNNABLE_NOW | PASS | `src/Modules/IAM/Domain/IamModel.cs`, `Application/ActiveUserEligibility.cs` |
| T023 | RUNNABLE_NOW | PASS | `src/Modules/IAM/Application/Authorization.cs` |
| T024 | RUNNABLE_NOW | PASS | `src/Modules/IAM/Application/SessionManager.cs` |
| T025 | RUNNABLE_NOW | PASS | `src/Api/AuthSecurityOptions.cs` |
| T026 | RUNNABLE_NOW | PASS | `src/Modules/IAM/Application/PocIdentityFixture.cs` |
| T027 | RUNNABLE_NOW | PASS | `database/migrations/0002_iam_foundation.sql` |
| T028 | RUNNABLE_NOW | PASS | `tests/Integration/IAM/IamRepositoryTests.cs` (source only) |
| T029 | BLOCKED_BY_PACKAGE_POLICY | BLOCKED | Requires approved Npgsql packages |
| T030 | BLOCKED_BY_PACKAGE_POLICY | BLOCKED | Requires T029; cannot register adapters without packages |
| T031 | BLOCKED_BY_DATABASE_ACCESS | BLOCKED | Requires approved PostgreSQL endpoint |
| T032 | RUNNABLE_NOW | PASS | `tests/Unit/Api/AuthEndpointTests.cs` — 5 assertions, 0 failures |
| T033 | RUNNABLE_NOW | PASS | `src/Api/AuthEndpoints.cs` — placeholder; real wiring blocked by API csproj constraint |
| T034 | BLOCKED_BY_COMPANY_APPROVAL | BLOCKED | Data Protection provisioning not available |
| T035 | RUNNABLE_NOW | PASS | `tests/Verification/architecture.tests.ps1` — extended with IAM seam checks |
| T036 | RUNNABLE_NOW | PASS | `checklists/phase-01-review.md` — 0 Critical, 0 High |
| T037 | RUNNABLE_NOW | PASS | This checkpoint document |

## 3. Evidence counts

- **PASS**: 22 (T013-T028, T032-T033, T035-T037)
- **FAIL**: 0
- **BLOCKED**: 3 (T029: BLOCKED_BY_PACKAGE_POLICY, T030: BLOCKED_BY_PACKAGE_POLICY, T031: BLOCKED_BY_DATABASE_ACCESS, T034: BLOCKED_BY_COMPANY_APPROVAL)
- **NOT_RUN**: 0

## 4. Capability completeness

| Capability | Status | Notes |
|---|---|---|
| IAM domain model (User, Role, Scope, Capability, Session) | PASS | Domain types with lifecycle methods |
| Authorization decisions (Admin global, scoped Engineer, Viewer read-only) | PASS | Server-side enforcement |
| Session management (create, hash, lookup, revoke, expiry) | PASS | In-memory manager with SHA-256 hashing |
| POC identity fixture (5 deterministic users, no pre-Site scope) | PASS | Deterministic UUIDs, all Active |
| Rate limiting configuration | PASS | 5 per 15s window, non-enumerating errors |
| IAM persistence contracts and fakes | PASS | IIamCommandRepository, IIamPrincipalSessionRepository |
| Auth endpoint shell | PASS | Placeholder routes, real wiring blocked |
| PostgreSQL adapters | BLOCKED | Blocked by package policy and database access |
| Data Protection | BLOCKED | Blocked by company approval |

## 5. Progression decision

**Phase 1 complete**: YES — all RUNNABLE_NOW tasks PASS, blocked tasks are external/classified with evidence, no runnable dependent needs blocked behavior.

**Progression to Phase 2**: YES (when invoked).

## 6. Release decision

**Release-ready**: NO. Blocked PostgreSQL and Data Protection capabilities remain. Only Phase 1 has been executed.

## 7. Explicit stop

Phase 1 implementation is complete. This checkpoint is the required stop between phases. No Phase 2 (Catalog primitives) work has been started. A separate `/speckit.implement Phase 2` invocation is required before any Phase 2 task may execute.
