# ADR-009: Local IAM and Scoped RBAC

**Status:** Accepted  
**Date:** 2026-07-23  
**Deciders:** Software Architect / Tech Lead  
**Reference:** DOC-05 §17, DOC-04 FR-IAM, DOC-02 §11  

## Context
MVP needs authentication and authorization. SSO/AD/LDAP integration is deferred. Roles control what actions users can perform, and scopes (site/area) control what data they can see.

## Decision
Build a local IAM system:
- **Authentication:** Username/password with hashed passwords (ASP.NET Core Identity or custom)
- **Roles:** Administrator, Engineer, Operator, Manager, Viewer, Reviewer
- **Scopes:** Site/area-level data visibility enforced server-side
- **Session:** Cookie or token-based with configurable expiry
- Authorization policies enforced at API layer and validated in domain services

## Consequences
- Self-contained, no external dependency for MVP
- SSO can be added later via adapter pattern
- Must implement password policies, lockout, and audit from the start
- Scope checking must be centralized to avoid bypass
