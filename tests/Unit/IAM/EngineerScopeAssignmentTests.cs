using IUMP.Modules.IAM.Application;
using IUMP.Modules.IAM.Contracts;
using IUMP.Modules.IAM.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.IAM;

public static class EngineerScopeAssignmentTests
{
    public static async Task<List<string>> Run()
    {
        var failures = new List<string>();
        var repository = new FakeIamCommandRepository();
        var admin = new User(UserId.New(), "admin-003", "test-hash", UserStatus.Active,
            new[] { Role.Administrator });
        var engineer = new User(UserId.New(), "engineer-003", "test-hash", UserStatus.Active,
            new[] { Role.Engineer });
        repository.SeedUser(admin);
        repository.SeedRole(admin.Id, Role.Administrator);
        repository.SeedUser(engineer);
        repository.SeedRole(engineer.Id, Role.Engineer);
        var siteId = Guid.NewGuid();
        var service = new EngineerScopeAssignmentService(repository, new ExistingWorkspaceSite(siteId));

        var eligible = await service.ListEligibleEngineersAsync();
        if (eligible.Count != 1 || eligible[0].UserId != engineer.Id.Value)
            failures.Add("T011: only active Engineer accounts are eligible.");

        var forbidden = await service.AssignSiteAsync(
            siteId, engineer.Id.Value, engineer.Id.Value);
        if (forbidden.Code != "FORBIDDEN")
            failures.Add("T011: non-Administrator assignment must fail closed.");

        var assigned = await service.AssignSiteAsync(
            siteId, engineer.Id.Value, admin.Id.Value);
        var replay = await service.AssignSiteAsync(
            siteId, engineer.Id.Value, admin.Id.Value);
        var scopes = await repository.GetScopesForUserAsync(engineer.Id);
        if (!assigned.IsSuccess || !replay.IsSuccess || scopes.Count(value => value.SiteId == siteId) != 1)
            failures.Add("T011: assignment and retry must produce one Site scope.");

        var areaId = Guid.NewGuid();
        var areaAssigned = await service.EnsureAreaScopeAsync(
            siteId, areaId, engineer.Id.Value);
        var areaReplay = await service.EnsureAreaScopeAsync(
            siteId, areaId, engineer.Id.Value);
        scopes = await repository.GetScopesForUserAsync(engineer.Id);
        if (!areaAssigned.IsSuccess || !areaReplay.IsSuccess ||
            scopes.Count(value =>
                value.SiteId == siteId && value.AreaId == areaId) != 1)
            failures.Add(
                "T011: assigned Engineer must receive one duplicate-safe Area scope.");

        return failures;
    }

    private sealed class ExistingWorkspaceSite(Guid siteId) : IWorkspaceSiteExistence
    {
        public Task<bool> ExistsAsync(Guid candidate, CancellationToken ct = default) =>
            Task.FromResult(candidate == siteId);
    }
}
