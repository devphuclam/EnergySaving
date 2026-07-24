namespace IUMP.Tests.Unit.Api;

public static class AuthSecurityPolicyTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();

        IsRateLimitedAfterFiveAttempts(failures);
        SixthAttemptRejected(failures);
        WindowReset(failures);

        return failures;
    }

    private static void IsRateLimitedAfterFiveAttempts(List<string> failures)
    {
        var policy = new IUMP.Api.AuthenticationPolicy(maxAttempts: 5, windowSeconds: 15);
        var now = new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);
        var username = "ratelimit-test";

        for (int i = 0; i < 5; i++)
            policy.RecordFailedAttempt(username, now);

        if (!policy.IsRateLimited(username, now))
            failures.Add("T015-FAIL: After 5 failed attempts, the username must be rate-limited.");
    }

    private static void SixthAttemptRejected(List<string> failures)
    {
        var policy = new IUMP.Api.AuthenticationPolicy(maxAttempts: 5, windowSeconds: 15);
        var now = new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);
        var username = "sixth-attempt";

        for (int i = 0; i < 5; i++)
            policy.RecordFailedAttempt(username, now.AddSeconds(i));

        var limited = policy.IsRateLimited(username, now.AddSeconds(5));
        if (!limited)
            failures.Add("T015-FAIL: Five failed attempts within window must be rate-limited.");
    }

    private static void WindowReset(List<string> failures)
    {
        var policy = new IUMP.Api.AuthenticationPolicy(maxAttempts: 5, windowSeconds: 15);
        var start = new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);
        var username = "window-reset";

        for (int i = 0; i < 5; i++)
            policy.RecordFailedAttempt(username, start.AddSeconds(i));

        if (!policy.IsRateLimited(username, start.AddSeconds(5)))
            failures.Add("T015-FAIL: Five attempts within window must be rate-limited.");

        var afterWindow = start.AddSeconds(20);
        if (policy.IsRateLimited(username, afterWindow))
            failures.Add("T015-FAIL: Rate limit must reset after the 15-second window.");
    }
}
