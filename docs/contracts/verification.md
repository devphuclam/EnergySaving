# Local Verification Contract

Every result contains a check identifier, classification, command, UTC timestamp, mandatory flag,
exit code when available, evidence, and optional blocker ID. The project-wide contract is defined by
`docs/contracts/verification-result.schema.json`. Historical feature schemas remain with their
original Spec Kit artifacts.

The aggregate exit code is `0` when all mandatory checks pass, `1` when any mandatory check fails,
and `20` when no mandatory check fails but at least one is blocked or not run. Verification
continues safe independent checks after a failure or blocker. Evidence must not contain real
credentials. A blocked result can become PASS only after the prerequisite is approved and the check
is executed again.
