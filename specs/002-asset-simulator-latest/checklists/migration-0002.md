# Migration 0002 / IAM PostgreSQL Evidence

Date: 2026-07-30  
Target: approved PostgreSQL 18 at `127.0.0.1:5433/iump_dev`

T031: **PASS**.

- Ordered clean and N-1 migration verification including 0002: exit **0**.
- PostgreSQL runtime leaf runner: exit **0**.
- Verified username uniqueness, token-hash session lookup, session revocation, and rollback with
  no staged-user publication.
- Owner mutation/outbox rollback and replay pass in the command-idempotency suite.
- Secret emitted: **NO**. Port 5432 contacted: **NO**.
