Set-StrictMode -Version Latest

function New-UnresolvedHarnessFeature {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Evidence)

    [pscustomobject]@{
        resolved = $false
        source = ''
        name = ''
        path = ''
        evidence = $Evidence
    }
}

function Resolve-HarnessFeatureCandidate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RepoRoot,
        [Parameter(Mandatory)][string]$Candidate,
        [Parameter(Mandatory)][string]$Source
    )

    $specsRoot = [IO.Path]::GetFullPath((Join-Path $RepoRoot 'specs'))
    $normalizedCandidate = $Candidate.Replace('/', [IO.Path]::DirectorySeparatorChar)
    $candidatePath = if ($normalizedCandidate -match '^specs[\\/]') {
        [IO.Path]::GetFullPath((Join-Path $RepoRoot $normalizedCandidate))
    }
    else {
        [IO.Path]::GetFullPath((Join-Path $specsRoot $normalizedCandidate))
    }

    $insideSpecs = $candidatePath.StartsWith(
        $specsRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase
    )
    if (-not $insideSpecs -or -not (Test-Path -LiteralPath $candidatePath -PathType Container)) {
        return New-UnresolvedHarnessFeature -Evidence "Feature '$Candidate' is not a valid directory under specs."
    }

    $directory = Get-Item -LiteralPath $candidatePath
    [pscustomobject]@{
        resolved = $true
        source = $Source
        name = $directory.Name
        path = $directory.FullName
        evidence = "Resolved feature '$($directory.Name)' from $Source."
    }
}

function Resolve-HarnessFeature {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RepoRoot,
        [string]$Feature,
        [string]$BranchName
    )

    $resolvedRoot = [IO.Path]::GetFullPath($RepoRoot)
    if (-not [string]::IsNullOrWhiteSpace($Feature)) {
        return Resolve-HarnessFeatureCandidate -RepoRoot $resolvedRoot -Candidate $Feature -Source 'argument'
    }

    $statePath = Join-Path $resolvedRoot '.specify\feature.json'
    if (Test-Path -LiteralPath $statePath -PathType Leaf) {
        try {
            $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
            if ([string]::IsNullOrWhiteSpace([string]$state.feature_directory)) {
                return New-UnresolvedHarnessFeature -Evidence 'Spec Kit feature state does not contain feature_directory.'
            }
            return Resolve-HarnessFeatureCandidate -RepoRoot $resolvedRoot `
                -Candidate ([string]$state.feature_directory) -Source 'state'
        }
        catch {
            return New-UnresolvedHarnessFeature -Evidence 'Spec Kit feature state is not valid JSON.'
        }
    }

    if ([string]::IsNullOrWhiteSpace($BranchName)) {
        $BranchName = (& git -C $resolvedRoot branch --show-current 2>$null | Out-String).Trim()
    }
    if ([string]::IsNullOrWhiteSpace($BranchName)) {
        return New-UnresolvedHarnessFeature -Evidence 'No explicit, state-file, or branch feature could be resolved.'
    }

    $specsRoot = Join-Path $resolvedRoot 'specs'
    if (-not (Test-Path -LiteralPath $specsRoot -PathType Container)) {
        return New-UnresolvedHarnessFeature -Evidence 'The repository does not contain a specs directory.'
    }

    $matches = @(Get-ChildItem -LiteralPath $specsRoot -Directory | Where-Object {
        $BranchName -match ('^' + [regex]::Escape($_.Name) + '(?:$|-)')
    })
    if ($matches.Count -ne 1) {
        return New-UnresolvedHarnessFeature `
            -Evidence "Branch '$BranchName' did not resolve to exactly one feature directory."
    }

    return Resolve-HarnessFeatureCandidate -RepoRoot $resolvedRoot -Candidate $matches[0].Name -Source 'branch'
}

function Test-FeatureArtifacts {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$FeatureResolution,
        [Parameter(Mandatory)]
        [ValidateSet('Fast', 'Full')]
        [string]$Mode
    )

    if (-not $FeatureResolution.resolved) {
        $classification = if ($Mode -eq 'Full') { 'FAIL' } else { 'NOT_RUN' }
        return New-VerificationResult -CheckId 'feature-artifacts' -Classification $classification `
            -Command 'Resolve active Spec Kit feature' -Mandatory ($Mode -eq 'Full') `
            -Evidence ([string]$FeatureResolution.evidence)
    }

    $required = if ($Mode -eq 'Full') { @('spec.md', 'plan.md', 'tasks.md') } else { @('spec.md') }
    $missing = @($required | Where-Object {
        -not (Test-Path -LiteralPath (Join-Path $FeatureResolution.path $_) -PathType Leaf)
    })
    if ($missing.Count -gt 0) {
        return New-VerificationResult -CheckId 'feature-artifacts' -Classification 'FAIL' `
            -Command "Validate $($FeatureResolution.name) Spec Kit artifacts" -Mandatory $true `
            -Evidence "Missing canonical artifact(s): $($missing -join ', ')."
    }

    New-VerificationResult -CheckId 'feature-artifacts' -Classification 'PASS' `
        -Command "Validate $($FeatureResolution.name) Spec Kit artifacts" -Mandatory $true `
        -Evidence "Required $Mode artifacts exist for '$($FeatureResolution.name)'."
}

function Get-HarnessCheckPlan {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('Fast', 'Full')]
        [string]$Mode
    )

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
    if ($Mode -eq 'Fast') {
        return $fast
    }
    return $fast + @('backend-build', 'frontend', 'database', 'ci', 'container-target')
}

function Protect-HarnessEvidence {
    [CmdletBinding()]
    param([AllowEmptyString()][string]$Evidence)

    $protected = $Evidence
    $patterns = @(
        '(?i)(Password\s*=\s*)[^;\s]+',
        '(?i)sk-proj-[A-Za-z0-9_-]+',
        '(?i)(?:ghp|github_pat)_[A-Za-z0-9_]{20,}'
    )
    foreach ($pattern in $patterns) {
        $protected = [regex]::Replace($protected, $pattern, {
            param($match)
            if ($match.Groups.Count -gt 1 -and $match.Groups[1].Success) {
                return $match.Groups[1].Value + 'REDACTED'
            }
            return 'REDACTED'
        })
    }
    return $protected.Trim()
}

function Invoke-HarnessCheck {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$CheckId,
        [Parameter(Mandatory)][string]$Command,
        [Parameter(Mandatory)][scriptblock]$Action,
        [bool]$Mandatory = $true,
        [string]$BlockedClassification = 'BLOCKED_BY_MISSING_TOOL',
        [AllowNull()][string]$BlockerId = $null
    )

    try {
        $global:LASTEXITCODE = 0
        $output = (& $Action 2>&1 | Out-String)
        $exitCode = [int]$LASTEXITCODE
        $classification = if ($exitCode -eq 0) {
            'PASS'
        }
        elseif ($exitCode -eq 20) {
            $BlockedClassification
        }
        else {
            'FAIL'
        }
        $evidence = Protect-HarnessEvidence -Evidence $output
        if ([string]::IsNullOrWhiteSpace($evidence)) {
            $evidence = "Command completed with exit code $exitCode."
        }
        return New-VerificationResult -CheckId $CheckId -Classification $classification `
            -Command $Command -Mandatory $Mandatory -Evidence $evidence -ExitCode $exitCode `
            -BlockerId $(if ($classification -like 'BLOCKED_*') { $BlockerId } else { $null })
    }
    catch {
        $evidence = Protect-HarnessEvidence -Evidence $_.Exception.Message
        return New-VerificationResult -CheckId $CheckId -Classification 'FAIL' `
            -Command $Command -Mandatory $Mandatory -Evidence $evidence -ExitCode 1
    }
}
