[CmdletBinding()]
param(
    [string]$ModuleRoot,
    [string]$BuildingBlocksProject,
    [string]$HostSourceRoot,
    [string]$ContractSourceRoot,
    [string]$OwnershipManifest
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$defaultModuleRoot = Join-Path $repoRoot 'src\Modules'
if ([string]::IsNullOrWhiteSpace($ModuleRoot)) { $ModuleRoot = $defaultModuleRoot }
$ModuleRoot = [IO.Path]::GetFullPath($ModuleRoot)
$isCanonicalModuleRoot = $ModuleRoot -eq [IO.Path]::GetFullPath($defaultModuleRoot)
if ([string]::IsNullOrWhiteSpace($BuildingBlocksProject)) {
    $BuildingBlocksProject = Join-Path $repoRoot 'src\BuildingBlocks\IUMP.BuildingBlocks.csproj'
}
if ([string]::IsNullOrWhiteSpace($HostSourceRoot)) { $HostSourceRoot = Join-Path $repoRoot 'src' }
if ([string]::IsNullOrWhiteSpace($ContractSourceRoot)) { $ContractSourceRoot = $ModuleRoot }
if ([string]::IsNullOrWhiteSpace($OwnershipManifest)) {
    $OwnershipManifest = Join-Path $repoRoot 'docs\architecture\module-ownership.json'
}

Get-ChildItem -LiteralPath $ModuleRoot -Recurse -Filter '*.csproj' | ForEach-Object {
    [xml]$project = Get-Content -LiteralPath $_.FullName -Raw
    $references = @($project.SelectNodes('//ProjectReference') | Where-Object { $_.Include })
    foreach ($reference in $references) {
        $target = [IO.Path]::GetFullPath((Join-Path $_.DirectoryName ([string]$reference.Include)))
        if ($target.StartsWith($ModuleRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Module-to-module project reference is forbidden: $($_.FullName) -> $($reference.Include)"
        }
    }
}

if ($isCanonicalModuleRoot) {
    $ownership = Get-Content -LiteralPath $OwnershipManifest -Raw | ConvertFrom-Json
    if (@($ownership.modules).Count -ne 13) {
        throw 'The canonical ownership manifest must contain exactly 13 R0 modules.'
    }
    foreach ($entry in $ownership.modules) {
        $contractPath = Join-Path $ModuleRoot "$($entry.name)\Contracts\ModuleContract.cs"
        if (-not (Test-Path -LiteralPath $contractPath)) {
            throw "Missing module ownership contract: $($entry.name)"
        }
        $contract = Get-Content -LiteralPath $contractPath -Raw
        if ($contract -notmatch ('OwnedSchema\s*=\s*"' + [regex]::Escape($entry.schema) + '"')) {
            throw "Incorrect owned schema for module $($entry.name); expected $($entry.schema)"
        }
    }

    $buildingBlocks = Get-Content -LiteralPath $BuildingBlocksProject -Raw
    if ($buildingBlocks -match '<(PackageReference|ProjectReference)\b') {
        throw 'BuildingBlocks must remain framework-light and dependency-free in R0.'
    }
}

Get-ChildItem -LiteralPath $ModuleRoot -Directory | ForEach-Object {
    $unexpected = Get-ChildItem -LiteralPath $_.FullName -Recurse -File | Where-Object {
        $_.Extension -eq '.cs' -and
        $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and
        $_.FullName -notmatch '[\\/]Contracts[\\/]'
    }
    if ($unexpected) {
        throw "R0 module exposes implementation outside Contracts: $($unexpected.FullName -join ', ')"
    }
}

if ($isCanonicalModuleRoot) {
    $defaultHostRoot = Join-Path $repoRoot 'src'
    $hostSources = Get-ChildItem -LiteralPath $HostSourceRoot -Recurse -Filter '*.cs' |
        Where-Object {
            ($HostSourceRoot -ne $defaultHostRoot -or $_.FullName -match '[\\/](Api|Worker)[\\/]') -and
            $_.FullName -notmatch '[\\/](bin|obj)[\\/]'
        }
    foreach ($source in $hostSources) {
        $content = Get-Content -LiteralPath $source.FullName -Raw
        if ($content -match 'IUMP\.Modules\.[A-Za-z0-9_]+\.(Domain|Application|Infrastructure)') {
            throw "Host references module internals: $($source.FullName)"
        }
    }

    $contractSources = Get-ChildItem -LiteralPath $ContractSourceRoot -Recurse -Filter '*.cs' |
        Where-Object { $ContractSourceRoot -ne $ModuleRoot -or $_.FullName -match '[\\/]Contracts[\\/]' }
    $prohibitedContractPattern = '(?i)Modbus|WriteBack|Setpoint|Actuat|EquipmentCommand|ControlCommand'
    foreach ($source in $contractSources) {
        if ((Get-Content -LiteralPath $source.FullName -Raw) -match $prohibitedContractPattern) {
            throw "Prohibited command/write-back contract surface: $($source.FullName)"
        }
    }
}

Write-Output 'PASS: architecture boundary contract'
