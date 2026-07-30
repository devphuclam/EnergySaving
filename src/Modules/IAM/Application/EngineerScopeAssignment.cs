using IUMP.Modules.IAM.Contracts;
using IUMP.Modules.IAM.Domain;

namespace IUMP.Modules.IAM.Application;

public sealed class EngineerScopeAssignmentService(
    IIamCommandRepository repository,
    IWorkspaceSiteExistence sites) : IEngineerScopeAssignmentService
{
    public async Task<IReadOnlyList<EligibleEngineer>> ListEligibleEngineersAsync(
        CancellationToken ct = default)
    {
        var results = new List<EligibleEngineer>();
        foreach (var user in await repository.GetAllUsersAsync(ct))
        {
            if (user.Status != UserStatus.Active || !user.HasRole(Role.Engineer))
                continue;
            var scopes = await repository.GetScopesForUserAsync(user.Id, ct);
            results.Add(new EligibleEngineer(
                user.Id.Value,
                user.Username,
                user.Status.ToString(),
                scopes.Where(scope => scope.SiteId.HasValue)
                    .Select(scope => scope.SiteId!.Value)
                    .Distinct()
                    .ToArray()));
        }
        return results;
    }

    public async Task<EngineerScopeAssignmentResult> AssignSiteAsync(
        Guid siteId,
        Guid engineerUserId,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        var actor = await repository.GetUserAsync(
            new UserId(actorUserId), ct);
        if (actor is null || actor.Status != UserStatus.Active ||
            !actor.HasRole(Role.Administrator))
            return EngineerScopeAssignmentResult.Failure("FORBIDDEN");
        if (siteId == Guid.Empty || engineerUserId == Guid.Empty ||
            !await sites.ExistsAsync(siteId, ct))
            return EngineerScopeAssignmentResult.Failure("NOT_FOUND");

        var user = await repository.GetUserAsync(new UserId(engineerUserId), ct);
        if (user is null)
            return EngineerScopeAssignmentResult.Failure("NOT_FOUND");
        if (user.Status != UserStatus.Active || !user.HasRole(Role.Engineer))
            return EngineerScopeAssignmentResult.Failure("ENGINEER_ASSIGNMENT_INVALID");

        var scopes = await repository.GetScopesForUserAsync(user.Id, ct);
        if (scopes.Any(scope => scope.SiteId == siteId && scope.AreaId is null))
            return EngineerScopeAssignmentResult.Success("ALREADY_ASSIGNED");

        var candidate = new Scope(
            ScopeId.New(), user.Id, siteId, null);
        await repository.AddScopeAsync(candidate, ct);
        var persisted = await repository.GetScopesForUserAsync(user.Id, ct);
        return persisted.Any(scope => scope.Id == candidate.Id)
            ? EngineerScopeAssignmentResult.Success()
            : EngineerScopeAssignmentResult.Success("ALREADY_ASSIGNED");
    }

    public async Task<EngineerScopeAssignmentResult> EnsureAreaScopeAsync(
        Guid siteId,
        Guid areaId,
        Guid engineerUserId,
        CancellationToken ct = default)
    {
        if (siteId == Guid.Empty || areaId == Guid.Empty ||
            engineerUserId == Guid.Empty)
            return EngineerScopeAssignmentResult.Failure("NOT_FOUND");
        var user = await repository.GetUserAsync(
            new UserId(engineerUserId), ct);
        if (user is null || user.Status != UserStatus.Active ||
            !user.HasRole(Role.Engineer))
            return EngineerScopeAssignmentResult.Failure(
                "ENGINEER_ASSIGNMENT_INVALID");
        var scopes = await repository.GetScopesForUserAsync(user.Id, ct);
        if (!scopes.Any(scope =>
                scope.SiteId == siteId && scope.AreaId is null))
            return EngineerScopeAssignmentResult.Failure("FORBIDDEN");
        if (scopes.Any(scope =>
                scope.SiteId == siteId && scope.AreaId == areaId))
            return EngineerScopeAssignmentResult.Success("ALREADY_ASSIGNED");
        var candidate = new Scope(
            ScopeId.New(), user.Id, siteId, areaId);
        await repository.AddScopeAsync(candidate, ct);
        var persisted = await repository.GetScopesForUserAsync(user.Id, ct);
        return persisted.Any(scope => scope.Id == candidate.Id)
            ? EngineerScopeAssignmentResult.Success()
            : EngineerScopeAssignmentResult.Success("ALREADY_ASSIGNED");
    }
}
