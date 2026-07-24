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
