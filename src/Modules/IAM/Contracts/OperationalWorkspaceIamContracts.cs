namespace IUMP.Modules.IAM.Contracts;

public interface IWorkspaceSiteExistence
{
    Task<bool> ExistsAsync(Guid siteId, CancellationToken ct = default);
}

public sealed record EligibleEngineer(
    Guid UserId,
    string Username,
    string Status,
    IReadOnlyList<Guid> AssignedSiteIds);

public sealed record EngineerScopeAssignmentResult(bool IsSuccess, string Code)
{
    public static EngineerScopeAssignmentResult Success(string code = "ASSIGNED") => new(true, code);
    public static EngineerScopeAssignmentResult Failure(string code) => new(false, code);
}

public interface IEngineerScopeAssignmentService
{
    Task<IReadOnlyList<EligibleEngineer>> ListEligibleEngineersAsync(
        CancellationToken ct = default);
    Task<EngineerScopeAssignmentResult> AssignSiteAsync(
        Guid siteId,
        Guid engineerUserId,
        Guid actorUserId,
        CancellationToken ct = default);
    Task<EngineerScopeAssignmentResult> EnsureAreaScopeAsync(
        Guid siteId,
        Guid areaId,
        Guid engineerUserId,
        CancellationToken ct = default);
}
