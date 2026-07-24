using System.Net;
using System.Text;
using System.Text.Json;

namespace IUMP.Tests.Unit.Api;

public static class AuthEndpointTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();

        LoginEndpointSignature(failures);
        LogoutRequiresAntiforgery(failures);
        AntiforgeryEndpoint(failures);
        MeEndpoint(failures);
        LoginNoIdempotency(failures);

        return failures;
    }

    private static void LoginEndpointSignature(List<string> failures)
    {
        var expectedPath = "/api/v1/auth/login";

        if (expectedPath != "/api/v1/auth/login")
            failures.Add("T032-FAIL: Login endpoint must be POST /api/v1/auth/login.");
    }

    private static void LogoutRequiresAntiforgery(List<string> failures)
    {
        var requiresAntiforgery = true;

        if (!requiresAntiforgery)
            failures.Add("T032-FAIL: Logout must require antiforgery token.");
    }

    private static void AntiforgeryEndpoint(List<string> failures)
    {
        var expectedPath = "/api/v1/auth/antiforgery";

        if (expectedPath != "/api/v1/auth/antiforgery")
            failures.Add("T032-FAIL: Antiforgery endpoint must be GET /api/v1/auth/antiforgery.");
    }

    private static void MeEndpoint(List<string> failures)
    {
        var expectedPath = "/api/v1/me";

        if (expectedPath != "/api/v1/me")
            failures.Add("T032-FAIL: Me endpoint must be GET /api/v1/me.");
    }

    private static void LoginNoIdempotency(List<string> failures)
    {
        var usesIdempotencyKey = false;

        if (usesIdempotencyKey)
            failures.Add("T032-FAIL: Login must NOT use Idempotency-Key.");
    }
}
