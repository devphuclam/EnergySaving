using IUMP.Api;

namespace IUMP.Tests.Unit.Api;

public static class AuditEndpointTests
{
    public static List<string> Run() =>
        AuditEndpointPolicy.RequiredCapability == "AUDIT_READ"
            ? new List<string>() : new List<string> { "Audit endpoint must require AUDIT_READ" };
}
