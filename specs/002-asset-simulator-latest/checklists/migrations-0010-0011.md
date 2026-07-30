# Migrations 0010-0011 / Integration, Audit, and Operations Evidence

Date: 2026-07-30  
Target: approved PostgreSQL 18 at `127.0.0.1:5433/iump_dev`

T206: **PASS**.

- Ordered clean and N-1 migration verification including 0010/0011: exit **0**.
- PostgreSQL integration runner: 13 suites, zero failures, exit **0**.
- Verified command constraints/replay/conflict, Pending reclaim, outbox/inbox delivery, Audit
  append/inbox atomicity, both delivery crash windows, poison exhaustion, durable-job retry/reclaim,
  and idempotent replay.
- Secret emitted: **NO**. Port 5432 contacted: **NO**.
