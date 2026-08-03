# ADR-010: Restricted Non-Containerized Development and Deployment

**Status:** Accepted for MVP-1 architecture; deployment approval pending
**Date:** 2026-07-23 (reconciled 2026-08-03)
**Reference:** DOC-05 v0.2 §19, §19.2, §19.3 and §30 AR-11; DOC-07 v0.2 §17 and §22.2

DOC-05 v0.2 is authoritative and defines MVP-1 as a restricted non-containerized deployment. This
ADR records that current architecture decision; it does not claim concrete TEST/UAT/PROD approval. The
API and Worker are approved-host executables/services, the Web is static output, and PostgreSQL is
an installed/internal service. Docker, Compose, Podman, image promotion, and downloaded runtime or
package tooling remain prohibited on the workstation and are not part of the target topology.

This ADR supersedes the earlier wording that treated a containerized target as the unverified
architecture. It does not fabricate TEST/UAT/PROD approval: Infrastructure and Security must still
approve the concrete host, service manager, lifecycle, and rollback evidence before release.
