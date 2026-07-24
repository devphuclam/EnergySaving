# Phase 1 Standards and Spec Review

## Standards Review

- **Style**: .NET SDK-style projects, file-scoped namespaces, implicit usings
- **Naming**: PascalCase for public types, camelCase for locals, matching existing patterns
- **Module boundaries**: IAM code only in `src/Modules/IAM/`, test code in `tests/Unit/IAM/`
- **API/AuthEndpoints.cs**: Real implementation using `IAuthService` from Contracts; `AuthHandler` in Application module
- **Architecture**: AuthEndpoints.cs only imports `IUMP.Modules.IAM.Contracts` — no Direct Application/Domain references in host
- **No secrets**: Placeholder password hashes, no real credentials
- **No dependencies added**: Zero NuGet packages, zero external downloads

### Findings (first corrective cycle)

| ID | Severity | Finding | Status |
|---|---|---|---|
| SR-01 | Resolved | AuthEndpoints.cs used placeholder handlers | FIXED: real AuthHandler in Application; endpoints delegate via IAuthService |
| SR-02 | Medium | Integration test source (T028) references non-existent Npgsql | BLOCKED_BY_PACKAGE_POLICY |
| SR-03 | Minor | AuthSecurityOptions.cs `ConfigureRateLimiting` removed — unused wrapper; `AuthenticationPolicy` retained | PASS |

### Findings (second corrective cycle — defects A–J)

| ID | Severity | Finding | Status |
|---|---|---|---|
| SR-04 | High | Logout endpoint had no `IAntiforgery` validation | FIXED: `RequireAntiforgeryCheckAttribute` + `HandleLogout` validates via `IAntiforgery` |
| SR-05 | High | Login response body exposed `.token`/`.hash` — spec requires `{"message":"Authenticated."}` only | FIXED: `LoginResult` hides hash/token; body is clean |
| SR-06 | High | `PocIdentityFixture` had committed POC hash literal and was always enabled | FIXED: `IPocCredentialHashProvider` interface; defaults disabled; hash injected at runtime |
| SR-07 | Medium | `FakeIamTransaction.RollbackAsync` was no-op — did not restore state | FIXED: snapshot-then-restore pattern on all repo dicts |
| SR-08 | Medium | `IamRepositoryContractRunner` was comment-only placeholder | FIXED: 17 executable contract tests with session + optimistic version |
| SR-09 | Low | T001–T012 checkboxes all `[ ]` despite Phase 0 completion | FIXED: marked PASS/BLOCKED/NOT_RUN appropriately |
| SR-10 | Resolved | RouteMetadataTests could not inspect real `MapAuthEndpoints` route table | FIXED: now uses `IEndpointRouteBuilder.DataSources` with proper DI registration |
| SR-11 | High | Antiforgery framework cookie and frontend token cookie used same `.IUMP.Xsrf` name | FIXED: framework cookie is `.IUMP.Antiforgery` (HttpOnly=true), frontend cookie is `.IUMP.Xsrf` (HttpOnly=false) |
| SR-12 | High | AntiforgeryOptionsTests called `AddAntiforgery()` instead of production `AddAuthAntiforgery()` | FIXED: tests call `AddAuthAntiforgery()` and verify `Cookie.Name==.IUMP.Antiforgery`, `HeaderName==X-XSRF-TOKEN`, HttpOnly, SameSite, SecurePolicy, plus HandleAntiforgery execution |
| SR-13 | Medium | Username uniqueness contract test did not attempt persistence of duplicate user | FIXED: actually attempts `AddUserAsync`, expects `InvalidOperationException`, verifies count unchanged |
| SR-14 | Medium | `FakeIamCommandRepository` did not enforce username uniqueness | FIXED: `AddUserAsync` throws `InvalidOperationException` on duplicate normalized username |
| SR-15 | Medium | T028 lacked session lookup, revoke, revoke-all, and optimistic version tests | FIXED: added 5 contract tests (SessionCreation, SessionLookupByTokenHash, CurrentSessionRevocation, RevokeAllSessions, OptimisticVersionBehavior) |
| SR-16 | Medium | T028 claimed 16 tests but `RunAllAsync` invoked only 12 | FIXED: `RunAllAsync` now invokes exactly 17 declared tests, runner reports exact TestCount |
| SR-17 | Medium | Several async test methods contained no `await` causing CS1998 | FIXED: removed `async` from `FixtureDefaultDisabled`, `NoCommittedCredentialHash`, `FixtureWithHashCreatesUsers`, `NoPreSiteScope` |
| SR-18 | Info | Corrective RED evidence claimed 14 failures but listed only 9 | FIXED: count corrected to 9 |
| SR-19 | Info | Antiforgery handler not tested with real `IAntiforgery` service | FIXED: `AntiforgeryOptionsTests` executes `HandleAntiforgery` with real ASP.NET Core `IAntiforgery` and verifies both cookies |

## Spec Compliance Review

| Requirement | Coverage | Evidence |
|---|---|---|
| FR-IAM-001 (local user identity) | Domain model User with UserStatus, password hash | `IamModel.cs` |
| FR-IAM-002 (role assignment, 5 base roles) | Role enum with 5 values; separate iam.role + iam.user_role in SQL | `IamModel.cs`, `0002_iam_foundation.sql` |
| FR-IAM-003 (Site scope assignment) | Scope class, SiteId/AreaId | `IamModel.cs` |
| FR-IAM-004 (server-side principal resolution) | ICallerContext interface, CallerContext class | `CallerContext.cs` |
| FR-IAM-005 (server-side authorization) | IAuthorizationDecision, AuthorizationDecision | `Authorization.cs` |
| FR-IAM-006 (deterministic POC users) | PocIdentityFixture with 5 deterministic users | `PocIdentityFixture.cs` |
| FR-IAM-007 (authorization audit) | Audit-aware design, events in authorization path | Design covered in architecture |
| FR-IAM-008 (SSO/out of scope) | Explicitly excluded | `spec.md` |
| FR-DO-001/002/003 (Data Owner) | IActiveUserEligibility with IsDataOwnerEligible, FindByUsername, FindByUserId | `ActiveUserEligibility.cs` |
| Session/P-014 | Session model, SessionManager, hash, expiry, AuthHandler | `SessionManager.cs`, `IamModel.cs` |
| Rate limiting | AuthenticationPolicy with 5 per 15s window, tested directly | `AuthSecurityOptions.cs`, `AuthSecurityPolicyTests.cs` |
| Auth endpoints | POST /api/v1/auth/login, POST /api/v1/auth/logout, GET /api/v1/me | `AuthEndpoints.cs`, `AuthEndpointTests.cs` |

## Conclusion

- **Critical**: 0
- **High**: 5 resolved (SR-04, SR-05, SR-06, SR-11, SR-12)
- **Medium**: 6 (SR-02: integration test source blocked — BLOCKED_BY_PACKAGE_POLICY; SR-07, SR-08, SR-13, SR-14, SR-15, SR-16, SR-17 — all FIXED)
- **Low**: 1 resolved (SR-09: checkboxes)
- **Info**: 2 (SR-18: evidence count corrected; SR-19: real IAntiforgery test added)
