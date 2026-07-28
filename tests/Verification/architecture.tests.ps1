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

$issues = @()

# --- STANDARD ARCHITECTURE CHECKS (unchanged) ---
Get-ChildItem -LiteralPath $ModuleRoot -Recurse -Filter '*.csproj' | ForEach-Object {
    [xml]$project = Get-Content -LiteralPath $_.FullName -Raw
    $references = @($project.SelectNodes('//ProjectReference') | Where-Object { $_.Include })
    foreach ($reference in $references) {
        $target = [IO.Path]::GetFullPath((Join-Path $_.DirectoryName ([string]$reference.Include)))
        if ($target.StartsWith($ModuleRoot, [StringComparison]::OrdinalIgnoreCase)) {
            $isIamToOrg = $_.FullName -match '[\\/]Modules\\IAM[\\/]' -and $reference.Include -match '[\\/]Organization[\\/]'
            $isCatalogToOrg = $_.FullName -match '[\\/]Modules\\Catalog[\\/]' -and $reference.Include -match '[\\/]Organization[\\/]'
            $isAcquisitionToCatalog = $_.FullName -match '[\\/]Modules\\Acquisition[\\/]' -and $reference.Include -match '[\\/]Catalog[\\/]'
            $isOrganizationToIntegration = $_.FullName -match '[\\/]Modules\\Organization[\\/]' -and $reference.Include -match '[\\/]Integration[\\/]'
            if (-not ($isIamToOrg -or $isCatalogToOrg -or $isAcquisitionToCatalog -or $isOrganizationToIntegration)) {
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

# --- T091 Phase 4 CORRECTIVE CONVERGENCE SEMANTIC CHECKS ---
if ($isCanonicalModuleRoot) {
    # 1. DeterministicSeed must be ulong (not string/object)
    $acqContractPath = Join-Path $ModuleRoot 'Acquisition\Contracts\ConfigurationPersistenceContracts.cs'
    $acqContract = Get-Content -LiteralPath $acqContractPath -Raw
    if ($acqContract -notmatch 'ulong\s+DeterministicSeed') {
        $issues += 'T091-01 FAIL: DeterministicSeed must be ulong, not string/object.'
    }

    # 2. Migration 0005 seed must be numeric(20,0) not text
    $mig5 = Join-Path $repoRoot 'database\migrations\0005_acquisition_configuration.sql'
    $mig5Sql = Get-Content -LiteralPath $mig5 -Raw
    if ($mig5Sql -notmatch 'deterministic_seed\s+numeric\(20') {
        $issues += 'T091-02 FAIL: Migration 0005 seed must use numeric(20,0) not a text type.'
    }

    # 3. Source scope must be multi-Site (CatalogSourceMappedScopeSnapshot with SiteId)
    $eligPath = Join-Path $ModuleRoot 'Catalog\Contracts\CatalogEligibilityContracts.cs'
    $eligContent = Get-Content -LiteralPath $eligPath -Raw
    if ($eligContent -notmatch 'CatalogSourceMappedScopeSnapshot' -or $eligContent -notmatch 'string\s+SiteId') {
        $issues += 'T091-03 FAIL: Source scope must use multi-Site CatalogSourceMappedScopeSnapshot.'
    }
    if ($eligContent -notmatch 'MappedScopes' -or $eligContent -notmatch 'CatalogSourceScopeSnapshot') {
        $issues += 'T091-03 FAIL: CatalogSourceScopeSnapshot must carry MappedScopes collection.'
    }

    # 4. CatalogSourceScopeQueryAdapter must exist
    $scopeAdapter = Join-Path $ModuleRoot 'Catalog\Application\CatalogSourceScopeQueryAdapter.cs'
    if (-not (Test-Path -LiteralPath $scopeAdapter)) {
        $issues += 'T091-04 FAIL: CatalogSourceScopeQueryAdapter must exist in Catalog.Application.'
    }

    # 5. Adapter must NOT use empty SiteId fallback AND must validate AreaId non-empty
    #    (Current Area-owned Point model means AreaId is non-empty for all valid points)
    $adapterSource = Get-Content -LiteralPath $scopeAdapter -Raw
    if ($adapterSource -match 'readiness\.SiteId\s*\?\?\s*string\.Empty') {
        $issues += 'T091-05 FAIL: Adapter must not fall back to empty SiteId for missing readiness.'
    }
    if ($adapterSource -match 'readiness\.AreaId\s*\?\?\s*string\.Empty') {
        $issues += 'T091-05 FAIL: Adapter must not fall back to empty AreaId; AreaId is non-empty for current Area-owned Point model.'
    }
    if ($adapterSource -notmatch 'readiness\.SiteId\b') {
        $issues += 'T091-05 FAIL: Adapter must use readiness.SiteId (without empty fallback).'
    }

    # 6. Adapter must validate version tuple components are positive
    if ($adapterSource -notmatch '\.(Point|Asset|Area|Site)Version\s*<=?\s*0') {
        $issues += 'T091-06 FAIL: Adapter must validate ReadinessVersions component positivity.'
    }

    # 7. Mapping tests must use real OrganizationPointReadinessAdapter not a fake
    $mappingTests = Join-Path $repoRoot 'tests\Unit\Catalog\MappingReadinessTests.cs'
    $mappingTestSource = Get-Content -LiteralPath $mappingTests -Raw
    if ($mappingTestSource -notmatch 'OrganizationPointReadinessAdapter') {
        $issues += 'T091-07 FAIL: MappingReadinessTests must use OrganizationPointReadinessAdapter.'
    }
    if ($mappingTestSource -match 'FakePointReadinessQuery') {
        $issues += 'T091-07 FAIL: MappingReadinessTests must not use FakePointReadinessQuery.'
    }

    # 8. Migration 0006 must use DO block for idempotent constraint creation
    $mig6 = Join-Path $repoRoot 'database\migrations\0006_catalog_source_mapping.sql'
    $mig6Sql = Get-Content -LiteralPath $mig6 -Raw
    if ($mig6Sql -notmatch 'DO\s*\$\$') {
        $issues += 'T091-08 FAIL: Migration 0006 must use DO block for idempotent constraint creation.'
    }

    # 9. Migration 0006 must have executable EXCLUDE constraint (not comment-only) AND conrelid filter
    if ($mig6Sql -notmatch 'EXCLUDE\s+USING\s+gist') {
        $issues += 'T091-09 FAIL: Migration 0006 must have executable EXCLUDE USING gist constraint.'
    }
    if ($mig6Sql -match '^\s*--.*EXCLUDE') {
        $issues += 'T091-09 FAIL: EXCLUDE constraint must not be comment-only.'
    }
    if ($mig6Sql -notmatch 'conrelid\s*=\s*''catalog\.source_point_mapping''::regclass') {
        $issues += 'T091-09 FAIL: Migration 0006 pg_constraint lookup must filter by conrelid.'
    }

    # 10. T088 must have proper test/assertion separation AND required scenarios
    $t088Path = Join-Path $repoRoot 'tests\Integration\Acquisition\ConfigurationRepositoryTests.cs'
    $t088Source = Get-Content -LiteralPath $t088Path -Raw
    # _testCount increments should be exactly 24 (one per scenario method)
    $testCountMatches = [regex]::Matches($t088Source, '_testCount\+\+').Count
    $assertionCountMatches = [regex]::Matches($t088Source, '_assertionCount\+\+').Count
    if ($testCountMatches -ne 24) {
        $issues += "T091-10 FAIL: T088 must have exactly 24 _testCount increments (one per scenario method); found $testCountMatches."
    }
    $assertMethod = [regex]::Match($t088Source, '(?s)private\s+void\s+Assert\(bool condition.*?\)\s*\{.*?\}')
    if ($assertMethod.Success -and $assertMethod.Value -notmatch '_assertionCount\+\+') {
        $issues += 'T091-10 FAIL: Assert helper must increment _assertionCount.'
    }
    # Check required scenario methods exist
    $requiredScenarios = @(
        'ConstantEqualAcceptedAsync',
        'NormalMinLessThanMaxAcceptedAsync',
        'NaNMaximumRejectedAsync',
        'NegativeInfinityMinimumRejectedAsync',
        'NegativeInfinityMaximumRejectedAsync'
    )
    foreach ($req in $requiredScenarios) {
        if ($t088Source -notmatch "private\s+async\s+Task\s+$req") {
            $issues += "T091-10 FAIL: T088 missing required scenario method: $req"
        }
    }

    # 11. No Phase 5 files in working tree
    $phase5Indicators = @(
        'TelemetryIngestion',
        'Worker\b',
        'Api\b',
        'SimulatorRun'
        # Point activation is the in-scope Phase 5 implementation; later-phase indicators remain forbidden here.
    )
    $allSourceFiles = Get-ChildItem -LiteralPath $repoRoot -Recurse -Filter '*.cs' |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj|node_modules)[\\/]' }
    foreach ($indicator in $phase5Indicators) {
        if ($allSourceFiles | Where-Object { $_.Name -match $indicator -and $_.FullName -notmatch 'Contracts' }) {
            $issues += "T091-11 FAIL: Phase 5 file detected matching indicator: $indicator"
        }
    }

    # 12. ConfigurationCommandTests must have real adapter chain (CatalogSourceScopeQueryAdapter + OrganizationPointReadinessAdapter)
    $cmdTests = Join-Path $repoRoot 'tests\Unit\Acquisition\ConfigurationCommandTests.cs'
    $cmdTestSource = Get-Content -LiteralPath $cmdTests -Raw
    if ($cmdTestSource -notmatch 'CatalogSourceScopeQueryAdapter') {
        $issues += 'T091-12 FAIL: ConfigurationCommandTests must use CatalogSourceScopeQueryAdapter.'
    }
    if ($cmdTestSource -notmatch 'OrganizationPointReadinessAdapter') {
        $issues += 'T091-12 FAIL: ConfigurationCommandTests must use OrganizationPointReadinessAdapter.'
    }
    # Required scenario methods
    $requiredCmdScenarios = @(
        'EngineerNoMappingDenied',
        'EngineerMultiSiteAllScopesSucceed',
        'EngineerMultiSitePartialDenied',
        'InactiveCallerDenied',
        'MissingSourceDenied',
        'DecommissionedSourceDenied',
        'UnresolvedReadinessDenied',
        'EmptySiteIdDenied',
        'EmptyAreaIdDenied',
        'ZeroVersionDenied',
        'DuplicateMappingScopes',
        'EventEnvelopeCompleteness'
    )
    foreach ($req in $requiredCmdScenarios) {
        if ($cmdTestSource -notmatch "private\s+static\s+async\s+Task\s+$req") {
            $issues += "T091-12 FAIL: ConfigurationCommandTests missing required scenario: $req"
        }
    }

    # 13. MappingReadinessTests must have EventProducingReadyAssertions and FourIndependentVersionCases
    if ($mappingTestSource -notmatch 'EventProducingReadyAssertions') {
        $issues += 'T091-13 FAIL: MappingReadinessTests must have EventProducingReadyAssertions method.'
    }
    if ($mappingTestSource -notmatch 'FourIndependentVersionCases') {
        $issues += 'T091-13 FAIL: MappingReadinessTests must have FourIndependentVersionCases method.'
    }

    # 14. Service authorization must enforce roles: Operator/Manager/Viewer do not create/edit
    $simConfigPath = Join-Path $ModuleRoot 'Acquisition\Application\SimulatorConfiguration.cs'
    $simConfigSource = Get-Content -LiteralPath $simConfigPath -Raw
    if ($simConfigSource -notmatch 'HasRole\("Engineer"\)' -or $simConfigSource -notmatch 'HasRole\("Administrator"\)') {
        $issues += 'T091-14 FAIL: SimulatorConfigurationService must check Engineer and Administrator roles.'
    }

    # 15. Catalog event must contain producingReady in both Before and After
    $catalogCommandsPath = Join-Path $ModuleRoot 'Catalog\Application\CatalogCommands.cs'
    $catalogCommandsSource = Get-Content -LiteralPath $catalogCommandsPath -Raw
    if ($catalogCommandsSource -notmatch '"producingReady"') {
        $issues += 'T091-15 FAIL: CatalogCommandHandler events must contain producingReady field.'
    }

    # --- Phase 5 transaction/activation boundary checks (T105) ---
    $orgProjectPath = Join-Path $ModuleRoot 'Organization\IUMP.Modules.Organization.csproj'
    $orgProject = Get-Content -LiteralPath $orgProjectPath -Raw
    if ($orgProject -match 'Modules\\(IAM|Catalog)') {
        $issues += 'T105 FAIL: Organization must not reference IAM or Catalog internals/projects.'
    }
    if ($orgProject -notmatch 'BuildingBlocks\\IUMP.BuildingBlocks.csproj' -or $orgProject -notmatch 'Integration\\IUMP.Modules.Integration.csproj') {
        $issues += 'T105 FAIL: Organization must reference only the provider-neutral BuildingBlocks and Integration seams for Phase 5.'
    }

    $hostTxPath = Join-Path $repoRoot 'src\BuildingBlocks\Persistence\HostTransactionCoordinator.cs'
    $hostTx = Get-Content -LiteralPath $hostTxPath -Raw
    $expectedLocks = 'IamUser\s*,\s*OrganizationSite\s*,\s*OrganizationArea\s*,\s*OrganizationAsset\s*,\s*OrganizationPoint\s*,\s*CatalogMetric\s*,\s*CatalogUnit\s*,\s*CatalogMapping\s*,\s*IntegrationOutbox'
    if ($hostTx -notmatch $expectedLocks) { $issues += 'T105 FAIL: canonical lock order must end with IntegrationOutbox.' }
    if ($hostTx -match 'DbContext|Npgsql|IQueryable|Microsoft\.EntityFrameworkCore') { $issues += 'T105 FAIL: host transaction coordinator must remain provider-neutral.' }
    if ($hostTx -notmatch 'FromSeconds\(2\)' -or $hostTx -notmatch '50,\s*150,\s*450' -or $hostTx -notmatch 'TRANSIENT_DATABASE_CONFLICT') { $issues += 'T105 FAIL: P-016 timeout/retry/exhaustion semantics are missing.' }

    $activationPath = Join-Path $ModuleRoot 'Organization\Application\ActivateMeasurementPoint.cs'
    $activation = Get-Content -LiteralPath $activationPath -Raw
    if ($activation -match '\.Max\s*\(') { $issues += 'T105 FAIL: activation must compare exact provider versions, not Max/sum compression.' }
    if ($activation -match 'Audit|password|secret') { $issues += 'T105 FAIL: activation path must not persist Audit or secret fields.' }
    if ($activation -notmatch 'RegisterRequiredParticipants' -or $activation -notmatch 'ITransactionalOutboxWriter' -or
        $activation -notmatch 'outbox.EnqueueAsync' -or $activation -match 'OutboxTransactionParticipantAdapter|OUTBOX_PARTICIPANT_REQUIRED') {
        $issues += 'T105 FAIL: activation must require the typed outbox host-transaction participant.'
    }
    if ($activation -notmatch 'UserVersion\s*<=\s*0|UserVersion\s*<\s*=\s*0') { $issues += 'T105 FAIL: activation must reject UserVersion <= 0.' }
    if ($activation -notmatch 'ScopeVersion\s*<=\s*0|ScopeVersion\s*<\s*=\s*0') { $issues += 'T105 FAIL: activation must reject ScopeVersion <= 0.' }
    if ($activation -notmatch 'MetricVersion\s*<=\s*0|MetricVersion\s*<\s*=\s*0') { $issues += 'T105 FAIL: activation must reject MetricVersion <= 0.' }
    if ($activation -notmatch 'UnitVersion\s*<=\s*0|UnitVersion\s*<\s*=\s*0') { $issues += 'T105 FAIL: activation must reject UnitVersion <= 0.' }
    if ($activation -notmatch 'CompatibilityVersion\s*<=\s*0|CompatibilityVersion\s*<\s*=\s*0') { $issues += 'T105 FAIL: activation must reject CompatibilityVersion <= 0.' }
    if ($activation -notmatch 'MappingVersion\s*<=\s*0|MappingVersion\s*<\s*=\s*0') { $issues += 'T105 FAIL: activation must reject MappingVersion <= 0.' }
    if ($activation -notmatch 'SourceVersion\s*<=\s*0|SourceVersion\s*<\s*=\s*0') { $issues += 'T105 FAIL: activation must reject SourceVersion <= 0.' }
    if ($activation -notmatch 'CompatibilityIdentity') { $issues += 'T105 FAIL: activation must check CompatibilityIdentity nonblank.' }
    if ($activation -notmatch 'CompatibilityStatus.*Active|CompatibilityStatus\s*!=\s*null') { $issues += 'T105 FAIL: activation must validate CompatibilityStatus exactly Active.' }

    $outboxPath = Join-Path $ModuleRoot 'Integration\Contracts\OutboxContracts.cs'
    $outbox = Get-Content -LiteralPath $outboxPath -Raw
    $writerContract = [regex]::Match($outbox, '(?s)interface\s+ITransactionalOutboxWriter.*?\}')
    if ($writerContract.Success -and $writerContract.Value -match 'Audit|PublishAsync|CommitAsync\s*\(') { $issues += 'T105 FAIL: Integration outbox writer contract must be enqueue-only; host commit owns atomicity.' }
    $eventPath = Join-Path $ModuleRoot 'Organization\Application\OrganizationEvents.cs'
    $eventSource = Get-Content -LiteralPath $eventPath -Raw
    if ($eventSource -notmatch 'PointStatusChanged\.v1' -or $eventSource -notmatch 'MeasurementPoint' -or $eventSource -match 'password|secret|token|Audit') { $issues += 'T105 FAIL: owner event envelope is not the safe PointStatusChanged.v1 contract.' }
    $integrationSource = Join-Path $repoRoot 'tests\Integration\Organization\PointActivationTransactionTests.cs'
    $integration = Get-Content -LiteralPath $integrationSource -Raw
    if ($integration -match 'Npgsql|DbContext|SELECT\s|INSERT\s|UPDATE\s|FakeOrganization') { $issues += 'T105 FAIL: Phase 5 integration source must remain provider-neutral and fake-free.' }

    # Corrective convergence: one typed host transaction, no automatic NoOp participants,
    # and all provider reads/rechecks/staging are transaction-aware.
    if ($hostTx -match 'NoOpParticipant|NoOp') { $issues += 'T105 FAIL: missing providers must not be replaced by NoOp participants.' }
    if ($hostTx -notmatch 'MISSING_TRANSACTION_PARTICIPANT' -or $hostTx -notmatch 'RequiredTargets') { $issues += 'T105 FAIL: BeginAsync must fail closed when any required participant is missing.' }

    # IHostTransactionParticipant must have only AcquireLockAsync
    $participantInterfacePath = Join-Path $repoRoot 'src\BuildingBlocks\Persistence\IHostTransactionParticipant.cs'
    $participantInterface = Get-Content -LiteralPath $participantInterfacePath -Raw
    if ($participantInterface -match 'PrepareAsync|FinalizeAsync|DiscardAsync') { $issues += 'T105 FAIL: IHostTransactionParticipant must not expose PrepareAsync/FinalizeAsync/DiscardAsync.' }
    if ($participantInterface -notmatch 'AcquireLockAsync') { $issues += 'T105 FAIL: IHostTransactionParticipant must expose AcquireLockAsync.' }

    # Only one backend owns CommitAsync/RollbackAsync — participants must not expose them
    $participantContract = [regex]::Match($hostTx, '(?s)interface\s+IHostTransactionParticipant.*?\}')
    if ($participantContract.Success -and $participantContract.Value -match 'CommitAsync|RollbackAsync') { $issues += 'T105 FAIL: participants must not own CommitAsync/RollbackAsync.' }
    # IHostTransactionBackend must own CommitAsync/RollbackAsync
    $backendInterfacePath = Join-Path $repoRoot 'src\BuildingBlocks\Persistence\IHostTransactionBackend.cs'
    $backendInterface = Get-Content -LiteralPath $backendInterfacePath -Raw
    if ($backendInterface -notmatch 'CommitAsync') { $issues += 'T105 FAIL: IHostTransactionBackend must expose CommitAsync.' }
    if ($backendInterface -notmatch 'RollbackAsync') { $issues += 'T105 FAIL: IHostTransactionBackend must expose RollbackAsync.' }
    # Count how many types expose CommitAsync - must be exactly 1
    $commitOwners = @()
    if ($hostTx -match 'CommitAsync') { $commitOwners += 'HostTransactionCoordinator' }
    if ($participantContract.Success -and $participantContract.Value -match 'CommitAsync') { $commitOwners += 'IHostTransactionParticipant' }
    if ($commitOwners.Count -gt 1) { $issues += 'T105 FAIL: more than one type owns CommitAsync/RollbackAsync.' }

    if ($activation -match 'BeginTransactionAsync|IOrganizationTransaction|new\s+AsyncTransaction') { $issues += 'T105 FAIL: activation must not open an independent Organization transaction.' }
    # Organization staging must write to backend workspace, not directly to committed repo
    if ($activation -match 'UpdatePointAsync|AddLifecycleEntryAsync') { $issues += 'T105 FAIL: activation must not directly write to committed repo — must stage through backend.' }

    # CommitAsync catch must not set _completed before backend rollback (Defect A)
    if ($hostTx -match '(?s)catch\s*\{\s*_completed\s*=\s*true') { $issues += 'T105 FAIL: CommitAsync catch must rollback backend before setting _completed.' }
    if ($hostTx -notmatch 'RollbackAsync\(_innerTx!,\s*CancellationToken\.None\)') { $issues += 'T105 FAIL: CommitAsync catch must pass CancellationToken.None (not caller ct) to rollback.' }
    if ($hostTx -notmatch 'canonicalIndex\s*=\s*\(int\)target\s*\+\s*1') { $issues += 'T105 FAIL: LockAsync must derive canonicalIndex = (int)target + 1.' }
    if ($hostTx -notmatch '_lockTrace\.Any\(l => l\.Target == target\)') { $issues += 'T105 FAIL: LockAsync must reject duplicate targets.' }
    if ($hostTx -match '_begun\s*=\s*true;\s*_innerTx\s*=\s*await') { $issues += 'T105 FAIL: BeginAsync must set _begun after backend succeeds, not before.' }

    $t095Path = Join-Path $repoRoot 'tests\Unit\Organization\PointActivationTransactionTests.cs'
    $t095Source = Get-Content -LiteralPath $t095Path -Raw
    if ($t095Source -match 'commit-fail.*RollbackCount.*!=\s*0') { $issues += 'T105 FAIL: T095 must expect RollbackCount=1 after commit failure, not 0.' }
    if ($t095Source -notmatch 'BeginFailureSafety') { $issues += 'T105 FAIL: T095 must include BeginFailureSafety test for begin-inconsistent defect.' }
    if ($t095Source -notmatch 'IsBegun') { $issues += 'T105 FAIL: T095 must assert IsBegun on coordinator after begin failure.' }
    if ($t095Source -notmatch 'GetWorkspace\(coord\) is not null') { $issues += 'T105 FAIL: T095 CancellationRollback must verify workspace cleanup with GetWorkspace.' }
    if ($t095Source -match 'AssertionCount') { $issues += 'T105 FAIL: T095 must use CompositeCheckCount not AssertionCount.' }
    if ($t095Source -notmatch 'GetWorkspace\(coord\) is not null') { $issues += 'T105 FAIL: T095 AtomicCommitFailure must verify workspace cleanup.' }
    if ($t095Source -match 'public const int CaseCount') { $issues += 'T105 FAIL: T095 must use runtime TestCount, not constant CaseCount.' }
    if ($t095Source -notmatch '50,\s*150,\s*450') { $issues += 'T105 FAIL: T095 RetryDelays must assert exact [50,150,450] trace.' }
    if ($t095Source -notmatch 'cts\.Cancel\(\)') { $issues += 'T105 FAIL: T095 CancellationRollback must use a cancelled token.' }
    if ($t095Source -notmatch 'lock-fail: rollback=1') { $issues += 'T105 FAIL: T095 LockFailureRollback must assert backend.RollbackCount == 1.' }

    $t103IntPath = Join-Path $repoRoot 'tests\Integration\Organization\PointActivationTransactionTests.cs'
    $t103Source = Get-Content -LiteralPath $t103IntPath -Raw
    if ($t103Source -notmatch 'RollbackCount\s*!=\s*1') { $issues += 'T105 FAIL: T103 AtomicCommitFailure must assert backend.RollbackCount == 1.' }
    if ($t103Source -notmatch 'GetWorkspace\(.*HostTransaction\) is not null') { $issues += 'T105 FAIL: T103 AtomicCommitFailure must verify workspace cleanup with GetWorkspace.' }
    if ($t103Source -notmatch 'CommitCount\s*!=\s*0') { $issues += 'T105 FAIL: T103 AtomicCommitFailure must assert backend.CommitCount == 0.' }

    $t094Path = Join-Path $repoRoot 'tests\Unit\Organization\PointActivationTests.cs'
    $t094Source = Get-Content -LiteralPath $t094Path -Raw
    if ($t094Source -match 'public const int CaseCount') { $issues += 'T105 FAIL: T094 must use runtime TestCount, not constant CaseCount.' }
    if ($t094Source -match 'AssertionCount') { $issues += 'T105 FAIL: T094 must use CompositeCheckCount not AssertionCount.' }

    $queryContracts = Get-Content -LiteralPath (Join-Path $ModuleRoot 'Organization\Contracts\OrganizationQueryContracts.cs') -Raw
    foreach ($port in @('IActivationIdentityParticipant','IActivationOrganizationParticipant','IActivationCatalogParticipant')) {
        if ($queryContracts -notmatch "interface\s+$port[\s\S]*?IHostTransaction") { $issues += "T105 FAIL: $port must be typed to IHostTransaction." }
    }
    if ($outbox -notmatch 'ITransactionalOutboxWriter\s*:\s*IHostTransactionParticipant' -or $outbox -notmatch 'IHostTransaction\s+hostTransaction') { $issues += 'T105 FAIL: outbox writer must be a typed host participant.' }
    if ($queryContracts -notmatch 'string\?\s+PointId' -or $queryContracts -notmatch 'string\?\s+MappingPointId' -or
        $queryContracts -notmatch 'CompatibilityIdentity' -or $queryContracts -notmatch 'CompatibilityStatus') { $issues += 'T105 FAIL: catalog snapshot must carry target Point and compatibility identity/version/status.' }
    if ($hostTx -notmatch 'new\[\]\s*\{\s*50,\s*150,\s*450\s*\}' -or $hostTx -notmatch 'attempt\s*<\s*4') { $issues += 'T105 FAIL: retry must include 50/150/450ms and four total attempts.' }
    if ($eventSource -match 'ctx\.CausationId\s*\?\?' -or $eventSource -notmatch 'ctx\.CausationId') { $issues += 'T105 FAIL: nullable CausationId must be preserved without correlation fallback.' }
    if ($integration -notmatch 'ActivateMeasurementPoint\.ExecuteAsync') { $issues += 'T105 FAIL: T103 must invoke the actual activation orchestrator.' }
    # T103 must include StaleVersion and AtomicCommitFailure cases
    if ($integration -notmatch 'StaleVersion|stale') { $issues += 'T105 FAIL: T103 must include a StaleVersion case.' }
    if ($integration -notmatch 'AtomicCommitFailure|atomic') { $issues += 'T105 FAIL: T103 must include an AtomicCommitFailure case.' }
    if ($integration -match 'AssertionCount') { $issues += 'T105 FAIL: T103 must use CompositeCheckCount not AssertionCount.' }
    $unitProgram = Get-Content -LiteralPath (Join-Path $repoRoot 'tests\Unit\Program.cs') -Raw
    if ($unitProgram -notmatch 'Unit\.Organization\.PointActivationTransactionTests\.Run') { $issues += 'T105 FAIL: T095 unit suite must be explicitly registered in Program.' }
    # Check that Phase 5 RED evidence does not describe intentional breakage induction
    # (Passive mentions like "no sabotage was used" are acceptable; active declarations of
    #  defect injection are not.)
    $phase5RedPath = Join-Path $repoRoot 'specs\002-asset-simulator-latest\checklists\phase-05-red.md'
    if (Test-Path -LiteralPath $phase5RedPath) {
        $redContent = Get-Content -LiteralPath $phase5RedPath -Raw
        if ($redContent -match '(?i)was (injected|sabotaged|intentionally (broken|defected))|PHASE5_REQUIRED was returned|changed production code to fail') {
            $issues += 'T105 FAIL: Phase 5 RED evidence must not rely on intentional defect injection.'
        }
        if ($redContent -notmatch 'dotnet build.*--no-restore') { $issues += 'T105 FAIL: RED evidence must include exact build command.' }
        if ($redContent -notmatch 'Exit code: \*\*0\*\*') { $issues += 'T105 FAIL: RED evidence must report build exit code 0.' }
        if ($redContent -notmatch 'dotnet run.*--no-build') { $issues += 'T105 FAIL: RED evidence must include exact run command.' }
        if ($redContent -notmatch 'Exit code: \*\*1\*\*') { $issues += 'T105 FAIL: RED evidence must report run exit code 1 (non-zero).' }
    }
    # Check that fake participant StageActivationAsync writes to backend workspace
    $fakeOrgParticipant = Get-Content -LiteralPath (Join-Path $repoRoot 'tests\Unit\Fakes\FakeActivationOrganizationParticipant.cs') -Raw
    if ($fakeOrgParticipant -match 'UpdatePointAsync|_repo\.UpdatePoint|_repo\.AddLifecycleEntry|_repo\.ReplacePoint') { $issues += 'T105 FAIL: fake Organization participant must write to backend workspace, not directly to committed repo.' }
    $fakeOutbox = Get-Content -LiteralPath (Join-Path $repoRoot 'tests\Unit\Fakes\FakeTransactionalOutboxWriter.cs') -Raw
    if ($fakeOutbox -match '_committed\.(Add|AddRange|Insert)') { $issues += 'T105 FAIL: fake outbox must write to backend workspace, not directly to committed store.' }

    $phase6Files = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'tests') -Recurse -File |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.FullName -match '[\\/]Phase6[\\/]|phase-06|TelemetryIngestion|SimulatorRun' }
    if ($phase6Files) { $issues += 'T105 FAIL: Phase 6 implementation/evidence files must not be introduced.' }

    # Check that T106 and T107 are rewritten with correct findings (Defect H)
    $t106Path = Join-Path $repoRoot 'specs\002-asset-simulator-latest\checklists\phase-05-review.md'
    $t106Content = Get-Content -LiteralPath $t106Path -Raw
    if ($t106Content -notmatch 'CommitAsync.*sets.*_completed.*true.*before.*backend.*rollback') {
        $issues += 'T105 FAIL: T106 must include CommitAsync catch ordering finding (F01).'
    }
    if ($t106Content -notmatch 'Critical=0') { $issues += 'T105 FAIL: T106 must report Critical=0 for PASS.' }
    if ($t106Content -notmatch 'High=0') { $issues += 'T105 FAIL: T106 must report High=0 for PASS.' }
    $t107Path = Join-Path $repoRoot 'specs\002-asset-simulator-latest\checklists\phase-05-activation.md'
    $t107Content = Get-Content -LiteralPath $t107Path -Raw
    if ($t107Content -notmatch 'PASS 13, BLOCKED 1, FAIL 0') { $issues += 'T105 FAIL: T107 must report final PASS 13, BLOCKED 1, FAIL 0 ledger.' }
}

if ($issues.Count -gt 0) {
    foreach ($issue in $issues) {
        Write-Error $issue
    }
    throw "Architecture boundary contract checks failed: $($issues.Count) issue(s)."
}

Write-Output 'PASS: architecture boundary contract'
