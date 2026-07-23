# ADR-016: Offline Dependency and Verification Strategy

**Status:** Proposed; CI execution blocked pending company runner  
**Date:** 2026-07-23  
**Reference:** DOC-07 §17 and §19; current company dependency and CI policy

R0 verification MUST use installed tools, locked dependencies already present in approved local or
internal sources, and a real approved PostgreSQL instance. Local scripts never restore/install,
download scanners/actions, or contact public registries; they emit explicit blocked states when a
prerequisite is absent. Hosted CI will use only a company runner and company-controlled templates,
package mirrors, and database access. Public marketplace actions, service containers, Testcontainers,
and fake PostgreSQL substitutes are rejected for this environment.
