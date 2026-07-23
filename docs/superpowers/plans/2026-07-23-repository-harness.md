# IUMP Repository Harness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provide one version-controlled command that guides Codex, OpenCode, and developers through project context, active Spec Kit feature validation, and honest Fast or Full verification.

**Architecture:** `scripts/harness.ps1` is the only public harness interface. Focused functions in `scripts/common/Harness.ps1` resolve the active feature, validate canonical artifacts, define mode-specific checks, and aggregate existing verification results. Existing build and test scripts remain the implementations behind that interface, while `scripts/verify.ps1` becomes a backward-compatible Full-mode wrapper.

**Tech Stack:** PowerShell 7-compatible scripts, .NET 10, existing repository verification contracts, JSON, Spec Kit 0.13.2.

## Global Constraints

- Do not use or create containers, public marketplace actions, or downloaded tools.
- Do not run package install or restore against public sources.
- Do not expose real credentials in console output or verification evidence.
- PostgreSQL remains the only accepted database adapter.
- Spec Kit remains the canonical feature specification, plan, and task system.
- Preserve all pre-existing uncommitted OpenCode integration and `specs/002-asset-simulator-latest/` changes.
- Use `apply_patch` for authored file edits.
- Every production behavior follows RED, GREEN, REFACTOR.

---

## File Map

| Path | Responsibility |
|---|---|
| `scripts/common/Verification.ps1` | Canonical result object and aggregate exit-code semantics |
| `docs/contracts/verification-result.schema.json` | Project-wide machine-readable result schema |
| `scripts/common/Harness.ps1` | Feature resolution, artifact validation, check execution, and profile planning |
| `scripts/harness.ps1` | Public Fast/Full command, result persistence, summary, and process exit |
| `scripts/verify.ps1` | Backward-compatible wrapper for `harness.ps1 -Mode Full` |
| `tests/Verification/verification-contract.tests.ps1` | Result schema and exit-code contract |
| `tests/Verification/repository-harness.tests.ps1` | Harness core and profile contract tests |
| `tests/Verification/repository-scope.tests.ps1` | Stable, mechanically detectable repository invariants |
| `tests/Verification/repository-scope-red-fixture.tests.ps1` | Proof that permanent scope violations are detected |
| `scripts/test.ps1` | Runs all verification contracts and unit tests |
| `docs/repository-harness.md` | Knowledge map and operating instructions |
| `AGENTS.md` | Short mandatory entry/completion rules and pointer to the knowledge map |
| `README.md` | Project landing page and supported harness commands |
| `docs/contracts/verification.md` | Human-readable result and exit-code contract |

---

### Task 1: Generalize Verification Results and Correct Exit Semantics

**Files:**
- Modify: `tests/Verification/verification-contract.tests.ps1`
- Modify: `scripts/common/Verification.ps1`
- Create: `docs/contracts/verification-result.schema.json`
- Modify: `docs/contracts/verification.md`

**Interfaces:**
- Consumes: existing `New-VerificationResult` result shape.
- Produces: `Get-VerificationExitCode -Results <object[]>` returning `0` for all mandatory PASS, `1` for any mandatory FAIL, and `20` for mandatory blocked/NOT_RUN results when no FAIL exists.

- [ ] **Step 1: Write failing exit-code and schema tests**

Add the following assertions after the existing blocked assertion in
`tests/Verification/verification-contract.tests.ps1`:

```powershell
if ((Get-VerificationExitCode -Results @($blocked)) -ne 20) {
    throw 'Mandatory blocked checks must produce aggregate exit code 20.'
}

$failed = New-VerificationResult -CheckId 'architecture' -Classification 'FAIL' `
    -Command 'architecture.tests.ps1' -Mandatory $true -Evidence 'forbidden dependency'
if ((Get-VerificationExitCode -Results @($failed)) -ne 1) {
    throw 'Mandatory failed checks must produce aggregate exit code 1.'
}
if ((Get-VerificationExitCode -Results @($blocked, $failed)) -ne 1) {
    throw 'FAIL must take precedence over blocked results.'
}

$schemaPath = Join-Path $repoRoot 'docs\contracts\verification-result.schema.json'
if (-not (Test-Path -LiteralPath $schemaPath)) {
    throw "Project-wide verification schema is missing: $schemaPath"
}
$schema = Get-Content -LiteralPath $schemaPath -Raw | ConvertFrom-Json
if ($schema.'$id' -ne 'urn:iump:verification-result:v1') {
    throw 'Verification schema must use the project-wide identifier.'
}
$schemaClassifications = @($schema.properties.classification.enum)
foreach ($classification in @(
    'PASS',
    'FAIL',
    'NOT_RUN',
    'BLOCKED_BY_MISSING_TOOL',
    'BLOCKED_BY_PACKAGE_POLICY',
    'BLOCKED_BY_DATABASE_ACCESS',
    'BLOCKED_BY_COMPANY_APPROVAL'
)) {
    if ($classification -notin $schemaClassifications) {
        throw "Verification schema is missing classification '$classification'."
    }
}
```

Replace the old blocked non-zero assertion so the test requires the exact value `20`.

- [ ] **Step 2: Run the contract test and verify RED**

Run:

```powershell
& .\tests\Verification\verification-contract.tests.ps1
```

Expected: FAIL because a mandatory `FAIL` currently returns `20` and the project-wide schema does
not exist.

- [ ] **Step 3: Implement minimal exit-code precedence**

Replace `Get-VerificationExitCode` in `scripts/common/Verification.ps1` with:

```powershell
function Get-VerificationExitCode {
    [CmdletBinding()]
    param([Parameter(Mandatory)][object[]]$Results)

    $mandatory = @($Results | Where-Object { $_.mandatory })
    if (@($mandatory | Where-Object { $_.classification -eq 'FAIL' }).Count -gt 0) {
        return 1
    }
    if (@($mandatory | Where-Object { $_.classification -ne 'PASS' }).Count -gt 0) {
        return 20
    }
    return 0
}
```

Create `docs/contracts/verification-result.schema.json` by copying the existing result property
contract and changing only its identity metadata:

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "urn:iump:verification-result:v1",
  "title": "IUMP Verification Result",
  "type": "object",
  "additionalProperties": false,
  "required": ["checkId", "classification", "command", "timestamp", "mandatory"],
  "properties": {
    "checkId": { "type": "string", "minLength": 1 },
    "classification": {
      "enum": [
        "PASS",
        "FAIL",
        "NOT_RUN",
        "BLOCKED_BY_MISSING_TOOL",
        "BLOCKED_BY_PACKAGE_POLICY",
        "BLOCKED_BY_DATABASE_ACCESS",
        "BLOCKED_BY_COMPANY_APPROVAL"
      ]
    },
    "command": { "type": "string", "minLength": 1 },
    "timestamp": { "type": "string", "format": "date-time" },
    "mandatory": { "type": "boolean" },
    "exitCode": { "type": ["integer", "null"] },
    "evidence": { "type": "string" },
    "blockerId": { "type": ["string", "null"] }
  }
}
```

Update `docs/contracts/verification.md` to reference the new schema and document exit codes `0`,
`1`, and `20`. Keep the R0 schema under the historical feature directory unchanged.

- [ ] **Step 4: Run the contract test and verify GREEN**

Run:

```powershell
& .\tests\Verification\verification-contract.tests.ps1
```

Expected: `PASS: verification result contract`.

- [ ] **Step 5: Commit Task 1**

```powershell
git add -- scripts/common/Verification.ps1 tests/Verification/verification-contract.tests.ps1 docs/contracts/verification-result.schema.json docs/contracts/verification.md
git commit -m "feat: generalize verification result contract"
```

---

### Task 2: Build the Testable Harness Core

**Files:**
- Create: `tests/Verification/repository-harness.tests.ps1`
- Create: `scripts/common/Harness.ps1`

**Interfaces:**
- Consumes: `New-VerificationResult` and `Get-VerificationExitCode` from `scripts/common/Verification.ps1`.
- Produces:
  - `Resolve-HarnessFeature -RepoRoot <string> [-Feature <string>] [-BranchName <string>]`
  - `Test-FeatureArtifacts -FeatureResolution <object> -Mode <Fast|Full>`
  - `Get-HarnessCheckPlan -Mode <Fast|Full>`
  - `Invoke-HarnessCheck -CheckId <string> -Command <string> -Action <scriptblock>
    [-Mandatory <bool>] [-BlockedClassification <string>] [-BlockerId <string>]`

- [ ] **Step 1: Write failing feature-resolution tests**

Create `tests/Verification/repository-harness.tests.ps1`. The test must:

1. Create an isolated directory with `specs/001-alpha/spec.md`,
   `specs/002-beta/spec.md`, `plan.md`, and `tasks.md`.
2. Dot-source `scripts/common/Verification.ps1` and fail with a clear RED message when
   `scripts/common/Harness.ps1` is absent.
3. Assert these cases:

```powershell
$explicit = Resolve-HarnessFeature -RepoRoot $fixture -Feature '002-beta'
Assert-Equal $explicit.name '002-beta' 'explicit feature name'
Assert-Equal $explicit.source 'argument' 'explicit feature source'

$relative = Resolve-HarnessFeature -RepoRoot $fixture -Feature 'specs/001-alpha'
Assert-Equal $relative.name '001-alpha' 'relative feature path'

$fromState = Resolve-HarnessFeature -RepoRoot $fixture
Assert-Equal $fromState.name '002-beta' 'state-file resolution'
Assert-Equal $fromState.source 'state' 'state-file source'

$fromBranch = Resolve-HarnessFeature -RepoRoot $fixture -BranchName '001-alpha-work'
Assert-Equal $fromBranch.name '001-alpha' 'branch resolution'

$traversal = Resolve-HarnessFeature -RepoRoot $fixture -Feature '..\outside'
Assert-Equal $traversal.resolved $false 'path traversal rejection'

$missing = Resolve-HarnessFeature -RepoRoot $fixture -Feature '999-missing'
Assert-Equal $missing.resolved $false 'missing feature rejection'
```

Write `.specify/feature.json` inside the fixture using:

```powershell
New-Item -ItemType Directory -Path (Join-Path $fixture '.specify') | Out-Null
@{ feature_directory = 'specs/002-beta' } |
    ConvertTo-Json |
    Set-Content -LiteralPath (Join-Path $fixture '.specify\feature.json') -Encoding UTF8
```

Always remove the exact temporary fixture inside `finally`.

- [ ] **Step 2: Add failing artifact and profile assertions**

In the same test, assert:

```powershell
$fullArtifacts = Test-FeatureArtifacts -FeatureResolution $explicit -Mode Full
Assert-Equal $fullArtifacts.classification 'PASS' 'full artifact set'

Remove-Item -LiteralPath (Join-Path $explicit.path 'tasks.md')
$missingTasks = Test-FeatureArtifacts -FeatureResolution $explicit -Mode Full
Assert-Equal $missingTasks.classification 'FAIL' 'full mode missing tasks'

$fastArtifacts = Test-FeatureArtifacts -FeatureResolution $explicit -Mode Fast
Assert-Equal $fastArtifacts.classification 'PASS' 'fast mode requires spec only'

$unresolvedFast = Test-FeatureArtifacts -FeatureResolution $missing -Mode Fast
Assert-Equal $unresolvedFast.classification 'NOT_RUN' 'fast mode without feature'

$unresolvedFull = Test-FeatureArtifacts -FeatureResolution $missing -Mode Full
Assert-Equal $unresolvedFull.classification 'FAIL' 'full mode without feature'

$fastPlan = @(Get-HarnessCheckPlan -Mode Fast)
$fullPlan = @(Get-HarnessCheckPlan -Mode Full)
foreach ($fullOnly in @('backend-build', 'frontend', 'database', 'ci', 'container-target')) {
    if ($fullOnly -in $fastPlan) { throw "Fast mode contains Full-only check '$fullOnly'." }
    if ($fullOnly -notin $fullPlan) { throw "Full mode is missing '$fullOnly'." }
}
```

- [ ] **Step 3: Run the harness test and verify RED**

Run:

```powershell
& .\tests\Verification\repository-harness.tests.ps1
```

Expected: FAIL with `RED: harness interface is missing`.

- [ ] **Step 4: Implement feature resolution and artifact validation**

Create `scripts/common/Harness.ps1` with strict mode and the four public functions. Implement
resolution with these rules:

```powershell
$specsRoot = [IO.Path]::GetFullPath((Join-Path $RepoRoot 'specs'))
$candidatePath = [IO.Path]::GetFullPath((Join-Path $RepoRoot $candidate))
$insideSpecs = $candidatePath.StartsWith(
    $specsRoot + [IO.Path]::DirectorySeparatorChar,
    [StringComparison]::OrdinalIgnoreCase
)
```

Never accept a candidate unless `$insideSpecs` is true and the path is an existing directory.
Argument resolution runs first. State resolution reads `.specify/feature.json`. Branch resolution
matches one and only one directory whose name is an exact prefix followed by end-of-string or `-`.

Return a resolution object with exactly:

```powershell
[pscustomobject]@{
    resolved = $true
    source = 'argument'
    name = $directory.Name
    path = $directory.FullName
    evidence = "Resolved feature '$($directory.Name)' from argument."
}
```

Return the same keys with `resolved = $false`, empty `name`/`path`, and sanitized evidence on
failure.

`Test-FeatureArtifacts` returns a `feature-artifacts` verification result. Fast requires only
`spec.md`; Full requires `spec.md`, `plan.md`, and `tasks.md`. An unresolved feature is `NOT_RUN`
and non-mandatory in Fast, but `FAIL` and mandatory in Full.

`Get-HarnessCheckPlan` returns:

```powershell
$fast = @(
    'feature-artifacts',
    'verification-contract',
    'repository-harness',
    'repository-policy',
    'repository-scope',
    'architecture',
    'architecture-red-fixture',
    'unit'
)
```

Full returns `$fast` plus `backend-build`, `frontend`, `database`, `ci`, and `container-target`.

`Invoke-HarnessCheck` captures output and `$LASTEXITCODE`, maps `0` to PASS, `20` to the caller's
provided blocked classification, every other non-zero exit to FAIL, catches exceptions as FAIL, and
returns `New-VerificationResult`. It must redact values matching password, OpenAI key, and GitHub
token patterns before storing evidence.

- [ ] **Step 5: Run the harness test and verify GREEN**

Run:

```powershell
& .\tests\Verification\repository-harness.tests.ps1
```

Expected: `PASS: repository harness contract`.

- [ ] **Step 6: Commit Task 2**

```powershell
git add -- scripts/common/Harness.ps1 tests/Verification/repository-harness.tests.ps1
git commit -m "feat: add repository harness core"
```

---

### Task 3: Replace the R0 Scope Freeze with Permanent Invariants

**Files:**
- Create: `tests/Verification/repository-scope-red-fixture.tests.ps1`
- Modify: `tests/Verification/repository-scope.tests.ps1`
- Modify: `scripts/test.ps1`

**Interfaces:**
- Consumes: optional `-ScanRoots <string[]>` in the scope test for isolated fixtures.
- Produces: stable enforcement of no Modbus, write-back, equipment commands, AI/ML product surface,
  and unapproved container workflow artifacts without rejecting all R1 implementation.

- [ ] **Step 1: Write a failing red-fixture proof**

Create `tests/Verification/repository-scope-red-fixture.tests.ps1`. It creates a temporary
`src/Forbidden/WriteBackCommand.cs`, invokes:

```powershell
& $scopeTest -ScanRoots @($fixtureRoot)
```

and passes only when the scope test throws a message matching:

```text
Permanent product invariant violated*
```

The test must clean up the exact temporary directory in `finally`.

- [ ] **Step 2: Run the new fixture test and verify RED**

Run:

```powershell
& .\tests\Verification\repository-scope-red-fixture.tests.ps1
```

Expected: FAIL because `repository-scope.tests.ps1` does not accept `-ScanRoots` and still encodes
an R0-only scope freeze.

- [ ] **Step 3: Implement stable invariant scanning**

Change the parameter block in `repository-scope.tests.ps1` to:

```powershell
[CmdletBinding()]
param([string[]]$ScanRoots)
```

When `ScanRoots` is empty, use `src`, `database`, and `scripts`. Replace R0-specific wording with
permanent invariants:

```powershell
$patterns = @(
    '(?i)\bModbus\b',
    '(?i)\bWriteBack\b|write[- ]back',
    '(?i)\bSetpoint\b|\bActuat(?:e|or|ion)\b',
    '(?i)\bEquipmentCommand\b|\bControlCommand\b',
    '(?i)\bAI/ML\b'
)
```

Throw:

```powershell
throw "Permanent product invariant violated by '$pattern' in $($_.FullName)"
```

Do not reject Simulator, hierarchy, ingestion, or latest-value names. Container/public-CI policy
continues to be enforced by `repository-policy.tests.ps1`.

Add both `repository-harness.tests.ps1` and `repository-scope-red-fixture.tests.ps1` to the
verification list in `scripts/test.ps1`.

- [ ] **Step 4: Run scope and complete script tests and verify GREEN**

Run:

```powershell
& .\tests\Verification\repository-scope.tests.ps1
& .\tests\Verification\repository-scope-red-fixture.tests.ps1
& .\scripts\test.ps1
```

Expected: both scope scripts print PASS; `scripts/test.ps1` exits `0`.

- [ ] **Step 5: Commit Task 3**

```powershell
git add -- tests/Verification/repository-scope.tests.ps1 tests/Verification/repository-scope-red-fixture.tests.ps1 scripts/test.ps1
git commit -m "feat: enforce permanent repository invariants"
```

---

### Task 4: Add the Public Fast and Full Harness Command

**Files:**
- Modify: `tests/Verification/repository-harness.tests.ps1`
- Create: `scripts/harness.ps1`
- Replace: `scripts/verify.ps1`

**Interfaces:**
- Consumes: the Task 2 harness core, existing `scripts/test.ps1`, `scripts/build.ps1`, and the
  existing frontend dependency tree.
- Produces:
  - `scripts/harness.ps1 -Mode Fast [-Feature <string>]`
  - `scripts/harness.ps1 -Mode Full [-Feature <string>]`
  - compatibility command `scripts/verify.ps1 [-Feature <string>]`

- [ ] **Step 1: Add failing process-level harness assertions**

Extend `repository-harness.tests.ps1` to verify:

```powershell
$entry = Join-Path $repoRoot 'scripts\harness.ps1'
if (-not (Test-Path -LiteralPath $entry)) {
    throw "RED: public harness command is missing at $entry"
}

$entryText = Get-Content -LiteralPath $entry -Raw
foreach ($requiredText in @(
    '[ValidateSet(''Fast'', ''Full'')]',
    'verification-results.json',
    'Get-HarnessExitCode',
    'Resolve-HarnessFeature'
)) {
    if ($entryText -notmatch [regex]::Escape($requiredText)) {
        throw "Public harness command is missing '$requiredText'."
    }
}

$verifyText = Get-Content -LiteralPath (Join-Path $repoRoot 'scripts\verify.ps1') -Raw
if ($verifyText -notmatch 'harness\.ps1' -or $verifyText -notmatch 'Full') {
    throw 'verify.ps1 must delegate to Full harness mode.'
}
```

- [ ] **Step 2: Run the harness test and verify RED**

Run:

```powershell
& .\tests\Verification\repository-harness.tests.ps1
```

Expected: FAIL because `scripts/harness.ps1` is missing.

- [ ] **Step 3: Implement `scripts/harness.ps1`**

The script parameter block is:

```powershell
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Fast', 'Full')]
    [string]$Mode,
    [string]$Feature
)
```

It dot-sources `Verification.ps1` and `Harness.ps1`, resolves the feature, adds the artifact result,
then executes the selected plan.

Use one local helper in the entry script:

```powershell
function Add-ScriptCheck {
    param(
        [string]$CheckId,
        [string]$RelativePath,
        [string]$BlockedClassification = 'BLOCKED_BY_MISSING_TOOL',
        [string]$BlockerId
    )
    $scriptPath = Join-Path $repoRoot $RelativePath
    $results.Add((Invoke-HarnessCheck -CheckId $CheckId -Command "& .\$RelativePath" `
        -Action { & $scriptPath } -BlockedClassification $BlockedClassification `
        -BlockerId $BlockerId))
}
```

Fast executes each verification contract script and the .NET unit project directly. Full executes
the Fast set plus:

- `scripts/build.ps1`;
- frontend `npm run lint` and `npm run build` only when `src/Web/node_modules` exists;
- `psql service=$env:PGSERVICE --no-psqlrc --command "SELECT 1"` only when `psql` and `PGSERVICE`
  exist;
- explicit `BLOCKED_BY_PACKAGE_POLICY`, `BLOCKED_BY_DATABASE_ACCESS`, or
  `BLOCKED_BY_COMPANY_APPROVAL` results when prerequisites are absent;
- the existing CI and container-target approval checks.

Do not print `PGSERVICE` or connection output containing credentials. Persist:

```powershell
$results | ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath (Join-Path $repoRoot 'verification-results.json') -Encoding UTF8
```

Print each result using `Write-VerificationResult`, print one final count summary, and exit with:

```powershell
exit (Get-HarnessExitCode -Results @($results))
```

- [ ] **Step 4: Replace `scripts/verify.ps1` with a compatibility wrapper**

Use:

```powershell
[CmdletBinding()]
param([string]$Feature)

$arguments = @('-Mode', 'Full')
if (-not [string]::IsNullOrWhiteSpace($Feature)) {
    $arguments += @('-Feature', $Feature)
}

& (Join-Path $PSScriptRoot 'harness.ps1') @arguments
exit $LASTEXITCODE
```

This direction prevents recursion: `verify.ps1` calls Full harness; the harness never calls
`verify.ps1`.

- [ ] **Step 5: Run contract and Fast-mode verification**

Run:

```powershell
& .\tests\Verification\repository-harness.tests.ps1
& .\scripts\harness.ps1 -Mode Fast -Feature 002-asset-simulator-latest
```

Expected:

- contract test prints `PASS: repository harness contract`;
- Fast mode does not run frontend, database, CI, or container-target checks;
- Fast mode exits `0` because the approved design requires only `spec.md` in Fast;
- `verification-results.json` is valid JSON and contains `feature-artifacts`.

- [ ] **Step 6: Run Full mode and verify honest blocker behavior**

Run:

```powershell
& .\scripts\harness.ps1 -Mode Full -Feature 002-asset-simulator-latest
$fullExit = $LASTEXITCODE
$fullExit
```

Expected at the current repository stage: exit `1` because feature 002 lacks `plan.md` and
`tasks.md`. Database/CI restrictions appear as blockers, not passes. Full mode must still run safe
independent checks after detecting the missing artifacts.

- [ ] **Step 7: Commit Task 4**

```powershell
git add -- scripts/harness.ps1 scripts/verify.ps1 tests/Verification/repository-harness.tests.ps1 verification-results.json
git commit -m "feat: add fast and full repository harness"
```

---

### Task 5: Make the Harness Discoverable to Humans and Agents

**Files:**
- Create: `docs/repository-harness.md`
- Modify: `AGENTS.md`
- Modify: `README.md`
- Modify: `docs/source-register.md`

**Interfaces:**
- Consumes: Task 4 public commands and existing source hierarchy.
- Produces: one progressive-disclosure knowledge map and unambiguous completion rule.

- [ ] **Step 1: Write the knowledge-map acceptance check**

Add static assertions to `repository-harness.tests.ps1` requiring:

```powershell
$knowledgeMap = Join-Path $repoRoot 'docs\repository-harness.md'
if (-not (Test-Path -LiteralPath $knowledgeMap)) {
    throw 'Repository harness knowledge map is missing.'
}
$knowledgeText = Get-Content -LiteralPath $knowledgeMap -Raw
foreach ($required in @(
    'Business Docs',
    'CONTEXT.md',
    'docs/source-register.md',
    'spec.md',
    'plan.md',
    'tasks.md',
    'scripts/harness.ps1 -Mode Fast',
    'scripts/harness.ps1 -Mode Full'
)) {
    if ($knowledgeText -notmatch [regex]::Escape($required)) {
        throw "Knowledge map is missing '$required'."
    }
}

$agentsText = Get-Content -LiteralPath (Join-Path $repoRoot 'AGENTS.md') -Raw
if ($agentsText -notmatch 'docs/repository-harness\.md') {
    throw 'AGENTS.md does not point to the repository harness.'
}
if ($agentsText -notmatch 'Full') {
    throw 'AGENTS.md does not require Full verification before completion.'
}
```

- [ ] **Step 2: Run the contract test and verify RED**

Run:

```powershell
& .\tests\Verification\repository-harness.tests.ps1
```

Expected: FAIL because `docs/repository-harness.md` is missing.

- [ ] **Step 3: Write `docs/repository-harness.md`**

The document contains:

1. The two harness commands and their purpose.
2. Active feature resolution order.
3. The required-context table from the approved design.
4. Source precedence: DOC-01..DOC-07, ADRs, active Spec Kit feature, `CONTEXT.md`, code/tests.
5. The Spec Kit lifecycle and Matt-skill role without duplicate artifacts.
6. Exit code and evidence classification table.
7. Current restrictions: no public package source, public CI, containers, credentials, or alternate
   database.
8. A completion checklist requiring a fresh Full result and explicit disclosure of blockers.

- [ ] **Step 4: Update repository entry documents**

Add a short `Repository harness` section near the top of `AGENTS.md`:

```markdown
## Repository harness

Read `docs/repository-harness.md` before making changes. Use Fast mode while iterating and run a
fresh Full mode before claiming completion. A blocked check must be reported as blocked and must
never be described as passing.
```

Update `README.md` to:

- title the repository as IDEA Utility Monitoring Platform, not only R0 foundation;
- describe R0 as the completed foundation and R1/VS-01 as the current active feature;
- replace `Stop after R0` with “work only inside the active Spec Kit feature scope”;
- make Fast and Full harness commands the approved local flow;
- retain the approved no-source restore and existing dependency restrictions;
- keep verified, not verified, environment blocker, and company-approval sections factually honest.

Update `docs/source-register.md` by renaming `Use in R0` to `Repository use`, retaining the source
precedence, and adding active feature artifacts as the current delivery authority subordinate to
DOC-01..DOC-07.

- [ ] **Step 5: Run documentation contract and Fast harness**

Run:

```powershell
& .\tests\Verification\repository-harness.tests.ps1
& .\scripts\harness.ps1 -Mode Fast -Feature 002-asset-simulator-latest
```

Expected: contract test PASS and Fast harness exit `0`.

- [ ] **Step 6: Commit Task 5**

```powershell
git add -- docs/repository-harness.md AGENTS.md README.md docs/source-register.md tests/Verification/repository-harness.tests.ps1 verification-results.json
git commit -m "docs: publish repository harness workflow"
```

---

### Task 6: Final Cross-Repository Verification

**Files:**
- Verify only unless a failing harness test exposes a defect in a Task 1–5 file.

**Interfaces:**
- Consumes: the complete harness.
- Produces: fresh evidence and a precise statement of PASS, FAIL, and BLOCKED checks.

- [ ] **Step 1: Run whitespace and change-scope checks**

Run:

```powershell
git diff --check HEAD~5..HEAD
git status --short
```

Expected: no whitespace errors. Confirm every remaining uncommitted path belonged to the user before
harness work or is intentionally left for the active feature.

- [ ] **Step 2: Run all local script tests**

Run:

```powershell
& .\scripts\test.ps1
```

Expected: exit `0`; all verification contracts, red-fixture proofs, and unit tests pass.

- [ ] **Step 3: Run Fast harness**

Run:

```powershell
& .\scripts\harness.ps1 -Mode Fast -Feature 002-asset-simulator-latest
```

Expected: exit `0` and no Full-only checks in `verification-results.json`.

- [ ] **Step 4: Run Full harness**

Run:

```powershell
& .\scripts\harness.ps1 -Mode Full -Feature 002-asset-simulator-latest
$LASTEXITCODE
```

Expected until feature planning is complete: exit `1` for missing `plan.md`/`tasks.md`, while safe
independent build and policy checks still run and database/CI constraints remain classified as
blocked. If feature planning artifacts now exist, the expected aggregate becomes `20` while
environmental blockers remain.

- [ ] **Step 5: Inspect the machine-readable evidence**

Run:

```powershell
$results = Get-Content -LiteralPath '.\verification-results.json' -Raw | ConvertFrom-Json
$results | Group-Object classification | Select-Object Name,Count
$results | Where-Object { $_.mandatory -and $_.classification -ne 'PASS' } |
    Select-Object checkId,classification,blockerId,evidence
```

Expected: valid JSON; no credential values; each mandatory non-PASS result has actionable evidence.

- [ ] **Step 6: Review against the approved design**

Open `docs/superpowers/specs/2026-07-23-repository-harness-design.md` and verify each acceptance
criterion against the new command, tests, docs, and evidence. Record any actual gap before claiming
completion.
