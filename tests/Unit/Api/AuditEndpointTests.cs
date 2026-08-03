using IUMP.Api;
using IUMP.Api.Infrastructure;
using IUMP.Modules.Audit.Contracts;
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
        // Administrator global query with cursor and page size
        var query = new FakeAuditQueryPort();
        var admin = new ServerPrincipal(Guid.NewGuid(), "administrator", new HashSet<string>(), new HashSet<string>(), true);
        var adminRequest = new DefaultHttpContext().Request;
        var validCursor = new AuditKeysetCursor(DateTime.UtcNow, Guid.NewGuid()).Encode();
        adminRequest.QueryString = new QueryString($"?objectType=Point&cursor={validCursor}&pageSize=20");
        var adminResult = await AuditEndpoints.QueryAsync(adminRequest, query, new FakeServerPrincipalAccessor(admin), CancellationToken.None);
        assertions++; if (query.LastPrincipal?.UserId != admin.UserId || query.LastCursor != validCursor || query.LastFilters!["objectType"] != "Point" || query.LastPageSize != 20)
            failures.Add("Admin audit handler must invoke query port with exact filters, cursor and page size");
        // Scoped AUDIT_READ query
        var scoped = new ServerPrincipal(Guid.NewGuid(), "manager", new HashSet<string> { Guid.NewGuid().ToString("D") }, new HashSet<string>(), false);
        var scopedRequest = new DefaultHttpContext().Request;
        scopedRequest.QueryString = new QueryString("?pageSize=5");
        var scopedResult = await AuditEndpoints.QueryAsync(scopedRequest, query, new FakeServerPrincipalAccessor(scoped), CancellationToken.None);
        assertions++; if (query.LastPrincipal?.UserId != scoped.UserId || query.LastPageSize != 5)
            failures.Add("Scoped audit handler must forward principal and page size");
        // Forbidden result from query port
        var forbiddenQuery = new FakeAuditQueryPort(returnForbidden: true);
        var forbiddenRequest = new DefaultHttpContext().Request;
        forbiddenRequest.QueryString = new QueryString("?objectType=Site");
        var forbiddenResult = await AuditEndpoints.QueryAsync(forbiddenRequest, forbiddenQuery, new FakeServerPrincipalAccessor(admin), CancellationToken.None);
        assertions++; if (forbiddenResult is not Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult forbiddenProblem ||
            forbiddenProblem.StatusCode != StatusCodes.Status403Forbidden)
            failures.Add("Audit handler must return 403 when query port returns FORBIDDEN");
        // Unauthorized without principal
        var unauth = await AuditEndpoints.QueryAsync(adminRequest, query, new FakeServerPrincipalAccessor(null), CancellationToken.None);
        assertions++; if (unauth is not Microsoft.AspNetCore.Http.HttpResults.UnauthorizedHttpResult) failures.Add("Audit handler must return unauthorized without server principal");
        // Default page size
        query = new FakeAuditQueryPort();
        var defaultRequest = new DefaultHttpContext().Request;
        var defaultResult = await AuditEndpoints.QueryAsync(defaultRequest, query, new FakeServerPrincipalAccessor(admin), CancellationToken.None);
        assertions++; if (query.LastPageSize != 50) failures.Add("Default page size must be 50");
        // Invalid page sizes are request errors, not silent defaults or clamps.
        foreach (var value in new[] { "not-an-integer", "0", "-1", "101" })
        {
            var invalidQuery = new FakeAuditQueryPort();
            var invalidRequest = new DefaultHttpContext().Request;
            invalidRequest.QueryString = new QueryString($"?pageSize={value}");
            var invalidResult = await AuditEndpoints.QueryAsync(
                invalidRequest, invalidQuery, new FakeServerPrincipalAccessor(admin), CancellationToken.None);
            assertions++;
            if (invalidResult is not Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult problem ||
                problem.StatusCode != StatusCodes.Status422UnprocessableEntity || invalidQuery.QueryCount != 0)
                failures.Add($"Invalid pageSize={value} must return HTTP 422 without invoking the query port");
        }
        // A malformed cursor is rejected before the query port is allowed to treat it as a first page.
        var malformedCursorQuery = new FakeAuditQueryPort();
        var malformedCursorRequest = new DefaultHttpContext().Request;
        malformedCursorRequest.QueryString = new QueryString("?cursor=not-a-cursor");
        var malformedCursorResult = await AuditEndpoints.QueryAsync(
            malformedCursorRequest, malformedCursorQuery, new FakeServerPrincipalAccessor(admin), CancellationToken.None);
        assertions++;
        if (malformedCursorResult is not Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult cursorProblem ||
            cursorProblem.StatusCode != StatusCodes.Status422UnprocessableEntity || malformedCursorQuery.QueryCount != 0)
            failures.Add("Malformed cursor must return HTTP 422 without invoking the query port");
        TestCount = assertions; AssertionCount = assertions;
        FailureCount = failures.Count;
        return failures;
    }
}
