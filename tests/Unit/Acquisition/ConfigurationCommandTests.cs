using IUMP.Modules.Acquisition.Application;
using IUMP.Modules.Acquisition.Contracts;
using IUMP.Modules.Catalog.Application;
using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.Catalog.Domain;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Acquisition;

public static class ConfigurationCommandTests
{
    private static int _assertionCount;

    public static List<string> Run()
    {
        var failures = new List<string>();
        _assertionCount = 0;
        AdminCreateAndEdit(failures).GetAwaiter().GetResult();
        EngineerCreateWithSingleScope(failures).GetAwaiter().GetResult();
        EngineerEditWithSingleScope(failures).GetAwaiter().GetResult();
        EngineerNoMappingDenied(failures).GetAwaiter().GetResult();
        EngineerMultiSiteAllScopesSucceed(failures).GetAwaiter().GetResult();
        EngineerMultiSitePartialDenied(failures).GetAwaiter().GetResult();
        InactiveCallerDenied(failures).GetAwaiter().GetResult();
        OperatorDenied(failures).GetAwaiter().GetResult();
        ManagerDenied(failures).GetAwaiter().GetResult();
        ViewerDenied(failures).GetAwaiter().GetResult();
        MissingSourceDenied(failures).GetAwaiter().GetResult();
        DecommissionedSourceDenied(failures).GetAwaiter().GetResult();
        UnresolvedReadinessDenied(failures).GetAwaiter().GetResult();
        EmptySiteIdDenied(failures).GetAwaiter().GetResult();
        EmptyAreaIdDenied(failures).GetAwaiter().GetResult();
        ZeroVersionDenied(failures).GetAwaiter().GetResult();
        DuplicateMappingScopes(failures).GetAwaiter().GetResult();
        StaleVersionAndNoop(failures).GetAwaiter().GetResult();
        EventEnvelopeCompleteness(failures).GetAwaiter().GetResult();
        Console.WriteLine($"T079: assertions={_assertionCount}; failures={failures.Count}");
        return failures;
    }

    private static async Task AdminCreateAndEdit(List<string> failures)
    {
        var sourceId = Guid.NewGuid();
        var pointId = Guid.NewGuid();
        var acqRepo = new FakeAcquisitionConfigurationRepository();
        var (adapter, _, callers) = CreateAdapterChain(sourceId, pointId,
            "trusted-site", "trusted-area", SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Draft, 60, 300);
        var service = new SimulatorConfigurationService(acqRepo, callers, adapter);

        var create = await service.CreateAsync(Command("admin", sourceId, 42, "corr-create", "caus-create"));
        AssertT079(create.IsSuccess, failures, "Administrator can create globally and source identity is resolved server-side.");
        var head = await acqRepo.GetBySourceIdAsync(sourceId);
        AssertT079(head is not null, failures, "Create persists a configuration head.");
        var firstEvent = service.Events.Single();
        AssertT079(firstEvent.EventType == SimulatorConfigurationConstants.EventType, failures, "EventType is correct.");
        AssertT079(firstEvent.SchemaVersion == "1", failures, "SchemaVersion is 1.");
        AssertT079(firstEvent.Producer == SimulatorConfigurationConstants.Producer, failures, "Producer is IUMP.Acquisition.");
        AssertT079(firstEvent.AggregateType == "SimulatorConfiguration", failures, "AggregateType is correct.");
        AssertT079(firstEvent.AggregateId == head!.ConfigurationId.ToString("D"), failures, "AggregateId matches head.");
        AssertT079(firstEvent.AggregateVersion == head.Version, failures, "AggregateVersion matches head version.");
        AssertT079(firstEvent.ActorId == "admin", failures, "ActorId is snapshot.");
        AssertT079(firstEvent.ActorUsername == "admin.user", failures, "ActorUsername is snapshot.");
        AssertT079(firstEvent.Action == "Created", failures, "Action is Created.");
        AssertT079(firstEvent.Summary == "Simulator configuration created.", failures, "Summary is correct.");
        AssertT079(firstEvent.OccurredAtUtc.Kind == DateTimeKind.Utc, failures, "OccurredAtUtc is UTC.");
        AssertT079(firstEvent.CorrelationId == "corr-create", failures, "CorrelationId is exact supplied value.");
        AssertT079(firstEvent.CausationId == "caus-create", failures, "CausationId is exact supplied value.");
        AssertT079(firstEvent.SiteIds.Count == 1 && firstEvent.SiteIds[0] == GuidHash("trusted-site").ToString("D"), failures, "Event has trusted SiteIds collection.");
        AssertT079(firstEvent.SiteIds.SequenceEqual(firstEvent.SiteIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)), failures, "SiteIds are ordinally sorted.");
        AssertT079(firstEvent.SiteIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() == firstEvent.SiteIds.Count, failures, "SiteIds are distinct.");

        var allowedFields = new[] { "sourceId", "configurationId", "configurationVersion", "intervalSeconds", "minimumValue", "maximumValue", "deterministicSeed", "deterministicSeedHex", "scenarioType", "algorithmId", "algorithmVersion" };
        AssertT079(firstEvent.After.Keys.OrderBy(x => x).SequenceEqual(allowedFields.OrderBy(x => x)), failures, "Event after fields use the explicit safe allowlist.");
        AssertT079(!firstEvent.After.Keys.Any(k => k.Contains("password", StringComparison.OrdinalIgnoreCase) || k.Contains("secret", StringComparison.OrdinalIgnoreCase) || k.Contains("connection", StringComparison.OrdinalIgnoreCase)), failures, "Event contains no credentials, secrets or connection information.");
        AssertT079(firstEvent.After["deterministicSeed"] is string ds && ds == "42", failures, "deterministicSeed is invariant decimal string.");
        AssertT079(firstEvent.After["deterministicSeedHex"] is string dh && dh == "000000000000002a", failures, "deterministicSeedHex is exact lowercase 16-hex.");
        AssertT079(firstEvent.Before.Count == 0, failures, "Create event has empty Before dictionary.");
    }

    private static async Task EngineerCreateWithSingleScope(List<string> failures)
    {
        var sourceId = Guid.NewGuid();
        var pointId = Guid.NewGuid();
        var (adapter, _, callers) = CreateAdapterChain(sourceId, pointId,
            "eng-site", "eng-area", SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Draft, 60, 300,
            new ConfigurationCallerSnapshot("engineer", "engineer.user", true, new[] { "Engineer" }, new[] { "eng-site" }));
        var service = new SimulatorConfigurationService(new FakeAcquisitionConfigurationRepository(), callers, adapter);

        var result = await service.CreateAsync(Command("engineer", sourceId, 42, "corr-eng", "caus-eng"));
        AssertT079(result.IsSuccess, failures, "Engineer with matching Site scope can create configuration.");
        var createdEvent = service.Events.Single();
        AssertT079(createdEvent.ActorId == "engineer", failures, "Engineer event ActorId is snapshot.");
        AssertT079(createdEvent.ActorUsername == "engineer.user", failures, "Engineer event ActorUsername is snapshot.");
        AssertT079(createdEvent.SiteIds.Count == 1 && createdEvent.SiteIds[0] == GuidHash("eng-site").ToString("D"), failures, "Engineer event SiteIds matches scope.");
    }

    private static async Task EngineerEditWithSingleScope(List<string> failures)
    {
        var sourceId = Guid.NewGuid();
        var pointId = Guid.NewGuid();
        var acqRepo = new FakeAcquisitionConfigurationRepository();
        var (adapter, _, callers) = CreateAdapterChain(sourceId, pointId,
            "eng-site", "eng-area", SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Draft, 60, 300,
            new ConfigurationCallerSnapshot("engineer", "engineer.user", true, new[] { "Engineer" }, new[] { "eng-site" }));
        var service = new SimulatorConfigurationService(acqRepo, callers, adapter);

        var create = await service.CreateAsync(Command("engineer", sourceId, 42, "corr-eng", "caus-eng"));
        AssertT079(create.IsSuccess, failures, "Engineer creates configuration for edit test.");
        var head = await acqRepo.GetBySourceIdAsync(sourceId);

        var edit = await service.EditAsync(Edit("engineer", head!.ConfigurationId, head.Version, 7, 30, 2, 4, SimulatorScenario.Normal, "corr-eng-edit", "caus-eng-edit"));
        AssertT079(edit.IsSuccess, failures, "Engineer can edit own-scope configuration.");
        AssertT079(service.Events.Count == 2, failures, "Engineer edit produces exactly two events.");
        var editEvent = service.Events.Last();
        AssertT079(editEvent.ActorId == "engineer", failures, "Engineer edit event ActorId is snapshot.");
        AssertT079(editEvent.Action == "Edited", failures, "Engineer edit event Action is Edited.");
    }

    private static async Task EngineerNoMappingDenied(List<string> failures)
    {
        var catalog = new FakeCatalogCommandRepository();
        var sourceId = Guid.NewGuid();
        var dataSource = new DataSource(new DataSourceId(sourceId), "SRC-NOMAP", "No Mapping", SourceType.Simulator, SourceStatus.Active, 1);
        await catalog.AddDataSourceAsync(dataSource);
        var callers = new FakeCallerProvider();
        callers.Set(new ConfigurationCallerSnapshot("admin", "admin.user", true, new[] { "Administrator" }, Array.Empty<string>()));
        var readiness = new NoOpReadinessQuery();
        var adapter = new CatalogSourceScopeQueryAdapter(catalog, readiness);
        var acqRepo = new FakeAcquisitionConfigurationRepository();
        var service = new SimulatorConfigurationService(acqRepo, callers, adapter);

        var adminResult = await service.CreateAsync(Command("admin", sourceId, 42, "corr-nomap", "caus-nomap"));
        AssertT079(adminResult.IsSuccess, failures, "Administrator can configure a Source with no Mapping.");
        AssertT079(service.Events.Count == 1, failures, "Admin no-Mapping event emitted.");

        callers.Set(new ConfigurationCallerSnapshot("engineer", "engineer.user", true, new[] { "Engineer" }, new[] { "site-a" }));
        var engResult = await service.CreateAsync(Command("engineer", sourceId, 42, "corr-nomap-eng", "caus-nomap-eng"));
        AssertT079(!engResult.IsSuccess, failures, "Engineer with no Mapping scope cannot configure.");
        AssertT079(service.Events.Count == 1, failures, "Engineer no-Mapping emits no event.");
    }

    private static async Task EngineerMultiSiteAllScopesSucceed(List<string> failures)
    {
        var sourceId = Guid.NewGuid();
        var pointA = Guid.NewGuid();
        var pointB = Guid.NewGuid();
        var catalog = new FakeCatalogCommandRepository();
        var dataSource = new DataSource(new DataSourceId(sourceId), "SRC-MULTI", "Multi-Site", SourceType.Simulator, SourceStatus.Active, 1);
        await catalog.AddDataSourceAsync(dataSource);
        var mappingA = new SourcePointMapping(MappingId.New(), dataSource.Id, pointA.ToString("D"), MappingStatus.Active, DateTime.UtcNow.AddDays(-1), null, 1);
        var mappingB = new SourcePointMapping(MappingId.New(), dataSource.Id, pointB.ToString("D"), MappingStatus.Active, DateTime.UtcNow.AddDays(-2), null, 1);
        await catalog.AddMappingAsync(mappingA);
        await catalog.AddMappingAsync(mappingB);

        var siteAGuid = GuidHash("site-a");
        var siteBGuid = GuidHash("site-b");
        var areaGuidA = Guid.NewGuid();
        var areaGuidB = Guid.NewGuid();
        var assetGuidA = Guid.NewGuid();
        var assetGuidB = Guid.NewGuid();
        var readiness = new ReadinessQueryDouble();
        readiness.Set((siteAGuid, areaGuidA, assetGuidA, pointA), SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Active, 60, 300);
        readiness.SetSecond((siteBGuid, areaGuidB, assetGuidB, pointB), SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Draft, 60, 300);
        var readinessAdapter = new OrganizationPointReadinessAdapter(readiness);
        var scopeAdapter = new CatalogSourceScopeQueryAdapter(catalog, readinessAdapter);

        var callers = new FakeCallerProvider();
        callers.Set(new ConfigurationCallerSnapshot("admin", "admin.user", true, new[] { "Administrator" }, Array.Empty<string>()));
        callers.Set(new ConfigurationCallerSnapshot("eng-full", "engineer.full", true, new[] { "Engineer" },
            new[] { siteAGuid.ToString("D"), siteBGuid.ToString("D") }));

        var service = new SimulatorConfigurationService(new FakeAcquisitionConfigurationRepository(), callers, scopeAdapter);
        var engResult = await service.CreateAsync(Command("eng-full", sourceId, 7, "corr-eng-multi", "caus-eng-multi"));
        AssertT079(engResult.IsSuccess, failures, "Engineer with all mapped Site scopes can configure multi-Site Source.");
        if (engResult.IsSuccess && service.Events.Count == 1)
        {
            var engEvent = service.Events[0];
            AssertT079(engEvent.SiteIds.Count == 2, failures, "Multi-Site event has both SiteIds.");
            AssertT079(engEvent.SiteIds.Contains(siteAGuid.ToString("D")), failures, "Event contains site-a.");
            AssertT079(engEvent.SiteIds.Contains(siteBGuid.ToString("D")), failures, "Event contains site-b.");
            AssertT079(engEvent.SiteIds.SequenceEqual(engEvent.SiteIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)), failures, "Multi-Site SiteIds are sorted.");
        }
    }

    private static async Task EngineerMultiSitePartialDenied(List<string> failures)
    {
        var sourceId = Guid.NewGuid();
        var pointA = Guid.NewGuid();
        var pointB = Guid.NewGuid();
        var catalog = new FakeCatalogCommandRepository();
        var dataSource = new DataSource(new DataSourceId(sourceId), "SRC-PARTIAL", "Partial", SourceType.Simulator, SourceStatus.Active, 1);
        await catalog.AddDataSourceAsync(dataSource);
        var mappingA = new SourcePointMapping(MappingId.New(), dataSource.Id, pointA.ToString("D"), MappingStatus.Active, DateTime.UtcNow.AddDays(-1), null, 1);
        var mappingB = new SourcePointMapping(MappingId.New(), dataSource.Id, pointB.ToString("D"), MappingStatus.Active, DateTime.UtcNow.AddDays(-2), null, 1);
        await catalog.AddMappingAsync(mappingA);
        await catalog.AddMappingAsync(mappingB);

        var siteAGuid = GuidHash("site-a");
        var siteBGuid = GuidHash("site-b");
        var areaGuidA = Guid.NewGuid();
        var areaGuidB = Guid.NewGuid();
        var assetGuidA = Guid.NewGuid();
        var assetGuidB = Guid.NewGuid();
        var readiness = new ReadinessQueryDouble();
        readiness.Set((siteAGuid, areaGuidA, assetGuidA, pointA), SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Active, 60, 300);
        readiness.SetSecond((siteBGuid, areaGuidB, assetGuidB, pointB), SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Draft, 60, 300);
        var readinessAdapter = new OrganizationPointReadinessAdapter(readiness);
        var scopeAdapter = new CatalogSourceScopeQueryAdapter(catalog, readinessAdapter);

        var callers = new FakeCallerProvider();
        callers.Set(new ConfigurationCallerSnapshot("eng-partial", "engineer.partial", true, new[] { "Engineer" },
            new[] { siteAGuid.ToString("D") }));

        var result = await new SimulatorConfigurationService(new FakeAcquisitionConfigurationRepository(), callers, scopeAdapter)
            .CreateAsync(Command("eng-partial", sourceId, 7, "corr-eng-partial", "caus-eng-partial"));
        AssertT079(!result.IsSuccess && result.Code == "NOT_FOUND", failures, "Engineer with partial Site scopes is denied.");
    }

    private static async Task InactiveCallerDenied(List<string> failures)
    {
        var sourceId = Guid.NewGuid();
        var pointId = Guid.NewGuid();
        var (adapter, _, callers) = CreateAdapterChain(sourceId, pointId,
            "site-a", "area-a", SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Draft, 60, 300,
            new ConfigurationCallerSnapshot("inactive", "inactive.user", false, new[] { "Engineer" }, new[] { "site-a" }));
        var result = await new SimulatorConfigurationService(new FakeAcquisitionConfigurationRepository(), callers, adapter)
            .CreateAsync(Command("inactive", sourceId, 1, "corr-inact", "caus-inact"));
        AssertT079(!result.IsSuccess && result.Code == "FORBIDDEN", failures, "Inactive caller is denied.");
    }

    private static async Task OperatorDenied(List<string> failures)
    {
        var sourceId = Guid.NewGuid();
        var pointId = Guid.NewGuid();
        var (adapter, _, callers) = CreateAdapterChain(sourceId, pointId,
            "site-a", "area-a", SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Draft, 60, 300,
            new ConfigurationCallerSnapshot("operator", "operator.user", true, new[] { "Operator" }, new[] { "site-a" }));
        var result = await new SimulatorConfigurationService(new FakeAcquisitionConfigurationRepository(), callers, adapter)
            .CreateAsync(Command("operator", sourceId, 1, "corr-op", "caus-op"));
        AssertT079(!result.IsSuccess && result.Code == "FORBIDDEN", failures, "Operator role is denied configuration mutation.");
    }

    private static async Task ManagerDenied(List<string> failures)
    {
        var sourceId = Guid.NewGuid();
        var pointId = Guid.NewGuid();
        var (adapter, _, callers) = CreateAdapterChain(sourceId, pointId,
            "site-a", "area-a", SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Draft, 60, 300,
            new ConfigurationCallerSnapshot("manager", "manager.user", true, new[] { "Manager" }, new[] { "site-a" }));
        var result = await new SimulatorConfigurationService(new FakeAcquisitionConfigurationRepository(), callers, adapter)
            .CreateAsync(Command("manager", sourceId, 1, "corr-mgr", "caus-mgr"));
        AssertT079(!result.IsSuccess && result.Code == "FORBIDDEN", failures, "Manager role is denied configuration mutation.");
    }

    private static async Task ViewerDenied(List<string> failures)
    {
        var sourceId = Guid.NewGuid();
        var pointId = Guid.NewGuid();
        var (adapter, _, callers) = CreateAdapterChain(sourceId, pointId,
            "site-a", "area-a", SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Draft, 60, 300,
            new ConfigurationCallerSnapshot("viewer", "viewer.user", true, new[] { "Viewer" }, new[] { "site-a" }));
        var result = await new SimulatorConfigurationService(new FakeAcquisitionConfigurationRepository(), callers, adapter)
            .CreateAsync(Command("viewer", sourceId, 1, "corr-view", "caus-view"));
        AssertT079(!result.IsSuccess && result.Code == "FORBIDDEN", failures, "Viewer role is denied configuration mutation.");
    }

    private static async Task MissingSourceDenied(List<string> failures)
    {
        var sourceId = Guid.NewGuid();
        var callers = new FakeCallerProvider();
        callers.Set(new ConfigurationCallerSnapshot("admin", "admin.user", true, new[] { "Administrator" }, Array.Empty<string>()));
        var adapter = new CatalogSourceScopeQueryAdapter(new FakeCatalogCommandRepository(), new NoOpReadinessQuery());
        var result = await new SimulatorConfigurationService(new FakeAcquisitionConfigurationRepository(), callers, adapter)
            .CreateAsync(Command("admin", sourceId, 1, "corr-miss", "caus-miss"));
        AssertT079(!result.IsSuccess && result.Code == "FORBIDDEN", failures, "Missing Source returns FORBIDDEN.");
    }

    private static async Task DecommissionedSourceDenied(List<string> failures)
    {
        var sourceId = Guid.NewGuid();
        var pointId = Guid.NewGuid();
        var catalog = new FakeCatalogCommandRepository();
        var dataSource = new DataSource(new DataSourceId(sourceId), "SRC-DECOM", "Decom", SourceType.Simulator, SourceStatus.Decommissioned, 1);
        await catalog.AddDataSourceAsync(dataSource);
        var mapping = new SourcePointMapping(MappingId.New(), dataSource.Id, pointId.ToString("D"), MappingStatus.Active, DateTime.UtcNow.AddDays(-1), null, 1);
        await catalog.AddMappingAsync(mapping);

        var callers = new FakeCallerProvider();
        callers.Set(new ConfigurationCallerSnapshot("admin", "admin.user", true, new[] { "Administrator" }, Array.Empty<string>()));
        var readiness = new ReadinessQueryDouble();
        readiness.Set((GuidHash("site-a"), Guid.NewGuid(), Guid.NewGuid(), pointId), SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Active, 60, 300);
        var adapter = new CatalogSourceScopeQueryAdapter(catalog, new OrganizationPointReadinessAdapter(readiness));
        var result = await new SimulatorConfigurationService(new FakeAcquisitionConfigurationRepository(), callers, adapter)
            .CreateAsync(Command("admin", sourceId, 1, "corr-decom", "caus-decom"));
        AssertT079(!result.IsSuccess && result.Code == "FORBIDDEN", failures, "Decommissioned Source returns FORBIDDEN.");
    }

    private static async Task UnresolvedReadinessDenied(List<string> failures)
    {
        var sourceId = Guid.NewGuid();
        var pointId = Guid.NewGuid();
        var catalog = new FakeCatalogCommandRepository();
        var dataSource = new DataSource(new DataSourceId(sourceId), "SRC-UNR", "Unresolved", SourceType.Simulator, SourceStatus.Active, 1);
        await catalog.AddDataSourceAsync(dataSource);
        var mapping = new SourcePointMapping(MappingId.New(), dataSource.Id, pointId.ToString("D"), MappingStatus.Active, DateTime.UtcNow.AddDays(-1), null, 1);
        await catalog.AddMappingAsync(mapping);

        var callers = new FakeCallerProvider();
        callers.Set(new ConfigurationCallerSnapshot("admin", "admin.user", true, new[] { "Administrator" }, Array.Empty<string>()));
        var readiness = new NoOpReadinessQuery();
        var adapter = new CatalogSourceScopeQueryAdapter(catalog, readiness);
        var result = await new SimulatorConfigurationService(new FakeAcquisitionConfigurationRepository(), callers, adapter)
            .CreateAsync(Command("admin", sourceId, 1, "corr-unr", "caus-unr"));
        AssertT079(!result.IsSuccess && result.Code == "FORBIDDEN", failures, "Unresolved readiness returns FORBIDDEN (fail-closed).");
    }

    private static async Task EmptySiteIdDenied(List<string> failures)
    {
        var sourceId = Guid.NewGuid();
        var pointId = Guid.NewGuid().ToString("D");
        var catalog = new FakeCatalogCommandRepository();
        var dataSource = new DataSource(new DataSourceId(sourceId), "SRC-EMPTYSITE", "Empty Site", SourceType.Simulator, SourceStatus.Active, 1);
        await catalog.AddDataSourceAsync(dataSource);
        var mapping = new SourcePointMapping(MappingId.New(), dataSource.Id, pointId, MappingStatus.Active, DateTime.UtcNow.AddDays(-1), null, 1);
        await catalog.AddMappingAsync(mapping);

        var readiness = new ConfigurableReadinessQuery();
        readiness.Set(pointId, new PointReadinessSnapshot(pointId, string.Empty, "area-id", true, true, true, 1, new ReadinessVersionTuple(1, 1, 1, 1)));
        var adapter = new CatalogSourceScopeQueryAdapter(catalog, readiness);
        var result = await adapter.GetSourceScopeAsync(sourceId);
        AssertT079(result is null, failures, "Adapter returns null for readiness with empty SiteId (fail-closed).");
    }

    private static async Task EmptyAreaIdDenied(List<string> failures)
    {
        var sourceId = Guid.NewGuid();
        var pointId = Guid.NewGuid().ToString("D");
        var catalog = new FakeCatalogCommandRepository();
        var dataSource = new DataSource(new DataSourceId(sourceId), "SRC-EMPTYAREA", "Empty Area", SourceType.Simulator, SourceStatus.Active, 1);
        await catalog.AddDataSourceAsync(dataSource);
        var mapping = new SourcePointMapping(MappingId.New(), dataSource.Id, pointId, MappingStatus.Active, DateTime.UtcNow.AddDays(-1), null, 1);
        await catalog.AddMappingAsync(mapping);

        var readiness = new ConfigurableReadinessQuery();
        readiness.Set(pointId, new PointReadinessSnapshot(pointId, "site-id", string.Empty, true, true, true, 1, new ReadinessVersionTuple(1, 1, 1, 1)));
        var adapter = new CatalogSourceScopeQueryAdapter(catalog, readiness);
        var result = await adapter.GetSourceScopeAsync(sourceId);
        // Current adapter uses AreaId ?? string.Empty fallback, allowing empty AreaId
        AssertT079(result is null, failures, "Adapter returns null for readiness with empty AreaId (fail-closed).");
    }

    private static async Task ZeroVersionDenied(List<string> failures)
    {
        var sourceId = Guid.NewGuid();
        var pointId = Guid.NewGuid().ToString("D");
        var catalog = new FakeCatalogCommandRepository();
        var dataSource = new DataSource(new DataSourceId(sourceId), "SRC-ZEROV", "Zero Version", SourceType.Simulator, SourceStatus.Active, 1);
        await catalog.AddDataSourceAsync(dataSource);
        var mapping = new SourcePointMapping(MappingId.New(), dataSource.Id, pointId, MappingStatus.Active, DateTime.UtcNow.AddDays(-1), null, 1);
        await catalog.AddMappingAsync(mapping);

        var readiness = new ConfigurableReadinessQuery();
        readiness.Set(pointId, new PointReadinessSnapshot(pointId, "site-id", "area-id", true, true, true, 1, new ReadinessVersionTuple(0, 1, 1, 1)));
        var adapter = new CatalogSourceScopeQueryAdapter(catalog, readiness);
        var result = await adapter.GetSourceScopeAsync(sourceId);
        // Current adapter doesn't validate version positivity
        AssertT079(result is null, failures, "Adapter returns null for readiness with zero PointVersion.");
    }

    private static async Task DuplicateMappingScopes(List<string> failures)
    {
        var sourceId = Guid.NewGuid();
        var pointId = Guid.NewGuid();
        var catalog = new FakeCatalogCommandRepository();
        var dataSource = new DataSource(new DataSourceId(sourceId), "SRC-DUP", "Duplicate Mappings", SourceType.Simulator, SourceStatus.Active, 1);
        await catalog.AddDataSourceAsync(dataSource);
        // Two mappings to the same point (nondeterministic repo order)
        var mapping1 = new SourcePointMapping(MappingId.New(), dataSource.Id, pointId.ToString("D"), MappingStatus.Active, DateTime.UtcNow.AddDays(-1), null, 1);
        var mapping2 = new SourcePointMapping(MappingId.New(), dataSource.Id, pointId.ToString("D"), MappingStatus.Draft, DateTime.UtcNow, null, 1);
        await catalog.AddMappingAsync(mapping1);
        await catalog.AddMappingAsync(mapping2);

        var siteGuid = GuidHash("dup-site");
        var areaGuid = Guid.NewGuid();
        var assetGuid = Guid.NewGuid();
        var readiness = new ReadinessQueryDouble();
        readiness.Set((siteGuid, areaGuid, assetGuid, pointId), SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Draft, 60, 300);
        var adapter = new CatalogSourceScopeQueryAdapter(catalog, new OrganizationPointReadinessAdapter(readiness));
        var callers = new FakeCallerProvider();
        callers.Set(new ConfigurationCallerSnapshot("admin", "admin.user", true, new[] { "Administrator" }, Array.Empty<string>()));
        var service = new SimulatorConfigurationService(new FakeAcquisitionConfigurationRepository(), callers, adapter);

        var result = await service.CreateAsync(Command("admin", sourceId, 42, "corr-dup", "caus-dup"));
        AssertT079(result.IsSuccess, failures, "Duplicate Mapping scopes are resolved successfully.");
        var evt = service.Events.Single();
        AssertT079(evt.SiteIds.Count == 1 && evt.SiteIds[0] == siteGuid.ToString("D"), failures, "Duplicate mappings produce deduplicated single SiteId.");
    }

    private static async Task StaleVersionAndNoop(List<string> failures)
    {
        var sourceId = Guid.NewGuid();
        var pointId = Guid.NewGuid();
        var acqRepo = new FakeAcquisitionConfigurationRepository();
        var (adapter, _, callers) = CreateAdapterChain(sourceId, pointId,
            "trusted-site", "trusted-area", SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Draft, 60, 300);
        var service = new SimulatorConfigurationService(acqRepo, callers, adapter);

        var create = await service.CreateAsync(Command("admin", sourceId, 42, "corr-stale", "caus-stale"));
        AssertT079(create.IsSuccess, failures, "Admin creates for stale/noop test.");
        var head = await acqRepo.GetBySourceIdAsync(sourceId);

        var edit1 = await service.EditAsync(Edit("admin", head!.ConfigurationId, head.Version, 7, 30, 2, 4, SimulatorScenario.Normal, "corr-edit1", "caus-edit1"));
        AssertT079(edit1.IsSuccess && service.Events.Count == 2, failures, "First edit succeeds.");

        var stale = await service.EditAsync(Edit("admin", head.ConfigurationId, 1, 99, 40, 5, 6, SimulatorScenario.Normal, "corr-stale2", "caus-stale2"));
        AssertT079(stale.Code == "VERSION_CONFLICT" && service.Events.Count == 2, failures, "Stale ExpectedVersion emits no event.");

        var noop = await service.EditAsync(Edit("admin", head.ConfigurationId, 2, 7, 30, 2, 4, SimulatorScenario.Normal, "corr-noop", "caus-noop"));
        AssertT079(noop.Code == "NO_OP" && service.Events.Count == 2, failures, "No-op edit emits no event.");
    }

    private static async Task EventEnvelopeCompleteness(List<string> failures)
    {
        var sourceId = Guid.NewGuid();
        var pointId = Guid.NewGuid();
        var acqRepo = new FakeAcquisitionConfigurationRepository();
        var (adapter, _, callers) = CreateAdapterChain(sourceId, pointId,
            "trusted-site", "trusted-area", SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Draft, 60, 300);
        var service = new SimulatorConfigurationService(acqRepo, callers, adapter);

        var create = await service.CreateAsync(Command("admin", sourceId, 42, "corr-env", "caus-env"));
        AssertT079(create.IsSuccess, failures, "Admin creates for event envelope test.");
        var head = await acqRepo.GetBySourceIdAsync(sourceId);

        var createEvent = service.Events.Single();
        AssertT079(createEvent.EventType == SimulatorConfigurationConstants.EventType, failures, "Create EventType.");
        AssertT079(createEvent.SchemaVersion == "1", failures, "Create SchemaVersion.");
        AssertT079(createEvent.Producer == SimulatorConfigurationConstants.Producer, failures, "Create Producer.");
        AssertT079(createEvent.AggregateType == "SimulatorConfiguration", failures, "Create AggregateType.");
        AssertT079(createEvent.AggregateId == head!.ConfigurationId.ToString("D"), failures, "Create AggregateId.");
        AssertT079(createEvent.AggregateVersion == head.Version, failures, "Create AggregateVersion.");
        AssertT079(createEvent.ActorId == "admin", failures, "Create ActorId.");
        AssertT079(createEvent.ActorUsername == "admin.user", failures, "Create ActorUsername.");
        AssertT079(createEvent.Action == "Created", failures, "Create Action.");
        AssertT079(createEvent.Summary == "Simulator configuration created.", failures, "Create Summary.");
        AssertT079(createEvent.OccurredAtUtc.Kind == DateTimeKind.Utc, failures, "Create OccurredAtUtc.");
        AssertT079(createEvent.CorrelationId == "corr-env", failures, "Create CorrelationId.");
        AssertT079(createEvent.CausationId == "caus-env", failures, "Create CausationId.");
        AssertT079(createEvent.Before.Count == 0, failures, "Create Before empty.");
        AssertT079(createEvent.After.ContainsKey("deterministicSeed") && createEvent.After["deterministicSeed"] is string, failures, "Create has deterministicSeed.");
        AssertT079(createEvent.After.ContainsKey("deterministicSeedHex") && createEvent.After["deterministicSeedHex"] is string, failures, "Create has deterministicSeedHex.");

        var edit = await service.EditAsync(Edit("admin", head.ConfigurationId, head.Version, 7, 30, 2, 4, SimulatorScenario.Normal, "corr-env-edit", "caus-env-edit"));
        AssertT079(edit.IsSuccess && service.Events.Count == 2, failures, "Edit succeeds.");
        var editEvent = service.Events.Last();
        AssertT079(editEvent.Action == "Edited", failures, "Edit Action.");
        AssertT079(editEvent.Summary == "Simulator configuration changed.", failures, "Edit Summary.");
        AssertT079(editEvent.AggregateVersion == 2, failures, "Edit AggregateVersion incremented.");
        AssertT079(editEvent.CorrelationId == "corr-env-edit", failures, "Edit CorrelationId.");
        AssertT079(editEvent.CausationId == "caus-env-edit", failures, "Edit CausationId.");
        AssertT079(editEvent.Before["deterministicSeed"] is string bds && bds == "42", failures, "Edit Before has previous seed.");
        AssertT079(editEvent.After["deterministicSeed"] is string ads && ads == "7", failures, "Edit After has new seed.");
        AssertT079(editEvent.After["deterministicSeedHex"] is string adh && adh == "0000000000000007", failures, "Edit deterministicSeedHex is correct.");
        AssertT079(editEvent.SiteIds.Count == 1, failures, "Edit SiteIds present.");
        AssertT079(editEvent.Before.Keys.OrderBy(x => x).SequenceEqual(editEvent.After.Keys.OrderBy(x => x)), failures, "Before and After have same key set.");
        AssertT079(!editEvent.After.Keys.Any(k => k.Contains("password", StringComparison.OrdinalIgnoreCase) || k.Contains("secret", StringComparison.OrdinalIgnoreCase) || k.Contains("connection", StringComparison.OrdinalIgnoreCase)), failures, "No secrets in event.");
    }

    private static (CatalogSourceScopeQueryAdapter, FakeCatalogCommandRepository, FakeCallerProvider) CreateAdapterChain(
        Guid sourceId, Guid pointId, string siteId, string areaId,
        SiteStatus siteStatus, AreaStatus areaStatus, AssetStatus assetStatus, PointStatus pointStatus,
        int interval, int noData,
        ConfigurationCallerSnapshot? engineerCaller = null)
    {
        var catalog = new FakeCatalogCommandRepository();
        var dataSource = new DataSource(new DataSourceId(sourceId), "SRC-" + sourceId.ToString("N")[..6], "Test Source", SourceType.Simulator, SourceStatus.Active, 1);
        var dsId = dataSource.Id;
        catalog.AddDataSourceAsync(dataSource).GetAwaiter().GetResult();
        var mapping = new SourcePointMapping(MappingId.New(), dsId, pointId.ToString("D"), MappingStatus.Active,
            DateTime.UtcNow.AddDays(-1), null, 1);
        catalog.AddMappingAsync(mapping).GetAwaiter().GetResult();

        var siteGuid = GuidHash(siteId);
        var areaGuid = GuidHash(areaId);
        var assetGuid = Guid.NewGuid();
        var readiness = new ReadinessQueryDouble();
        readiness.Set((siteGuid, areaGuid, assetGuid, pointId), siteStatus, areaStatus, assetStatus, pointStatus, interval, noData);
        var readinessAdapter = new OrganizationPointReadinessAdapter(readiness);
        var scopeAdapter = new CatalogSourceScopeQueryAdapter(catalog, readinessAdapter);

        var callers = new FakeCallerProvider();
        callers.Set(new ConfigurationCallerSnapshot("admin", "admin.user", true, new[] { "Administrator" }, Array.Empty<string>()));
        if (engineerCaller is not null)
        {
            var mappedSiteScopes = engineerCaller.SiteScopes
                .Select(s => GuidHash(s).ToString("D"))
                .ToList();
            callers.Set(new ConfigurationCallerSnapshot(
                engineerCaller.UserId, engineerCaller.Username, engineerCaller.IsActive,
                engineerCaller.Roles, mappedSiteScopes));
        }

        return (scopeAdapter, catalog, callers);
    }

    private static Guid GuidHash(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(bytes[..16]);
    }

    private static SimulatorConfigurationCreateCommand Command(string actor, Guid sourceId, ulong seed, string correlation, string causation) =>
        new(sourceId, seed, 60, 1, 1, SimulatorScenario.Constant, SimulatorConfigurationConstants.AlgorithmId,
            SimulatorConfigurationConstants.AlgorithmVersion, actor, correlation, causation);

    private static SimulatorConfigurationEditCommand Edit(string actor, Guid configurationId, long expected, ulong seed,
        int interval, double min, double max, SimulatorScenario scenario, string correlation, string causation) =>
        new(configurationId, expected, seed, interval, min, max, scenario,
            SimulatorConfigurationConstants.AlgorithmId, SimulatorConfigurationConstants.AlgorithmVersion, actor, correlation, causation);

    private static void AssertT079(bool condition, List<string> failures, string message)
    {
        _assertionCount++;
        if (!condition) failures.Add($"T079: {message}");
    }

    private sealed class FakeCallerProvider : IConfigurationCallerSnapshotProvider
    {
        private readonly Dictionary<string, ConfigurationCallerSnapshot> _callers = new(StringComparer.Ordinal);
        public void Set(ConfigurationCallerSnapshot caller) => _callers[caller.UserId] = caller;
        public Task<ConfigurationCallerSnapshot?> ResolveAsync(string userId, CancellationToken ct = default) => Task.FromResult(_callers.GetValueOrDefault(userId));
    }

    private sealed class ConfigurableReadinessQuery : ICatalogPointReadinessQuery
    {
        private PointReadinessSnapshot? _snapshot;
        public void Set(string pointId, PointReadinessSnapshot snapshot) => _snapshot = snapshot;
        public Task<PointReadinessSnapshot?> GetPointReadinessAsync(string pointId, CancellationToken ct = default) =>
            Task.FromResult(_snapshot?.PointId == pointId ? _snapshot : null);
    }

    private sealed class NoOpReadinessQuery : ICatalogPointReadinessQuery
    {
        public Task<PointReadinessSnapshot?> GetPointReadinessAsync(string pointId, CancellationToken ct = default) =>
            Task.FromResult<PointReadinessSnapshot?>(null);
    }

    private sealed class ReadinessQueryDouble : IOrganizationQueryRepository
    {
        private PointSnapshot? _point;
        private PointSnapshot? _point2;
        private AssetSnapshot? _asset;
        private AssetSnapshot? _asset2;
        private AreaSnapshot? _area;
        private AreaSnapshot? _area2;
        private SiteSnapshot? _site;
        private SiteSnapshot? _site2;

        public void Set((Guid Site, Guid Area, Guid Asset, Guid Point) ids, SiteStatus siteStatus, AreaStatus areaStatus,
            AssetStatus assetStatus, PointStatus pointStatus, int interval, int noData)
        {
            _site = new SiteSnapshot(ids.Site, "SITE", "Site", null, "UTC", siteStatus, 1);
            _area = new AreaSnapshot(ids.Area, ids.Site, "AREA", "Area", null, areaStatus, 2);
            _asset = new AssetSnapshot(ids.Asset, ids.Site, ids.Area, "ASSET", "Asset", null, assetStatus, 3);
            _point = new PointSnapshot(ids.Point, ids.Site, ids.Area, ids.Asset, "POINT", null, "metric", "unit", "owner", interval, noData, pointStatus, 4);
        }

        public void SetSecond((Guid Site, Guid Area, Guid Asset, Guid Point) ids, SiteStatus siteStatus, AreaStatus areaStatus,
            AssetStatus assetStatus, PointStatus pointStatus, int interval, int noData)
        {
            _site2 = new SiteSnapshot(ids.Site, "SITE2", "Site 2", null, "UTC", siteStatus, 5);
            _area2 = new AreaSnapshot(ids.Area, ids.Site, "AREA2", "Area 2", null, areaStatus, 6);
            _asset2 = new AssetSnapshot(ids.Asset, ids.Site, ids.Area, "ASSET2", "Asset 2", null, assetStatus, 7);
            _point2 = new PointSnapshot(ids.Point, ids.Site, ids.Area, ids.Asset, "POINT2", null, "metric", "unit", "owner", interval, noData, pointStatus, 8);
        }

        public Task<SiteSnapshot?> GetSiteSnapshotAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_site?.Id == id ? _site : _site2?.Id == id ? _site2 : null);
        public Task<AreaSnapshot?> GetAreaSnapshotAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_area?.Id == id ? _area : _area2?.Id == id ? _area2 : null);
        public Task<AssetSnapshot?> GetAssetSnapshotAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_asset?.Id == id ? _asset : _asset2?.Id == id ? _asset2 : null);
        public Task<PointSnapshot?> GetPointSnapshotAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_point?.Id == id ? _point : _point2?.Id == id ? _point2 : null);
        public Task<SiteSnapshot?> FindSiteByCodeAsync(string code, CancellationToken ct = default) => Task.FromResult<SiteSnapshot?>(null);
        public Task<PagedResult<SiteSnapshot>> GetSitesAsync(OrganizationQueryScope scope, ScopeFilter filter, CancellationToken ct = default) => Task.FromResult(new PagedResult<SiteSnapshot>(Array.Empty<SiteSnapshot>(), 0, filter.Page, filter.PageSize));
        public Task<PagedResult<AreaSnapshot>> GetAreasForSiteAsync(Guid siteId, OrganizationQueryScope scope, ScopeFilter filter, CancellationToken ct = default) => Task.FromResult(new PagedResult<AreaSnapshot>(Array.Empty<AreaSnapshot>(), 0, filter.Page, filter.PageSize));
        public Task<PagedResult<AssetSnapshot>> GetAssetsForAreaAsync(Guid areaId, OrganizationQueryScope scope, ScopeFilter filter, CancellationToken ct = default) => Task.FromResult(new PagedResult<AssetSnapshot>(Array.Empty<AssetSnapshot>(), 0, filter.Page, filter.PageSize));
        public Task<PagedResult<PointSnapshot>> GetPointsForAssetAsync(Guid assetId, OrganizationQueryScope scope, ScopeFilter filter, CancellationToken ct = default) => Task.FromResult(new PagedResult<PointSnapshot>(Array.Empty<PointSnapshot>(), 0, filter.Page, filter.PageSize));
        public Task<PagedResult<PointSnapshot>> GetPointsForSiteAsync(Guid siteId, OrganizationQueryScope scope, ScopeFilter filter, CancellationToken ct = default) => Task.FromResult(new PagedResult<PointSnapshot>(Array.Empty<PointSnapshot>(), 0, filter.Page, filter.PageSize));
        public Task<bool> SiteExistsAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_site?.Id == id || _site2?.Id == id);
        public Task<long> GetSiteVersionAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_site?.Id == id ? _site.Version : _site2?.Id == id ? _site2.Version : 0);
        public Task<AreaAncestrySnapshot?> GetAreaAncestryAsync(Guid areaId, CancellationToken ct = default) =>
            Task.FromResult(_area?.Id == areaId ? new AreaAncestrySnapshot(_area.Id, _area.SiteId) : _area2?.Id == areaId ? new AreaAncestrySnapshot(_area2.Id, _area2.SiteId) : null);
    }
}
