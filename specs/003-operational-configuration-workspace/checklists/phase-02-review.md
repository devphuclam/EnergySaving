# Phase 2 Corrective Two-Axis Review

Date: 2026-07-31
Feature: `003-operational-configuration-workspace`
Baseline: `f9a6c740780d699e3da7479cfa5639e18c558b94`

## Standards

Result: **NOT ACCEPTED — evidence boundary only**

- Implementation Critical: 0
- Implementation High: 0
- Actionable implementation Medium: 0
- Evidence closure: incomplete; the requested authenticated hosted HTTP matrix and real browser
  journey were not runnable because no approved application-session credential capability was
  available. The frontend behavior runner is separately `BLOCKED_BY_PACKAGE_POLICY`.

The current implementation uses Acquisition-owned receipts, exact Draft payload and relationship
fingerprints, ambient host transactions for owner state and outbox staging, server principals,
scope filtering, idempotency, optimistic versions, antiforgery metadata, immutable relationships,
explicit selectors, and strict JSON parsing. Simulator duplication persists a v1 baseline and an
activatable v2 Draft, and its Duplicated event aligns with aggregate version 2 and Draft payload.
The duplicate route/UI require an explicit authorized target Source different from the original
Source, matching the PostgreSQL uniqueness invariant.

Scope note: FR-014 uses broad duplication language for every entity, while this corrective request
explicitly assigns persisted review/validation receipts and activation gating to the Acquisition
Simulator Configuration Draft. Non-Simulator duplicates retain their owner-domain Draft/lifecycle
contracts and display copied/excluded relationship metadata; extending Acquisition receipts to all
owner modules would be a separate design decision, not introduced in this Phase 2 corrective run.

## Specification

Result: **NOT ACCEPTED — required hosted/browser evidence incomplete**

- Implementation Critical: 0
- Implementation High: 0
- Actionable implementation Medium: 0
- T038/T048 evidence gap: complete hosted authentication, antiforgery, lifecycle/replay/
  authorization/Audit-outbox HTTP matrix and the authenticated browser journey remain unverified.

Automated Unit and PostgreSQL integration evidence is green: receipt authority and invalidation,
explicit Mapping/Data Source/Simulator selectors, duplicate Draft behavior, malformed JSON, and
owner command seams are covered. The public endpoint tests are not substituted for the required
real hosted matrix.

## Evidence boundary

The browser tab reached the real Web UI but remained unauthenticated with a session-expired notice;
no app-session credential was guessed or taken from the database password. No package or runner was
installed or downloaded. Hosted health was verified against `127.0.0.1:5000` with `Host: localhost`
and the approved database target; it does not prove the management matrix.

## Disposition

Phase 2 corrective closure remains **open**. T043, T044, and T046 are implementation-complete;
T038 and T048 remain unchecked because required runnable hosted/browser evidence is unavailable.
Phase 2 acceptance is **NO**. Stop before T049; the next phase remains T049-T056 and needs a
separate explicit invocation after the evidence capability is approved.
