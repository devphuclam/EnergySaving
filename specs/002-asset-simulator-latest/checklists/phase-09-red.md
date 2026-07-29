# Phase 9 corrective RED evidence

Parent baseline: `6e7ff79942188517c644eb43ae541d6eddc23d06` (verified with `git rev-parse HEAD`).
The temporary native worktree `C:\Users\TD-999\Research\EnergySaving\phase9-red-worktree` was
created at that exact commit, used only for the corrective probe, and removed after evidence capture.

## True RED commands and results

1. `dotnet build .\IUMP.slnx --no-restore --configuration Debug` — **exit 0**.
2. `dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-restore --configuration Debug` —
   **exit 1**. Existing Phase 0–8 suites remained green; the corrective probe reported
   `Phase9FunctionalCoverageRed: cases=15; failures=15` and listed the fifteen natural baseline
   defects below. The process did not crash and no database command ran.

## Natural baseline failures (15 cases)

| Case | Defect demonstrated by the baseline |
|---|---|
| T170 | Fingerprint source lacked the UUID/integer/decimal/timestamp and field-order matrix, If-Match inclusion, transport exclusion and secret/auth exclusion. |
| T171 | Typed request/response canonical-shape evidence was absent. |
| T172 | Executor source lacked live/expired Pending, concurrent duplicate, crash-safe replay, exact Location/ETag/original correlation and one mutation+outbox proof. |
| T173 | Inbox source lacked payload hash conflict, lease, retry and Completed deduplication evidence. |
| T174 | Dispatcher source lacked per-consumer inbox/restart behavior and correlation preservation. |
| T175 | Audit source lacked schema-version/hash/conflict and complete redaction evidence. |
| T176 | Audit query source lacked scope-before-paging and keyset evidence. |
| T177 | Operations source lacked lease/retry/exhaustion/reconciliation/replay evidence. |
| T178–T181 | Endpoint tests did not demonstrate invocation of the actual configuration, Simulator, Telemetry and Audit handlers/ports. |
| API identity/fingerprint | API trusted `X-Caller-Id` and fingerprinted `Idempotency-Key`. |
| delivery/audit transaction | Dispatcher used a fixed 250ms retry and Audit append/inbox completion had no one-host transaction. |
| T211/Web | Web screens used component-local POC data and lacked the loading/forbidden/expired behavior matrix. |
| T221 | Static verification did not detect the Phase 9 functional defects. |
| T222 | Review lacked finding rows with Severity, Resolution and State. |
| T223 | Checkpoint lacked exact changed-file, T221 and Full-harness evidence fields. |

## Corrective green gate

The same Debug build and focused runner now exit **0** after the source/test corrections. Release
build and runner, Fast harness, architecture/policy checks and Web lint/build are recorded in the
Phase 9 checkpoint. Package-policy and runtime PostgreSQL evidence remain blocked/not-run and are
never promoted to PASS.
