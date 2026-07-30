# Migrations 0005-0006 / Configuration and Mapping Evidence

Date: 2026-07-30  
Target: approved PostgreSQL 18 at `127.0.0.1:5433/iump_dev`

T090: **PASS**.

- Ordered clean and N-1 migration verification including 0005/0006: exit **0**.
- PostgreSQL runtime and acceptance runner: exit **0**.
- Verified ordered immutable history, stale append rejection, rollback, concurrent append with one
  winner, Mapping overlap exclusion, and Mapping activation race with one winner.
- The functional journey verifies SC-007 readiness through effective Mapping and Point activation.
- Secret emitted: **NO**. Port 5432 contacted: **NO**.
