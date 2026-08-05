# Phase 5 SC-002 Usability Evidence Protocol

**Feature**: 004 Industrial Operations UI/UX Redesign
**Status**: NOT_RUN — this file defines the evidence protocol; it does not claim participant
results.
**Owner**: Phase 5 acceptance owner (named in the completed checkpoint).
**Scope**: Desktop and tablet representative P0 workflows; mobile remains non-regression only.

## Required P0 workflows

1. Navigate to a permitted operational capability and confirm current section and scope.
2. Find a configuration record using the permitted list/filter flow.
3. Inspect current measurement data, including zero versus No Data/Missing and quality context.
4. Investigate a dashboard exception through the permitted detail path.
5. Review a Simulator run and its outcome/next action.

## Attempt and participant rules

- Record the participant role/context and workflow identifier, never names, credentials, secrets, or
  out-of-scope object metadata.
- `valid_attempts` counts only started attempts with an eligible participant and a usable fixture.
- Exclude invalid, aborted-before-start, duplicate, and environment-failure attempts from the
  denominator, and list each exclusion with a reason.
- A successful attempt must complete without facilitator intervention and without a critical
  misunderstanding of scope or status. Facilitator hints make the attempt unsuccessful, not
  successful.
- Record the evidence path (approved notes, screenshots, or recording reference) and limitation or
  small-sample statement; do not invent rendering or participant evidence.

## Required calculation and disposition

```text
success_rate = successful_unassisted_attempts / valid_attempts * 100
SC-002 PASS only when success_rate >= 90% and zero critical misunderstandings are recorded.
```

The completed evidence must record `successful_unassisted_attempts`, `valid_attempts`, excluded or
invalid attempts, participant set, evidence owner, evidence path, and one of `PASS`, `FAIL`,
`BLOCKED`, or `NOT_RUN`. If participants, fixtures, or an approved evidence channel are unavailable,
record the exact blocker and keep SC-002 `BLOCKED` or `NOT_RUN`; never infer a PASS.
