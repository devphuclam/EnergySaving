using IUMP.Api.Infrastructure;
using IUMP.Modules.Acquisition.Application;
using IUMP.Modules.Acquisition.Contracts;
using IUMP.Modules.Catalog.Application;
using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.Catalog.Domain;
using IUMP.Modules.Organization.Application;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;
using IUMP.Tests.Unit.Fakes;
using IUMP.Tests.Unit.Organization;

namespace IUMP.Tests.Unit.OperationalWorkspace;

/// T037: duplicate-to-Draft and exclusion red tests for every eligible configuration
/// entity type. These tests are written against the Phase 2 public seams before green
/// implementation; they do not compile until the contracts exist.
public static class ConfigurationDuplicationTests
{
    private static int _assertionCount;
    private static int _testCount;

    public static int TestCount => _testCount;
    public static int AssertionCount => _assertionCount;

    public static List<string> Run()
    {
        var failures = new List<string>();
        _assertionCount = 0;
        _testCount = 0;

        RunAsync(failures).GetAwaiter().GetResult();
        Console.WriteLine(
            $"T037: cases={_testCount}; assertions={_assertionCount}; failures={failures.Count}");
        return failures;
    }

    private static async Task RunAsync(List<string> failures)
    {
        await ScenarioAsync(DuplicateSiteProducesUniqueDraftAsync, failures);
        await ScenarioAsync(DuplicateSiteCodeCollisionProducesUniqueSuffixAsync, failures);
        await ScenarioAsync(DuplicateSiteUnknownAndForbiddenAsync, failures);
        await ScenarioAsync(DuplicateAreaPreservesSiteReviewRelationshipAsync, failures);
        await ScenarioAsync(DuplicateAssetPreservesParentReviewRelationshipsAsync, failures);
        await ScenarioAsync(DuplicatePointPreservesConfigurationAndParentsAsync, failures);
        await ScenarioAsync(DuplicatePointNeverCopiesLifecycleHistoryAsync, failures);
        await ScenarioAsync(DuplicateSourceProducesDraftWithoutMappingsAsync, failures);
        await ScenarioAsync(DuplicateMappingProducesDraftWithReviewRelationshipsAsync, failures);
        await ScenarioAsync(DuplicateOutcomesNeverExposeHistoryOrSecretsAsync, failures);
        await ScenarioAsync(EditCreatesDraftVersionWithoutChangingCurrentAsync, failures);
        await ScenarioAsync(ActivateVersionPromotesDraftAndKeepsHistoryAsync, failures);
        await ScenarioAsync(ActivateVersionRejectsStaleOrUnknownDraftAsync, failures);
        await ScenarioAsync(DuplicateConfigurationProducesNewHeadAsync, failures);
        await ScenarioAsync(SimulatorManagementSearchMatchesSafeIdentifiersAsync, failures);
    }

    private static async Task ScenarioAsync(Func<List<string>, Task> scenario, List<string> failures)
    {
        _testCount++;
        await scenario(failures);
    }

    private static async Task DuplicateSiteProducesUniqueDraftAsync(List<string> failures)
    {
        var repo = new FakeOrganizationCommandRepository();
        var source = new Site(SiteId.New(), "S-T037", "Original Site", "original", "Asia/Ho_Chi_Minh",
            SiteStatus.Active, 5);
        await repo.AddSiteAsync(source);
        var service = new OrganizationDuplicationService(repo, new FakeOrganizationAuthorization(AdminCaller()));

        var outcome = await service.DuplicateSiteAsync(source.Id, "admin-user");

        AssertT037(outcome.IsSuccess, failures, "Site duplicate succeeds.");
        AssertT037(outcome.NewId is not null && outcome.NewId != source.Id.Value,
            failures, "Site duplicate receives a new identity.");
        AssertT037(outcome.ProposedCode == "S-T037-COPY", failures, "Site duplicate receives a unique proposed code.");
        AssertT037(outcome.ProposedName == "Original Site", failures, "Site duplicate keeps the source name.");
        AssertT037(outcome.Status == "Draft" && outcome.Version == 1, failures,
            "Site duplicate is a Draft at version 1 and never copies the Active status or optimistic version.");
        var copy = await repo.GetSiteAsync(new SiteId(outcome.NewId!.Value));
        AssertT037(copy is not null && copy.Timezone == "Asia/Ho_Chi_Minh" && copy.Description == "original",
            failures, "Site duplicate keeps non-behavioral metadata fields.");
        AssertT037(service.Events.Count == 1 && service.Events[0].Action == "Duplicated",
            failures, "Site duplicate emits exactly one duplication event.");
    }

    private static async Task DuplicateSiteCodeCollisionProducesUniqueSuffixAsync(List<string> failures)
    {
        var repo = new FakeOrganizationCommandRepository();
        var source = new Site(SiteId.New(), "S-COLLIDE", "Original", null, "UTC", SiteStatus.Active, 1);
        var collision = new Site(SiteId.New(), "S-COLLIDE-COPY", "Existing copy", null, "UTC", SiteStatus.Draft, 1);
        await repo.AddSiteAsync(source);
        await repo.AddSiteAsync(collision);
        var service = new OrganizationDuplicationService(repo, new FakeOrganizationAuthorization(AdminCaller()));

        var outcome = await service.DuplicateSiteAsync(source.Id, "admin-user");

        AssertT037(outcome.IsSuccess && outcome.ProposedCode == "S-COLLIDE-COPY2",
            failures, "Site duplicate suffix increments until the proposed code is unique.");
    }

    private static async Task DuplicateSiteUnknownAndForbiddenAsync(List<string> failures)
    {
        var repo = new FakeOrganizationCommandRepository();
        var service = new OrganizationDuplicationService(repo, new FakeOrganizationAuthorization(AdminCaller()));
        var missing = await service.DuplicateSiteAsync(SiteId.New(), "admin-user");
        AssertT037(!missing.IsSuccess && missing.Code == "NotFound",
            failures, "Duplicating an unknown Site is a NotFound, never a created entity.");

        var site = new Site(SiteId.New(), "S-OTHER", "Other Site", null, "UTC", SiteStatus.Active, 1);
        await repo.AddSiteAsync(site);
        var scopedService = new OrganizationDuplicationService(
            repo, new FakeOrganizationAuthorization(NoScopeEngineer()));
        var denied = await scopedService.DuplicateSiteAsync(site.Id, "eng-user");
        AssertT037(!denied.IsSuccess && denied.Code is "NotFound" or "Forbidden",
            failures, "Duplicating a Site outside the caller scope fails closed.");
    }

    private static async Task DuplicateAreaPreservesSiteReviewRelationshipAsync(List<string> failures)
    {
        var repo = new FakeOrganizationCommandRepository();
        var site = new Site(SiteId.New(), "S-AREA", "Site", null, "UTC", SiteStatus.Active, 1);
        await repo.AddSiteAsync(site);
        var source = new Area(AreaId.New(), site.Id, "A-037", "Area 037", "area", AreaStatus.Active, 4);
        await repo.AddAreaAsync(source);
        var service = new OrganizationDuplicationService(repo, new FakeOrganizationAuthorization(AdminCaller()));

        var outcome = await service.DuplicateAreaAsync(source.Id, "admin-user");

        AssertT037(outcome.IsSuccess, failures, "Area duplicate succeeds.");
        AssertT037(outcome.NewId is not null && outcome.NewId != source.Id.Value,
            failures, "Area duplicate receives a new identity.");
        AssertT037(outcome.ProposedCode == "A-037-COPY" && outcome.Status == "Draft" && outcome.Version == 1,
            failures, "Area duplicate is a unique Draft at version 1.");
        AssertT037(outcome.ReviewRelationships.Contains($"site:{site.Id.Value:D}"),
            failures, "Area duplicate carries the parent Site as a reviewable relationship.");
        var copy = await repo.GetAreaAsync(new AreaId(outcome.NewId!.Value));
        AssertT037(copy is not null && copy.SiteId == site.Id && copy.Name == "Area 037",
            failures, "Area duplicate keeps the parent Site and metadata.");
    }

    private static async Task DuplicateAssetPreservesParentReviewRelationshipsAsync(List<string> failures)
    {
        var repo = new FakeOrganizationCommandRepository();
        var site = new Site(SiteId.New(), "S-ASSET", "Site", null, "UTC", SiteStatus.Active, 1);
        var area = new Area(AreaId.New(), site.Id, "A-ASSET", "Area", null, AreaStatus.Active, 1);
        await repo.AddSiteAsync(site);
        await repo.AddAreaAsync(area);
        var source = new Asset(AssetId.New(), site.Id, area.Id, "AS-037", "Asset 037", "asset",
            AssetStatus.Decommissioned, 9);
        await repo.AddAssetAsync(source);
        var service = new OrganizationDuplicationService(repo, new FakeOrganizationAuthorization(AdminCaller()));

        var outcome = await service.DuplicateAssetAsync(source.Id, "admin-user");

        AssertT037(outcome.IsSuccess, failures, "Asset duplicate succeeds.");
        AssertT037(outcome.NewId is not null && outcome.NewId != source.Id.Value &&
            outcome.Status == "Draft" && outcome.Version == 1,
            failures, "Asset duplicate is a unique Draft at version 1 with a new identity.");
        AssertT037(outcome.ReviewRelationships.Contains($"site:{site.Id.Value:D}") &&
            outcome.ReviewRelationships.Contains($"area:{area.Id.Value:D}"),
            failures, "Asset duplicate carries Site and Area as reviewable relationships.");
        var copy = await repo.GetAssetAsync(new AssetId(outcome.NewId!.Value));
        AssertT037(copy is not null && copy.SiteId == site.Id && copy.AreaId == area.Id &&
            copy.Name == "Asset 037",
            failures, "Asset duplicate keeps parent references and metadata.");
    }

    private static async Task DuplicatePointPreservesConfigurationAndParentsAsync(List<string> failures)
    {
        var repo = new FakeOrganizationCommandRepository();
        var site = new Site(SiteId.New(), "S-PT", "Site", null, "UTC", SiteStatus.Active, 1);
        var area = new Area(AreaId.New(), site.Id, "A-PT", "Area", null, AreaStatus.Active, 1);
        var asset = new Asset(AssetId.New(), site.Id, area.Id, "AS-PT", "Asset", null, AssetStatus.Active, 1);
        await repo.AddSiteAsync(site);
        await repo.AddAreaAsync(area);
        await repo.AddAssetAsync(asset);
        var source = new MeasurementPoint(PointId.New(), site.Id, area.Id, asset.Id,
            "P-037", "point desc", "metric-el", "unit-kwh", "owner-1", 60, 300, PointStatus.Active, 7);
        await repo.AddPointAsync(source);
        await repo.AddLifecycleEntryAsync(new PointLifecycleEntry(
            Guid.NewGuid().ToString(), source.Id.ToString(), 7, PointStatus.Draft, PointStatus.Active,
            "admin-user", "admin.user", "activated", DateTime.UtcNow, "corr", "caus"));
        var service = new OrganizationDuplicationService(repo, new FakeOrganizationAuthorization(AdminCaller()));

        var outcome = await service.DuplicatePointAsync(source.Id, "admin-user");

        AssertT037(outcome.IsSuccess, failures, "Point duplicate succeeds.");
        AssertT037(outcome.NewId is not null && outcome.NewId != source.Id.Value &&
            outcome.Status == "Draft" && outcome.Version == 1,
            failures, "Point duplicate is a unique Draft at version 1 with a new identity.");
        AssertT037(outcome.ReviewRelationships.Contains($"site:{site.Id.Value:D}") &&
            outcome.ReviewRelationships.Contains($"area:{area.Id.Value:D}") &&
            outcome.ReviewRelationships.Contains($"asset:{asset.Id.Value:D}") &&
            outcome.ReviewRelationships.Contains("metric:metric-el") &&
            outcome.ReviewRelationships.Contains("unit:unit-kwh"),
            failures, "Point duplicate carries parents, metric, and unit as reviewable relationships.");
        var copy = await repo.GetPointAsync(new PointId(outcome.NewId!.Value));
        AssertT037(copy is not null && copy.MetricId == "metric-el" && copy.UnitId == "unit-kwh" &&
            copy.ExpectedIntervalSeconds == 60 && copy.NoDataAfterSeconds == 300 &&
            copy.DataOwnerUserId == "owner-1" && copy.Description == "point desc",
            failures, "Point duplicate keeps configuration fields as Draft content.");
    }

    private static async Task DuplicatePointNeverCopiesLifecycleHistoryAsync(List<string> failures)
    {
        var repo = new FakeOrganizationCommandRepository();
        var site = new Site(SiteId.New(), "S-HIST", "Site", null, "UTC", SiteStatus.Active, 1);
        var area = new Area(AreaId.New(), site.Id, "A-HIST", "Area", null, AreaStatus.Active, 1);
        var asset = new Asset(AssetId.New(), site.Id, area.Id, "AS-HIST", "Asset", null, AssetStatus.Active, 1);
        await repo.AddSiteAsync(site);
        await repo.AddAreaAsync(area);
        await repo.AddAssetAsync(asset);
        var source = new MeasurementPoint(PointId.New(), site.Id, area.Id, asset.Id,
            "P-HIST", null, "metric-el", "unit-kwh", "owner-1", 60, 300, PointStatus.Active, 7);
        await repo.AddPointAsync(source);
        await repo.AddLifecycleEntryAsync(new PointLifecycleEntry(
            Guid.NewGuid().ToString(), source.Id.ToString(), 7, PointStatus.Draft, PointStatus.Active,
            "admin-user", "admin.user", "activated", DateTime.UtcNow, "corr", "caus"));
        var service = new OrganizationDuplicationService(repo, new FakeOrganizationAuthorization(AdminCaller()));

        var outcome = await service.DuplicatePointAsync(source.Id, "admin-user");

        var copyHistory = await repo.GetLifecycleForPointAsync(outcome.NewId!.Value.ToString("D"));
        AssertT037(copyHistory.Count == 0,
            failures, "Point duplicate never copies lifecycle history rows.");
    }

    private static async Task DuplicateSourceProducesDraftWithoutMappingsAsync(List<string> failures)
    {
        var repo = new FakeCatalogCommandRepository();
        var siteId = Guid.NewGuid();
        var source = new DataSource(DataSourceId.New(), "SRC-037", "Source 037",
            SourceType.Simulator, SourceStatus.Active, 3, siteId);
        await repo.AddDataSourceAsync(source);
        var pointId = Guid.NewGuid().ToString("D");
        await repo.AddMappingAsync(new SourcePointMapping(MappingId.New(), source.Id, pointId,
            MappingStatus.Active, DateTime.UtcNow.AddDays(-1), null, 1));
        var service = new CatalogDuplicationService(repo, new FakeCatalogAuthorization(AdminCaller()));

        var outcome = await service.DuplicateSourceAsync(source.Id, "admin-user");

        AssertT037(outcome.IsSuccess, failures, "Source duplicate succeeds.");
        AssertT037(outcome.NewId is not null && outcome.NewId != source.Id.Value &&
            outcome.Status == "Draft" && outcome.Version == 1,
            failures, "Source duplicate is a unique Draft at version 1 with a new identity.");
        AssertT037(outcome.ProposedCode == "SRC-037-COPY" && outcome.ProposedName == "Source 037",
            failures, "Source duplicate proposes a unique code and keeps the name.");
        AssertT037(outcome.ReviewRelationships.Contains($"site:{siteId:D}"),
            failures, "Source duplicate carries its Site as a reviewable relationship.");
        var copy = await repo.GetDataSourceAsync(new DataSourceId(outcome.NewId!.Value));
        var copyMappings = await repo.GetMappingsForSourceAsync(copy!.Id);
        AssertT037(copy.SiteId == siteId && copy.SourceType == SourceType.Simulator &&
            copyMappings.Count == 0,
            failures, "Source duplicate keeps metadata but never copies operational Mappings.");
    }

    private static async Task DuplicateMappingProducesDraftWithReviewRelationshipsAsync(List<string> failures)
    {
        var repo = new FakeCatalogCommandRepository();
        var source = new DataSource(DataSourceId.New(), "SRC-MAP", "Source", SourceType.Simulator,
            SourceStatus.Active, 2, Guid.NewGuid());
        await repo.AddDataSourceAsync(source);
        var pointId = Guid.NewGuid().ToString("D");
        var effectiveFrom = DateTime.UtcNow.AddDays(-1);
        var mapping = new SourcePointMapping(MappingId.New(), source.Id, pointId, MappingStatus.Active,
            effectiveFrom, null, 4);
        await repo.AddMappingAsync(mapping);
        var service = new CatalogDuplicationService(repo, new FakeCatalogAuthorization(AdminCaller()));

        var outcome = await service.DuplicateMappingAsync(mapping.Id, "admin-user");

        AssertT037(outcome.IsSuccess, failures, "Mapping duplicate succeeds.");
        AssertT037(outcome.NewId is not null && outcome.NewId != mapping.Id.Value &&
            outcome.Status == "Draft" && outcome.Version == 1,
            failures, "Mapping duplicate is a unique Draft at version 1 with a new identity.");
        AssertT037(outcome.ReviewRelationships.Contains($"source:{source.Id.Value:D}") &&
            outcome.ReviewRelationships.Contains($"point:{pointId}"),
            failures, "Mapping duplicate carries Source and Point as reviewable relationships.");
        var copy = await repo.GetMappingAsync(new MappingId(outcome.NewId!.Value));
        AssertT037(copy is not null && copy.DataSourceId == source.Id && copy.PointId == pointId &&
            copy.EffectiveFrom == effectiveFrom && copy.EffectiveTo is null && !copy.IsActive,
            failures, "Mapping duplicate keeps the source/point/effective period but is never Active.");
    }

    private static async Task DuplicateOutcomesNeverExposeHistoryOrSecretsAsync(List<string> failures)
    {
        var orgRepo = new FakeOrganizationCommandRepository();
        var site = new Site(SiteId.New(), "S-SECRET", "Site", null, "UTC", SiteStatus.Active, 1);
        await orgRepo.AddSiteAsync(site);
        var orgService = new OrganizationDuplicationService(orgRepo, new FakeOrganizationAuthorization(AdminCaller()));
        var siteOutcome = await orgService.DuplicateSiteAsync(site.Id, "admin-user");
        AssertT037(siteOutcome.ReviewRelationships is not null && siteOutcome.ReviewRelationships.Count == 0,
            failures, "Root Site duplicate carries no inherited relationship claims.");

        var catalogRepo = new FakeCatalogCommandRepository();
        var source = new DataSource(DataSourceId.New(), "SRC-SECRET", "Source", SourceType.Simulator,
            SourceStatus.Active, 1, Guid.NewGuid());
        await catalogRepo.AddDataSourceAsync(source);
        var pointId = Guid.NewGuid().ToString("D");
        var mapping = new SourcePointMapping(MappingId.New(), source.Id, pointId, MappingStatus.Active,
            DateTime.UtcNow.AddDays(-1), null, 1);
        await catalogRepo.AddMappingAsync(mapping);
        var catalogService = new CatalogDuplicationService(catalogRepo, new FakeCatalogAuthorization(AdminCaller()));
        _ = await catalogService.DuplicateSourceAsync(source.Id, "admin-user");
        var mappingOutcome = await catalogService.DuplicateMappingAsync(mapping.Id, "admin-user");
        AssertT037(mappingOutcome.Status == "Draft" && mappingOutcome.Version == 1,
            failures, "Mapping duplicate exposes only Draft status and fresh version.");

        var secretFree = orgService.Events.All(eventValue => SnapshotsSecretFree(eventValue.Before) &&
            SnapshotsSecretFree(eventValue.After)) &&
            catalogService.Events.All(eventValue => SnapshotsSecretFree(eventValue.Before) &&
                SnapshotsSecretFree(eventValue.After));
        AssertT037(secretFree,
            failures, "Duplication events never carry password, secret, credential, or session material.");
    }

    private static async Task EditCreatesDraftVersionWithoutChangingCurrentAsync(List<string> failures)
    {
        var acqRepo = new FakeAcquisitionConfigurationRepository();
        var sourceId = Guid.NewGuid();
        var pointId = Guid.NewGuid();
        var (adapter, _, callers) = CreateAdapterChain(sourceId, pointId);
        var service = new SimulatorConfigurationService(acqRepo, callers, adapter);

        var create = await service.CreateAsync(Create("admin", sourceId, 42));
        var head = await acqRepo.GetBySourceIdAsync(sourceId);

        var edit = await service.EditAsync(Edit("admin", head!.ConfigurationId, head.Version, 7));

        AssertT037(create.IsSuccess && edit.IsSuccess, failures, "Edit succeeds after create.");
        var afterEdit = await acqRepo.GetHeadAsync(head.ConfigurationId);
        AssertT037(afterEdit is not null && afterEdit.Version == 2 &&
            afterEdit.CurrentConfigurationVersion == 1,
            failures, "Behavior-changing edit appends a Draft version without promoting Current.");
        var draft = await acqRepo.GetVersionAsync(head.ConfigurationId, 2);
        var historical = await acqRepo.GetVersionAsync(head.ConfigurationId, 1);
        AssertT037(draft is not null && draft.DeterministicSeed == 7 &&
            historical is not null && historical.DeterministicSeed == 42,
            failures, "Draft version exists and the historical version keeps its meaning.");
    }

    private static async Task ActivateVersionPromotesDraftAndKeepsHistoryAsync(List<string> failures)
    {
        var acqRepo = new FakeAcquisitionConfigurationRepository();
        var sourceId = Guid.NewGuid();
        var pointId = Guid.NewGuid();
        var (adapter, _, callers) = CreateAdapterChain(sourceId, pointId);
        var service = new SimulatorConfigurationService(acqRepo, callers, adapter);

        _ = await service.CreateAsync(Create("admin", sourceId, 42));
        var head = await acqRepo.GetBySourceIdAsync(sourceId);
        _ = await service.EditAsync(Edit("admin", head!.ConfigurationId, head.Version, 7));

        var activated = await service.ActivateVersionAsync(
            new SimulatorConfigurationActivateVersionCommand(
                head.ConfigurationId, 2, 2, "admin", "corr-activate", "caus-activate"));

        var after = await acqRepo.GetHeadAsync(head.ConfigurationId);
        var version1 = await acqRepo.GetVersionAsync(head.ConfigurationId, 1);
        var version2 = await acqRepo.GetVersionAsync(head.ConfigurationId, 2);
        AssertT037(activated.IsSuccess, failures, "Draft activation succeeds.");
        AssertT037(after is not null && after.Version == 3 && after.CurrentConfigurationVersion == 2,
            failures, "Activation promotes the Draft version and bumps the aggregate version.");
        AssertT037(version1 is not null && version1.MinimumValue == 1 && version2 is not null &&
            version2.DeterministicSeed == 7,
            failures, "Activation never rewrites historical versions.");
        AssertT037(service.Events.Count == 3 && service.Events[2].Action == "VersionActivated",
            failures, "Activation emits exactly one VersionActivated event.");
    }

    private static async Task ActivateVersionRejectsStaleOrUnknownDraftAsync(List<string> failures)
    {
        var acqRepo = new FakeAcquisitionConfigurationRepository();
        var sourceId = Guid.NewGuid();
        var pointId = Guid.NewGuid();
        var (adapter, _, callers) = CreateAdapterChain(sourceId, pointId);
        var service = new SimulatorConfigurationService(acqRepo, callers, adapter);

        _ = await service.CreateAsync(Create("admin", sourceId, 42));
        var head = await acqRepo.GetBySourceIdAsync(sourceId);
        _ = await service.EditAsync(Edit("admin", head!.ConfigurationId, head.Version, 7));

        var stale = await service.ActivateVersionAsync(
            new SimulatorConfigurationActivateVersionCommand(
                head.ConfigurationId, 1, 2, "admin", "corr-stale", "caus-stale"));
        AssertT037(stale.Code == "VERSION_CONFLICT",
            failures, "Stale head version cannot silently promote a Draft.");

        var unknown = await service.ActivateVersionAsync(
            new SimulatorConfigurationActivateVersionCommand(
                head.ConfigurationId, 2, 99, "admin", "corr-unknown", "caus-unknown"));
        AssertT037(unknown.Code is "NOT_FOUND" or "VALIDATION",
            failures, "Unknown Draft version cannot be activated.");

        var stillDraft = await acqRepo.GetHeadAsync(head.ConfigurationId);
        AssertT037(stillDraft is not null && stillDraft.CurrentConfigurationVersion == 1,
            failures, "Failed activation never mutates the current version.");
    }

    private static async Task DuplicateConfigurationProducesNewHeadAsync(List<string> failures)
    {
        var acqRepo = new FakeAcquisitionConfigurationRepository();
        var sourceId = Guid.NewGuid();
        var targetSourceId = Guid.NewGuid();
        var pointId = Guid.NewGuid();
        var (adapter, catalog, callers) = CreateAdapterChain(sourceId, pointId);
        await catalog.AddDataSourceAsync(new DataSource(new DataSourceId(targetSourceId),
            "SRC-DUP-TARGET", "Duplicate Target", SourceType.Simulator, SourceStatus.Active, 1));
        await catalog.AddMappingAsync(new SourcePointMapping(MappingId.New(),
            new DataSourceId(targetSourceId), pointId.ToString("D"), MappingStatus.Active,
            DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(-10), 1));
        var service = new SimulatorConfigurationService(acqRepo, callers, adapter);

        _ = await service.CreateAsync(Create("admin", sourceId, 42));
        var head = await acqRepo.GetBySourceIdAsync(sourceId);

        var duplicated = await service.DuplicateAsync(
            new SimulatorConfigurationDuplicateCommand(
                head!.ConfigurationId, targetSourceId, "admin", "corr-dup", "caus-dup"));

        AssertT037(duplicated.IsSuccess && duplicated.NewConfigurationId is not null,
            failures, "Configuration duplicate succeeds and returns a new identity.");
        var newId = duplicated.NewConfigurationId!.Value;
        AssertT037(newId != head.ConfigurationId, failures, "Configuration duplicate gets a new identity.");
        var newHead = await acqRepo.GetHeadAsync(newId);
        var newVersion = await acqRepo.GetVersionAsync(newId, 1);
        AssertT037(newHead is not null && newHead.SourceId == targetSourceId &&
            newHead.CurrentConfigurationVersion == 1 && newHead.Version == 1 &&
            newVersion is not null && newVersion.DeterministicSeed == 42,
            failures, "Configuration duplicate is a fresh head at version 1 with the copied behavior as Draft content.");
        AssertT037(service.Events.Count == 2 && service.Events[1].Action == "Duplicated",
            failures, "Configuration duplicate emits exactly one Duplicated event.");
    }

    private static Task SimulatorManagementSearchMatchesSafeIdentifiersAsync(List<string> failures)
    {
        var configurationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var sourceId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        AssertT037(ConfigurationManagementSearch.MatchesSimulatorConfiguration(
                configurationId, sourceId, 7, configurationId.ToString("D")),
            failures, "Simulator management search matches the configuration ID.");
        AssertT037(ConfigurationManagementSearch.MatchesSimulatorConfiguration(
                configurationId, sourceId, 7, sourceId.ToString("D")),
            failures, "Simulator management search matches the Source ID.");
        AssertT037(ConfigurationManagementSearch.MatchesSimulatorConfiguration(
                configurationId, sourceId, 7, "7"),
            failures, "Simulator management search matches the current version.");
        AssertT037(!ConfigurationManagementSearch.MatchesSimulatorConfiguration(
                configurationId, sourceId, 7, "999"),
            failures, "Simulator management search excludes unrelated identifiers.");
        return Task.CompletedTask;
    }

    private static bool SnapshotsSecretFree(IReadOnlyDictionary<string, object?> values) =>
        values.All(entry => !entry.Key.Contains("password", StringComparison.OrdinalIgnoreCase) &&
            !entry.Key.Contains("secret", StringComparison.OrdinalIgnoreCase) &&
            !entry.Key.Contains("credential", StringComparison.OrdinalIgnoreCase) &&
            !entry.Key.Contains("session", StringComparison.OrdinalIgnoreCase) &&
            !entry.Key.Contains("token", StringComparison.OrdinalIgnoreCase));

    private static OrganizationCallerSnapshot AdminCaller() => new("admin-user", "Admin", true,
        new[] { "Administrator" }, Array.Empty<string>(), Array.Empty<string>());

    private static OrganizationCallerSnapshot NoScopeEngineer() => new("eng-user", "Engineer", true,
        new[] { "Engineer" }, Array.Empty<string>(), Array.Empty<string>());

    private sealed class FakeCatalogAuthorization : ICatalogAuthorization
    {
        private readonly CatalogCallerSnapshot? _caller;
        public FakeCatalogAuthorization(OrganizationCallerSnapshot caller) => _caller = caller is null
            ? null
            : new CatalogCallerSnapshot(caller.UserId, caller.Username, caller.IsActive,
                caller.Roles, caller.SiteScopes, Array.Empty<string>());

        public Task<CatalogAuthorizationDecision> AuthorizeAsync(string requestedByUserId,
            CatalogResource resource, string? targetSiteId = null, CancellationToken ct = default)
        {
            if (_caller is null || !_caller.IsActive) return Task.FromResult(CatalogAuthorizationDecision.Forbidden());
            if (_caller.HasRole("Administrator")) return Task.FromResult(CatalogAuthorizationDecision.Allowed());
            return Task.FromResult(CatalogAuthorizationDecision.NotFound());
        }

        public Task<CatalogCallerSnapshot?> ResolveCallerAsync(string requestedByUserId, CancellationToken ct = default) =>
            Task.FromResult(_caller);
    }

    private static (CatalogSourceScopeQueryAdapter, FakeCatalogCommandRepository, CallerProvider) CreateAdapterChain(
        Guid sourceId, Guid pointId)
    {
        var catalog = new FakeCatalogCommandRepository();
        var dataSource = new DataSource(new DataSourceId(sourceId), "SRC-" + sourceId.ToString("N")[..6],
            "Test Source", SourceType.Simulator, SourceStatus.Active, 1);
        catalog.AddDataSourceAsync(dataSource).GetAwaiter().GetResult();
        catalog.AddMappingAsync(new SourcePointMapping(MappingId.New(), dataSource.Id, pointId.ToString("D"),
            MappingStatus.Active, DateTime.UtcNow.AddDays(-1), null, 1)).GetAwaiter().GetResult();

        var readiness = new ReadinessDouble();
        readiness.Set(site: Guid.NewGuid(), area: Guid.NewGuid(), asset: Guid.NewGuid(), point: pointId);
        var readinessAdapter = new OrganizationPointReadinessAdapter(readiness);
        var scopeAdapter = new CatalogSourceScopeQueryAdapter(catalog, readinessAdapter);

        var callers = new CallerProvider();
        callers.Set(new ConfigurationCallerSnapshot("admin", "admin.user", true,
            new[] { "Administrator" }, Array.Empty<string>()));
        return (scopeAdapter, catalog, callers);
    }

    private static SimulatorConfigurationCreateCommand Create(string actor, Guid sourceId, ulong seed) =>
        new(sourceId, seed, 60, 1, 1, SimulatorScenario.Constant,
            SimulatorConfigurationConstants.AlgorithmId, SimulatorConfigurationConstants.AlgorithmVersion,
            actor, "corr-" + seed, "caus-" + seed);

    private static SimulatorConfigurationEditCommand Edit(string actor, Guid configurationId, long expected, ulong seed) =>
        new(configurationId, expected, seed, 60, 1, 1, SimulatorScenario.Constant,
            SimulatorConfigurationConstants.AlgorithmId, SimulatorConfigurationConstants.AlgorithmVersion,
            actor, "corr-edit-" + seed, "caus-edit-" + seed);

    private static void AssertT037(bool condition, List<string> failures, string message)
    {
        _assertionCount++;
        if (!condition) failures.Add($"T037: {message}");
    }

    private sealed class CallerProvider : IConfigurationCallerSnapshotProvider
    {
        private readonly Dictionary<string, ConfigurationCallerSnapshot> _callers = new(StringComparer.Ordinal);
        public void Set(ConfigurationCallerSnapshot caller) => _callers[caller.UserId] = caller;
        public Task<ConfigurationCallerSnapshot?> ResolveAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult(_callers.GetValueOrDefault(userId));
    }

    private sealed class ReadinessDouble : IOrganizationQueryRepository
    {
        private PointSnapshot? _point;
        private AssetSnapshot? _asset;
        private AreaSnapshot? _area;
        private SiteSnapshot? _site;

        public void Set(Guid site, Guid area, Guid asset, Guid point) =>
            (_site, _area, _asset, _point) = (
                new SiteSnapshot(site, "SITE", "Site", null, "UTC", SiteStatus.Active, 1),
                new AreaSnapshot(area, site, "AREA", "Area", null, AreaStatus.Active, 2),
                new AssetSnapshot(asset, site, area, "ASSET", "Asset", null, AssetStatus.Active, 3),
                new PointSnapshot(point, site, area, asset, "POINT", null, "metric-el", "unit-kwh", "owner-1",
                    60, 300, PointStatus.Draft, 4));

        public Task<SiteSnapshot?> GetSiteSnapshotAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_site?.Id == id ? _site : null);
        public Task<AreaSnapshot?> GetAreaSnapshotAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_area?.Id == id ? _area : null);
        public Task<AssetSnapshot?> GetAssetSnapshotAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_asset?.Id == id ? _asset : null);
        public Task<PointSnapshot?> GetPointSnapshotAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_point?.Id == id ? _point : null);
        public Task<SiteSnapshot?> FindSiteByCodeAsync(string code, CancellationToken ct = default) =>
            Task.FromResult<SiteSnapshot?>(null);
        public Task<PagedResult<SiteSnapshot>> GetSitesAsync(OrganizationQueryScope scope, ScopeFilter filter, CancellationToken ct = default) =>
            Task.FromResult(new PagedResult<SiteSnapshot>(Array.Empty<SiteSnapshot>(), 0, filter.Page, filter.PageSize));
        public Task<PagedResult<AreaSnapshot>> GetAreasForSiteAsync(Guid siteId, OrganizationQueryScope scope, ScopeFilter filter, CancellationToken ct = default) =>
            Task.FromResult(new PagedResult<AreaSnapshot>(Array.Empty<AreaSnapshot>(), 0, filter.Page, filter.PageSize));
        public Task<PagedResult<AssetSnapshot>> GetAssetsForAreaAsync(Guid areaId, OrganizationQueryScope scope, ScopeFilter filter, CancellationToken ct = default) =>
            Task.FromResult(new PagedResult<AssetSnapshot>(Array.Empty<AssetSnapshot>(), 0, filter.Page, filter.PageSize));
        public Task<PagedResult<PointSnapshot>> GetPointsForAssetAsync(Guid assetId, OrganizationQueryScope scope, ScopeFilter filter, CancellationToken ct = default) =>
            Task.FromResult(new PagedResult<PointSnapshot>(Array.Empty<PointSnapshot>(), 0, filter.Page, filter.PageSize));
        public Task<PagedResult<PointSnapshot>> GetPointsForSiteAsync(Guid siteId, OrganizationQueryScope scope, ScopeFilter filter, CancellationToken ct = default) =>
            Task.FromResult(new PagedResult<PointSnapshot>(Array.Empty<PointSnapshot>(), 0, filter.Page, filter.PageSize));
        public Task<bool> SiteExistsAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_site?.Id == id);
        public Task<long> GetSiteVersionAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_site?.Id == id ? _site.Version : 0);
        public Task<AreaAncestrySnapshot?> GetAreaAncestryAsync(Guid areaId, CancellationToken ct = default) =>
            Task.FromResult(_area?.Id == areaId ? new AreaAncestrySnapshot(_area.Id, _area.SiteId) : null);
    }
}
