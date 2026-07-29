# Phase 9 final contract-alignment RED evidence

Parent baseline: `2ba23ca10dce6a051ac6cfe1e9806258023d1826` (verified with
`git rev-parse HEAD`). A temporary native worktree at that exact commit ran the
contract-alignment probe before the green corrections. It was removed after evidence capture.

## True RED command and result

`tests/Verification/phase9-final-red.ps1` — **exit 1**; output:
`Phase9ContractAlignmentRed: failures=5`.

The baseline probe executed source-level contract assertions and found:

1. duplicated `CommandFingerprintV1` implementation;
2. mutation endpoints without the transactional executor and with plain executor usage;
3. T178 endpoint evidence not invoking the real delegate/ports;
4. placeholder Web routes (`/auth/session`, `/simulators/current`, or `/points/current/latest`).

No database command, package install/download, Docker command, or port `5432` connection ran.

## Green closure

The corrected tree now removes the duplicate implementation, routes all configuration and
Simulator mutations through the transactional executor, executes T170–T181 behavior against
provider-neutral fakes, and aligns the Web gateway with the backend route contracts. Debug and
Release builds/runners, architecture checks, Fast harness and Web lint/build are recorded in the
Phase 9 checkpoint. T218 remains unchecked and blocked by package policy; T224+ remain untouched.
