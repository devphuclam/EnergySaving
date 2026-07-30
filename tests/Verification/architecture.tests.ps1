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
    if ($hostTx -notmatch 'if\s*\(!_begun\s*\|\|\s*_innerTx\s+is\s+null\)\s*return') { $issues += 'T105 FAIL: RollbackAsync must guard before begin and null backend transaction.' }
    if ($hostTx -notmatch 'RollbackAsync\(_innerTx!,\s*ct\)') { $issues += 'T105 FAIL: RollbackAsync must call backend only with a real transaction.' }

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
    if ($integration -notmatch 'BeginFailure') { $issues += 'T105 FAIL: T103 must include a BeginFailure outcome.' }
    # T103 must include StaleVersion and AtomicCommitFailure cases
    if ($integration -notmatch 'StaleVersion|stale') { $issues += 'T105 FAIL: T103 must include a StaleVersion case.' }
    if ($integration -notmatch 'AtomicCommitFailure|atomic') { $issues += 'T105 FAIL: T103 must include an AtomicCommitFailure case.' }
    if ($integration -match 'AssertionCount') { $issues += 'T105 FAIL: T103 must use CompositeCheckCount not AssertionCount.' }
    $unitProgram = Get-Content -LiteralPath (Join-Path $repoRoot 'tests\Unit\Program.cs') -Raw
    if ($unitProgram -notmatch 'Unit\.Organization\.PointActivationTransactionTests\.Run') { $issues += 'T105 FAIL: T095 unit suite must be explicitly registered in Program.' }
    if ($t095Source -notmatch 'BeginFailureRetry') { $issues += 'T105 FAIL: T095 must prove same-coordinator begin retry.' }
    if ($t095Source -match 'catch\s*\(NullReferenceException\).*Check\([^\)]*,\s*null\)') { $issues += 'T105 FAIL: DisposeAsync exception must be recorded as a failure, not a pass.' }
    $t106Path = Join-Path $repoRoot 'specs\002-asset-simulator-latest\checklists\phase-05-review.md'
    $t106Content = Get-Content -LiteralPath $t106Path -Raw
    if ($t106Content -notmatch 'cb5b6b46c10b90be5501e6c9ff9f3dc47522fd89' -or $t106Content -notmatch 'BeginFailure') { $issues += 'T105 FAIL: T106 must use the closure baseline and record BeginFailure.' }
    $t107Path = Join-Path $repoRoot 'specs\002-asset-simulator-latest\checklists\phase-05-activation.md'
    $t107Content = Get-Content -LiteralPath $t107Path -Raw
    if ($t107Content -notmatch 'cb5b6b46c10b90be5501e6c9ff9f3dc47522fd89' -or $t107Content -notmatch 'BeginFailure') { $issues += 'T105 FAIL: T107 must use the closure baseline and record BeginFailure evidence.' }
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

    # --- T128 Phase 6 Simulator Run/Worker boundary and determinism checks ---
    $generatorPath = Join-Path $ModuleRoot 'Acquisition\Domain\DeterministicGenerator.cs'
    $generator = Get-Content -LiteralPath $generatorPath -Raw
    foreach ($constant in @(
        '0x5851F42D4C957F2D',
        '0x14057B7EF767814F',
        '0xAEF17502108EF2D9',
        '0xCBF29CE484222325',
        '0x00000100000001B3'
    )) {
        if ($generator -notmatch [regex]::Escape($constant)) {
            $issues += "T128 FAIL: deterministic generator is missing normative constant $constant."
        }
    }
    if ($generator -match '\b(Random|RandomNumberGenerator)\b|DateTime\.(UtcNow|Now)|Environment\.TickCount') {
        $issues += 'T128 FAIL: deterministic generator must not use random/time/current entropy.'
    }
    if ($generator -notmatch 'SerializedStateLength\s*=\s*25' -or
        $generator -notmatch 'scenario\s*==\s*SimulatorScenario\.Constant[\s\S]*DrawCount|new\s+DeterministicGeneration\(minimumValue,\s*Serialize\(current\),\s*0\)') {
        $issues += 'T128 FAIL: state must be exactly 25 bytes and Constant must consume zero draws.'
    }

    $vectorTestsPath = Join-Path $repoRoot 'tests\Unit\Acquisition\DeterministicGeneratorVectorTests.cs'
    $vectorTests = Get-Content -LiteralPath $vectorTestsPath -Raw
    foreach ($literal in @(
        '032ba308f46f1f8e4f8167f77e7b0514000000000000000000',
        '11.6519',
        'ed99faae39338fb74f8167f77e7b0514013f80c23bc5fbfb3f',
        '17.9149',
        'ed99faae39338fb74f8167f77e7b0514000000000000000000'
    )) {
        if ($vectorTests -notmatch [regex]::Escape($literal)) {
            $issues += "T128 FAIL: literal generator test is missing normative fixture $literal."
        }
    }

    $identityPath = Join-Path $ModuleRoot 'Acquisition\Domain\MeasurementIdentity.cs'
    $identity = Get-Content -LiteralPath $identityPath -Raw
    if ($identity -notmatch '02e993bb-c767-5ff6-963f-530e1dfdff6b' -or
        $identity -notmatch 'SHA1\.HashData' -or
        $identity -notmatch '0x50' -or
        $identity -notmatch '0x80' -or
        $identity -match 'Guid\.NewGuid') {
        $issues += 'T128 FAIL: Measurement identity must use the fixed SHA-1 UUIDv5 namespace without random fallback.'
    }

    $attemptServicePath = Join-Path $ModuleRoot 'Acquisition\Application\ProductionAttemptService.cs'
    $attemptService = Get-Content -LiteralPath $attemptServicePath -Raw
    $runCommandsPath = Join-Path $ModuleRoot 'Acquisition\Application\RunCommands.cs'
    $runCommands = Get-Content -LiteralPath $runCommandsPath -Raw
    if ($runCommands -notmatch 'AlgorithmVersion\s*!=\s*SimulatorConfigurationConstants\.AlgorithmVersion') {
        $issues += 'T128 FAIL: Start must require the normative AlgorithmVersion == 1.'
    }
    if ($runCommands -notmatch 'Enum\.IsDefined\(snapshot\.Scenario\)' -or
        $runCommands -notmatch 'SimulatorScenario\.Constant\s+or\s+SimulatorScenario\.Normal') {
        $issues += 'T128 FAIL: Start must reject unknown SimulatorScenario values.'
    }
    $startValidation = $runCommands.IndexOf('ValidateStart(snapshot', [StringComparison]::Ordinal)
    $startInitialization = $runCommands.IndexOf('_generator.Initialize', [StringComparison]::Ordinal)
    $startRunId = $runCommands.IndexOf('var runId = Guid.NewGuid()', [StringComparison]::Ordinal)
    $startTransaction = $runCommands.IndexOf('_unitOfWork.BeginAsync', [StringComparison]::Ordinal)
    if ($startValidation -lt 0 -or $startInitialization -le $startValidation -or
        $startRunId -le $startValidation -or $startTransaction -le $startValidation) {
        $issues += 'T128 FAIL: complete Start validation must precede PRNG, Run ID, and transaction begin.'
    }
    $existingRunRead = $runCommands.IndexOf(
        '_runs.GetCurrentBySourceAsync(command.SourceId', [StringComparison]::Ordinal)
    $currentSnapshotRead = $runCommands.IndexOf(
        '_snapshots.ResolveAsync(command.SourceId', [StringComparison]::Ordinal)
    if ($existingRunRead -lt 0 -or $currentSnapshotRead -lt 0 -or
        $existingRunRead -gt $currentSnapshotRead) {
        $issues += 'T128 FAIL: Start must query an existing nonterminal Run before current snapshot resolution.'
    }
    if ($runCommands -notmatch 'ListPointStatesAsync\(existing\.RunId[\s\S]*pinnedPoints\.Select\(point\s*=>\s*\(point\.SiteId,\s*point\.AreaId\)\)' -or
        $runCommands -notmatch 'trustedScopes\?\.Distinct\(\)') {
        $issues += 'T128 FAIL: existing Run authorization must use distinct pinned Run-Point Site/Area scopes.'
    }
    if ($runCommands -notmatch 'snapshot\.SourceId\s*!=\s*command\.SourceId') {
        $issues += 'T128 FAIL: new Start must reject a snapshot whose SourceId differs from the command.'
    }
    $pendingRead = $attemptService.IndexOf('GetPendingAsync', [StringComparison]::Ordinal)
    $generation = $attemptService.IndexOf('_generator.Generate', [StringComparison]::Ordinal)
    if ($pendingRead -lt 0 -or $generation -lt 0 -or $pendingRead -gt $generation) {
        $issues += 'T128 FAIL: existing Pending must be read before generator/state/cursor work.'
    }
    $coordinator = [regex]::Match(
        $attemptService,
        '(?s)public sealed class SimulatorProductionCoordinator.*').Value
    $coordinatorPending = $coordinator.IndexOf('_attempts.LoadPendingAsync', [StringComparison]::Ordinal)
    $coordinatorEligibility = $coordinator.IndexOf('_eligibility.IsPinnedInputActiveAsync', [StringComparison]::Ordinal)
    if ($coordinatorPending -lt 0 -or $coordinatorEligibility -lt 0 -or
        $coordinatorPending -gt $coordinatorEligibility) {
        $issues += 'T128 FAIL: Worker coordinator must load existing Pending before owner eligibility.'
    }
    $ownerFailureBranch = [regex]::Match(
        $coordinator,
        '(?s)if\s*\(!eligibility\.IsActive\).*?continue;').Value
    if ($ownerFailureBranch -notmatch 'ownerCode\s*==\s*"SOURCE_INACTIVE"[\s\S]*StopForOwnerDriftAsync' -or
        $ownerFailureBranch -match 'StopForOwnerDriftAsync\(run\.RunId,\s*ownerCode,\s*ct\);\s*failures') {
        $issues += 'T128 FAIL: SOURCE_INACTIVE must be the only owner failure routed to global Run Stop.'
    }
    if ([regex]::Matches($attemptService, 'StageReservationAsync').Count -ne 1 -or
        $attemptService -notmatch 'SimulatorRunPointReservationTransition[\s\S]*checked\(sequence\s*\+\s*1\)') {
        $issues += 'T128 FAIL: one new reservation must advance cursor/state/Generated exactly once.'
    }
    $tryReserve = $attemptService.IndexOf('_attempts.TryReserveAsync', [StringComparison]::Ordinal)
    $stageReservation = $attemptService.IndexOf('_runs.StageReservationAsync', [StringComparison]::Ordinal)
    if ($tryReserve -lt 0 -or $stageReservation -le $tryReserve) {
        $issues += 'T128 FAIL: Pending insertion must win before Run-Point/Generated staging.'
    }
    if ($attemptService -notmatch 'TelemetryDispatchResultValidator\.EnsureValid\((result|existing\.Payload,\s*result)\)') {
        $issues += 'T128 FAIL: terminal result validation must run before completion staging.'
    }
    if ($attemptService -notmatch 'if\s*\(finalized\.FirstTransition\)[\s\S]*StageFinalCounterAsync') {
        $issues += 'T128 FAIL: final counter mutation must occur only on the first terminal transition.'
    }
    if ($coordinator -notmatch 'MaintainLeaseAsync' -or
        $coordinator -notmatch 'RenewLeaseAsync' -or
        $coordinator -notmatch 'LEASE_LOST' -or
        $coordinator -notmatch 'ReleaseLeaseAsync\([\s\S]*CancellationToken\.None') {
        $issues += 'T128 FAIL: long dispatch must renew the versioned lease and release safely.'
    }

    $workerPath = Join-Path $repoRoot 'src\Worker\SimulatorProductionWorker.cs'
    $worker = Get-Content -LiteralPath $workerPath -Raw
    $reserveCall = $coordinator.IndexOf('_attempts.ReserveAsync', [StringComparison]::Ordinal)
    $finalizerCall = $coordinator.IndexOf('_finalizer.ExecuteAsync', [StringComparison]::Ordinal)
    $finalizerSource = Get-Content -LiteralPath (
        Join-Path $ModuleRoot 'Acquisition\Application\FinalizeTelemetryAttempt.cs') -Raw
    $dispatchCall = $finalizerSource.IndexOf('_telemetry.DispatchCanonicalAsync', [StringComparison]::Ordinal)
    $finalizeCall = $finalizerSource.IndexOf('_attempts.FinalizeAsync', [StringComparison]::Ordinal)
    if ($reserveCall -lt 0 -or $finalizerCall -le $reserveCall -or
        $dispatchCall -lt 0 -or $finalizeCall -le $dispatchCall) {
        $issues += 'T128 FAIL: Acquisition coordinator must reserve, dispatch outside reservation, then finalize.'
    }
    if ($attemptService -notmatch 'ListRunningAsync' -or
        $worker -notmatch 'ISimulatorProductionCoordinator' -or
        $worker -match 'IAcquisitionRunRepository|ISimulatorRunUnitOfWork|IUMP\.Modules\.Acquisition\.Application') {
        $issues += 'T128 FAIL: Worker must delegate through the public Acquisition coordinator contract.'
    }
    if ($worker -notmatch 'LogWarning' -or $worker -notmatch 'CorrelationId') {
        $issues += 'T128 FAIL: Worker Point failures must be structured and correlation-aware.'
    }
    if ($worker -match '(?i)\b(INSERT|UPDATE|DELETE)\b|DbContext|Npgsql|Telemetry.*Repository') {
        $issues += 'T128 FAIL: Worker must not write Telemetry storage.'
    }

    $runContractsPath = Join-Path $ModuleRoot 'Acquisition\Contracts\RunPersistenceContracts.cs'
    $attemptContractsPath = Join-Path $ModuleRoot 'Acquisition\Contracts\ProductionAttemptContracts.cs'
    $runContracts = Get-Content -LiteralPath $runContractsPath -Raw
    $attemptContracts = Get-Content -LiteralPath $attemptContractsPath -Raw
    if ($runContracts -notmatch 'ConfigurationId' -or $runContracts -notmatch 'MappingId' -or
        $runContracts -notmatch 'PointVersionAtStart' -or
        ($runContracts + $attemptContracts) -match 'DbContext|IQueryable|Npgsql') {
        $issues += 'T128 FAIL: Acquisition must own pinned provider-neutral Run/state/attempt contracts.'
    }
    if ($runContracts -match 'StageReservationAsync\([^)]*SimulatorRunPointState' -or
        $runContracts -notmatch 'StageReservationAsync\(SimulatorRunPointReservationTransition') {
        $issues += 'T128 FAIL: StageReservationAsync must accept only a mutable transition contract.'
    }
    $transitionContract = [regex]::Match(
        $runContracts,
        '(?s)record SimulatorRunPointReservationTransition\(.*?\);').Value
    foreach ($pinned in @(
        'PointVersionAtStart','MappingId','MappingVersion','MetricId','UnitId',
        'UnitCode','SourceVersion','SiteId','AreaId'
    )) {
        if ($transitionContract -match "\b$pinned\b") {
            $issues += "T128 FAIL: reservation transition exposes pinned update field $pinned."
        }
    }
    if ($attemptContracts -notmatch 'TERMINAL_RESULT_INVALID' -or
        $attemptContracts -notmatch 'TelemetryAttemptOutcome\.Accepted[\s\S]*ProductionFinalClassification\.Accepted' -or
        $attemptContracts -notmatch 'TelemetryAttemptOutcome\.Rejected[\s\S]*ProductionFinalClassification\.Rejected') {
        $issues += 'T128 FAIL: terminal outcome/classification validation is absent or incomplete.'
    }

    $fakeRunRepositoryPath = Join-Path $repoRoot 'tests\Unit\Fakes\FakeAcquisitionRunRepositories.cs'
    $fakeRunRepository = Get-Content -LiteralPath $fakeRunRepositoryPath -Raw
    if ($fakeRunRepository -match '_committed\.Attempts\[key\]\s*=\s*Clone\(attempt\)' -or
        $fakeRunRepository -notmatch '_committed\s*=\s*winner') {
        $issues += 'T128 FAIL: uniqueness-race fake must publish the complete winning transaction, not an isolated attempt.'
    }
    foreach ($required in @(
        'ExpectedRunVersion','ExpectedPointStateVersion','ExpectedNextSourceSequence',
        'ResultingPrngState','NextDueAtUtc'
    )) {
        if ($fakeRunRepository -notmatch $required) {
            $issues += "T128 FAIL: fake reservation is missing optimistic/mutable check $required."
        }
    }

    $migration7Path = Join-Path $repoRoot 'database\migrations\0007_acquisition_run.sql'
    $migration7 = Get-Content -LiteralPath $migration7Path -Raw
    foreach ($table in @(
        'acquisition.simulator_run',
        'acquisition.simulator_run_point_state',
        'acquisition.simulator_production_attempt'
    )) {
        if ($migration7 -notmatch [regex]::Escape($table)) {
            $issues += "T128 FAIL: migration 0007 is missing Acquisition-owned table $table."
        }
    }
    if ($migration7 -match '(?i)REFERENCES\s+(catalog|organization|telemetry)\.' -or
        $migration7 -match '(?i)CREATE\s+EXTENSION') {
        $issues += 'T128 FAIL: migration 0007 must have no cross-schema FK or CREATE EXTENSION.'
    }
    if ($migration7 -notmatch 'octet_length\(prng_state\)\s*=\s*25' -or
        $migration7 -notmatch 'UNIQUE\s*\(measurement_id\)' -or
        $migration7 -notmatch 'accepted_count\s*\+\s*rejected_count\s*<=\s*generated_count') {
        $issues += 'T128 FAIL: migration 0007 is missing state length, identity uniqueness, or counter invariants.'
    }
    if ($migration7 -notmatch 'reject_simulator_run_point_pinned_mutation' -or
        $migration7 -notmatch 'trg_simulator_run_point_pinned_immutable') {
        $issues += 'T128 FAIL: migration 0007 lacks Run-Point pinned-field immutability.'
    }
    foreach ($column in @(
        'run_id','point_id','point_version_at_start','mapping_id','mapping_version',
        'metric_id','unit_id','unit_code','source_version','site_id','area_id'
    )) {
        if ($migration7 -notmatch "NEW\.$column\s+IS\s+DISTINCT\s+FROM\s+OLD\.$column") {
            $issues += "T128 FAIL: migration 0007 pinned immutability omits $column."
        }
    }
    if ($migration7 -notmatch "telemetry_outcome\s*=\s*'Accepted'[\s\S]*final_classification\s*=\s*'Accepted'" -or
        $migration7 -notmatch "telemetry_outcome\s*=\s*'Rejected'[\s\S]*final_classification\s*=\s*'Rejected'[\s\S]*latest_advanced\s*=\s*false[\s\S]*rejection_code\s+IS\s+NOT\s+NULL[\s\S]*length\(btrim\(rejection_code\)\)\s*>\s*0") {
        $issues += 'T128 FAIL: migration 0007 lacks Accepted/Rejected terminal-pair consistency.'
    }

    $t124Path = Join-Path $repoRoot 'tests\Integration\Acquisition\RunAttemptRepositoryTests.cs'
    $t124 = Get-Content -LiteralPath $t124Path -Raw
    $credentialAssignmentToken = 'Pass' + 'word='
    if ($t124 -match '\bFake[A-Za-z0-9_]*|\bas\s+Fake|Skip|TODO|Npgsql|Host=' -or
        $t124.IndexOf($credentialAssignmentToken, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        $issues += 'T128 FAIL: T124 runner must remain provider-neutral, credential-free, and fully executable.'
    }
    if ($t124 -notmatch 'TestCount\+\+' -or $t124 -notmatch 'AssertionCount\+\+') {
        $issues += 'T128 FAIL: T124 must maintain actual scenario and assertion counters.'
    }
    $raceScenario = [regex]::Match(
        $t124,
        '(?s)private async Task ReservationRaceAsync\(\).*?(?=private async Task)').Value
    if ($raceScenario -match 'GeneratedCount\s*==\s*0|NextSourceSequence\s*==\s*0' -or
        $raceScenario -notmatch 'GeneratedCount:\s*1' -or
        $raceScenario -notmatch 'NextSourceSequence:\s*1') {
        $issues += 'T128 FAIL: T124 race scenario must expect the committed winner advancement.'
    }
    foreach ($scenario in @(
        'PinnedStateAndOptimisticConflictAsync',
        'ReservationRaceAsync',
        'AcceptedFinalizeReplayConflictAsync',
        'RejectedAndDuplicateClassificationAsync',
        'InvalidTerminalResultsAsync',
        'FinalizationCommitFailureAsync'
    )) {
        if ($t124 -notmatch [regex]::Escape($scenario)) {
            $issues += "T128 FAIL: T124 omits required provider-neutral scenario $scenario."
        }
    }
    if ($t124 -notmatch 'AttemptPinnedMutationAsync' -or
        $t124 -notmatch 'AttemptPayloadMutationAsync' -or
        $t124 -notmatch 'PINNED_STATE_IMMUTABLE' -or
        $t124 -notmatch 'ATTEMPT_PAYLOAD_IMMUTABLE') {
        $issues += 'T128 FAIL: T124 must execute pinned-state and immutable-payload rejection scenarios.'
    }
    foreach ($counterPath in @(
        'tests\Unit\Acquisition\DeterministicGeneratorVectorTests.cs',
        'tests\Unit\Acquisition\MeasurementIdentityTests.cs',
        'tests\Unit\Acquisition\RunControlTests.cs',
        'tests\Unit\Worker\ProductionDispatchTests.cs',
        'tests\Unit\Acquisition\ProductionAttemptTests.cs',
        'tests\Unit\Acquisition\AcquisitionEventTests.cs'
    )) {
        $counterSource = Get-Content -LiteralPath (Join-Path $repoRoot $counterPath) -Raw
        if ($counterSource -match '(TestCount|CheckCount)\s*=\s*[1-9][0-9]*\s*;' -or
            $counterSource -notmatch 'TestCount\+\+' -or
            $counterSource -notmatch 'CheckCount\+\+') {
            $issues += "T128 FAIL: $counterPath must use actual executed counters."
        }
    }

    $t110Path = Join-Path $repoRoot 'tests\Unit\Acquisition\RunControlTests.cs'
    $t110 = Get-Content -LiteralPath $t110Path -Raw
    foreach ($requiredStartCase in @(
        'missing Source',
        'non-Simulator Source',
        'interval zero',
        'interval negative',
        'Constant bounds mismatch',
        'Normal bounds invalid',
        'NaN minimum',
        'NaN maximum',
        'positive Infinity',
        'negative Infinity',
        'snapshot Source mismatch',
        'changed current Mapping Site',
        'existing Paused Run'
    )) {
        if ($t110 -notmatch [regex]::Escape($requiredStartCase)) {
            $issues += "T128 FAIL: T110 omits required Start case $requiredStartCase."
        }
    }
    if ($t110 -notmatch 'ResolveCount\s*==\s*0' -or
        $t110 -notmatch 'BeginCount\s*==\s*0' -or
        $t110 -notmatch 'CommittedPointCount\s*==\s*0') {
        $issues += 'T128 FAIL: T110 must prove existing-Run short-circuit and rejected Start atomicity.'
    }

    $t111Path = Join-Path $repoRoot 'tests\Unit\Worker\ProductionDispatchTests.cs'
    $t111 = Get-Content -LiteralPath $t111Path -Raw
    if ($t111 -notmatch 'Point-specific owner isolation' -or
        $t111 -notmatch 'both due Points independently' -or
        $t111 -notmatch 'SOURCE_INACTIVE' -or
        $t111 -notmatch 'no global Stop event') {
        $issues += 'T128 FAIL: T111 omits Point-specific isolation or Source-wide multi-Point Stop evidence.'
    }

    $simulatorContractPath = Join-Path $repoRoot (
        'specs\002-asset-simulator-latest\contracts\simulator.md')
    $simulatorContract = Get-Content -LiteralPath $simulatorContractPath -Raw
    if ($simulatorContract -notmatch 'SOURCE_INACTIVE.*Source-wide' -or
        $simulatorContract -notmatch 'MAPPING_INACTIVE.*POINT_INACTIVE.*ANCESTOR_INACTIVE.*Point-specific' -or
        $simulatorContract -notmatch 'unrelated due Points continue independently' -or
        $simulatorContract -match 'Resulting Run status is Stopped with error code') {
        $issues += 'T128 FAIL: Simulator owner-state contract must preserve Point isolation and Source-wide Stop only.'
    }

    if (-not (Test-Path -LiteralPath (Join-Path $ModuleRoot 'Acquisition\Infrastructure\PostgresRunRepositories.cs'))) {
        $issues += 'T128 FAIL: resolved local Npgsql capability requires the PostgreSQL Run adapter.'
    }
    foreach ($compositionRoot in @(
        (Join-Path $repoRoot 'src\Api\Program.cs'),
        (Join-Path $repoRoot 'src\Worker\Program.cs')
    )) {
        $composition = Get-Content -LiteralPath $compositionRoot -Raw
        if ($composition -notmatch 'AddIumpPostgresModules') {
            $issues += "T128 FAIL: PostgreSQL module registration is missing from $compositionRoot."
        }
    }

    # --- T149 Phase 7 canonical Telemetry identity/ownership/atomicity checks ---
    $telemetryContractsPath = Join-Path $ModuleRoot 'Telemetry\Contracts\TelemetryPersistenceContracts.cs'
    $telemetryProjectionPath = Join-Path $ModuleRoot 'Telemetry\Contracts\TelemetryProjectionContracts.cs'
    $telemetryDomainPath = Join-Path $ModuleRoot 'Telemetry\Domain\MeasurementIdentityResult.cs'
    $telemetryIngestionPath = Join-Path $ModuleRoot 'Telemetry\Application\IngestMeasurement.cs'
    $telemetryPersistencePath = Join-Path $ModuleRoot 'Telemetry\Application\TelemetryPersistenceService.cs'
    $telemetryContracts = Get-Content -LiteralPath $telemetryContractsPath -Raw
    $telemetryProjection = Get-Content -LiteralPath $telemetryProjectionPath -Raw
    $telemetryDomain = Get-Content -LiteralPath $telemetryDomainPath -Raw
    $telemetryIngestion = Get-Content -LiteralPath $telemetryIngestionPath -Raw
    $telemetryPersistence = Get-Content -LiteralPath $telemetryPersistencePath -Raw

    if ($telemetryDomain -notmatch '02e993bb-c767-5ff6-963f-530e1dfdff6b' -or
        $telemetryDomain -notmatch 'SHA1\.HashData' -or
        $telemetryDomain -notmatch '0x50' -or $telemetryDomain -notmatch '0x80' -or
        $telemetryDomain -match 'Guid\.NewGuid') {
        $issues += 'T149 FAIL: Telemetry must verify the exact UUIDv5 tuple without generating a replacement ID.'
    }
    if ($telemetryDomain -match 'JsonSerializer|CurrentCulture' -or
        $telemetryDomain -match 'received_at|processing_at|retry|lease|transport|tracing') {
        $issues += 'T149 FAIL: Telemetry fingerprint must use typed deterministic encoding and exclude retry/time metadata.'
    }
    if ($telemetryDomain -notmatch 'IUMP:TELEMETRY:FINGERPRINT:V1' -or
        $telemetryDomain -notmatch 'SHA256\.HashData' -or
        $telemetryDomain -match 'ICommandIdempotencyStore|inbox_message|production.attempt') {
        $issues += 'T149 FAIL: Telemetry fingerprint must remain distinct from API/inbox/Acquisition identity.'
    }
    $trustIndex = $telemetryIngestion.IndexOf('!producer.IsTrusted', [StringComparison]::Ordinal)
    $identityIndex = $telemetryIngestion.IndexOf('MeasurementIdentityVerifier.TryVerify', [StringComparison]::Ordinal)
    $fingerprintIndex = $telemetryIngestion.IndexOf('TelemetryRequestFingerprintV1.Compute', [StringComparison]::Ordinal)
    $registryIndex = $telemetryIngestion.IndexOf('_repository.GetTerminalAsync', [StringComparison]::Ordinal)
    $providerIndex = $telemetryIngestion.IndexOf('_providers.GetAsync', [StringComparison]::Ordinal)
    if ($trustIndex -lt 0 -or $identityIndex -le $trustIndex -or
        $fingerprintIndex -le $identityIndex -or $registryIndex -le $fingerprintIndex -or
        $providerIndex -le $registryIndex) {
        $issues += 'T149 FAIL: trusted producer -> identity -> fingerprint -> registry -> provider order is not explicit.'
    }
    if ($telemetryContracts -match 'TelemetryFinalClassification[\s\S]{0,200}(Pending|InProgress)' -or
        $telemetryContracts -notmatch 'ITelemetryIngestionRepository' -or
        $telemetryContracts -match 'DbContext|IQueryable|Npgsql') {
        $issues += 'T149 FAIL: terminal registry must be terminal-only and provider-neutral.'
    }
    foreach ($port in @(
        'ILatestProjectionRepository','ISourceHealthRepository','ITelemetryQueryRepository'
    )) {
        if ($telemetryProjection -notmatch [regex]::Escape($port)) {
            $issues += "T149 FAIL: missing provider-neutral Telemetry projection/query port $port."
        }
    }
    if ($telemetryPersistence -notmatch 'quality\s*!=\s*MeasurementQuality\.Bad' -or
        $telemetryPersistence -notmatch 'TelemetryDisposition\.Rejected' -or
        $telemetryPersistence -match 'PersistRejectedAsync[\s\S]*StageRawAsync') {
        $issues += 'T149 FAIL: Bad Latest bypass or Rejected registry-only persistence is absent.'
    }
    foreach ($target in @(
        'OrganizationSite','OrganizationArea','OrganizationAsset','OrganizationPoint',
        'CatalogSource','CatalogMapping','CatalogMetric','CatalogUnit',
        'TelemetryIdentityRawLatest','IntegrationOutbox'
    )) {
        if ($telemetryPersistence -notmatch [regex]::Escape($target)) {
            $issues += "T149 FAIL: Telemetry flow is missing lock stage $target."
        }
    }
    $organizationLock = $telemetryPersistence.IndexOf('TelemetryFlowLockTarget.OrganizationPoint', [StringComparison]::Ordinal)
    $catalogLock = $telemetryPersistence.IndexOf('TelemetryFlowLockTarget.CatalogSource', [StringComparison]::Ordinal)
    $telemetryLock = $telemetryPersistence.IndexOf('TelemetryFlowLockTarget.TelemetryIdentityRawLatest', [StringComparison]::Ordinal)
    $integrationLock = $telemetryPersistence.IndexOf('TelemetryFlowLockTarget.IntegrationOutbox', [StringComparison]::Ordinal)
    if ($organizationLock -lt 0 -or $catalogLock -le $organizationLock -or
        $telemetryLock -le $catalogLock -or $integrationLock -lt 0) {
        $issues += 'T149 FAIL: Telemetry lock order must be Organization -> Catalog -> Telemetry -> Integration.'
    }
    if ($telemetryPersistence -notmatch 'MeasurementAccepted\.v1' -or
        $telemetryPersistence -match 'PointLatestAdvanced\.v1' -or
        $telemetryPersistence -notmatch 'RequestFingerprint' -and
        $telemetryPersistence -match '\["requestFingerprint"\]') {
        $issues += 'T149 FAIL: Accepted event must be safe and Phase 7 must not emit PointLatestAdvanced.'
    }
    $acquisitionContractsPath = Join-Path $repoRoot 'src\Modules\Acquisition\Contracts\ProductionAttemptContracts.cs'
    $finalizerPath = Join-Path $repoRoot 'src\Modules\Acquisition\Application\FinalizeTelemetryAttempt.cs'
    $attemptServicePath = Join-Path $repoRoot 'src\Modules\Acquisition\Application\ProductionAttemptService.cs'
    $acquisitionFakePath = Join-Path $repoRoot 'tests\Unit\Fakes\FakeAcquisitionRunRepositories.cs'
    $telemetryFakePath = Join-Path $repoRoot 'tests\Unit\Fakes\FakeTelemetryRepositories.cs'
    $acquisitionContracts = Get-Content -LiteralPath $acquisitionContractsPath -Raw
    $finalizer = Get-Content -LiteralPath $finalizerPath -Raw
    $attemptService = Get-Content -LiteralPath $attemptServicePath -Raw
    $acquisitionFake = Get-Content -LiteralPath $acquisitionFakePath -Raw
    $telemetryFake = Get-Content -LiteralPath $telemetryFakePath -Raw
    if ($acquisitionContracts -match 'async\s+Task<CanonicalTelemetryIngestionResult>\s+DispatchCanonicalAsync' -or
        $acquisitionContracts -match 'return\s+await\s+DispatchAsync\(') {
        $issues += 'T149 FAIL: canonical ingestion client must not provide a default legacy metadata bridge.'
    }
    if ($acquisitionContracts -notmatch 'EnsureValid\(\s*SimulatorProductionPayload\s+payload\s*,\s*CanonicalTelemetryIngestionResult') {
        $issues += 'T149 FAIL: canonical result validation must receive the payload context.'
    }
    if ($finalizer -match 'LatestAdvanced\s*\?\?\s*false' -or
        $attemptService -match 'CompletedAtUtc\s*\?\?\s*_clock\.UtcNow') {
        $issues += 'T149 FAIL: canonical terminal metadata must not be silently coerced or clock-fabricated.'
    }
    if ($acquisitionContracts -notmatch 'bool\?\s+LatestAdvanced' -or
        $acquisitionContracts -notmatch 'CANONICAL_ORIGINAL_RESULT_INVALID') {
        $issues += 'T149 FAIL: nullable LatestAdvanced and canonical invalid-result code are required.'
    }
    if ($acquisitionFake -match 'DateTime\.UtcNow|auto-generated|async\s+Task<TelemetryDispatchResult>\s+DispatchAsync' -or
        $acquisitionFake -match 'payload\.SourceTimestampUtc' -or
        $acquisitionFake -notmatch 'CanonicalTelemetryFixtures\.Accepted') {
        $issues += 'T149 FAIL: acquisition fake/client must return an explicit complete canonical fixture.'
    }
    foreach ($tupleField in @('SiteId','AreaId','AssetId','MetricId','UnitId','EffectiveFromUtc','EffectiveToUtc','CompatibilityIdentity')) {
        if ($telemetryContracts -notmatch [regex]::Escape($tupleField)) {
            $issues += "T149 FAIL: provider snapshot exact tuple is missing $tupleField."
        }
    }
    if ($telemetryFake -match 'RecheckResult\s*=' -or
        $telemetryFake -notmatch 'TelemetryProviderRecheckResult\.Compare' -or
        $telemetryContracts -notmatch 'SourceType == current\.SourceType' -or
        $telemetryContracts -notmatch 'MappingPointId == current\.MappingPointId' -or
        $telemetryContracts -notmatch 'UnitCode == current\.UnitCode' -or
        $telemetryContracts -notmatch 'PointExistsMatches' -or
        $telemetryContracts -notmatch 'TrustedAreaIdMatches') {
        $issues += 'T149 FAIL: provider recheck must compare the independent exact tuple, not a generic boolean.'
    }
    if ($telemetryFake -match 'AddSeconds\(-2\)|Guid\.NewGuid\(\),\s*"MeasurementAccepted') {
        $issues += 'T149 FAIL: race winner fake must copy a complete supplied fixture without synthesis.'
    }
    foreach ($requiredMigration7 in @(
        'status = ''Pending''',
        'measurement_persisted IS NULL',
        'persisted_measurement_id IS NULL',
        'quality_code IS NULL',
        'reason_code IS NULL',
        'latest_advanced IS NULL',
        'completed_at_utc IS NULL',
        'original_correlation_id IS NULL',
        'original_lineage_id IS NULL',
        'persisted_measurement_id = measurement_id',
        'quality_code IN (''Good'', ''Uncertain'', ''Bad'')',
        'reject_completed_terminal_mutation',
        'trg_simulator_attempt_completed_terminal_immutable'
    )) {
        if ($migration7 -notmatch [regex]::Escape($requiredMigration7)) {
            $issues += "T149 FAIL: migration 0007 exact terminal invariant missing $requiredMigration7."
        }
    }
    $t134Path = Join-Path $repoRoot 'tests\Unit\Acquisition\TelemetryFinalizationTests.cs'
    $t145Path = Join-Path $repoRoot 'tests\Integration\Telemetry\TelemetryIngestionRepositoryTests.cs'
    $t134 = Get-Content -LiteralPath $t134Path -Raw
    $t145 = Get-Content -LiteralPath $t145Path -Raw
    if ($t134 -notmatch 'GetAsync\(' -or $t134 -notmatch 'TERMINAL_RESULT_CONFLICT' -or
        $t134 -notmatch 'QualityCode = "Unknown"' -or
        $t134 -notmatch 'concrete service rejects every terminal replay mutation') {
        $issues += 'T149 FAIL: T134 must perform repository round-trip and per-field replay conflicts.'
    }
    if ($t145 -notmatch 'TerminalEqual' -or $t145 -notmatch 'RequestFingerprint' -or
        $t145 -notmatch 'ReplayProbe' -or $t145 -notmatch 'RaceWinnerProbe' -or
        $t145 -notmatch 'ReplayTerminal' -or $t145 -notmatch 'StageRaceWinner' -or
        $t145 -notmatch 'EventEqual') {
        $issues += 'T149 FAIL: T145 must compare the complete terminal result and replay identity.'
    }
    foreach ($checkpoint in @(
        (Join-Path $repoRoot 'specs\002-asset-simulator-latest\checklists\phase-07-red.md'),
        (Join-Path $repoRoot 'specs\002-asset-simulator-latest\checklists\phase-07-review.md'),
        (Join-Path $repoRoot 'specs\002-asset-simulator-latest\checklists\phase-07-telemetry.md')
    )) {
        $checkpointText = Get-Content -LiteralPath $checkpoint -Raw
        if ($checkpointText -notmatch '0710ba158e9616262a94120a3800988884a8d7c7' -or
            $checkpointText -notmatch 'T151') {
            $issues += "T149 FAIL: Phase 7 checkpoint is missing the f852/T151 corrective baseline in $checkpoint."
        }
    }
    $reviewCheckPath = Join-Path $repoRoot 'tests\Unit\Telemetry\Phase7ReviewCheck.cs'
    if ((Get-Content -LiteralPath $reviewCheckPath -Raw) -match 'Check\(true') {
        $issues += 'T149 FAIL: Phase7ReviewCheck contains an unconditional passing assertion.'
    }
    # Atomic-evidence closure checks (Phase 7)
    $telemetryFake = Get-Content -LiteralPath (Join-Path $repoRoot 'tests\Unit\Fakes\FakeTelemetryRepositories.cs') -Raw
    if ($telemetryFake -notmatch 'TelemetryCommittedState') {
        $issues += 'T149 FAIL: Fake lacks aggregate TelemetryCommittedState holder.'
    }
    $publishBody = [regex]::Match($telemetryFake, '(?s)private void PublishRaceWinner.*?(?=private|public|class)').Value
    if ($publishBody -match '_terminals\[winner\.MeasurementId\]\s*=') {
        $issues += 'T149 FAIL: PublishRaceWinner still assigns terminal directly.'
    }
    $publishMethod = [regex]::Match($telemetryFake, '(?s)private void PublishRaceWinner.*?(?=private|public|class)')
    $publishAssigns = [regex]::Matches($publishMethod.Value, '_committedState\s*=\s*new TelemetryCommittedState').Count
    if ($publishAssigns -ne 1) {
        $issues += "T149 FAIL: PublishRaceWinner must perform exactly one committed-state assignment; found $publishAssigns."
    }
    if ($telemetryFake -notmatch 'RACE_WINNER_FIXTURE_CONFLICT') {
        $issues += 'T149 FAIL: Race-winner does not reject existing Measurement-ID conflict.'
    }
    if ($telemetryFake -notmatch 'RACE_WINNER_SLOT_CONFLICT') {
        $issues += 'T149 FAIL: Race-winner does not reject slot conflict.'
    }
    $t145 = Get-Content -LiteralPath (Join-Path $repoRoot 'tests\Integration\Telemetry\TelemetryIngestionRepositoryTests.cs') -Raw
    if ($t145 -notmatch 'GetCommittedLatestAsync') {
        $issues += 'T149 FAIL: T145 never calls GetCommittedLatestAsync.'
    }
    if ($t145 -notmatch 'committed!.MeasurementId == data.Latest!.MeasurementId') {
        $issues += 'T149 FAIL: T145 compares only LatestCount not exact Latest fields.'
    }
    $invalidAcceptedCount = [regex]::Matches($t145, '"invalid Accepted').Count
    $terminalUnchangedCount = [regex]::Matches($t145, 'terminal count unchanged').Count
    if ($invalidAcceptedCount -lt 2 -or $terminalUnchangedCount -lt 2) {
        $issues += "T149 FAIL: T145 lacks invalid Accepted fixture scenario (labels=$invalidAcceptedCount, terminalUnchanged=$terminalUnchangedCount)."
    }
    $invalidRejectedCount = [regex]::Matches($t145, '"invalid Rejected').Count
    if ($invalidRejectedCount -lt 1) {
        $issues += "T149 FAIL: T145 lacks invalid Rejected fixture scenario (count=$invalidRejectedCount)."
    }
    $persistenceService = Get-Content -LiteralPath (Join-Path $repoRoot 'src\Modules\Telemetry\Application\TelemetryPersistenceService.cs') -Raw
    if ($persistenceService -match 'TelemetryUniqueRaceException|InvalidOperationException.*PROVIDER_SCOPE_MISMATCH' -and
        $persistenceService -notmatch 'TelemetryIngestionResult\.Failed\("PROVIDER_SCOPE_MISMATCH"') {
        $issues += 'T149 FAIL: Trusted-scope mismatch throws instead of returning stable result.'
    }
    $factoryMethod = [regex]::Match($persistenceService, '(?s)public static TelemetryOwnerEvent Create\(.*?\)')
    if ($factoryMethod.Success -and $factoryMethod.Value -match '\? eventSiteId = null|\? eventAreaId = null') {
        $issues += 'T149 FAIL: Event factory contains optional fallback parameters.'
    }
    $telemetrySources = Get-ChildItem -LiteralPath (Join-Path $ModuleRoot 'Telemetry') -Recurse -File |
        Where-Object { $_.Extension -eq '.cs' } |
        ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
    if (($telemetrySources -join "`n") -match 'IUMP\.Modules\.Acquisition\.Application') {
        $issues += 'T149 FAIL: Telemetry references Acquisition implementation internals.'
    }
    $workerSource = Get-Content -LiteralPath (Join-Path $repoRoot 'src\Worker\Program.cs') -Raw
    if ($workerSource -match 'measurement_identity|measurement_raw|ITelemetryIngestionRepository') {
        $issues += 'T149 FAIL: Worker writes or registers Telemetry storage directly.'
    }

    # Phase 7 concurrency-and-scope closure checks (A-J)
    if ($telemetryFake -notmatch '_committedGate') {
        $issues += 'T149 FAIL: FakeTelemetryRepositories lacks _committedGate synchronization field.'
    }
    $commitAsyncBody = [regex]::Match($telemetryFake, '(?s)public ValueTask CommitAsync.*?(?=public|private|class)').Value
    if ($commitAsyncBody -notmatch 'lock\s*\(_owner\._committedGate\)') {
        $issues += 'T149 FAIL: CommitAsync not serialized inside _committedGate lock.'
    }
    if ($commitAsyncBody -notmatch 'current\.Terminals\.ContainsKey\(terminal\.MeasurementId\)') {
        $issues += 'T149 FAIL: CommitAsync missing commit-time Measurement-ID recheck.'
    }
    if ($commitAsyncBody -notmatch '(?s)SimulatorRunId.*PointId.*SourceSequence.*MeasurementId') {
        $issues += 'T149 FAIL: CommitAsync missing commit-time slot recheck.'
    }
    if ($publishBody -notmatch 'lock\s*\(_committedGate\)') {
        $issues += 'T149 FAIL: PublishRaceWinner not serialized inside _committedGate lock.'
    }
    if ($publishBody -notmatch 'storedRaw.*Equals.*fixture\.Raw') {
        $issues += 'T149 FAIL: PublishRaceWinner no-op does not verify raw equality.'
    }
    if ($publishBody -notmatch 'storedLatest.*Equals.*fixture\.Latest') {
        $issues += 'T149 FAIL: PublishRaceWinner no-op does not verify Latest equality.'
    }
    if ($publishBody -notmatch 'EventEqualsComplete') {
        $issues += 'T149 FAIL: PublishRaceWinner no-op does not verify event equality.'
    }
    $committedStateGetter = [regex]::Match($telemetryFake, '(?s)public TelemetryCommittedState CommittedState[\s\S]*?}').Value
    if ($committedStateGetter -notmatch 'lock\s*\(_committedGate\)') {
        $issues += 'T149 FAIL: CommittedState getter returns shallow reference without lock.'
    }
    if ($committedStateGetter -notmatch 'new TelemetryCommittedState') {
        $issues += 'T149 FAIL: CommittedState getter does not return a deep-copy snapshot.'
    }
    $listCommittedAsyncBody = [regex]::Match($telemetryFake, '(?s)Task.*ListCommittedAsync.*?(?=public Task|public ValueTask|private|class)').Value
    if ($listCommittedAsyncBody -notmatch 'new Dictionary.*Before.*StringComparer') {
        $issues += 'T149 FAIL: ListCommittedAsync does not deep-copy event dictionaries.'
    }
    if ($telemetryIngestion -notmatch 'TelemetryPersistenceService\.CheckTrustedScope') {
        $issues += 'T149 FAIL: IngestMeasurement does not call CheckTrustedScope before ValidateProvider.'
    }
    $scopeIndex = $telemetryIngestion.IndexOf('CheckTrustedScope', [StringComparison]::Ordinal)
    $providerIndex2 = $telemetryIngestion.IndexOf('ValidateProvider', [StringComparison]::Ordinal)
    if ($scopeIndex -lt 0 -or $providerIndex2 -lt 0 -or $scopeIndex -gt $providerIndex2) {
        $issues += 'T149 FAIL: CheckTrustedScope must precede ValidateProvider in IngestMeasurement.'
    }
    if ($persistenceService -notmatch 'EVENT_SCOPE_ID_BLANK') {
        $issues += 'T149 FAIL: Event factory missing nonblank scope ID validation.'
    }
    $factoryCreateSig = [regex]::Match($persistenceService, '(?s)public static TelemetryOwnerEvent Create\(.*?\)').Value
    if ($factoryCreateSig -match 'string\?\s+eventAreaId') {
        $issues += 'T149 FAIL: Event factory eventAreaId parameter is still nullable.'
    }
    $t145 = Get-Content -LiteralPath (Join-Path $repoRoot 'tests\Integration\Telemetry\TelemetryIngestionRepositoryTests.cs') -Raw
    if ($t145 -notmatch 'Rejected fixture preserves pre-existing Accepted state') {
        $issues += 'T149 FAIL: T145 missing pre-existing state proof for Rejected fixture.'
    }
    if ($t145 -notmatch 'Rejected fixture with multiple rejection codes') {
        $issues += 'T149 FAIL: T145 missing Rejected fixture matrix with multiple codes.'
    }
    if ($t145 -notmatch 'direct fixture conflict probe rejects different terminal') {
        $issues += 'T149 FAIL: T145 missing direct fixture conflict probe test.'
    }
    if ($t145 -notmatch 'direct slot conflict probe rejects different Terminal') {
        $issues += 'T149 FAIL: T145 missing direct slot conflict probe test.'
    }
    $t135Path = Join-Path $repoRoot 'tests\Unit\Telemetry\TelemetryEventTests.cs'
    $t135 = Get-Content -LiteralPath $t135Path -Raw
    if ($t135 -notmatch 'factory rejects blank eventSiteId') {
        $issues += 'T149 FAIL: T135 missing factory blank eventSiteId test.'
    }
    if ($t135 -notmatch 'factory rejects blank eventAreaId') {
        $issues += 'T149 FAIL: T135 missing factory blank eventAreaId test.'
    }
    if ($t135 -notmatch 'factory rejects mismatched trusted scope') {
        $issues += 'T149 FAIL: T135 missing factory mismatched trusted site test.'
    }
    if ($t135 -notmatch 'factory rejects mismatched trusted area') {
        $issues += 'T149 FAIL: T135 missing factory mismatched trusted area test.'
    }

    $migration8Path = Join-Path $repoRoot 'database\migrations\0008_telemetry_measurement.sql'
    $migration8 = Get-Content -LiteralPath $migration8Path -Raw
    foreach ($required in @(
        'telemetry.measurement_identity','telemetry.measurement_raw',
        'octet_length(request_fingerprint) = 32',
        'UNIQUE (simulator_run_id, point_id, source_sequence)',
        'trg_measurement_identity_immutable','trg_measurement_raw_immutable',
        'Accepted terminal result requires exactly one raw Measurement',
        'Rejected terminal result cannot have a raw Measurement',
        'Accepted terminal and raw Measurement provenance must match'
    )) {
        if ($migration8 -notmatch [regex]::Escape($required)) {
            $issues += "T149 FAIL: migration 0008 is missing required invariant $required."
        }
    }
    if ($migration8 -match '(?i)REFERENCES\s+(organization|catalog|acquisition|integration)\.' -or
        $migration8 -match '(?i)CREATE\s+EXTENSION|point_latest|point_source_status') {
        $issues += 'T149 FAIL: migration 0008 has a cross-schema FK, extension, or Phase 8 table.'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $ModuleRoot 'Telemetry\Infrastructure\PostgresTelemetryRepositories.cs'))) {
        $issues += 'T149 FAIL: resolved local Npgsql capability requires the PostgreSQL Telemetry adapter.'
    }
    foreach ($compositionRoot in @(
        (Join-Path $repoRoot 'src\Api\Program.cs'),
        (Join-Path $repoRoot 'src\Worker\Program.cs')
    )) {
        $composition = Get-Content -LiteralPath $compositionRoot -Raw
        if ($composition -notmatch 'AddIumpPostgresModules') {
            $issues += "T149 FAIL: PostgreSQL module registration is missing from $compositionRoot."
        }
    }

    # --- T167 Phase 8 Latest/Source Health/Operations boundary checks ---
    $latestServicePath = Join-Path $ModuleRoot 'Telemetry\Application\PointLatestService.cs'
    $healthServicePath = Join-Path $ModuleRoot 'Telemetry\Application\SourceHealthService.cs'
    $healthJobsPath = Join-Path $ModuleRoot 'Operations\Application\SourceHealthJobs.cs'
    $projectionPath = Join-Path $ModuleRoot 'Telemetry\Contracts\TelemetryProjectionContracts.cs'
    $durableJobContractsPath = Join-Path $ModuleRoot 'Operations\Contracts\DurableJobContracts.cs'
    $jobClaimContractsPath = Join-Path $ModuleRoot 'Operations\Contracts\JobClaimContracts.cs'
    $migration9Path = Join-Path $repoRoot 'database\migrations\0009_telemetry_latest_status.sql'
    foreach ($requiredPath in @($latestServicePath, $healthServicePath, $healthJobsPath,
        $projectionPath, $durableJobContractsPath, $jobClaimContractsPath, $migration9Path)) {
        if (-not (Test-Path -LiteralPath $requiredPath)) {
            $issues += "T167 FAIL: missing Phase 8 artifact $requiredPath."
        }
    }
    if (Test-Path -LiteralPath $latestServicePath) {
        $latest = Get-Content -LiteralPath $latestServicePath -Raw
        if ($latest -notmatch 'MeasurementQuality' -or $latest -notmatch 'IsEligible' -or
            $latest -notmatch 'LatestOrdering\.Compare' -or
            $latest -notmatch 'IPointLatestProjectionRepository' -or
            $latest -notmatch 'StageAdvancedEventAsync') {
            $issues += 'T167 FAIL: Latest service is missing eligibility, ordering, CAS, or event seam.'
        }
        if ($latest -match 'BeginRepeatableRead|BeginAsync\(') {
            $issues += 'T167 FAIL: Latest service must not open an independent transaction.'
        }
    }
    if (Test-Path -LiteralPath $healthServicePath) {
        $health = Get-Content -LiteralPath $healthServicePath -Raw
        if ($health -notmatch 'ExpectedIntervalSeconds' -or
            $health -notmatch 'NoDataAfterSeconds' -or
            $health -notmatch 'Decommissioned' -or
            $health -notmatch 'Suspended' -or
            $health -notmatch 'StageChangedEventAsync') {
            $issues += 'T167 FAIL: Source Health thresholds/precedence/event seam is incomplete.'
        }
    }
    if (Test-Path -LiteralPath $healthJobsPath) {
        $jobs = Get-Content -LiteralPath $healthJobsPath -Raw
        if ($jobs -notmatch 'source-health' -or $jobs -notmatch 'ClaimDueAsync' -or
            $jobs -notmatch 'AddSeconds\(30\)' -or $jobs -notmatch 'ListExpiredAsync') {
            $issues += 'T167 FAIL: health job scheduling/claim/lease/reconciliation is incomplete.'
        }
    }
    if (Test-Path -LiteralPath $durableJobContractsPath) {
        $jobContracts = Get-Content -LiteralPath $durableJobContractsPath -Raw
        if ($jobContracts -notmatch 'IDurableJobScheduler' -or
            $jobContracts -notmatch 'JobType' -or $jobContracts -notmatch 'IdempotencyKey' -or
            $jobContracts -match 'DbContext|IQueryable|Npgsql|ConnectionString') {
            $issues += 'T167 FAIL: durable scheduler port is not provider-neutral or identity-safe.'
        }
    }
    if (Test-Path -LiteralPath $jobClaimContractsPath) {
        $claimContracts = Get-Content -LiteralPath $jobClaimContractsPath -Raw
        if ($claimContracts -notmatch 'IJobClaimRepository' -or
            $claimContracts -notmatch 'RenewAsync' -or $claimContracts -notmatch 'RescheduleAsync' -or
            $claimContracts -notmatch 'FailAsync' -or
            $claimContracts -match 'DbContext|IQueryable|Npgsql') {
            $issues += 'T167 FAIL: job claim port is incomplete or provider-specific.'
        }
    }
    if (Test-Path -LiteralPath $migration9Path) {
        $migration9 = Get-Content -LiteralPath $migration9Path -Raw
        if ($migration9 -notmatch 'telemetry\.point_latest' -or
            $migration9 -notmatch 'telemetry\.point_source_status' -or
            $migration9 -match '(?i)REFERENCES\s+(organization|catalog|acquisition|operations|integration|audit)\.' -or
            $migration9 -match '(?i)CREATE\s+TABLE\s+.*(measurement_raw|measurement_identity|job|audit_event)' -or
            $migration9 -match '(?i)NoData.*numeric|numeric.*NoData') {
            $issues += 'T167 FAIL: migration 0009 violates Telemetry ownership or NoData constraints.'
        }
        foreach ($constraint in @('quality_code IN (''Good'', ''Uncertain'')',
            'expected_interval_seconds > 0',
            'no_data_after_seconds > expected_interval_seconds',
            'source_sequence IS NULL OR source_sequence >= 0')) {
            if ($migration9 -notmatch [regex]::Escape($constraint)) {
                $issues += "T167 FAIL: migration 0009 missing static constraint $constraint."
            }
        }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $ModuleRoot 'Operations\Infrastructure\PostgresJobRepositories.cs'))) {
        $issues += 'T167 FAIL: resolved local Npgsql capability requires the PostgreSQL Operations adapter.'
    }
    foreach ($compositionRoot in @(
        (Join-Path $repoRoot 'src\Api\Program.cs'),
        (Join-Path $repoRoot 'src\Worker\Program.cs')
    )) {
        $composition = Get-Content -LiteralPath $compositionRoot -Raw
        if ($composition -notmatch 'AddIumpPostgresModules') {
            $issues += "T167 FAIL: PostgreSQL module registration is missing from $compositionRoot."
        }
    }
    if (Test-Path -LiteralPath (Join-Path $repoRoot 'database\migrations\0001_r0_foundation.sql')) {
        $migration1 = Get-Content -LiteralPath (Join-Path $repoRoot 'database\migrations\0001_r0_foundation.sql') -Raw
        if ($migration1 -notmatch 'operations\.job' -or $migration1 -notmatch 'idempotency_key' -or
            ($migration1 -notmatch 'lease_until' -and $migration1 -notmatch 'lease_expires_at') -or
            $migration1 -notmatch 'attempt_count' -or $migration1 -notmatch 'payload_json') {
            $issues += 'T167 FAIL: existing R0 operations.job source review is incomplete.'
        }
    }
# --- T221 Phase 9 API/Audit/Web seam and ownership checks ---
    $phase9Required = @(
        (Join-Path $ModuleRoot 'Integration\Domain\CommandIdempotency.cs'),
        (Join-Path $ModuleRoot 'Integration\Contracts\CommandFingerprintContracts.cs'),
        (Join-Path $ModuleRoot 'Integration\Contracts\CommandIdempotencyContracts.cs'),
        (Join-Path $ModuleRoot 'Integration\Contracts\DeliveryPersistenceContracts.cs'),
        (Join-Path $repoRoot 'src\Api\Infrastructure\IdempotentCommandExecutor.cs'),
        (Join-Path $repoRoot 'src\Worker\Integration\OutboxDispatcherWorker.cs'),
        (Join-Path $ModuleRoot 'Audit\Application\AuditEventConsumer.cs'),
        (Join-Path $ModuleRoot 'Audit\Application\AuditQueryService.cs'),
        (Join-Path $repoRoot 'src\Worker\Integration\AuditDeliveryHandler.cs'),
        (Join-Path $repoRoot 'src\Api\ConfigurationEndpoints.cs'),
        (Join-Path $repoRoot 'src\Api\SimulatorEndpoints.cs'),
        (Join-Path $repoRoot 'src\Api\TelemetryQueryEndpoints.cs'),
        (Join-Path $repoRoot 'src\Api\AuditEndpoints.cs'),
        (Join-Path $repoRoot 'database\migrations\0010_audit_event.sql'),
        (Join-Path $repoRoot 'database\migrations\0011_r1_infrastructure_expand.sql'),
        (Join-Path $repoRoot 'src\Web\src\app\AppShell.tsx'),
        (Join-Path $repoRoot 'src\Web\src\features\configuration\ConfigurationRoutes.tsx'),
        (Join-Path $repoRoot 'src\Web\src\features\simulator\SimulatorRoute.tsx'),
        (Join-Path $repoRoot 'src\Web\src\features\telemetry\PointCurrentRoute.tsx'),
        (Join-Path $repoRoot 'src\Web\src\features\audit\AuditRoute.tsx'))
    foreach ($requiredPath in $phase9Required) {
        if (-not (Test-Path -LiteralPath $requiredPath)) { $issues += "T221 FAIL: missing Phase 9 seam $requiredPath." }
    }
    $duplicateFingerprintPath = Join-Path $ModuleRoot 'Integration\Application\CommandFingerprintV1.cs'
    if (Test-Path -LiteralPath $duplicateFingerprintPath) {
        $issues += 'T221 FAIL: duplicated CommandFingerprintV1 implementation remains under Integration.Application.'
    }
    foreach ($requiredAdapter in @(
        (Join-Path $ModuleRoot 'Integration\Infrastructure\PostgresIntegrationRepositories.cs'),
        (Join-Path $ModuleRoot 'Audit\Infrastructure\PostgresAuditRepositories.cs'))) {
        if (-not (Test-Path -LiteralPath $requiredAdapter)) {
            $issues += "T221 FAIL: resolved local Npgsql capability requires adapter: $requiredAdapter."
        }
    }
    $phase9Ports = @(
        (Join-Path $ModuleRoot 'Integration\Contracts\CommandIdempotencyContracts.cs'),
        (Join-Path $ModuleRoot 'Integration\Contracts\DeliveryPersistenceContracts.cs'),
        (Join-Path $ModuleRoot 'Audit\Contracts\AuditContracts.cs')) | ForEach-Object { Get-Content -LiteralPath $_ -Raw }
    if (($phase9Ports -join "`n") -match '(?i)Npgsql|DbContext|IQueryable|ConnectionString') {
        $issues += 'T221 FAIL: Phase 9 public ports expose provider-specific persistence types.'
    }
    foreach ($compositionRoot in @(
        (Join-Path $repoRoot 'src\Api\Program.cs'),
        (Join-Path $repoRoot 'src\Worker\Program.cs'))) {
        $composition = Get-Content -LiteralPath $compositionRoot -Raw
        if ($composition -notmatch 'AddIumpPostgresModules') {
            $issues += "T221 FAIL: PostgreSQL module registration is missing from $compositionRoot."
        }
    }
    $migration10 = Get-Content -LiteralPath (Join-Path $repoRoot 'database\migrations\0010_audit_event.sql') -Raw
    if ($migration10 -notmatch 'source_event_id.*UNIQUE' -or $migration10 -match '(?i)REFERENCES\s+(organization|catalog|acquisition|integration)\.') {
        $issues += 'T221 FAIL: Audit migration must be unique by source event and cross-schema-FK free.'
    }
    $migration11 = Get-Content -LiteralPath (Join-Path $repoRoot 'database\migrations\0011_r1_infrastructure_expand.sql') -Raw
    if ($migration11 -notmatch 'integration\.command_idempotency' -or $migration11 -notmatch 'ALTER TABLE integration\.inbox_message' -or
        $migration11 -match '(?i)CREATE\s+TABLE\s+.*(outbox_event|inbox_message|job)') {
        $issues += 'T221 FAIL: 0011 must be additive and must not recreate R0 delivery/job tables.'
    }
    $webApp = Get-Content -LiteralPath (Join-Path $repoRoot 'src\Web\src\App.tsx') -Raw
    $webCss = Get-Content -LiteralPath (Join-Path $repoRoot 'src\Web\src\App.css') -Raw
    if ($webApp -notmatch 'AppShell' -or $webCss -notmatch 'focus-visible' -or $webCss -notmatch 'prefers-reduced-motion') {
        $issues += 'T221 FAIL: Web shell/accessibility seam is incomplete.'
    }

    # Phase 9 functional closure: these checks intentionally detect the previous placeholder
    # implementation even when its source still compiled.
    $configurationEndpoint = Get-Content -LiteralPath (Join-Path $repoRoot 'src\Api\ConfigurationEndpoints.cs') -Raw
    $simulatorEndpoint = Get-Content -LiteralPath (Join-Path $repoRoot 'src\Api\SimulatorEndpoints.cs') -Raw
    $executorSource = Get-Content -LiteralPath (Join-Path $repoRoot 'src\Api\Infrastructure\IdempotentCommandExecutor.cs') -Raw
    if ($configurationEndpoint -match 'X-Caller-Id' -or $simulatorEndpoint -match 'X-Caller-Id') {
        $issues += 'T221 FAIL: API trusts X-Caller-Id instead of the server principal.'
    }
    if ($executorSource -match 'Encoding\.UTF8\.GetBytes\(key' -or $configurationEndpoint -match 'SHA256\.HashData.*key') {
        $issues += 'T221 FAIL: API fingerprints Idempotency-Key instead of the canonical business request.'
    }
    $dispatcherSource = Get-Content -LiteralPath (Join-Path $repoRoot 'src\Worker\Integration\OutboxDispatcherWorker.cs') -Raw
    if ($dispatcherSource -match 'AddMilliseconds\(250\)' -and $dispatcherSource -notmatch 'RetrySchedule') {
        $issues += 'T221 FAIL: dispatcher uses a fixed 250ms retry rather than the 250ms/1s/2s/5s/30s schedule.'
    }
    # Published is allowed only after every required inbox is Completed.
    if ($dispatcherSource -notmatch 'RequiredConsumersFor' -or $dispatcherSource -notmatch 'inbox' -or $dispatcherSource -notmatch 'MarkPublishedAsync' -or $dispatcherSource -notmatch 'Published') {
        $issues += 'T221 FAIL: dispatcher does not claim per-consumer inbox rows and publish only after completion.'
    }
    $auditDelivery = Get-Content -LiteralPath (Join-Path $repoRoot 'src\Worker\Integration\AuditDeliveryHandler.cs') -Raw
    if ($auditDelivery -notmatch 'IHostTransaction' -or $auditDelivery -notmatch 'CommitAsync' -or $auditDelivery -notmatch 'RollbackAsync') {
        $issues += 'T221 FAIL: Audit append and inbox completion lack one host transaction.'
    }
    $webSources = @(
        (Join-Path $repoRoot 'src\Web\src\app\AppShell.tsx'),
        (Join-Path $repoRoot 'src\Web\src\features\configuration\ConfigurationRoutes.tsx'),
        (Join-Path $repoRoot 'src\Web\src\features\simulator\SimulatorRoute.tsx'),
        (Join-Path $repoRoot 'src\Web\src\gateways\webGateways.ts')) | ForEach-Object { Get-Content -LiteralPath $_ -Raw }
    if (($webSources -join "`n") -match 'POC Site scope|GeneratedCount|setAuthenticated' -or
        ($webSources -join "`n") -notmatch 'GatewayState|useWebGateways|loading|forbidden|expired') {
        $issues += 'T221 FAIL: Web screens use component-local hardcoded data or lack gateway behavior states.'
    }
    $webTestSource = Get-Content -LiteralPath (Join-Path $repoRoot 'src\Web\src\test\app-shell.test.tsx') -Raw
    if ($webTestSource -notmatch 'loading' -or $webTestSource -notmatch 'forbidden' -or $webTestSource -notmatch 'expired') {
        $issues += 'T211 FAIL: web behavior matrix does not cover loading/forbidden/expired.'
    }

    # Final Phase 9 contract-alignment closure checks.
    if ($configurationEndpoint -match 'executor\.ExecuteAsync\(' -or $simulatorEndpoint -match 'executor\.ExecuteAsync\(') {
        $issues += 'T221 FAIL: mutation endpoint still uses plain ExecuteAsync instead of the transactional executor.'
    }
    foreach ($route in @('/sites','/areas','/assets','/points','/metrics','/units','/data-sources','/source-point-mappings','/simulator-configurations','/simulator-configurations/validate','/points/{pointId:guid}/activate','/points/{pointId:guid}/deactivate','/sites/{siteId:guid}/activate','/areas/{areaId:guid}/activate','/assets/{assetId:guid}/activate','/source-point-mappings/{mappingId:guid}/supersede')) {
        if ($configurationEndpoint -notmatch [regex]::Escape($route)) { $issues += "T221 FAIL: missing configuration route $route." }
    }
    $phase9TestFiles = @(
        'tests\Unit\Integration\CommandFingerprintTests.cs','tests\Unit\Integration\CommandIdempotencyDomainTests.cs',
        'tests\Unit\Integration\IdempotentCommandExecutorTests.cs','tests\Unit\Integration\DeliveryRepositoryContractTests.cs',
        'tests\Unit\Worker\OutboxDispatcherTests.cs','tests\Unit\Audit\AuditConsumerTests.cs',
        'tests\Unit\Audit\AuditQueryTests.cs','tests\Unit\Operations\AuditDeliveryJobsTests.cs',
        'tests\Unit\Api\ConfigurationEndpointTests.cs','tests\Unit\Api\SimulatorEndpointTests.cs',
        'tests\Unit\Api\TelemetryQueryEndpointTests.cs','tests\Unit\Api\AuditEndpointTests.cs') |
        ForEach-Object { Join-Path $repoRoot $_ }
    foreach ($testFile in $phase9TestFiles) {
        if ((Get-Content -LiteralPath $testFile -Raw) -match 'public\s+const\s+int\s+(TestCount|AssertionCount)') {
            $issues += "T221 FAIL: Phase 9 measured evidence may not be declared as constants: $testFile."
        }
    }
    $t178 = Get-Content -LiteralPath (Join-Path $repoRoot 'tests\Unit\Api\ConfigurationEndpointTests.cs') -Raw
    $t179 = Get-Content -LiteralPath (Join-Path $repoRoot 'tests\Unit\Api\SimulatorEndpointTests.cs') -Raw
    $t180 = Get-Content -LiteralPath (Join-Path $repoRoot 'tests\Unit\Api\TelemetryQueryEndpointTests.cs') -Raw
    $t181 = Get-Content -LiteralPath (Join-Path $repoRoot 'tests\Unit\Api\AuditEndpointTests.cs') -Raw
    if ($t178 -notmatch 'CreateSiteAsync|FakeConfigurationPorts|ExecuteTransactionalAsync' -or
        $t179 -notmatch 'SimulatorEndpoints\.ExecuteAsync|FakeSimulatorPorts|ExecuteTransactionalAsync' -or
        $t180 -notmatch 'TelemetryQueryEndpoints\.(LatestAsync|HealthAsync|CurrentAsync)|FakeTelemetryPorts' -or
        $t181 -notmatch 'AuditEndpoints\.QueryAsync|FakeAuditQueryPort') {
        $issues += 'T221 FAIL: T178-T181 must invoke real endpoint delegates with fake ports and principals.'
    }
    if ($dispatcherSource -match 'inbox\s+is\s+null\)\s*continue') {
        $issues += 'T221 FAIL: a null inbox claim must not be treated as completed delivery.'
    }
    if ($auditDelivery -notmatch 'ITransactionalInboxRepository' -or $auditDelivery -notmatch 'ITransactionalAuditEventConsumer' -or
        $auditDelivery -notmatch 'transaction') {
        $issues += 'T221 FAIL: audit delivery must pass the host transaction to audit and inbox writes.'
    }
    $auditContracts = Get-Content -LiteralPath (Join-Path $ModuleRoot 'Audit\Contracts\AuditContracts.cs') -Raw
    $auditConsumerSource = Get-Content -LiteralPath (Join-Path $ModuleRoot 'Audit\Application\AuditEventConsumer.cs') -Raw
    foreach ($hashField in @('sourceEventId','eventType','schemaVersion','producer','occurredAtUtc','correlationId','causationId','before','after')) {
        if ($auditContracts -notmatch [regex]::Escape($hashField)) { $issues += "T221 FAIL: audit payload hash omits $hashField." }
    }
    if ($auditConsumerSource -notmatch 'AUDIT_SOURCE_HASH_CONFLICT') {
        $issues += 'T221 FAIL: audit source hash conflict code is not stable.'
    }
    $auditQuery = Get-Content -LiteralPath (Join-Path $ModuleRoot 'Audit\Application\AuditQueryService.cs') -Raw
    if ($auditQuery -notmatch 'AuditKeysetCursor' -or $auditQuery -notmatch 'OccurredAtUtc' -or $auditQuery -notmatch 'AuditEventId') {
        $issues += 'T221 FAIL: audit query must use the strict OccurredAtUtc/AuditEventId keyset tuple.'
    }
    $jobsSource = Get-Content -LiteralPath (Join-Path $ModuleRoot 'Operations\Application\AuditDeliveryJobs.cs') -Raw
    if ($jobsSource -match 'AuditDeliveryJobs\(object' -or $jobsSource -notmatch 'ClaimDueAsync' -or $jobsSource -notmatch 'ListExpiredAsync' -or
        $jobsSource -notmatch 'ReplayAsync') {
        $issues += 'T221 FAIL: operations reconciliation must use real job contracts and operator replay.'
    }
    if ($migration11 -notmatch 'prevent_completed_command_mutation' -or $migration11 -notmatch 'COMMAND_COMPLETED_IMMUTABLE' -or
        $migration11 -notmatch "status = 'Pending'" -or $migration11 -notmatch "status = 'Completed'" -or
        $migration11 -match "status IN \('Processing', 'Pending'\)") {
        $issues += 'T221 FAIL: 0011 command Pending/Completed constraints or R0 inbox vocabulary are inaccurate.'
    }
    $webGateway = Get-Content -LiteralPath (Join-Path $repoRoot 'src\Web\src\gateways\webGateways.ts') -Raw
    foreach ($forbiddenRoute in @('/auth/session','/simulators/current','/points/current/latest')) {
        if ($webGateway -match [regex]::Escape($forbiddenRoute)) { $issues += "T221 FAIL: web gateway still uses forbidden route $forbiddenRoute." }
    }
    if ($webGateway -match "state:\s*'ready'[^}]*?(?:areaCount|pointCount|siteCount):\s*0") {
        $issues += 'T221 FAIL: configuration summary may not hardcode zero counts.'
    }
    if ($webTestSource -notmatch 'createFakeWebGateways' -or $webTestSource -notmatch 'transitionAppShell' -or
        $webTestSource -notmatch 'runAppShellBehaviorScenarios') {
        $issues += 'T211 FAIL: web behavior evidence must execute fake gateway state transitions.'
    }

    # Phase 9 closure: detect remaining placeholder patterns
    $appShellSource = Get-Content -LiteralPath (Join-Path $repoRoot 'src\Web\src\app\AppShell.tsx') -Raw
    if ($appShellSource -notmatch 'type="text"' -or $appShellSource -notmatch 'type="password"' -or
        $appShellSource -notmatch 'auth\.signIn\(\{\s*username,\s*password\s*\}\)' -or
        $webGateway -match "signIn:\s*async\s*\(credentials\s*=\s*\{\s*username:\s*''") {
        $issues += 'T221 FAIL: AppShell signIn must accept username/password credentials.'
    }
    if ($configurationEndpoint -notmatch 'RequiresIfMatch\(request\.Method,\s*operationCode\)' -or
        $configurationEndpoint -notmatch 'TryReadExpectedVersion' -or
        $configurationEndpoint -notmatch 'A valid If-Match is required') {
        $issues += 'T221 FAIL: generic configuration update/delete handlers must reject missing or malformed If-Match.'
    }
    foreach ($routeTarget in @('sourceId','mappingId','configurationId')) {
        if ($configurationEndpoint -notmatch [regex]::Escape("`"$routeTarget`"") -or
            $configurationEndpoint -notmatch 'ResolveRouteTarget\(request\)') {
            $issues += "T221 FAIL: configuration route target $routeTarget may be lost before command execution."
        }
    }
    $configRoutes = Get-Content -LiteralPath (Join-Path $repoRoot 'src\Web\src\features\configuration\ConfigurationRoutes.tsx') -Raw
    if ($configRoutes -match 'Catalog gateway ready|Source gateway ready|Mapping gateway ready|Activation state supplied by server') {
        $issues += 'T221 FAIL: ConfigurationRoutes must not contain fixed placeholder strings.'
    }
    $auditRoute = Get-Content -LiteralPath (Join-Path $repoRoot 'src\Web\src\features\audit\AuditRoute.tsx') -Raw
    if ($auditRoute -notmatch 'record\.before' -or $auditRoute -notmatch 'record\.after') {
        $issues += 'T221 FAIL: AuditRoute must display Before/After fields.'
    }
    $fakeAuditRepos = Get-Content -LiteralPath (Join-Path $repoRoot 'tests\Unit\Fakes\FakeAuditRepositories.cs') -Raw
    if ($fakeAuditRepos -match '=> AppendIfAbsentAsync\(record, ct\)') {
        $issues += 'T221 FAIL: FakeAuditAppendRepository transactional overload must use staging not immediate write.'
    }
    $fakeInboxRepos = Get-Content -LiteralPath (Join-Path $repoRoot 'tests\Unit\Fakes\FakeIntegrationDeliveryRepositories.cs') -Raw
    if ($fakeInboxRepos -match '=> CompleteAsync\(record, ct\)') {
        $issues += 'T221 FAIL: FakeIntegrationDeliveryRepositories transactional overload must use staging not immediate write.'
    }
    if ($webTestSource -notmatch "from\s+'../app/AppShell'" -or $webTestSource -notmatch '\bAppShell\b') {
        $issues += 'T221 FAIL: T211 app-shell test must import AppShell for scenario coverage.'
    }
    $migration11Path = Join-Path $repoRoot 'database\migrations\0011_r1_infrastructure_expand.sql'
    $migration11 = Get-Content -LiteralPath $migration11Path -Raw
    if ($migration11 -notmatch "length\(btrim\(pending_owner\)\)\s*>\s*0" -or
        $migration11 -notmatch "pending_until\s+IS\s+NOT\s+NULL" -or
        $migration11 -notmatch "pending_until\s*<=\s*created_at\s*\+\s*INTERVAL\s+'24 hours'") {
        $issues += 'T221 FAIL: 0011 command Pending lease must require a nonblank owner and a bounded nonnull pending_until.'
    }
}

if ($issues.Count -gt 0) {
    foreach ($issue in $issues) {
        Write-Error $issue
    }
    throw "Architecture boundary contract checks failed: $($issues.Count) issue(s)."
}

Write-Output 'PASS: architecture boundary contract'
