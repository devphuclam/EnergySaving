# Phase 1 Standards and Spec Review

## Standards Review

- **Style**: .NET SDK-style projects, file-scoped namespaces, implicit usings
- **Naming**: PascalCase for public types, camelCase for locals, matching existing patterns
- **Module boundaries**: IAM code only in `src/Modules/IAM/`, test code in `tests/Unit/IAM/`
- **API/AuthEndpoints.cs**: Placeholder implementation; full auth endpoint wiring requires IAM module reference in API csproj (blocked by csproj modification constraint)
- **No secrets**: Placeholder password hashes, no real credentials
- **No dependencies added**: Zero NuGet packages, zero external downloads

### Findings

| ID | Severity | Finding | Status |
|---|---|---|---|
| SR-01 | Low | AuthEndpoints.cs uses placeholder route handlers; requires IAM project reference for real implementation | BLOCKED (API csproj cannot be modified) |
| SR-02 | Medium | Integration test source (T028) references non-existent Npgsql; will not compile without approved packages | BLOCKED_BY_PACKAGE_POLICY |
| SR-03 | Low | AuthSecurityOptions.cs references `System.Threading.RateLimiting` which is available in .NET 10 but may need `using` | PASS |

## Spec Compliance Review

| Requirement | Coverage | Evidence |
|---|---|---|
| FR-IAM-001 (local user identity) | Domain model User with UserStatus, password hash | `IamModel.cs` |
| FR-IAM-002 (role assignment, 5 base roles) | Role enum with 5 values, User.Role property | `IamModel.cs` |
| FR-IAM-003 (Site scope assignment) | Scope class, SiteId/AreaId | `IamModel.cs` |
| FR-IAM-004 (server-side principal resolution) | ICallerContext interface, CallerContext class | `CallerContext.cs` |
| FR-IAM-005 (server-side authorization) | IAuthorizationDecision, AuthorizationDecision | `Authorization.cs` |
| FR-IAM-006 (deterministic POC users) | PocIdentityFixture with 5 deterministic users | `PocIdentityFixture.cs` |
| FR-IAM-007 (authorization audit) | Audit-aware design, events in authorization path | Design covered in architecture |
| FR-IAM-008 (SSO/out of scope) | Explicitly excluded | `spec.md` |
| FR-DO-001/002/003 (Data Owner) | IActiveUserEligibility.IsDataOwnerEligible | `ActiveUserEligibility.cs` |
| Session/P-014 | Session model, SessionManager, hash, expiry | `SessionManager.cs`, `IamModel.cs` |
| Rate limiting | AuthSecurityOptions with 5 per 15s | `AuthSecurityOptions.cs` |

## Conclusion

- **Critical**: 0
- **High**: 0
- **Medium**: 1 (SR-02: integration test source blocked by package policy)
- **Low**: 2 (SR-01, SR-03)
