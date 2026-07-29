using System.Text;

namespace IUMP.Modules.Integration.Contracts;

public enum CommandIdempotencyStatus { Pending, Completed }

public static class CommandOperationCodes
{
    public const string CreateSite = "Organization.CreateSite.v1";
    public const string UpdateSite = "Organization.UpdateSite.v1";
    public const string StartSimulator = "Simulator.Start.v1";
    public const string PauseSimulator = "Simulator.Pause.v1";
    public const string ResumeSimulator = "Simulator.Resume.v1";
    public const string StopSimulator = "Simulator.Stop.v1";

    private static readonly IReadOnlySet<string> AllCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        CreateSite, UpdateSite, StartSimulator, PauseSimulator, ResumeSimulator, StopSimulator
    };

    public static bool IsKnown(string operationCode) => AllCodes.Contains(operationCode);
}

public sealed record CommandIdentity
{
    public Guid CallerUserId { get; }
    public string OperationCode { get; }
    public string IdempotencyKey { get; }

    public CommandIdentity(Guid callerUserId, string operationCode, string idempotencyKey)
    {
        if (callerUserId == Guid.Empty) throw new ArgumentException("Caller is required.", nameof(callerUserId));
        if (string.IsNullOrWhiteSpace(operationCode) || !operationCode.EndsWith(".v1", StringComparison.Ordinal))
            throw new ArgumentException("OperationCode must be a stable versioned code.", nameof(operationCode));
        if (string.IsNullOrWhiteSpace(idempotencyKey) || Encoding.UTF8.GetByteCount(idempotencyKey) > 128)
            throw new ArgumentException("Idempotency-Key must be 1..128 UTF-8 bytes.", nameof(idempotencyKey));
        CallerUserId = callerUserId;
        OperationCode = operationCode;
        IdempotencyKey = idempotencyKey;
    }
}

public sealed record StoredHttpResult
{
    public int StatusCode { get; }
    public string Body { get; }
    public string? ResourceReference { get; }
    public string? Location { get; }
    public string? ETag { get; }
    public string? OriginalCorrelationId { get; }

    public StoredHttpResult(int statusCode, string body, string? resourceReference = null,
        string? location = null, string? etag = null, string? originalCorrelationId = null)
    {
        if (statusCode is < 100 or > 599) throw new ArgumentOutOfRangeException(nameof(statusCode));
        if (body is null) throw new ArgumentNullException(nameof(body));
        StatusCode = statusCode; Body = body; ResourceReference = resourceReference; Location = location;
        ETag = etag; OriginalCorrelationId = originalCorrelationId;
    }
}

public sealed record CommandIdempotencyRecord(
    Guid Id,
    CommandIdentity Identity,
    byte[] Fingerprint,
    CommandIdempotencyStatus Status,
    string? PendingOwner,
    DateTime? PendingUntilUtc,
    int AttemptCount,
    StoredHttpResult? OriginalResult,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime ExpiresAtUtc,
    long Version)
{
    public static CommandIdempotencyRecord Pending(CommandIdentity identity, byte[] fingerprint, DateTime pendingUntilUtc,
        string? owner = null, DateTime? nowUtc = null)
    {
        ValidateFingerprint(fingerprint);
        var now = nowUtc ?? DateTime.UtcNow;
        return new(Guid.NewGuid(), identity, fingerprint.ToArray(), CommandIdempotencyStatus.Pending, owner,
            pendingUntilUtc.ToUniversalTime(), 0, null, now, now, null, now.AddHours(24), 1);
    }

    public CommandIdempotencyRecord Complete(int statusCode, string body, string? resourceReference, DateTime expiresAtUtc,
        string? location = null, string? etag = null, string? correlationId = null, DateTime? nowUtc = null)
    {
        if (Status == CommandIdempotencyStatus.Completed) return this;
        var now = nowUtc ?? DateTime.UtcNow;
        return this with
        {
            Status = CommandIdempotencyStatus.Completed,
            PendingOwner = null,
            PendingUntilUtc = null,
            OriginalResult = new StoredHttpResult(statusCode, body, resourceReference, location, etag, correlationId),
            CompletedAtUtc = now,
            ExpiresAtUtc = expiresAtUtc.ToUniversalTime(),
            UpdatedAtUtc = now,
            Version = Version + 1
        };
    }

    public bool IsLeaseLive(DateTime nowUtc) => Status == CommandIdempotencyStatus.Pending &&
        PendingUntilUtc.HasValue && PendingUntilUtc.Value > nowUtc.ToUniversalTime();

    public int? OriginalHttpStatus => OriginalResult?.StatusCode;

    public bool IsExpired(DateTime nowUtc) => ExpiresAtUtc <= nowUtc.ToUniversalTime();

    private static void ValidateFingerprint(byte[] value)
    {
        if (value is null || value.Length != 32) throw new ArgumentException("Fingerprint must be SHA-256.", nameof(value));
    }
}

public sealed record CommandRegistrationResult(CommandIdempotencyRecord Record, bool Created, bool Equivalent, bool Conflict,
    bool InProgress)
{
    public string Code => Conflict ? "IDEMPOTENCY_CONFLICT" : InProgress ? "IDEMPOTENCY_IN_PROGRESS" :
        Equivalent ? "DUPLICATE" : Created ? "REGISTERED" : "UNKNOWN";
}
