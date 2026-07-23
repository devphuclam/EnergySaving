# CI Readiness

Status: `CI_EXECUTION_BLOCKED_PENDING_COMPANY_RUNNER`

The prior workflow referenced `actions/checkout`, `actions/setup-dotnet`, an Ubuntu hosted runner,
and a PostgreSQL container image. Those dependencies are not approved under the current policy and
must not be used or downloaded. No replacement hosted pipeline is asserted.

## Internal runner requirements

- Company-approved Windows or Linux runner with Git, .NET SDK 10.0.300 (or approved compatible
  pinned SDK), Node.js 24.16.0, npm 11.13.0, and PostgreSQL client/server access.
- Approved internal/local NuGet and npm sources with every lockfile dependency mirrored.
- Checkout/build facilities supplied by company-controlled templates; no public marketplace action.
- Non-administrator execution, protected secrets, TLS validation, and network egress policy.
- Workspace cleanup, artifact retention, log redaction, and immutable build metadata.

## Required lanes

1. Fast: format/lint, backend build, frontend typecheck/lint/build, unit and architecture tests.
2. Main: approved PostgreSQL integration/contract tests, migration clean/upgrade, secret pattern
   scan, dependency inventory, and smoke checks.
3. Release: security/fault/performance evidence, backup/restore and controlled approval.

Until an internal runner/template is supplied, `scripts/verify.ps1` is the local equivalent. It
must preserve blocked classifications and must not restore, install, or contact public registries.
