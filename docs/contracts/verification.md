# Local Verification Contract

Every result contains a check identifier, classification, command, UTC timestamp, mandatory flag,
exit code when available, evidence, and optional blocker ID. Classifications are defined by
`specs/001-r0-engineering-foundation/contracts/verification-result.schema.json`.

`scripts/verify.ps1` continues safe independent checks after a blocked check and returns 20 when any
mandatory check is not PASS. Evidence must not contain real credentials. A blocked result can become
PASS only after the prerequisite is approved and the check is executed again.
