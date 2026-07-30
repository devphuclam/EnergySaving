# Migration 0008 / Telemetry PostgreSQL Evidence

Date: 2026-07-30  
Target: approved PostgreSQL 18 at `127.0.0.1:5433/iump_dev`

T148: **PASS**.

- Ordered clean and N-1 migration verification including 0008: exit **0**.
- PostgreSQL runtime leaf and recovery runners: exit **0**.
- Verified terminal identity, Accepted-plus-raw atomicity, Rejected-without-raw shape, rollback,
  exact Duplicate replay, and conflicting identity/slot rejection.
- Secret emitted: **NO**. Port 5432 contacted: **NO**.
