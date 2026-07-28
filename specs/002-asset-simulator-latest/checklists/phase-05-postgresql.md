# Phase 5 PostgreSQL Evidence (T104)

Status: **BLOCKED**
Classification: **BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE**

The approved PostgreSQL capability is authoritative and available at `127.0.0.1:5433/iump_dev`. This invocation did not connect, run `psql`, execute migrations, mutate the database, access port `5432`, install packages, use Docker, or print a secret. Database access is therefore not the blocker.

T104 depends transitively on the PostgreSQL adapters in T029, T050, and T072, which remain package-policy blocked. The unchecked task must remain non-passing until those adapter dependencies are approved and registered. Required future evidence is the concurrency/rollback/outbox run against the approved target with fail-fast behavior and a redacted result.
