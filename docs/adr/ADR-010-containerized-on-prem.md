# ADR-010: Restricted Non-Containerized Development Environment

**Status:** Proposed / Deferred / Needs Infrastructure Review  
**Date:** 2026-07-23  
**Reference:** DOC-05 §19 and §30 AR-11; current company workstation policy

DOC-05 retains a containerized on-premise deployment as the unverified target architecture, but the
current workstation MUST NOT use Docker, Compose, Podman, images, or downloaded tooling. Local
development therefore runs only approved preinstalled executables and an approved local/internal
PostgreSQL service. This is an environment constraint, not a silent architecture change. A separate
Infrastructure/Security decision is required if the same restriction applies to TEST/UAT/PROD; the
target-container decision is not Accepted until that review occurs.
