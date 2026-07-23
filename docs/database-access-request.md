# Database Access Request — R0

Status: `DATABASE_EXECUTION_BLOCKED`

Please provide either an approved local PostgreSQL installation or an internal development
PostgreSQL endpoint. Do not provide production credentials or production data.

## Requested capability

- PostgreSQL version: company-supported version compatible with Npgsql 10; DOC-06 leaves the exact
  version open for Tech Lead/IT approval.
- Development database: `iump_dev` (or company naming standard).
- Runtime principals: separate API and Worker identities where practical; a migration identity for
  controlled DDL.
- Minimum migration rights: connect; create/alter objects in approved IUMP schemas; create indexes,
  constraints, and migration-history table. No superuser or server administration rights.
- Runtime rights: least-privilege DML only on owned schemas; no role/database administration.
- Network: only the approved hostname, port, TLS/certificate mode, and firewall route supplied by
  IT. The project will not scan the network.
- Secret delivery: approved environment variable or user-secret mechanism; never commit plaintext.

## Required schemas

`iam`, `organization`, `catalog`, `acquisition`, `telemetry`, `rules`, `alerts`, `notifications`,
`reporting`, `integration`, `operations`, `audit`, and `files`. R0 creates only migration structure;
business schemas and seeds must remain within the approved R0 baseline.

## Prohibited data

Production credentials, real personnel data, real OT addresses/registers, production telemetry,
and unapproved extracts must not be used. R0 verification uses synthetic, non-sensitive data only.

## Verification after access

1. Confirm client and server versions without changing server configuration.
2. Validate TLS/connectivity with redacted output.
3. Run clean migration and N-1/upgrade validation where an earlier baseline exists.
4. Verify seed idempotency using synthetic data.
5. Run PostgreSQL integration tests for migration, outbox/inbox duplicate semantics, and health.
6. Record commands, results, migration version, and cleanup/rollback evidence.
