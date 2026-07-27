# Phase 5 PostgreSQL Evidence (T104)

Status: **BLOCKED**

Classification: **BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE**

The provider-neutral Phase 5 transaction source and fake pass their runnable checks, but the
PostgreSQL execution leaf depends transitively on the PostgreSQL adapters in T029, T050, and T072.
Those adapters remain `BLOCKED_BY_PACKAGE_POLICY`; no approved package set is available for this
invocation. Therefore T104 remains unchecked and is not a PASS.

The approved runtime target remains `127.0.0.1:5433/iump_dev`. No connection, migration, mutation,
port `5432` access, substitute database, package install, or secret output was used. The Full
harness separately reports `BLK-ENV-002` because its `psql` executable is unavailable; that tool
block does not change T104's task classification or the verified database capability statement.

Required future evidence after package approval: run the concurrency, rollback, and transactional
outbox suite against the approved PostgreSQL target with fail-fast database behavior, then record
actual PASS/FAIL evidence here. Until then, this evidence is truthfully BLOCKED.
