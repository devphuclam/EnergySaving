# Phase 2 Corrective Two-Axis Review

Date: 2026-08-01
Feature: `003-operational-configuration-workspace`
Baseline: `c6da87b638ee9f55002a2deb98f7cba96a55abd5`

## Standards

Result: **PASS — closure reviewed**

- Implementation Critical: 0
- Implementation High: 0
- Actionable implementation Medium: 0
- Evidence closure: the hosted HTTP matrix and real browser journey are complete. Standards review
  is C0/H0/M0. The frontend behavior runner is separately `BLOCKED_BY_PACKAGE_POLICY`.

The current implementation uses Acquisition-owned receipts, exact Draft payload and relationship
fingerprints, ambient host transactions for owner state and outbox staging, server principals,
scope filtering, idempotency, optimistic versions, antiforgery metadata, immutable relationships,
explicit selectors, strict JSON parsing, and server-derived review/validation detail state. Simulator
duplication persists a v1 baseline and an activatable v2 Draft, and its Duplicated event aligns with
aggregate version 2 and Draft payload.
The duplicate route/UI require an explicit authorized target Source different from the original
Source, matching the PostgreSQL uniqueness invariant.

Scope note: FR-014 uses broad duplication language for every entity, while this corrective request
explicitly assigns persisted review/validation receipts and activation gating to the Acquisition
Simulator Configuration Draft. Non-Simulator duplicates retain their owner-domain Draft/lifecycle
contracts and display copied/excluded relationship metadata; extending Acquisition receipts to all
owner modules would be a separate design decision, not introduced in this Phase 2 corrective run.

## Specification

Result: **ACCEPTED WITH DOCUMENTED MEDIUM EVIDENCE BOUNDARY**

- Implementation Critical: 0
- Implementation High: 0
- Actionable implementation Medium: 1 documented evidence boundary (API/Web restart-specific receipt persistence)
- The hosted authentication, antiforgery, lifecycle/replay/key-conflict, authorization/not-found,
  malformed/unsupported JSON, exact receipt gating, and Audit/outbox HTTP matrix is complete. The
  authenticated browser journey is PASS12/FAIL0 and includes refresh/detail, edit invalidation,
  logout/login rehydration, re-review/re-validation/re-activation, exact validation focus, and zero
  console errors.

Automated Unit and PostgreSQL integration evidence is green: receipt authority and invalidation,
including direct stale-relationship activation rejection, explicit Area/Asset/Mapping/Data
Source/Simulator selectors, duplicate Draft behavior, malformed JSON, and owner command seams are
covered. Duplicate-refresh-review and logout/login receipt rehydration are evidenced by the hosted
HTTP matrix and PASS12 browser journey rather than new automated browser tests. The public endpoint
tests are not substituted for the required real hosted matrix.

## Evidence boundary

The browser journey ran against the real Web UI. No credential value was guessed, printed, or copied
from the database password; the approved local app credential remains outside repository artifacts.
No package or runner was installed or downloaded. Hosted health and the management matrix were
verified against `127.0.0.1:5000` with `Host: localhost` and `127.0.0.1:5433/iump_dev`. A controlled
API process-restart probe reached ready/login but its synthetic Draft create returned 503, so
restart-specific receipt persistence is not claimed beyond the completed browser logout/login
rehydration. A transient 2026-08-01 runtime failure was recovered; the follow-up Integration and
hosted HTTP matrix passed. No port 5432 fallback or substitute database was attempted.

## Disposition

Phase 2 corrective closure is **complete for the implemented scope**. T044 is reevaluated with no
regression; T038, T043, T046, and T048 have hosted/browser evidence and no Critical/High findings.
The documented API restart probe is an evidence boundary, not a product failure; release-ready is
still **NO** because the frontend behavior runner is `BLOCKED_BY_PACKAGE_POLICY` and Full harness
environment checks are `BLOCKED_BY_COMPANY_APPROVAL`. Stop before T049; the next phase remains
T049-T056 and needs a separate explicit invocation.
