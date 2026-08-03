# Feature 003 Phase 5 red evidence — T065–T066

The Phase 5 red tests were run against the authoritative baseline before the
Audit and Operational Dashboard production changes. They were registered in
the repository test entry points and failed for the intended missing behavior.

## Unit red — T065

Command:

```text
dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-restore -- T065
```

Result: **exit 1** — `T065: cases=2; assertions=5; failures=3`.

The three failures were the missing Administrator-only correlation permission,
missing top-level redaction in the audit result, and missing no-authorized-scope
dashboard counts/state. No secret values were written to this evidence.

## PostgreSQL integration red — T066

Command:

```text
dotnet run --project .\tests\Integration\IUMP.Tests.Integration.csproj --no-restore -- T066
```

Result: **exit 1** — target `127.0.0.1:5433/iump_dev`,
`T066: cases=4; assertions=4; failures=2`.

The two failures were non-Administrator correlation being returned and
credential-like values not being redacted. The test used the approved local
PostgreSQL target only; port 5432, SQLite, InMemory, Docker, and package
installation were not used.

