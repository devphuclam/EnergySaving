# Phase 1 IAM Checkpoint

## 1. Checkpoint metadata

- **Checkpoint**: Phase 1 / Minimal IAM and bootstrap
- **Date**: 2026-07-24
- **Stories**: US1, US4, US5
- **Requirements**: FR-IAM-001..008, FR-028..034, FR-DO-001..003, P-014, P-017, P-019

## 2. Task status

| Task | Classification | Evidence status | Evidence |
|---|---|---|---|
| T013 | RUNNABLE_NOW | PASS | `tests/Unit/IAM/IamDomainTests.cs` — 12 assertions (corrected: roles collection, NoScope, ScopeMismatch, DataOwner with-site), 0 failures |
| T014 | RUNNABLE_NOW | PASS | `tests/Unit/IAM/AuthorizationPolicyTests.cs` — 8 assertions (corrected: admin audit cap, engineer cap, multi-role context), 0 failures |
| T015 | RUNNABLE_NOW | PASS | `tests/Unit/Api/AuthSecurityPolicyTests.cs` — 3 assertions (corrected: tests real AuthenticationPolicy directly), 0 failures |
 | T016 | RUNNABLE_NOW | PASS | `tests/Unit/IAM/PocIdentityFixtureTests.cs` — 6 assertions (disabled default, no committed hash, enabled+hash returns 5 active, idempotent, rollback restores state), 0 failures |
 | T017 | RUNNABLE_NOW | PASS | `tests/Unit/IAM/SessionPolicyTests.cs` — 10 assertions (hash format, expiry, disabled invalidation, logout revoke, multi-session, revoke-all), 0 failures |
| T018 | RUNNABLE_NOW | PASS | `checklists/phase-01-red.md` — red + corrective green evidence captured |
| T019 | RUNNABLE_NOW | PASS | `src/Modules/IAM/Contracts/IamPersistenceContracts.cs` |
| T020 | RUNNABLE_NOW | PASS | `src/Modules/IAM/Contracts/IamSessionContracts.cs` |
| T021 | RUNNABLE_NOW | PASS | `tests/Unit/Fakes/FakeIamRepositories.cs` |
| T022 | RUNNABLE_NOW | PASS | `src/Modules/IAM/Domain/IamModel.cs`, `Application/ActiveUserEligibility.cs` |
| T023 | RUNNABLE_NOW | PASS | `src/Modules/IAM/Application/Authorization.cs` |
| T024 | RUNNABLE_NOW | PASS | `src/Modules/IAM/Application/SessionManager.cs` (includes AuthHandler) |
| T025 | RUNNABLE_NOW | PASS | `src/Api/AuthSecurityOptions.cs` (AuthenticationPolicy, not framework wrapper) |
| T026 | RUNNABLE_NOW | PASS | `src/Modules/IAM/Application/PocIdentityFixture.cs` |
| T027 | RUNNABLE_NOW | PASS | `database/migrations/0002_iam_foundation.sql` (corrected: separate role + user_role tables) |
 | T028 | RUNNABLE_NOW | PASS | `tests/Integration/IAM/IamRepositoryTests.cs` — 16 contract tests (uniqueness verification, 5 canonical roles, role assignment/duplicate/revoke, site/area scope, duplicate scope prevention, capability assign/revoke, transaction commit/rollback) |
| T029 | BLOCKED_BY_PACKAGE_POLICY | BLOCKED | Requires approved Npgsql packages |
| T030 | BLOCKED_BY_PACKAGE_POLICY | BLOCKED | Requires T029; cannot register adapters without packages |
| T031 | BLOCKED_BY_DATABASE_ACCESS | BLOCKED | Requires approved PostgreSQL endpoint |
 | T032 | RUNNABLE_NOW | PASS | `tests/Unit/Api/AuthEndpointTests.cs` — 12+ assertions (route metadata: RequireAntiforgeryCheckAttribute = IAntiforgeryMetadata; antiforgery options: defaults differ from .IUMP.Xsrf; login: Set-Cookie .IUMP.Auth, HttpOnly, SameSite, clean body; logout: invokes HandleLogout; me: userId/username/roles/scopes/capabilities; antiforgery handler), 0 failures |
| T033 | RUNNABLE_NOW | PASS | `src/Api/AuthEndpoints.cs` (corrected: real CredentialVerifier, antiforgery, token-safe response body, cookie flags, testable handlers) + `src/Modules/IAM/Application/SessionManager.cs` (AuthHandler with ICredentialVerifier, role collection in MeSnapshot) |
| T034 | BLOCKED_BY_COMPANY_APPROVAL | BLOCKED | Data Protection provisioning not available |
| T035 | RUNNABLE_NOW | PASS | `tests/Verification/architecture.tests.ps1` — extended with IAM seam checks |
| T036 | RUNNABLE_NOW | PASS | `checklists/phase-01-review.md` — 0 Critical, 0 High |
| T037 | RUNNABLE_NOW | PASS | This checkpoint document |

## 3. Evidence counts

- **PASS**: 21 (T013-T028, T032-T033, T035-T037) — 12+8+3+6+10+16+12 = 67 assertions across all test classes and 16 contract tests
- **FAIL**: 0
- **BLOCKED**: 4 (T029: BLOCKED_BY_PACKAGE_POLICY, T030: BLOCKED_BY_PACKAGE_POLICY, T031: BLOCKED_BY_DATABASE_ACCESS, T034: BLOCKED_BY_COMPANY_APPROVAL)
- **NOT_RUN**: 0

## 4. Capability completeness

| Capability | Status | Notes |
|---|---|---|
| IAM domain model (User, Role, Scope, Capability, Session) | PASS | Domain types with lifecycle methods |
| Authorization decisions (Admin global, scoped Engineer, Viewer read-only, out-of-scope NotFound) | PASS | Server-side enforcement with NotFound for out-of-scope |
| Session management (create, hash, lookup, revoke, expiry) | PASS | In-memory manager with SHA-256 hashing; AuthHandler delegates auth logic to Application |
| POC identity fixture (5 deterministic users, no pre-Site scope) | PASS | Deterministic UUIDs, all Active |
| Rate limiting (AuthenticationPolicy) | PASS | 5 per 15s window, non-enumerating errors, tested directly |
| IAM persistence contracts and fakes | PASS | IIamCommandRepository, IIamPrincipalSessionRepository |
| Auth endpoint (login/logout/me with real AuthHandler) | PASS | AuthHandler in Application module; AuthEndpoints delegates via IAuthService contract |
| Role model (separate role + user_role tables) | PASS | Migration 0002 uses iam.role and iam.user_role, not single user_account.role column |
| PostgreSQL adapters | BLOCKED | Blocked by package policy and database access |
| Data Protection | BLOCKED | Blocked by company approval |

## 5. Progression decision

**Phase 1 complete**: YES — all RUNNABLE_NOW tasks PASS, blocked tasks are external/classified with evidence, no runnable dependent needs blocked behavior.

## 6. Release decision

**Release-ready**: NO. Blocked PostgreSQL and Data Protection capabilities remain. Only Phase 1 has been executed.

## 7. Explicit stop

Phase 1 implementation is complete. This checkpoint is the required stop between phases. No Phase 2 (Catalog primitives) work has been started. A separate `/speckit.implement Phase 2` invocation is required before any Phase 2 task may execute.
