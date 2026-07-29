using IUMP.Api;
using IUMP.Api.Infrastructure;
using IUMP.Tests.Unit.Fakes;
using Microsoft.AspNetCore.Http;

namespace IUMP.Tests.Unit.Api;

public static class AuditEndpointTests
{
    public static int TestCount { get; private set; }
    public static int AssertionCount { get; private set; }
    public static int FailureCount { get; private set; }

    public static async Task<List<string>> Run()
    {
        var failures = AuditEndpointPolicy.RequiredCapability == "AUDIT_READ"
            ? new List<string>() : new List<string> { "Audit endpoint must require AUDIT_READ" };
        var assertions = 1;
        assertions++; if (!AuditEndpointPolicy.Route.Contains("audit-events", StringComparison.Ordinal)) failures.Add("Audit route missing");
        // Handler delegates to the scoped AuditQueryService port and never trusts a client capability header.
        var query = new FakeAuditQueryPort();
        var principal = new ServerPrincipal(Guid.NewGuid(), "administrator", new HashSet<string>(), new HashSet<string>(), true);
        var request = new DefaultHttpContext().Request;
        request.QueryString = new QueryString("?objectType=Point&cursor=cursor-1&pageSize=20");
        var result = await AuditEndpoints.QueryAsync(request, query, new FakeServerPrincipalAccessor(principal), CancellationToken.None);
        assertions++; if (result is not IResult || query.LastPrincipal?.UserId != principal.UserId || query.LastCursor != "cursor-1" || query.LastFilters!["objectType"] != "Point") failures.Add("Audit handler must invoke scoped query port and forward filters/cursor");
        var forbidden = await AuditEndpoints.QueryAsync(request, query, new FakeServerPrincipalAccessor(null), CancellationToken.None);
        assertions++; if (forbidden is not Microsoft.AspNetCore.Http.HttpResults.UnauthorizedHttpResult) failures.Add("Audit handler must return forbidden/unauthorized without server principal");
        TestCount = 4; AssertionCount = assertions;
        FailureCount = failures.Count;
        return failures;
    }
}
