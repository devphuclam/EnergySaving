# Phase 2 corrective RED evidence — Catalog

This is fresh corrective evidence captured before the Catalog production corrections. No
production fix had been applied when this command ran.

| Field | Evidence |
|---|---|
| Command | `dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-restore` |
| Start time | `2026-07-27T09:10:20.9111623+07:00` |
| Exit code | `1` |
| Result | FAIL (expected corrective RED) |

## Failing assertions and defective behavior

- T032 `/me` response omitted `userId`, `username`, `roles`, `scopes`, and `capabilities`.
- Fake persistence silently accepted a duplicate compatibility pair.
- Fake persistence allowed a second canonical Unit for one Metric.
- Inactive Metric and Unit eligibility did not produce an ineligible result.
- Reapplying deterministic seed records was not idempotent.
- Mapping construction accepted `EffectiveTo <= EffectiveFrom`.
- Fake persistence accepted overlapping Active mapping periods.
- Active-mapping eligibility did not expose a distinct Missing outcome.
- Source and mapping deletion decisions were not executable repository operations.
- Owner events lacked actor, schema, and causation fields.
- Authorization used a user-ID allowlist rather than a server-resolved role/scope decision.
- T049 was comment-only Skip pseudocode rather than an executable provider contract runner.

The command produced the following non-zero failure output before fixes:

```text
FAILURES:
  T032-RED: /me response must include userId.
  T032-RED: /me response must include username.
  T032-RED: /me response must include roles array.
  T032-RED: /me response must include scopes array.
  T032-RED: /me response must include capabilities array.
  duplicate compatibility pair must be rejected
  second canonical Unit must be rejected
  inactive Metric must be ineligible
  inactive Unit must be ineligible
  applying deterministic seeds twice must be idempotent
  EffectiveTo <= EffectiveFrom must be rejected
  overlapping Active periods must be rejected
  missing mapping eligibility must be explicit
  source and mapping deletion decisions must be executable repository operations
  owner events must expose actor, schema and causation fields
  authorization must use a server-resolved role/scope decision, not a user-ID allowlist
  T049 contract source must be executable and must not be Skip-only pseudocode
EXIT=1
```

Corrective RED is complete. Green work begins only after this evidence.
