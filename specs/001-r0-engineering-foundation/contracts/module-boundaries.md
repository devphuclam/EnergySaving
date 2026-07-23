# R0 Module Boundary Contract

1. `Api` and `Worker` are composition roots. They may reference module public contracts and approved
   infrastructure adapters.
2. A module owns its Domain/Application behavior and business schema writes. It must not reference a
   host or another module's internal Domain/Infrastructure implementation.
3. `BuildingBlocks` contains stable technical primitives only; it must not contain IUMP business
   entities, workflows, or module-specific persistence.
4. Cross-module effects use versioned integration contracts and transactional outbox/inbox
   semantics. R0 defines persistence and duplicate identity only; no R1 business event is executed.
5. The database adapter is PostgreSQL. Integration verification requires an approved instance; no
   alternate database adapter is accepted as equivalent evidence.
6. No interface, route, message, or field may express OT command/write-back behavior.
7. Architecture tests use project/namespace references as the observable interface and must fail on
   a deliberately forbidden fixture before the enforcing implementation is accepted.
