# Phase 5 PostgreSQL Evidence (T104)

Status: **BLOCKED**
Classification: **BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE**

The approved PostgreSQL capability is authoritative and available at `127.0.0.1:5433/iump_dev`. This invocation did not connect, run `psql`, execute migrations, mutate the database, access port `5432`, install packages, use Docker, or print a secret. Database access is therefore not the blocker.

T104 depends transitively on the PostgreSQL adapters in T029, T050, and T072, which remain package-policy blocked. The unchecked task must remain non-passing until those adapter dependencies are approved and registered. Required future evidence is the concurrency/rollback/outbox run against the approved target with fail-fast behavior and a redacted result.

## 2026-07-30 runtime-resolution addendum

The T029/T050/T072 adapter dependencies are now PASS and database access is PASS. T104 is therefore
reclassified `RUNNABLE_NOW`, not package-policy blocked. It remains unchecked because its complete
Point-activation concurrency/rollback/outbox PostgreSQL suite was not executed. This addendum
supersedes only the stale blocker classification; it does not convert missing behavior evidence to
PASS.

## 2026-07-30 executable PostgreSQL resolution

T104: **PASS**.

- The functional PostgreSQL runner races Point activation against a concurrent Point configuration
  change for the same Draft Point and expected version.
- Exactly one mutation wins; if configuration wins, the subsequent activation uses the new version.
- The canonical activation path uses the nine-target lock order and stages the owner event/outbox
  in the host transaction.
- Command-idempotency crash tests verify rollback before completion and after staged completion
  with no mutation/outbox publication.
- Functional runner exit: **0**. PostgreSQL integration runner exit: **0**.
- Secret emitted: **NO**. Port 5432 contacted: **NO**.
