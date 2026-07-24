# Phase 1 RED Evidence

## Command and exit evidence

**Command**: `dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-build`

**Exit code**: 1 (FAIL)

**Date**: 2026-07-24

## Failure summary

| Test file | Test class | Failure count | Evidence |
|---|---|---|---|
| `tests/Unit/IAM/IamDomainTests.cs` | IamDomainTests | 2 | ActiveUserEligibility stub always returns Eligible; IsDataOwnerEligible stub always returns true |
| `tests/Unit/IAM/AuthorizationPolicyTests.cs` | AuthorizationPolicyTests | 5 | AuthorizationDecision stub always returns Allowed for all checks |
| `tests/Unit/Api/AuthSecurityPolicyTests.cs` | AuthSecurityPolicyTests | 0 | Constants match expectations - rate limit structure is trivially correct |
| `tests/Unit/IAM/PocIdentityFixtureTests.cs` | PocIdentityFixtureTests | 7 | GetDeterministicUsers returns empty; ApplyPostSiteFixture returns false |
| `tests/Unit/IAM/SessionPolicyTests.cs` | SessionPolicyTests | 4 | IsSessionValid ignores revoked state; RevokeAllSessions has no effect |
| tests/Unit/Program.cs | R0 correlation | 0 | All R0 correlation tests pass |

**Total assertions**: 42 assertions across 5 test classes
**PASS**: 25
**FAIL**: 17 (expected: missing authorization logic, eligibility checks, fixture completeness, session management)

## Blocking classification

All failures are **expected RED** - the stub implementations intentionally return trivial/incorrect values. No external dependency blocks these failures.

## Progression

RED evidence is complete. Proceeding to T019-T028 (contracts, domain, application implementations) to make tests PASS.

## Corrective red cycle (Phase 1 correction)

### Defects found in review

| Defect | Task | Description | Correction |
|---|---|---|---|
| A | T015 | Tests used constant-vs-constant and hardcoded-boolean assertions, never invoking production behavior | Rewrote to test `AuthenticationPolicy` directly via `IsRateLimited`/`RecordFailedAttempt`/`RecordSuccessfulAttempt` |
| B | T032 | Tests used source-text-only assertions (comparing paths to themselves) | Rewrote to test real `AuthHandler` via `Login`/`ResolveMe`/`RevokeSession` with concrete eligibility and session manager |
| C | T033 | `AuthEndpoints.cs` contained placeholder `Results.Ok(new { message = "..." })` handlers | Moved `AuthHandler` to `Application/SessionManager.cs`; endpoints now call `IAuthService` from Contracts |
| D/E | T014 | `AuthorizationDecision.CheckTarget` returned `Forbidden` for out-of-scope instead of `NotFound` | Code already returned `NotFound`; defect was from original stub analysis, not current green code |
| F | T027 | Migration 0002 used single `user_account.role` column instead of separate `iam.role` + `iam.user_role` | Rewrote migration with separate role table and user_role join table |
| G | Phase 1 checkpoint | Claimed 22 PASS tasks but T015/T032 were tautological, T033 was placeholder | Corrected to 21 PASS; 4 BLOCKED; T015/T032/T033 reclassified as real PASS |
| H | Tasks.md | All checkboxes `[ ]` despite completed tasks | Updated checkboxes to reflect current status |

### Corrective test command and exit evidence

**Command**: `dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-build`

**Exit code**: 0 (PASS)

**Assertion coverage**:
- T013: 12 assertions (User construction, status lifecycle, roles collection, role enum, scope, capability, session, eligibility, NoScope, ScopeMismatch, DataOwner no-site, DataOwner with-site)
- T014: 8 assertions (Admin global, admin audit, scoped engineer, engineer no-create-root, engineer capability, out-of-scope NotFound, server principal, multi-role context)
- T015: 3 assertions (Rate-limit after 5, sixth rejected, window reset)
- T016: 4 assertions (5 deterministic users, no pre-Site scope, post-Site applies, idempotent)
- T017: 6 assertions (Hash format, expiry, disabled invalidation, logout revoke, multi-session, revoke-all)
- T032: 8 assertions (Login success, wrong-password fails, unknown user fails, disabled user fails, public error for all three, token absent from body, ResolveMe with roles, RevokeSession)

**Total RUNNABLE_NOW**: 21 PASS, 0 FAIL, 4 BLOCKED
