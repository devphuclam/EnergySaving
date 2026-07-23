# ADR-006: CQRS-lite and No Event Sourcing

**Status:** Accepted  
**Date:** 2026-07-23  
**Deciders:** Software Architect / Tech Lead  
**Reference:** DOC-05 §12, DOC-02 §10  

## Context
CQRS and Event Sourcing add complexity. MVP domains (IAM, asset, telemetry, rules, alerts) have straightforward read/write patterns.

## Decision
Use a lightweight CQRS pattern:
- Separate read and write models at the module boundary (not separate databases)
- Queries return read-optimized DTOs directly from EF Core
- Commands go through service layer with domain validation
- No event sourcing — current state is source of truth
- Events are emitted as side effects (via outbox) for cross-module communication

## Consequences
- Simpler than full CQRS/ES
- Read models can be optimized independently later
- Cross-module communication still uses events for loose coupling
