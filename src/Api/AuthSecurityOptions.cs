using System.Threading.RateLimiting;

namespace IUMP.Api;

public static class AuthSecurityOptions
{
    public const int RateLimitWindowSeconds = 15;
    public const int MaxAttemptsPerWindow = 5;

    public static void ConfigureRateLimiting(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy("login", context =>
                RateLimitPartition.GetTokenBucketLimiter(
                    $"{context.Connection.RemoteIpAddress}_login",
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = MaxAttemptsPerWindow,
                        TokensPerPeriod = MaxAttemptsPerWindow,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(RateLimitWindowSeconds),
                        QueueLimit = 0
                    }));
        });
    }

    public const string LoginError = "Authentication failed.";
}
