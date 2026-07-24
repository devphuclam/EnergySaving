using System.Collections.Concurrent;

namespace IUMP.Api;

public sealed class AuthenticationPolicy
{
    private readonly int _maxAttempts;
    private readonly int _windowSeconds;
    private readonly ConcurrentDictionary<string, List<DateTime>> _attempts = new();
    private readonly object _lock = new();

    public AuthenticationPolicy(int maxAttempts = 5, int windowSeconds = 15)
    {
        _maxAttempts = maxAttempts;
        _windowSeconds = windowSeconds;
    }

    public bool IsRateLimited(string normalizedUsername, DateTime now)
    {
        if (!_attempts.TryGetValue(normalizedUsername, out var attempts))
            return false;

        lock (_lock)
        {
            var windowStart = now.AddSeconds(-_windowSeconds);
            attempts.RemoveAll(a => a < windowStart);
            return attempts.Count >= _maxAttempts;
        }
    }

    public void RecordFailedAttempt(string normalizedUsername, DateTime now)
    {
        var attempts = _attempts.GetOrAdd(normalizedUsername, _ => new List<DateTime>());
        lock (_lock)
        {
            var windowStart = now.AddSeconds(-_windowSeconds);
            attempts.RemoveAll(a => a < windowStart);
            attempts.Add(now);
        }
    }

    public void RecordSuccessfulAttempt(string normalizedUsername)
    {
        _attempts.TryRemove(normalizedUsername, out _);
    }

    public static readonly string PublicError = "Authentication failed.";
}

public static class AuthSecurityOptions
{
    public const int RateLimitWindowSeconds = 15;
    public const int MaxAttemptsPerWindow = 5;

    public const string LoginError = "Authentication failed.";
}
