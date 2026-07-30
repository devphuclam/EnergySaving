# Migration 0007 / Run and Attempt PostgreSQL Evidence

Date: 2026-07-30  
Target: approved PostgreSQL 18 at `127.0.0.1:5433/iump_dev`

T127: **PASS**.

- Ordered clean and N-1 migration verification including 0007: exit **0**.
- PostgreSQL runtime leaf runner: exit **0**.
- Verified live lease exclusion, reclaim at expiry, unique `(Run, Point, SourceSequence)` winner,
  and atomic cursor/PRNG/Generated-counter commit.
- Recovery runner: six scenarios, zero failures, exit **0**.
- Secret emitted: **NO**. Port 5432 contacted: **NO**.
