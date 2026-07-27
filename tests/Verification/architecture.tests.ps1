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
            # Approved public-contract dependencies: IAM -> Organization (post-Site
            # fixture), Catalog -> Organization (readiness snapshots), and
            # Acquisition -> Catalog (source-scope fact only).
            $isIamToOrg = $_.FullName -match '[\\/]Modules\\IAM[\\/]' -and $reference.Include -match '[\\/]Organization[\\/]'
            $isCatalogToOrg = $_.FullName -match '[\\/]Modules\\Catalog[\\/]' -and $reference.Include -match '[\\/]Organization[\\/]'
            $isAcquisitionToCatalog = $_.FullName -match '[\\/]Modules\\Acquisition[\\/]' -and $reference.Include -match '[\\/]Catalog[\\/]'
            if (-not ($isIamToOrg -or $isCatalogToOrg -or $isAcquisitionToCatalog)) {
                throw "Module-to-module project reference is forbidden: $($_.FullName) -> $($reference.Include)"
            }
        }
    }
}

if ($isCanonicalModuleRoot) {
    $ownership = Get-Content -LiteralPath $OwnershipManifest -Raw | ConvertFrom-Json
    if (@($ownership.modules).Count -ne 13) {
        throw 'The canonical ownership manifest must contain exactly 13 owned modules.'
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
        throw 'BuildingBlocks must remain framework-light and dependency-free.'
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

if ($isCanonicalModuleRoot) {
    $iamContractPath = Join-Path $ModuleRoot 'IAM\Contracts\ModuleContract.cs'
    if (Test-Path -LiteralPath $iamContractPath) {
        $iamContract = Get-Content -LiteralPath $iamContractPath -Raw
        if ($iamContract -notmatch 'OwnedSchema\s*=\s*"iam"') {
            throw "IAM module must declare OwnedSchema = ""iam""."
        }
    }

    $iamInternalPattern = 'IUMP\.Modules\.IAM\.(Domain|Application|Infrastructure)'
    $iamSourceFiles = Get-ChildItem -LiteralPath $HostSourceRoot -Recurse -Filter '*.cs' |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj|Modules\\IAM)[\\/]' }
    foreach ($source in $iamSourceFiles) {
        $content = Get-Content -LiteralPath $source.FullName -Raw
        if ($content -match $iamInternalPattern) {
            throw "Non-IAM source references IAM internals: $($source.FullName)"
        }
    }
}

if ($isCanonicalModuleRoot) {
    $catalogInternalPattern = 'IUMP\.Modules\.Catalog\.(Domain|Application|Infrastructure)'
    $catalogSourceFiles = Get-ChildItem -LiteralPath $HostSourceRoot -Recurse -Filter '*.cs' |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj|Modules\\Catalog)[\\/]' }
    foreach ($source in $catalogSourceFiles) {
        $content = Get-Content -LiteralPath $source.FullName -Raw
        if ($content -match $catalogInternalPattern) {
            throw "Non-Catalog source references Catalog internals: $($source.FullName)"
        }
    }

    $orgContractPath = Join-Path $ModuleRoot 'Organization\Contracts\ModuleContract.cs'
    if (Test-Path -LiteralPath $orgContractPath) {
        $orgContract = Get-Content -LiteralPath $orgContractPath -Raw
        if ($orgContract -notmatch 'OwnedSchema\s*=\s*"organization"') {
            throw "Organization module must declare OwnedSchema = ""organization""."
        }
    }

    # Organization public surface includes Domain types (SiteId, AreaId, etc.)
    # consumed by IAM's PostSiteFixtureOrganizationAdapter. No internal reference
    # is expected from outside Organization.

    $postSiteAdapter = Join-Path $ModuleRoot 'IAM\Application\PostSiteFixtureOrganizationAdapter.cs'
    if (Test-Path -LiteralPath $postSiteAdapter) {
        $adapterSource = Get-Content -LiteralPath $postSiteAdapter -Raw
        if ($adapterSource -match 'IUMP\.Modules\.Organization\.(Domain|Application|Infrastructure)') {
            throw 'IAM Post-Site adapter may consume only Organization.Contracts.'
        }
        if ($adapterSource -match 'IOrganizationCommandRepository') {
            throw 'IAM Post-Site adapter must not depend on Organization command persistence.'
        }
    }

    $t071Runner = Join-Path $repoRoot 'tests\Integration\Organization\OrganizationRepositoryTests.cs'
    if (Test-Path -LiteralPath $t071Runner) {
        $runnerSource = Get-Content -LiteralPath $t071Runner -Raw
        if ($runnerSource -match 'FakeOrganization(CommandRepository|Transaction)|as\s+FakeOrganization') {
            throw 'T071 contract runner must remain provider-neutral and must not cast to a fake.'
        }
    }

    $hierarchyCommands = Join-Path $ModuleRoot 'Organization\Application\HierarchyCommands.cs'
    $hierarchySource = Get-Content -LiteralPath $hierarchyCommands -Raw
    $pointStatusBlock = [regex]::Match($hierarchySource, '(?s)HandleAsync\(UpdatePointStatusCommand.*?(?=private async Task|\z)').Value
    if ($pointStatusBlock -match '"activate"\s*=>|"reactivate"\s*=>') {
        throw 'Normal Point activate/reactivate command paths must remain deferred to Phase 5.'
    }

    if ($hierarchySource -match 'NullRunningSimulatorQuery|IRunningSimulatorQuery\?\s+simQuery|IRunningSimulatorQuery\s+simQuery\s*=|new\s+NullRunningSimulatorQuery') {
        throw 'OrganizationCommandHandler must require an explicit running-Simulator dependency; fail-open defaults are forbidden.'
    }
    foreach ($commandName in @('UpdateSiteCommand','UpdateSiteStatusCommand','UpdateAreaCommand','UpdateAreaStatusCommand',
            'UpdateAssetCommand','UpdateAssetStatusCommand','DecommissionAssetCommand','UpdatePointConfigurationCommand',
            'UpdatePointStatusCommand','DecommissionPointCommand')) {
        $commandMatch = [regex]::Match($hierarchySource, "(?ms)^public sealed record $commandName\(.*?\);")
        if (-not $commandMatch.Success -or $commandMatch.Value -notmatch '\bExpectedVersion\b') {
            throw "$commandName must carry ExpectedVersion for optimistic concurrency."
        }
    }
    foreach ($eventType in @('SiteStatusChanged.v1','AreaStatusChanged.v1','AssetStatusChanged.v1','PointConfigurationChanged.v1','PointStatusChanged.v1')) {
        if ($hierarchySource -notmatch [regex]::Escape($eventType)) {
            throw "Missing Organization event family: $eventType"
        }
    }
    if ($hierarchySource -notmatch 'PARENT_NOT_CONFIGURABLE' -or $hierarchySource -notmatch 'AreaStatus\.Inactive' -or
        $hierarchySource -notmatch 'AssetStatus\.Inactive\s+or\s+AssetStatus\.Decommissioned') {
        throw 'Child creation must reject non-configurable parent statuses.'
    }

    $hierarchyQueries = Join-Path $ModuleRoot 'Organization\Application\HierarchyQueries.cs'
    $querySource = Get-Content -LiteralPath $hierarchyQueries -Raw
    if ($querySource -match 'ScopeFilter\(1\s*,\s*200\)' -or $querySource -notmatch 'GetAreaAncestryAsync') {
        throw 'Area-scoped Site visibility must use trusted ancestry and must not rely on a first-200 Area page.'
    }

    $readinessAdapter = Join-Path $ModuleRoot 'Catalog\Application\OrganizationPointReadinessAdapter.cs'
    if (Test-Path -LiteralPath $readinessAdapter) {
        $readinessSource = Get-Content -LiteralPath $readinessAdapter -Raw
        if ($readinessSource -match 'IUMP\.Modules\.Organization\.(Domain|Application|Infrastructure)') {
            throw 'Catalog readiness adapter may consume only Organization.Contracts snapshots.'
        }
        if ($readinessSource -match 'IOrganizationCommandRepository|Add[A-Za-z]+Async|Update[A-Za-z]+Async') {
            throw 'Catalog readiness adapter must be read-only and must not write Organization.'
        }
    }

    $catalogSourceScopeAdapter = Join-Path $ModuleRoot 'Catalog\Application\CatalogSourceScopeQueryAdapter.cs'
    if (Test-Path -LiteralPath $catalogSourceScopeAdapter) {
        $sourceScopeSource = Get-Content -LiteralPath $catalogSourceScopeAdapter -Raw
        if ($sourceScopeSource -match 'IUMP\.Modules\.Organization\.|IUMP\.Modules\.Acquisition\.') {
            throw 'CatalogSourceScopeQueryAdapter may consume only Catalog contracts and ICatalogPointReadinessQuery.'
        }
    }

    $acquisitionContract = Join-Path $ModuleRoot 'Acquisition\Contracts\ConfigurationPersistenceContracts.cs'
    $acquisitionApp = Join-Path $ModuleRoot 'Acquisition\Application\SimulatorConfiguration.cs'
    if (Test-Path -LiteralPath $acquisitionContract) {
        $acqContractSource = Get-Content -LiteralPath $acquisitionContract -Raw
        if ($acqContractSource -match 'IQueryable|DbContext|Npgsql') {
            throw 'Acquisition configuration contract must remain provider-neutral and append-only.'
        }
        if ($acqContractSource -match '(?m)^\s*(Task|void).*\b(Delete|Update).*Version') {
            throw 'Immutable configuration versions must not have update/delete ports.'
        }
    }
    if (Test-Path -LiteralPath $acquisitionApp) {
        $acqAppSource = Get-Content -LiteralPath $acquisitionApp -Raw
        if ($acqAppSource -match 'Run|Worker|Telemetry|Start|Pause|Resume|Stop') {
            throw 'Phase 4 Acquisition must not implement Run, Worker or Telemetry behavior.'
        }
        if ($acqAppSource -match 'IUMP\.Modules\.Catalog\.(Domain|Application|Infrastructure)') {
            throw 'Acquisition may consume only Catalog public contracts.'
        }
    }

    foreach ($migration in @('0005_acquisition_configuration.sql','0006_catalog_source_mapping.sql')) {
        $migrationPath = Join-Path $repoRoot "database\migrations\$migration"
        $sql = Get-Content -LiteralPath $migrationPath -Raw
        if ($sql -match '(?i)REFERENCES\s+(catalog|organization)\.' -and $migration -eq '0005_acquisition_configuration.sql') {
            throw '0005 must not contain a cross-schema FK.'
        }
        if ($sql -match '(?i)REFERENCES\s+organization\.' -and $migration -eq '0006_catalog_source_mapping.sql') {
            throw '0006 must not contain a cross-schema FK.'
        }
    }
}

# T091 Phase 4 corrective convergence invariant checks
if ($isCanonicalModuleRoot) {
    # 1. DeterministicSeed must be ulong (not string/object)
    $acqContractPath = Join-Path $ModuleRoot 'Acquisition\Contracts\ConfigurationPersistenceContracts.cs'
    $acqContract = Get-Content -LiteralPath $acqContractPath -Raw
    if ($acqContract -notmatch 'ulong\s+DeterministicSeed') {
        throw 'T091-01: DeterministicSeed must be ulong, not string/object.'
    }

    # 2. Migration 0005 seed must be numeric(20,0) not text
    $mig5 = Join-Path $repoRoot 'database\migrations\0005_acquisition_configuration.sql'
    $mig5Sql = Get-Content -LiteralPath $mig5 -Raw
    if ($mig5Sql -notmatch 'deterministic_seed\s+numeric\(20') {
        throw 'T091-02: Migration 0005 seed must use numeric(20,0) not a text type.'
    }

    # 3. Source scope must be multi-Site (CatalogSourceMappedScopeSnapshot with SiteId)
    $eligPath = Join-Path $ModuleRoot 'Catalog\Contracts\CatalogEligibilityContracts.cs'
    $eligContent = Get-Content -LiteralPath $eligPath -Raw
    if ($eligContent -notmatch 'CatalogSourceMappedScopeSnapshot' -or $eligContent -notmatch 'string\s+SiteId') {
        throw 'T091-03: Source scope must use multi-Site CatalogSourceMappedScopeSnapshot.'
    }
    if ($eligContent -notmatch 'MappedScopes' -or $eligContent -notmatch 'CatalogSourceScopeSnapshot') {
        throw 'T091-03: CatalogSourceScopeSnapshot must carry MappedScopes collection.'
    }

    # 4. CatalogSourceScopeQueryAdapter must exist
    $scopeAdapter = Join-Path $ModuleRoot 'Catalog\Application\CatalogSourceScopeQueryAdapter.cs'
    if (-not (Test-Path -LiteralPath $scopeAdapter)) {
        throw 'T091-04: CatalogSourceScopeQueryAdapter must exist in Catalog.Application.'
    }

    # 5. Adapter must not use empty SiteId fallback for missing readiness.
    #    AreaId may be null (nullable metadata); SiteId is authoritative.
    $adapterSource = Get-Content -LiteralPath $scopeAdapter -Raw
    if ($adapterSource -match 'readiness\?\.SiteId\s*\?\?\s*string\.Empty') {
        throw 'T091-05: Adapter must not fall back to empty SiteId for missing readiness.'
    }
    if ($adapterSource -notmatch 'readiness\.AreaId\s*\?\?\s*string\.Empty') {
        throw 'T091-05: Adapter must handle nullable AreaId with empty fallback.'
    }

    # 6. ReadinessVersionTuple must exist
    if ($eligContent -notmatch 'ReadinessVersionTuple') {
        throw 'T091-06: ReadinessVersionTuple record must exist for per-object version tracking.'
    }

    # 7. Mapping tests must use real OrganizationPointReadinessAdapter not FakePointReadinessQuery
    $mappingTests = Join-Path $repoRoot 'tests\Unit\Catalog\MappingReadinessTests.cs'
    $mappingTestSource = Get-Content -LiteralPath $mappingTests -Raw
    if ($mappingTestSource -notmatch 'OrganizationPointReadinessAdapter') {
        throw 'T091-07: MappingReadinessTests must use OrganizationPointReadinessAdapter not FakePointReadinessQuery.'
    }

    # 8. Migration 0006 must use DO block not ADD CONSTRAINT IF NOT EXISTS
    $mig6 = Join-Path $repoRoot 'database\migrations\0006_catalog_source_mapping.sql'
    $mig6Sql = Get-Content -LiteralPath $mig6 -Raw
    if ($mig6Sql -notmatch 'DO\s*\$\$') {
        throw 'T091-08: Migration 0006 must use DO block for idempotent constraint creation.'
    }

    # 9. EXCLUDE constraint must be executable, not comment-only
    if ($mig6Sql -notmatch 'EXCLUDE\s+USING\s+gist') {
        throw 'T091-09: Migration 0006 must have executable EXCLUDE USING gist constraint.'
    }
    if ($mig6Sql -match '^\s*--.*EXCLUDE') {
        throw 'T091-09: EXCLUDE constraint must not be comment-only.'
    }

    # 10. T088 must increment _testCount at method level, not inside Assert helper
    $t088Path = Join-Path $repoRoot 'tests\Integration\Acquisition\ConfigurationRepositoryTests.cs'
    $t088Source = Get-Content -LiteralPath $t088Path -Raw
    $testCountInAssert = [regex]::Matches($t088Source, '_testCount\+\+').Count
    $assertionCountInAssert = [regex]::Matches($t088Source, '_assertionCount\+\+').Count
    if ($testCountInAssert -ne 19) {
        throw "T091-10: T088 must have exactly 19 _testCount increments (one per scenario method); found $testCountInAssert."
    }
    # _assertionCount should be inside Assert method body only
    $assertMethod = [regex]::Match($t088Source, '(?s)private\s+void\s+Assert\(bool condition.*?\)\s*\{.*?\}')
    if ($assertMethod.Success -and $assertMethod.Value -notmatch '_assertionCount\+\+') {
        throw 'T091-10: Assert helper must increment _assertionCount.'
    }

    # 11. No Phase 5 files must exist in the working tree
    $phase5Indicators = @(
        'TelemetryIngestion',
        'Worker\b',
        'Api\b',
        'SimulatorRun',
        'PointActivation'
    )
    $allSourceFiles = Get-ChildItem -LiteralPath $repoRoot -Recurse -Filter '*.cs' |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj|node_modules)[\\/]' }
    foreach ($indicator in $phase5Indicators) {
        if ($allSourceFiles | Where-Object { $_.Name -match $indicator -and $_.FullName -notmatch 'Contracts' }) {
            throw "T091-11: Phase 5 file detected matching indicator: $indicator"
        }
    }
}

Write-Output 'PASS: architecture boundary contract'
