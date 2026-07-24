using IUMP.BuildingBlocks.Correlation;
using IUMP.Modules.IAM.Domain;
using IUMP.Tests.Unit.IAM;
using IUMP.Tests.Unit.Api;
using IUMP.Tests.Integration.IAM;
using IUMP.Tests.Unit.Fakes;

var failures = new List<string>();

failures.AddRange(R0CorrelationIdTests());

failures.AddRange(IamDomainTests.Run());
failures.AddRange(AuthorizationPolicyTests.Run());
failures.AddRange(SessionPolicyTests.Run());
failures.AddRange(await PocIdentityFixtureTests.Run());
failures.AddRange(AuthSecurityPolicyTests.Run());
failures.AddRange(AuthEndpointTests.Run());

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
