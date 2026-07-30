using System.Text;

namespace IUMP.Modules.Integration.Contracts;

public enum CommandIdempotencyStatus { Pending, Completed }

public static class CommandOperationCodes
{
    public const string CreateSite = "Organization.CreateSite.v1";
    public const string UpdateSite = "Organization.UpdateSite.v1";
    public const string CreateArea = "Organization.CreateArea.v1";
    public const string UpdateArea = "Organization.UpdateArea.v1";
    public const string CreateAsset = "Organization.CreateAsset.v1";
    public const string UpdateAsset = "Organization.UpdateAsset.v1";
    public const string CreatePoint = "Organization.CreatePoint.v1";
    public const string UpdatePoint = "Organization.UpdatePoint.v1";
    public const string ActivatePoint = "Organization.ActivatePoint.v1";
    public const string DeactivatePoint = "Organization.DeactivatePoint.v1";
    public const string ActivateSite = "Organization.ActivateSite.v1";
    public const string DeactivateSite = "Organization.DeactivateSite.v1";
    public const string ActivateArea = "Organization.ActivateArea.v1";
    public const string DeactivateArea = "Organization.DeactivateArea.v1";
    public const string ActivateAsset = "Organization.ActivateAsset.v1";
    public const string DeactivateAsset = "Organization.DeactivateAsset.v1";
    public const string SupersedeSite = "Organization.SupersedeSite.v1";
    public const string SupersedeArea = "Organization.SupersedeArea.v1";
    public const string SupersedeAsset = "Organization.SupersedeAsset.v1";
    public const string SupersedePoint = "Organization.SupersedePoint.v1";
    public const string CreateMetric = "Catalog.CreateMetric.v1";
    public const string UpdateMetric = "Catalog.UpdateMetric.v1";
    public const string CreateUnit = "Catalog.CreateUnit.v1";
    public const string UpdateUnit = "Catalog.UpdateUnit.v1";
    public const string SetMetricCompatibleUnits = "Catalog.SetMetricCompatibleUnits.v1";
    public const string CreateSource = "Acquisition.CreateSource.v1";
    public const string UpdateSource = "Acquisition.UpdateSource.v1";
    public const string CreateMapping = "Acquisition.CreateMapping.v1";
    public const string UpdateMapping = "Acquisition.UpdateMapping.v1";
    public const string ActivateMapping = "Acquisition.ActivateMapping.v1";
    public const string InactivateMapping = "Acquisition.InactivateMapping.v1";
    public const string SupersedeMapping = "Acquisition.SupersedeMapping.v1";
    public const string SuspendSource = "Acquisition.SuspendSource.v1";
    public const string DecommissionSource = "Acquisition.DecommissionSource.v1";
    public const string CreateSimulatorConfiguration = "Acquisition.CreateSimulatorConfiguration.v1";
    public const string UpdateSimulatorConfiguration = "Acquisition.UpdateSimulatorConfiguration.v1";
    public const string ValidateSimulatorConfiguration = "Acquisition.ValidateSimulatorConfiguration.v1";
    public const string AssignEngineerSiteScope = "IAM.AssignEngineerSiteScope.v1";
    public const string StartSimulator = "Simulator.Start.v1";
    public const string PauseSimulator = "Simulator.Pause.v1";
    public const string ResumeSimulator = "Simulator.Resume.v1";
    public const string StopSimulator = "Simulator.Stop.v1";

    private static readonly IReadOnlySet<string> AllCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        CreateSite, UpdateSite, CreateArea, UpdateArea, CreateAsset, UpdateAsset, CreatePoint, UpdatePoint,
        ActivatePoint, DeactivatePoint, ActivateSite, DeactivateSite, ActivateArea, DeactivateArea,
        ActivateAsset, DeactivateAsset, SupersedeSite, SupersedeArea, SupersedeAsset, SupersedePoint,
        CreateMetric, UpdateMetric, CreateUnit, UpdateUnit, SetMetricCompatibleUnits, CreateSource, UpdateSource,
        CreateMapping, UpdateMapping, ActivateMapping, InactivateMapping, SupersedeMapping, SuspendSource,
        DecommissionSource, CreateSimulatorConfiguration, UpdateSimulatorConfiguration,
        ValidateSimulatorConfiguration, AssignEngineerSiteScope,
        StartSimulator, PauseSimulator, ResumeSimulator, StopSimulator
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
