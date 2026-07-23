# ADR-004: API and Worker Process Separation

**Status:** Accepted  
**Date:** 2026-07-23  
**Deciders:** Software Architect / Tech Lead  
**Reference:** DOC-05 §13, DOC-07 §8.1  

## Context
Background work (ingestion, aggregation, rule evaluation, report generation) should not block API responsiveness. Some jobs need different scaling than web requests.

## Decision
Run two process types:
- **API process:** Hosts HTTP endpoints, serves web UI static files, handles synchronous requests
- **Worker process:** Runs background jobs (ingestion, aggregation, rule evaluation, report generation) via a job/outbox pattern

Both share the same module code but deploy as separate containers/processes.

## Consequences
- API remains responsive under load
- Worker can scale independently if needed
- Shared codebase avoids duplication
- Deployment complexity increases slightly (two processes vs one)
