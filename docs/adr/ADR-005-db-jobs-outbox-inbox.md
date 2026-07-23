# ADR-005: Database Jobs and Transactional Outbox/Inbox

**Status:** Accepted; database execution blocked in current environment  
**Date:** 2026-07-23  
**Reference:** DOC-05 §14; DOC-06 §23

Use PostgreSQL-backed jobs and transactional outbox/inbox so business state and outgoing events commit
atomically. Delivery is at-least-once; idempotency keys and consumer inbox records provide an
effectively-once business outcome. An external broker is deferred until measured queue lag,
throughput, or isolation needs justify the added infrastructure.
