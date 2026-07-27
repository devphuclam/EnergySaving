namespace IUMP.Modules.Catalog.Domain;

public sealed record Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Code { get; }
    public string? Error { get; }

    private Result(bool isSuccess, string code, string? error)
    {
        IsSuccess = isSuccess;
        Code = code;
        Error = error;
    }

    public static Result Success() => new(true, string.Empty, null);
    public static Result Failure(string code, string? error) => new(false, code, error);
}

public sealed record CatalogDependencySnapshot(
    bool MappingUsage = false,
    bool SimulatorRun = false,
    bool Measurement = false,
    bool CurrentProjection = false,
    bool ScheduledJob = false,
    bool OtherBusinessReference = false,
    bool AuditOnlySnapshot = false)
{
    public bool HasOperationalDependency => MappingUsage || SimulatorRun || Measurement ||
        CurrentProjection || ScheduledJob || OtherBusinessReference;
}

public sealed record CatalogDeletionDecision(bool IsAllowed, string Code, string? Error)
{
    public static CatalogDeletionDecision Allowed() => new(true, "DELETED", null);
    public static CatalogDeletionDecision DependentHistory(string? detail = null) =>
        new(false, "DEPENDENT_HISTORY", detail ?? "Operational history prevents hard deletion.");
    public static CatalogDeletionDecision NotFound() => new(false, "NotFound", "Catalog object was not found.");
    public static CatalogDeletionDecision InvalidState(string detail) => new(false, "InvalidState", detail);
}
