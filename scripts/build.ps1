[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'common\Verification.ps1')

if (-not (Test-CommandAvailable -Name 'dotnet')) {
    Write-Output 'backend-build: BLOCKED_BY_MISSING_TOOL [BLK-R0-001] - dotnet is missing'
    exit 20
}

$projects = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src') -Recurse -Filter '*.csproj'
$missingAssets = @($projects | Where-Object {
    -not (Test-Path -LiteralPath (Join-Path $_.DirectoryName 'obj\project.assets.json'))
})
if ($missingAssets.Count -gt 0) {
    Write-Output 'backend-build: BLOCKED_BY_PACKAGE_POLICY [BLK-R0-001] - assets not generated; run only the approved no-source restore documented in README'
    exit 20
}

$packageProjects = @($projects | Where-Object {
    Select-String -LiteralPath $_.FullName -Pattern '<PackageReference\b' -Quiet
})
$missingLocks = @($packageProjects | Where-Object {
    -not (Test-Path -LiteralPath (Join-Path $_.DirectoryName 'packages.lock.json') -PathType Leaf)
})
if ($missingLocks.Count -gt 0) {
    Write-Output 'backend-build: BLOCKED_BY_PACKAGE_POLICY [BLK-R0-001] - package lock evidence is missing'
    exit 20
}

& dotnet build (Join-Path $repoRoot 'IUMP.slnx') --no-restore --configuration Release
exit $LASTEXITCODE
