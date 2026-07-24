# Integration Contract

Integration owns API command idempotency and the existing transactional delivery records. Other
modules and HTTP/Worker handlers use public ports; only Integration PostgreSQL adapters write the
`integration` schema.

## Command idempotency

`ICommandIdempotencyStore` exposes:

- `RegisterOrReadAsync(identity, fingerprint, target, lease)`
- `GetAsync(identity)`
- `TryReclaimExpiredAsync(id, expectedVersion, owner, leaseUntil)`
- `CompleteAsync(id, expectedVersion, storedHttpResult, resourceReference, expiresAt, transaction)`

The identity is `(caller_user_id, operation_code, idempotency_key)`. Stable versioned operation
codes such as `Organization.CreateSite.v1` are part of the API contract. Fingerprinting and
same/different request behavior are normative in `README.md`.

The PostgreSQL entity has:

| Field | Contract |
|---|---|
| `command_idempotency_id` | UUID primary key |
| `caller_user_id` | Logical IAM UUID; no cross-schema FK |
| `operation_code` | Nonblank bounded stable operation/version code |
| `idempotency_key` | 1..128 UTF-8 bytes |
| `request_fingerprint` | Exactly 32 SHA-256 bytes |
| target | Nullable scope type/ID and aggregate type/ID |
| `status` | `Pending` or `Completed` |
| Pending recovery | Nullable `pending_owner`, `pending_until`; nonnegative `attempt_count` |
| original response | Nullable-until-complete `original_http_status`, `original_result_payload` or `stable_result_reference`, and allowlisted `Location`/`ETag` response headers |
| resource/error | Nullable `resource_id`, `resource_version`, and stable `error_code`; redacted `last_error` |
| time/version | `created_at`, `updated_at`, nullable `completed_at`, `expires_at`, positive `version` |

Required PostgreSQL rules are unique
`(caller_user_id, operation_code, idempotency_key)`, fingerprint-length/status-shape/HTTP-range/
expiry/version checks, a partial Pending recovery index on `(pending_until, created_at)`, retention
index on `expires_at`, and partial target lookup index where an aggregate ID exists. Completed rows
require completion/response metadata and no lease; Pending rows have no original response.

Registration commits in a short transaction. Business execution uses the host transaction; after
all owner locks/mutations, Integration locks the command row, writes owner events through the
outbox port, and completes the stored response last in the same commit. No raw request, cookie,
authorization/antiforgery/session material, credential, secret, or credential hash is stored.

## Transactional outbox and consumer inbox

`ITransactionalOutboxWriter.EnqueueAsync(envelope, transaction)` writes owner events atomically
with accepted mutations. Envelopes contain event ID, immutable `eventType.v1`, schema version,
producer, aggregate ID/version, occurred-at, safe payload, correlation ID, and causation ID.

`IOutboxClaimRepository` owns `FOR UPDATE SKIP LOCKED` claim, lease renewal, Published transition,
retry scheduling, and terminal Failed transition for `integration.outbox_event`.

`IInboxDeduplicationRepository` owns claim/reclaim, payload-hash validation, Completed transition,
retry scheduling, and terminal Failed transition for `integration.inbox_message`. Its identity is
`(consumer_name,event_id)`. It is event delivery state, never API command replay.

The dispatcher and Audit-consumer transaction are specified in `audit-events.md`; retry policy and
durable control jobs are specified in `operations.md`.

## Distinct identities

| Mechanism | Owner and purpose |
|---|---|
| command idempotency | Integration; caller HTTP mutation replay |
| production attempt | Acquisition; generated slot/checkpoint recovery |
| measurement identity | Telemetry; immutable ingestion result |
| inbox identity | Integration; consumer/event delivery deduplication |
| job idempotency | Operations; durable job scheduling uniqueness |

No mechanism substitutes for another.
