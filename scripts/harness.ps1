[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Fast', 'Full')]
    [string]$Mode,
    [string]$Feature
)

$ErrorActionPreference = 'Continue'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'common\Verification.ps1')
. (Join-Path $PSScriptRoot 'common\Harness.ps1')

$results = [System.Collections.Generic.List[object]]::new()
$featureResolution = Resolve-HarnessFeature -RepoRoot $repoRoot -Feature $Feature
$results.Add((Test-FeatureArtifacts -FeatureResolution $featureResolution -Mode $Mode))
$checkPlan = @(Get-HarnessCheckPlan -Mode $Mode)

function Add-ScriptCheck {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$CheckId,
        [Parameter(Mandatory)][string]$RelativePath,
        [string]$BlockedClassification = 'BLOCKED_BY_MISSING_TOOL',
        [AllowNull()][string]$BlockerId = $null
    )

    $scriptPath = Join-Path $repoRoot $RelativePath
    $action = { & $scriptPath }.GetNewClosure()
    $result = Invoke-HarnessCheck -CheckId $CheckId -Command "& .\$RelativePath" `
        -Action $action -BlockedClassification $BlockedClassification -BlockerId $BlockerId
    $results.Add($result)
}

$scriptChecks = [ordered]@{
    'verification-contract' = 'tests\Verification\verification-contract.tests.ps1'
    'repository-harness' = 'tests\Verification\repository-harness.tests.ps1'
    'repository-policy' = 'tests\Verification\repository-policy.tests.ps1'
    'repository-scope' = 'tests\Verification\repository-scope.tests.ps1'
    'architecture' = 'tests\Verification\architecture.tests.ps1'
    'architecture-red-fixture' = 'tests\Verification\architecture-red-fixture.tests.ps1'
}
foreach ($entry in $scriptChecks.GetEnumerator()) {
    if ($entry.Key -in $checkPlan) {
        Add-ScriptCheck -CheckId $entry.Key -RelativePath $entry.Value
    }
}

if ('unit' -in $checkPlan) {
    if (-not (Test-CommandAvailable -Name 'dotnet')) {
        $results.Add((New-VerificationResult -CheckId 'unit' `
            -Classification 'BLOCKED_BY_MISSING_TOOL' `
            -Command 'dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-restore' `
            -Mandatory $true -Evidence 'dotnet executable is missing.' -BlockerId 'BLK-ENV-001'))
    }
    else {
        $unitProject = Join-Path $repoRoot 'tests\Unit\IUMP.Tests.Unit.csproj'
        $unitAction = { & dotnet run --project $unitProject --no-restore }.GetNewClosure()
        $results.Add((Invoke-HarnessCheck -CheckId 'unit' `
            -Command 'dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-restore' `
            -Action $unitAction))
    }
}

if ($Mode -eq 'Full') {
    if (-not (Test-CommandAvailable -Name 'dotnet')) {
        $results.Add((New-VerificationResult -CheckId 'backend-build' `
            -Classification 'BLOCKED_BY_MISSING_TOOL' -Command '& .\scripts\build.ps1' `
            -Mandatory $true -Evidence 'dotnet executable is missing.' -BlockerId 'BLK-ENV-001'))
    }
    else {
        Add-ScriptCheck -CheckId 'backend-build' -RelativePath 'scripts\build.ps1' `
            -BlockedClassification 'BLOCKED_BY_PACKAGE_POLICY' -BlockerId 'BLK-ENV-001'
    }

    $webRoot = Join-Path $repoRoot 'src\Web'
    if (-not (Test-CommandAvailable -Name 'npm')) {
        $results.Add((New-VerificationResult -CheckId 'frontend' `
            -Classification 'BLOCKED_BY_MISSING_TOOL' -Command 'npm run lint; npm run build' `
            -Mandatory $true -Evidence 'npm executable is missing.' -BlockerId 'BLK-ENV-001'))
    }
    elseif (-not (Test-Path -LiteralPath (Join-Path $webRoot 'node_modules') -PathType Container)) {
        $results.Add((New-VerificationResult -CheckId 'frontend' `
            -Classification 'BLOCKED_BY_PACKAGE_POLICY' -Command 'npm run lint; npm run build' `
            -Mandatory $true -Evidence 'Existing approved node_modules tree is missing.' `
            -BlockerId 'BLK-ENV-001'))
    }
    else {
        $frontendAction = {
            Push-Location $webRoot
            try {
                & npm run lint
                $lintExit = $LASTEXITCODE
                & npm run build
                $buildExit = $LASTEXITCODE
                $global:LASTEXITCODE = if ($lintExit -eq 0 -and $buildExit -eq 0) { 0 } else { 1 }
            }
            finally {
                Pop-Location
            }
        }.GetNewClosure()
        $results.Add((Invoke-HarnessCheck -CheckId 'frontend' `
            -Command 'npm run lint; npm run build' -Action $frontendAction))
    }

    if (-not (Test-CommandAvailable -Name 'psql')) {
        $results.Add((New-VerificationResult -CheckId 'database' `
            -Classification 'BLOCKED_BY_MISSING_TOOL' -Command 'psql service=<configured> SELECT 1' `
            -Mandatory $true -Evidence 'psql executable is missing.' -BlockerId 'BLK-ENV-002'))
    }
    elseif ([string]::IsNullOrWhiteSpace($env:PGSERVICE)) {
        $results.Add((New-VerificationResult -CheckId 'database' `
            -Classification 'BLOCKED_BY_DATABASE_ACCESS' `
            -Command 'psql service=<configured> SELECT 1' -Mandatory $true `
            -Evidence 'PGSERVICE is not configured.' -BlockerId 'BLK-ENV-002'))
    }
    else {
        $databaseAction = {
            & psql "service=$env:PGSERVICE" --no-psqlrc --tuples-only --no-align --command 'SELECT 1'
        }
        $results.Add((Invoke-HarnessCheck -CheckId 'database' `
            -Command 'psql service=<configured> SELECT 1' -Action $databaseAction `
            -BlockedClassification 'BLOCKED_BY_DATABASE_ACCESS' -BlockerId 'BLK-ENV-002'))
    }

    $approvedCi = $env:CI -eq 'true' -and $env:IUMP_COMPANY_CI_APPROVED -eq 'true'
    $results.Add((New-VerificationResult -CheckId 'ci' `
        -Classification $(if ($approvedCi) { 'PASS' } else { 'BLOCKED_BY_COMPANY_APPROVAL' }) `
        -Command 'Full harness on approved company runner' -Mandatory $true `
        -Evidence $(if ($approvedCi) {
            'Running inside an approved company CI context.'
        } else {
            'No approved company runner or template context.'
        }) -BlockerId $(if ($approvedCi) { $null } else { 'BLK-ENV-003' })))

    $results.Add((New-VerificationResult -CheckId 'container-target' `
        -Classification 'BLOCKED_BY_COMPANY_APPROVAL' `
        -Command 'infrastructure deployment verification' -Mandatory $true `
        -Evidence 'Container target remains deferred pending company approval.' `
        -BlockerId 'BLK-ENV-004'))
}

$resultPath = Join-Path $repoRoot 'verification-results.json'
$results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $resultPath -Encoding UTF8
$results | ForEach-Object { Write-VerificationResult -Result $_ }

$groups = @($results | Group-Object classification | Sort-Object Name)
$summary = ($groups | ForEach-Object { "$($_.Name)=$($_.Count)" }) -join ', '
Write-Output "Harness $Mode summary: $summary"

exit (Get-VerificationExitCode -Results @($results))
