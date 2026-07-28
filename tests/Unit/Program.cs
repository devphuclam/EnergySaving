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
