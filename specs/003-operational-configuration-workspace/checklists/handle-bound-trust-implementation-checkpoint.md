# Historical Feature 003 Handle-Bound Trust Implementation Checkpoint (superseded 2026-08-04)

Date: 2026-08-04

## 1. Baseline

| Field | Value |
|---|---|
| Starting SHA | `4b4713cb42b1a03270a2688b344988d2945bab2c` |
| Starting branch | `main` (clean; baseline ancestor verified) |
| Corrective branch | `fix/003-handle-bound-trust-closure` |
| Final corrective commit | Created immediately after this checkpoint; no push or merge |

## 2. Spec Kit workflow

| Step | Actual command/status | Evidence |
|---|---|---|
| Analyze | Provider-native `NOT_RUN` | No executable provider command is installed; direct analysis is recorded in `handle-bound-trust-analyze.md` |
| Task append | Direct append `T157-T170` | Unique ledger rows `T001-T170`; T138-T140 reconciled complete; T034 historical classification retained |
| Implement | One bounded direct implementation phase | Red → green → refactor completed for handle trust and process outcomes |
| Checkpoint | This artifact | Explicit stop; no final review/convergence/merge/release |

## 3. Corrective task status

T157-T168 are complete. T169 is this checkpoint. T170 is the commit-and-stop action. The current
ledger contains 170 unique task IDs with no duplicate task rows; T138, T139, and T140 are checked
with later evidence references. Historical T034 remains classified by its existing package/company
approval blocker and was not silently changed.

## 4. Handle-bound policy flow

| Evidence | Result |
|---|---|
| Policy path | Production path remains fixed `%ProgramData%\IUMP\DeploymentTrustPolicy.json`; synthetic path is test-only |
| Policy open | One `SafeFileHandle` via `CreateFile`; file handle shares read only (write/delete sharing denied) |
| Policy read | One byte read from the same `FileStream`/handle; `policyReadCount=1` |
| File identity | `GetFileInformationByHandle` volume serial + file index before/after read; fixture identity stable |
| File security | `GetSecurityInfo` (`SE_FILE_OBJECT`, owner/group/DACL) from the opened file handle |
| Effective access | Windows `AccessCheck` against a duplicated current-process impersonation token |
| Directory security | Immediate and higher ancestors opened with `FILE_FLAG_BACKUP_SEMANTICS`; effective rights are evaluated from their handles |
| Pathname ACL authority | No production `GetAccessControl` or custom Allow/Deny authority remains in `Program.cs` |
| Replacement lock | Fixture replacement attempt while the no-delete-sharing handle is open is blocked |
| Fallback | Unsupported/invalid Windows handle-security capability fails closed as `BLOCKED_BY_MISSING_TOOL`/`BLK-ENV-001` |
| Evidence safety | No SID, raw descriptor, internal path, policy bytes, or secret is emitted |

## 5. Security disposition

The deterministic handle fixture reports stable identity, one policy read, handle-sourced security,
effective file/directory/ancestor unsafe rights for the current owner, replacement blocking, and a
missing-capability scenario that returns a blocked result. No synthetic fixture is production
approval evidence.

## 6. Process classification

`DeploymentTarget.ps1` now carries explicit invocation outcomes. Missing command/project/runtime and
process-start exceptions map to `BLOCKED_BY_MISSING_TOOL`/20/`BLK-ENV-001`. A process that started
but exits with no protocol result maps to `FAIL`/1; malformed/multiple/invalid structured results
also map to `FAIL`. Valid structured results preserve their validated status/classification. Stderr
and evidence remain redacted.

## 7. TDD evidence

Initial RED command:

```text
& powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Verification\deployment-signature.tests.ps1
& powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Verification\deployment-target.tests.ps1
```

RED result: non-zero; signature source contract reported missing `HandleSecurityEvaluator`, and the
target parser reported the started-process crash expected `FAIL`/1 but received the pre-remediation
missing-tool/20 mapping. No RED result was fabricated.

Green focused result after minimal implementation and refactor:

- `deployment-signature.tests.ps1`: `PASS`, `checks=79`, `failures=0`.
- `deployment-target.tests.ps1`: `PASS`, `checks=99`, `failures=0`.

## 8. Verification

| Check | Exit | Status / classification | Evidence |
|---|---:|---|---|
| `dotnet build .\IUMP.slnx --no-restore --configuration Release` | 0 | PASS / RUNNABLE_NOW | 0 warnings, 0 errors |
| Unit (`scripts/test.ps1`) | 0 | PASS / RUNNABLE_NOW | all registered unit/repository suites passed |
| PostgreSQL Integration | 0 | PASS / RUNNABLE_NOW | 15 suites, 0 failures; target `127.0.0.1:5433/iump_dev` only |
| Web `npm run lint` | 0 | PASS / RUNNABLE_NOW | existing Fast Refresh warnings only; no install/download |
| Web `npm run build` | 0 | PASS / RUNNABLE_NOW | Vite/TypeScript build succeeded |
| Repository policy | 0 | PASS / RUNNABLE_NOW | contract passed |
| Architecture | 0 | PASS / RUNNABLE_NOW | boundary contract passed |
| Repository harness | 0 | PASS / RUNNABLE_NOW | contract passed |
| Fast Feature 003 | 0 | PASS / RUNNABLE_NOW | `Harness Fast summary: PASS=14` |
| Full Feature 003 | 20 | BLOCKED | `PASS=17`; `BLK-ENV-003` and `BLK-ENV-005` company-approval blockers; no mandatory FAIL |
| `git diff --check` | 0 | PASS / RUNNABLE_NOW | no whitespace errors |

Full remains non-passing by design. Frontend behavior runner remains separately
`BLOCKED_BY_PACKAGE_POLICY` where applicable; this phase did not install packages or substitute a
runner. PostgreSQL capability remains `AVAILABLE`; no database mutation was performed and port 5432
was not contacted.

## 9. Current post-merge truth

| Field | Current value |
|---|---|
| Main baseline | `4b4713cb42b1a03270a2688b344988d2945bab2c` |
| Corrective branch | `fix/003-handle-bound-trust-closure` |
| Integrated into main | `NO` (this checkpoint precedes commit; no merge performed) |
| Corrective PR | `NO` |
| Reviewer requested | `NO` |
| Independent human review | `NO` |
| GitHub CI/status evidence | `NO` |
| Internal two-axis Standards/Specification self-review | `PASS` carried from prior recorded T152/T153 evidence; no human approval implied |
| AC-005 / AC-011 | `PARTIAL` / `PARTIAL` |
| Acceptance evidence complete | `NO` |
| Release-ready | `NO` |

Historical checkpoints remain historical and are not rewritten. This is the sole current state for
the handle-bound phase.

## 10. Explicit stop

Commit is the next and final action for this invocation. After the exact corrective commit is
created, stop. Do not push, create a PR, merge, run final review/convergence, create Phase 7 or
Spec 004, deploy, or release.
