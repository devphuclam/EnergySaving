# Operations Contract

Operations owns durable job scheduling and execution state in the existing R0 `operations.job`.
It does not own business events, outbox/inbox records, or Audit rows.

## Ports and runtime

- `IDurableJobScheduler.EnqueueAsync(jobType, idempotencyKey, safePayload, availableAt)` schedules a
  unique durable wakeup through the Operations PostgreSQL adapter.
- `IJobClaimRepository` claims with `FOR UPDATE SKIP LOCKED`, renews leases, completes, reschedules,
  and marks terminal Failed.
- Worker handlers run Simulator production, outbox dispatch wakeups, lease recovery, Audit delivery
  reconciliation, idempotency cleanup, and authorized replay.
- `IOutboxDispatcher` is a Worker runtime service using Integration ports. It is not an Operations
  repository and never writes Integration SQL directly.

Job uniqueness remains `(job_type,idempotency_key)`. Leases are 30 seconds and renewed while work
continues. The Audit delivery policy permits at most 10 attempts with delays 250 ms, 1 s, 2 s, 5 s,
then 30 s capped with bounded jitter. Exhaustion moves the applicable job/delivery record to Failed,
records a redacted error, and raises poison/backlog telemetry; it never reports completion.

Reconciliation:

- reclaims expired job/outbox/inbox leases;
- reschedules retryable Failed or due records within the attempt policy;
- detects Published outbox events lacking expected Completed Audit inbox/append evidence;
- detects command Pending records past lease/expiry and verifies owner/outbox/result state;
- reports event payload-hash or identity conflicts without overwrite.

Replay is an operator-authorized control action after correction. Audit replay retains event ID,
payload, correlation ID, and causation ID and resets delivery state; command recovery uses a retry
of the same canonical HTTP command and never reconstructs a request from stored secrets.

`0011_r1_infrastructure_expand.sql` reuses `operations.job` without recreation because R0 already
provides availability, lease, attempt, status, and version fields. Required PostgreSQL evidence
covers unique scheduling under concurrency, lease expiry/reclaim, retry/poison transitions,
reconciliation idempotence, and absence of direct cross-schema writes.
