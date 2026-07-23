# ADR-007: Module and Schema Ownership

**Status:** Accepted  
**Date:** 2026-07-23  
**Reference:** DOC-05 §9; DOC-06 §7

Each module exclusively owns business writes in its canonical schema: `iam`, `organization`,
`catalog`, `acquisition`, `telemetry`, `rules`, `alerts`, `notifications`, `reporting`, `audit`,
`integration`, `operations`, and `files` as allocated by DOC-06. Cross-module behavior uses public
application/event/query contracts; no module references another module's internal implementation or
writes its tables. Cross-schema foreign keys/views require architecture review and do not transfer
write ownership.
