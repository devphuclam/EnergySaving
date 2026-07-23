[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'common\Verification.ps1')
$results = @()
$toolPassedByName = @{}

foreach ($tool in @('git', 'dotnet', 'node', 'npm')) {
    $available = Test-CommandAvailable -Name $tool
    $toolOutput = ''
    $toolPassed = $false
    if ($available) {
        $toolOutput = (& $tool --version 2>&1 | Out-String).Trim()
        $toolPassed = $LASTEXITCODE -eq 0
    }
    $toolPassedByName[$tool] = $toolPassed
    $results += New-VerificationResult -CheckId "tool-$tool" `
        -Classification $(if ($toolPassed) { 'PASS' } elseif ($available) { 'FAIL' } else { 'BLOCKED_BY_MISSING_TOOL' }) `
        -Command "$tool --version" -Mandatory $true `
        -Evidence $(if ($toolPassed) { $toolOutput } elseif ($available) { 'version command failed' } else { 'executable missing' })
}

$scriptChecks = @(
    @{ Id = 'verification-contract'; Path = 'tests\Verification\verification-contract.tests.ps1' },
    @{ Id = 'repository-policy'; Path = 'tests\Verification\repository-policy.tests.ps1' },
    @{ Id = 'repository-scope'; Path = 'tests\Verification\repository-scope.tests.ps1' },
    @{ Id = 'architecture'; Path = 'tests\Verification\architecture.tests.ps1' },
    @{ Id = 'architecture-red-fixture'; Path = 'tests\Verification\architecture-red-fixture.tests.ps1' }
)
foreach ($check in $scriptChecks) {
    try {
        $output = & (Join-Path $repoRoot $check.Path) 2>&1 | Out-String
        $passed = $?
        $scriptExitCode = if ($passed) { 0 } else { 1 }
        $results += New-VerificationResult -CheckId $check.Id `
            -Classification $(if ($passed) { 'PASS' } else { 'FAIL' }) `
            -Command "& .\$($check.Path)" -Mandatory $true -ExitCode $scriptExitCode `
            -Evidence $output.Trim()
    }
    catch {
        $results += New-VerificationResult -CheckId $check.Id -Classification 'FAIL' `
            -Command "& .\$($check.Path)" -Mandatory $true -ExitCode 1 -Evidence $_.Exception.Message
    }
}

if ($toolPassedByName.dotnet) {
    $unitOutput = (& dotnet run --project (Join-Path $repoRoot 'tests\Unit\IUMP.Tests.Unit.csproj') --no-restore 2>&1 | Out-String).Trim()
    $unitExit = $LASTEXITCODE
    $results += New-VerificationResult -CheckId 'unit-correlation' `
        -Classification $(if ($unitExit -eq 0) { 'PASS' } else { 'FAIL' }) `
        -Command 'dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-restore' `
        -Mandatory $true -ExitCode $unitExit -Evidence $unitOutput
}
else {
    $results += New-VerificationResult -CheckId 'unit-correlation' -Classification 'BLOCKED_BY_MISSING_TOOL' `
        -Command 'dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-restore' `
        -Mandatory $true -Evidence 'dotnet version check did not pass' -BlockerId 'BLK-R0-001'
}

if ($toolPassedByName.dotnet) {
    & (Join-Path $PSScriptRoot 'build.ps1')
    $buildExit = $LASTEXITCODE
    $results += New-VerificationResult -CheckId 'backend-build' `
        -Classification $(if ($buildExit -eq 0) { 'PASS' } elseif ($buildExit -eq 20) { 'BLOCKED_BY_PACKAGE_POLICY' } else { 'FAIL' }) `
        -Command '& .\scripts\build.ps1' -Mandatory $true -ExitCode $buildExit `
        -Evidence $(if ($buildExit -eq 0) { 'Release build completed' } else { 'See build output above' }) `
        -BlockerId $(if ($buildExit -eq 20) { 'BLK-R0-001' } else { $null })
}
else {
    $results += New-VerificationResult -CheckId 'backend-build' -Classification 'BLOCKED_BY_MISSING_TOOL' `
        -Command '& .\scripts\build.ps1' -Mandatory $true -Evidence 'dotnet version check did not pass' `
        -BlockerId 'BLK-R0-001'
}

if (-not $toolPassedByName.npm) {
    $results += New-VerificationResult -CheckId 'frontend' -Classification 'BLOCKED_BY_MISSING_TOOL' `
        -Command 'npm run lint; npm run build' -Mandatory $true -Evidence 'npm version check did not pass' `
        -BlockerId 'BLK-R0-001'
}
elseif (Test-Path -LiteralPath (Join-Path $repoRoot 'src\Web\node_modules')) {
    Push-Location (Join-Path $repoRoot 'src\Web')
    try {
        & npm run lint
        $lintExit = $LASTEXITCODE
        & npm run build
        $webBuildExit = $LASTEXITCODE
    }
    finally { Pop-Location }
    $webPassed = $lintExit -eq 0 -and $webBuildExit -eq 0
    $results += New-VerificationResult -CheckId 'frontend' `
        -Classification $(if ($webPassed) { 'PASS' } else { 'FAIL' }) `
        -Command 'npm run lint; npm run build' -Mandatory $true `
        -ExitCode $(if ($webPassed) { 0 } else { 1 }) -Evidence 'Used existing node_modules; no install executed'
}
else {
    $results += New-VerificationResult -CheckId 'frontend' -Classification 'BLOCKED_BY_PACKAGE_POLICY' `
        -Command 'npm run lint; npm run build' -Mandatory $true -Evidence 'node_modules missing' `
        -BlockerId 'BLK-R0-001'
}

$psqlAvailable = Test-CommandAvailable -Name 'psql'
if (-not $psqlAvailable) {
    $results += New-VerificationResult -CheckId 'database' -Classification 'BLOCKED_BY_MISSING_TOOL' `
        -Command 'psql --version' -Mandatory $true -Evidence 'psql executable missing' -BlockerId 'BLK-R0-002'
}
elseif ([string]::IsNullOrWhiteSpace($env:PGSERVICE)) {
    $results += New-VerificationResult -CheckId 'database' -Classification 'BLOCKED_BY_DATABASE_ACCESS' `
        -Command 'psql service=$env:PGSERVICE --no-psqlrc --command "SELECT 1"' -Mandatory $true `
        -Evidence 'PGSERVICE not configured' -BlockerId 'BLK-R0-002'
}
else {
    $dbOutput = (& psql "service=$env:PGSERVICE" --no-psqlrc --tuples-only --no-align --command 'SELECT 1' 2>&1 | Out-String).Trim()
    $dbPassed = $LASTEXITCODE -eq 0 -and $dbOutput -eq '1'
    $results += New-VerificationResult -CheckId 'database' `
        -Classification $(if ($dbPassed) { 'PASS' } else { 'FAIL' }) `
        -Command 'psql service=$env:PGSERVICE --no-psqlrc --command "SELECT 1"' -Mandatory $true `
        -ExitCode $LASTEXITCODE -Evidence $(if ($dbPassed) { 'Approved service returned SELECT 1' } else { 'Connectivity query failed; credential redacted' }) `
        -BlockerId $(if ($dbPassed) { $null } else { 'BLK-R0-002' })
}

$approvedCi = $env:CI -eq 'true' -and $env:IUMP_COMPANY_CI_APPROVED -eq 'true'
$results += New-VerificationResult -CheckId 'ci' `
    -Classification $(if ($approvedCi) { 'PASS' } else { 'BLOCKED_BY_COMPANY_APPROVAL' }) `
    -Command '& .\scripts\verify.ps1 on approved company runner' -Mandatory $true `
    -Evidence $(if ($approvedCi) { 'Running inside approved company CI context' } else { 'No approved runner/template context' }) `
    -BlockerId $(if ($approvedCi) { $null } else { 'BLK-R0-003' })
$results += New-VerificationResult -CheckId 'container-target' -Classification 'BLOCKED_BY_COMPANY_APPROVAL' `
    -Command 'infrastructure deployment verification' -Mandatory $true `
    -Evidence 'Container target deferred by workstation policy' -BlockerId 'BLK-R0-004'

$results | ForEach-Object { Write-VerificationResult -Result $_ }
$results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $repoRoot 'verification-results.json') -Encoding UTF8
exit (Get-VerificationExitCode -Results $results)
