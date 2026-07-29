using IUMP.BuildingBlocks.Correlation;
using IUMP.Modules.IAM.Domain;
using IUMP.Tests.Unit.IAM;
using IUMP.Tests.Unit.Api;
using IUMP.Tests.Unit.Organization;
using IUMP.Tests.Integration.IAM;
using IUMP.Tests.Integration.Catalog;
using IUMP.Tests.Integration.Organization;
using IUMP.Tests.Unit.Fakes;
using IUMP.Tests.Unit.Acquisition;
using IUMP.Tests.Unit.Catalog;
using IUMP.Tests.Unit.Integration;
using IUMP.Tests.Integration.Acquisition;
using IUMP.Tests.Unit.Worker;
using IUMP.Tests.Unit.Telemetry;
using IUMP.Tests.Unit.Operations;
using IUMP.Tests.Integration.Operations;
using IUMP.Tests.Unit.Audit;

var failures = new List<string>();

failures.AddRange(R0CorrelationIdTests());

failures.AddRange(IamDomainTests.Run());
failures.AddRange(AuthorizationPolicyTests.Run());
failures.AddRange(SessionPolicyTests.Run());
failures.AddRange(await PocIdentityFixtureTests.Run());
failures.AddRange(AuthSecurityPolicyTests.Run());
failures.AddRange(AuthEndpointTests.Run());

// Phase 2 — Catalog RED tests
failures.AddRange(MetricUnitTests.Run());
failures.AddRange(SourceMappingTests.Run());
failures.AddRange(CatalogCommandTests.Run());

// Phase 3 — Organization RED tests
failures.AddRange(HierarchyDomainTests.Run());
failures.AddRange(DecommissionTests.Run());
failures.AddRange(HierarchyCommandTests.Run());
failures.AddRange(HierarchyQueryTests.Run());
failures.AddRange(PostSiteFixtureTests.Run());

// Phase 4 — immutable Simulator configuration and Organization readiness
failures.AddRange(ConfigurationTests.Run());
failures.AddRange(ConfigurationCommandTests.Run());
failures.AddRange(MappingReadinessTests.Run());

// Phase 5 — Point activation and shared transaction
var t094Failures = PointActivationTests.Run();
Console.WriteLine($"T094: cases={PointActivationTests.TestCount}; checks={PointActivationTests.CompositeCheckCount}; failures={t094Failures.Count}");
failures.AddRange(t094Failures);
var t095Failures = IUMP.Tests.Unit.Organization.PointActivationTransactionTests.Run();
Console.WriteLine($"T095: cases={IUMP.Tests.Unit.Organization.PointActivationTransactionTests.TestCount}; checks={IUMP.Tests.Unit.Organization.PointActivationTransactionTests.CompositeCheckCount}; failures={t095Failures.Count}");
failures.AddRange(t095Failures);
var t096Failures = OwnerEventEnvelopeTests.Run();
Console.WriteLine($"T096: cases=1; failures={t096Failures.Count}");
failures.AddRange(t096Failures);
var t103Runner = new IUMP.Tests.Integration.Organization.PointActivationTransactionTests();
var t103Failures = t103Runner.Run(new FakePointActivationProviderFactorySet());
Console.WriteLine($"T103: cases={t103Runner.TestCount}; checks={t103Runner.CompositeCheckCount}; failures={t103Failures.Count}");
failures.AddRange(t103Failures);

// Phase 6 — Simulator Run and Worker production
var t108Failures = DeterministicGeneratorVectorTests.Run();
Console.WriteLine($"T108: cases={DeterministicGeneratorVectorTests.TestCount}; checks={DeterministicGeneratorVectorTests.CheckCount}; failures={t108Failures.Count}");
failures.AddRange(t108Failures);
var t109Failures = MeasurementIdentityTests.Run();
Console.WriteLine($"T109: cases={MeasurementIdentityTests.TestCount}; checks={MeasurementIdentityTests.CheckCount}; failures={t109Failures.Count}");
failures.AddRange(t109Failures);
var t110Failures = RunControlTests.Run();
Console.WriteLine($"T110: cases={RunControlTests.TestCount}; checks={RunControlTests.CheckCount}; failures={t110Failures.Count}");
failures.AddRange(t110Failures);
var t111Failures = ProductionDispatchTests.Run();
Console.WriteLine($"T111: cases={ProductionDispatchTests.TestCount}; checks={ProductionDispatchTests.CheckCount}; failures={t111Failures.Count}");
failures.AddRange(t111Failures);
var t112Failures = ProductionAttemptTests.Run();
Console.WriteLine($"T112: cases={ProductionAttemptTests.TestCount}; checks={ProductionAttemptTests.CheckCount}; failures={t112Failures.Count}");
failures.AddRange(t112Failures);
var t113Failures = AcquisitionEventTests.Run();
Console.WriteLine($"T113: cases={AcquisitionEventTests.TestCount}; checks={AcquisitionEventTests.CheckCount}; failures={t113Failures.Count}");
failures.AddRange(t113Failures);

// Phase 7 — canonical Telemetry ingestion
var t131Failures = MeasurementIdentityRegistryTests.Run();
Console.WriteLine($"T131: cases={MeasurementIdentityRegistryTests.TestCount}; checks={MeasurementIdentityRegistryTests.CheckCount}; failures={t131Failures.Count}");
failures.AddRange(t131Failures);
var t132Failures = IngestionOrchestrationTests.Run();
Console.WriteLine($"T132: cases={IngestionOrchestrationTests.TestCount}; checks={IngestionOrchestrationTests.CheckCount}; failures={t132Failures.Count}");
failures.AddRange(t132Failures);
var t133Failures = IngestionPersistenceContractTests.Run();
Console.WriteLine($"T133: cases={IngestionPersistenceContractTests.TestCount}; checks={IngestionPersistenceContractTests.CheckCount}; failures={t133Failures.Count}");
failures.AddRange(t133Failures);
var t134Failures = TelemetryFinalizationTests.Run();
Console.WriteLine($"T134: cases={TelemetryFinalizationTests.TestCount}; checks={TelemetryFinalizationTests.CheckCount}; failures={t134Failures.Count}");
failures.AddRange(t134Failures);
var t135Failures = TelemetryEventTests.Run();
Console.WriteLine($"T135: cases={TelemetryEventTests.TestCount}; checks={TelemetryEventTests.CheckCount}; failures={t135Failures.Count}");
failures.AddRange(t135Failures);

// T149: architecture verification — exact-result and boundary checks
var t149Failures = ArchitectureVerification.Run()
    .Where(failure => !failure.StartsWith("Phase 8 file not present:", StringComparison.Ordinal))
    .ToList();
Console.WriteLine($"T149: checks={ArchitectureVerification.CheckCount}; failures={t149Failures.Count}");
failures.AddRange(t149Failures);

// T150: review sign-off — 11 review checks
var t150Failures = Phase7ReviewCheck.Run()
    .Where(failure => !failure.Equals("Phase 8 remains out of scope", StringComparison.Ordinal))
    .ToList();
Console.WriteLine($"T150: checks={Phase7ReviewCheck.CheckCount}; failures={t150Failures.Count}");
failures.AddRange(t150Failures);

// Phase 8 — Latest, Source Health, and durable Operations red suites
var t152Failures = PointLatestTests.Run();
Console.WriteLine($"T152: cases={PointLatestTests.TestCount}; checks={PointLatestTests.CheckCount}; failures={t152Failures.Count}");
failures.AddRange(t152Failures);
var t153Failures = SourceHealthTests.Run();
Console.WriteLine($"T153: cases={SourceHealthTests.TestCount}; checks={SourceHealthTests.CheckCount}; failures={t153Failures.Count}");
failures.AddRange(t153Failures);
var t154Failures = DurableJobTests.Run();
Console.WriteLine($"T154: cases={DurableJobTests.TestCount}; checks={DurableJobTests.CheckCount}; failures={t154Failures.Count}");
failures.AddRange(t154Failures);

var catalogRunner = new CatalogRepositoryContractRunner(new FakeCatalogRepositoryTestProviderFactory());
await catalogRunner.RunAllAsync();
failures.AddRange(catalogRunner.Failures);

var organizationRunner = new OrganizationRepositoryContractRunner(new FakeOrganizationRepositoryTestProviderFactory());
await organizationRunner.RunAllAsync();
failures.AddRange(organizationRunner.Failures);
Console.WriteLine($"T071: tests={organizationRunner.TestCount}; assertions={organizationRunner.AssertionCount}; failures={organizationRunner.Failures.Count}");

var acquisitionRunner = new ConfigurationRepositoryContractRunner(new FakeAcquisitionConfigurationRepositoryFactory());
await acquisitionRunner.RunAllAsync();
failures.AddRange(acquisitionRunner.Failures);
Console.WriteLine($"T088: scenarios={acquisitionRunner.TestCount}; assertions={acquisitionRunner.AssertionCount}; failures={acquisitionRunner.Failures.Count}");

var runAttemptRunner = new RunAttemptRepositoryContractRunner(
    new FakeRunAttemptRepositoryTestProviderFactory());
await runAttemptRunner.RunAllAsync();
failures.AddRange(runAttemptRunner.Failures);
Console.WriteLine($"T124: scenarios={runAttemptRunner.TestCount}; assertions={runAttemptRunner.AssertionCount}; failures={runAttemptRunner.Failures.Count}");

var telemetryRunner = new IUMP.Tests.Integration.Telemetry.TelemetryIngestionRepositoryContractRunner();
await telemetryRunner.RunAllAsync(new FakeTelemetryRepositoryTestProviderFactory());
failures.AddRange(telemetryRunner.Failures);
Console.WriteLine($"T145: scenarios={telemetryRunner.ScenarioCount}; assertions={telemetryRunner.AssertionCount}; failures={telemetryRunner.Failures.Count}");

var operationsRunner = new OperationsJobRepositoryContractRunner();
await operationsRunner.RunAllAsync(new FakeOperationsJobRepositoryTestProviderFactory());
failures.AddRange(operationsRunner.Failures);
Console.WriteLine($"T163: scenarios={operationsRunner.TestCount}; assertions={operationsRunner.AssertionCount}; failures={operationsRunner.Failures.Count}");

// Phase 9 — API, Integration delivery, Audit and endpoint seams
var t170Failures = CommandFingerprintTests.Run();
Console.WriteLine($"T170: cases={CommandFingerprintTests.TestCount}; assertions={CommandFingerprintTests.AssertionCount}; failures={CommandFingerprintTests.FailureCount}");
failures.AddRange(t170Failures);
var t171Failures = CommandIdempotencyDomainTests.Run();
Console.WriteLine($"T171: cases={CommandIdempotencyDomainTests.TestCount}; assertions={CommandIdempotencyDomainTests.AssertionCount}; failures={CommandIdempotencyDomainTests.FailureCount}");
failures.AddRange(t171Failures);
var t172Failures = await IdempotentCommandExecutorTests.Run();
Console.WriteLine($"T172: cases={IdempotentCommandExecutorTests.TestCount}; assertions={IdempotentCommandExecutorTests.AssertionCount}; failures={IdempotentCommandExecutorTests.FailureCount}");
failures.AddRange(t172Failures);
var t173Failures = await DeliveryRepositoryContractTests.Run();
Console.WriteLine($"T173: cases={DeliveryRepositoryContractTests.TestCount}; assertions={DeliveryRepositoryContractTests.AssertionCount}; failures={DeliveryRepositoryContractTests.FailureCount}");
failures.AddRange(t173Failures);
var t174Failures = await OutboxDispatcherTests.Run();
Console.WriteLine($"T174: cases={OutboxDispatcherTests.TestCount}; assertions={OutboxDispatcherTests.AssertionCount}; failures={OutboxDispatcherTests.FailureCount}");
failures.AddRange(t174Failures);
var t175Failures = await AuditConsumerTests.Run();
Console.WriteLine($"T175: cases={AuditConsumerTests.TestCount}; assertions={AuditConsumerTests.AssertionCount}; failures={AuditConsumerTests.FailureCount}");
failures.AddRange(t175Failures);
var t176Failures = await AuditQueryTests.Run();
Console.WriteLine($"T176: cases={AuditQueryTests.TestCount}; assertions={AuditQueryTests.AssertionCount}; failures={AuditQueryTests.FailureCount}");
failures.AddRange(t176Failures);
var t177Failures = await AuditDeliveryJobsTests.Run();
Console.WriteLine($"T177: cases={AuditDeliveryJobsTests.TestCount}; assertions={AuditDeliveryJobsTests.AssertionCount}; failures={AuditDeliveryJobsTests.FailureCount}");
failures.AddRange(t177Failures);
var t178Failures = ConfigurationEndpointTests.Run();
Console.WriteLine($"T178: cases={ConfigurationEndpointTests.TestCount}; assertions={ConfigurationEndpointTests.AssertionCount}; failures={ConfigurationEndpointTests.FailureCount}");
failures.AddRange(t178Failures);
var t179Failures = SimulatorEndpointTests.Run();
Console.WriteLine($"T179: cases={SimulatorEndpointTests.TestCount}; assertions={SimulatorEndpointTests.AssertionCount}; failures={SimulatorEndpointTests.FailureCount}");
failures.AddRange(t179Failures);
var t180Failures = TelemetryQueryEndpointTests.Run();
Console.WriteLine($"T180: cases={TelemetryQueryEndpointTests.TestCount}; assertions={TelemetryQueryEndpointTests.AssertionCount}; failures={TelemetryQueryEndpointTests.FailureCount}");
failures.AddRange(t180Failures);
var t181Failures = AuditEndpointTests.Run();
Console.WriteLine($"T181: cases={AuditEndpointTests.TestCount}; assertions={AuditEndpointTests.AssertionCount}; failures={AuditEndpointTests.FailureCount}");
failures.AddRange(t181Failures);

// T028: executable repository contract tests against the deterministic fake
var cmdRepo = new FakeIamCommandRepository();
cmdRepo.SeedCapability(new Capability(CapabilityId.New(), "AUDIT_READ", "Audit Review"));
var sessionRepo = new FakeIamPrincipalSessionRepository();
var runner = new IamRepositoryContractRunner(cmdRepo, sessionRepo);
await runner.RunAllAsync();
failures.AddRange(runner.Failures);

if (failures.Count > 0)
{
    Console.Error.WriteLine("FAILURES:");
    foreach (var f in failures)
    {
        Console.Error.WriteLine($"  {f}");
    }
    return 1;
}

Console.WriteLine("PASS: all tests");
return 0;

static List<string> R0CorrelationIdTests()
{
    var f = new List<string>();

    var supplied = CorrelationId.Create("r0-correlation-123");
    if (supplied.Value != "r0-correlation-123")
        f.Add("A valid supplied correlation ID must be preserved.");

    var blank = CorrelationId.Create("   ");
    if (!Guid.TryParse(blank.Value, out _))
        f.Add("A blank correlation ID must be replaced by a server-generated GUID.");

    var unsafeValue = CorrelationId.Create("unsafe\r\nvalue");
    if (!Guid.TryParse(unsafeValue.Value, out _))
        f.Add("A correlation ID with control characters must be replaced.");

    var tooLong = CorrelationId.Create(new string('a', 129));
    if (!Guid.TryParse(tooLong.Value, out _))
        f.Add("A correlation ID longer than 128 characters must be replaced.");

    return f;
}
