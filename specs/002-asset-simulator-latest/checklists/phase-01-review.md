# Phase 1 Standards and Spec Review

## Standards Review

- **Style**: .NET SDK-style projects, file-scoped namespaces, implicit usings
- **Naming**: PascalCase for public types, camelCase for locals, matching existing patterns
- **Module boundaries**: IAM code only in `src/Modules/IAM/`, test code in `tests/Unit/IAM/`
- **API/AuthEndpoints.cs**: Real implementation using `IAuthService` from Contracts; `AuthHandler` in Application module
- **Architecture**: AuthEndpoints.cs only imports `IUMP.Modules.IAM.Contracts` — no Direct Application/Domain references in host
- **No secrets**: Placeholder password hashes, no real credentials
- **No dependencies added**: Zero NuGet packages, zero external downloads

### Findings (corrective)

| ID | Severity | Finding | Status |
|---|---|---|---|
| SR-01 | Resolved | AuthEndpoints.cs previously used placeholder handlers | FIXED: real AuthHandler in Application; endpoints delegate via IAuthService |
| SR-02 | Medium | Integration test source (T028) references non-existent Npgsql; will not compile without approved packages | BLOCKED_BY_PACKAGE_POLICY |
| SR-03 | Minor | AuthSecurityOptions.cs `ConfigureRateLimiting` removed — unused framework wrapper; `AuthenticationPolicy` retained and tested | PASS |

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
- **High**: 0
- **Medium**: 1 (SR-02: integration test source blocked by package policy)
- **Low**: 0
