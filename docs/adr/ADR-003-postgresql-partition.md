# ADR-003: PostgreSQL and Partition Candidate

**Status:** Accepted  
**Date:** 2026-07-23  
**Deciders:** Software Architect / Tech Lead  
**Reference:** DOC-05 §11, DOC-06 §7-13, DOC-02 §8  

## Context
MVP telemetry volume is ~10M records/year for 20 points. The model needs time-range queries, retention, and aggregation. A separate time-series DB adds operational cost.

## Decision
Use PostgreSQL with:
- Partitioned telemetry tables by time range (monthly for raw, yearly for aggregates)
- Indexes on (point_id, timestamp) for query performance
- Numeric columns for measurement values
- JSONB for flexible quality/metadata flags

## Consequences
- Single database technology simplifies operations
- Partitioning enables efficient retention cleanup
- Re-evaluate if volume exceeds 100M records or query latency degrades
