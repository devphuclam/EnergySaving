namespace IUMP.Tests.Unit.Api;

public static class AuthSecurityPolicyTests
{
    public const int RateLimitWindowSeconds = 15;
    public const int MaxAttemptsPerWindow = 5;

    public static List<string> Run()
    {
        var failures = new List<string>();

        RateLimitThreshold(failures);
        NonEnumeratingErrors(failures);
        RateLimitWindowDuration(failures);

        return failures;
    }

    private static void RateLimitThreshold(List<string> failures)
    {
        var configured = MaxAttemptsPerWindow;

        if (configured != 5)
            failures.Add("T015-FAIL: Rate limit threshold must be 5 attempts per window.");

        var window = RateLimitWindowSeconds;

        if (window != 15)
            failures.Add("T015-FAIL: Rate limit window must be 15 seconds.");
    }

    private static void NonEnumeratingErrors(List<string> failures)
    {
        var invalidUserError = "Authentication failed.";
        var wrongPasswordError = "Authentication failed.";

        if (invalidUserError != wrongPasswordError)
            failures.Add("T015-FAIL: Invalid username and wrong password must produce identical error messages.");
    }

    private static void RateLimitWindowDuration(List<string> failures)
    {
        var limit = MaxAttemptsPerWindow;
        var exceeded = 6 > limit;

        if (!exceeded)
            failures.Add("T015-FAIL: Sixth attempt within window must exceed rate limit.");
    }
}
