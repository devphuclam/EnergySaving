# Phase 9 RED Evidence

Baseline was verified at `dc90503639f1fc89af5b2edec8ecd10b0803257e` before this phase.

## Expected RED

Command: `dotnet build .\IUMP.slnx --no-restore --configuration Debug`

Exit code: **1** (expected). The new T170-T181 test seams failed to compile because the Phase 9
Integration, Audit, Worker, API and fake ports were not yet implemented. No production Phase 9
source was changed before this red run.

The compile failures were the expected missing namespaces/types (`CommandFingerprintV1`, command
idempotency domain, delivery contracts, dispatcher, Audit ports and endpoint policies). This is
the required test-first red boundary; the baseline build itself was green immediately before the
new tests were added.

## Green gate

After implementation, the same no-restore build and the focused T170-T181 runner must exit **0**.
Any package-policy or runtime database evidence remains classified as BLOCKED and is not counted
as a pass.
