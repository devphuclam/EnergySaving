using IUMP.Modules.IAM.Domain;
using IUMP.Modules.IAM.Contracts;

namespace IUMP.Modules.IAM.Application;

public interface IPocIdentityFixture
{
    IReadOnlyList<User> GetDeterministicUsers();
    bool IsFixtureEnabled { get; }
    Task<bool> ApplyPostSiteFixtureAsync(Guid siteId, CancellationToken ct = default);
    Task<bool> ApplyPostAreaFixtureAsync(Guid siteId, Guid areaId, CancellationToken ct = default);
}

public sealed class PocIdentityFixture : IPocIdentityFixture
{
    private static readonly IReadOnlyList<User> DeterministicUsers = CreateUsers();
    private static readonly Role[] AllRoles = { Role.Administrator, Role.Engineer, Role.Operator, Role.Manager, Role.Viewer };
    private static readonly Role[] ScopedRoles = { Role.Engineer, Role.Operator, Role.Manager, Role.Viewer };

    private readonly IIamCommandRepository _repository;

    public PocIdentityFixture(IIamCommandRepository repository)
    {
        _repository = repository;
    }

    public IReadOnlyList<User> GetDeterministicUsers() => DeterministicUsers;
    public bool IsFixtureEnabled { get; } = true;

    public async Task<bool> ApplyPostSiteFixtureAsync(Guid siteId, CancellationToken ct = default)
    {
        if (!IsFixtureEnabled || siteId == Guid.Empty)
            return false;

        foreach (var user in DeterministicUsers)
        {
            var existing = await _repository.FindUserByUsernameAsync(user.Username, ct);
            if (existing == null)
            {
                await _repository.AddUserAsync(user, ct);
            }

            foreach (var role in user.Roles)
            {
                var existingRoles = await _repository.GetRolesForUserAsync(user.Id, ct);
                if (!existingRoles.Contains(role))
                {
                    await _repository.AssignRoleAsync(user.Id, role, user.Id, ct);
                }
            }

            if (ScopedRoles.Contains(user.Roles[0]))
            {
                var existingScopes = await _repository.GetScopesForUserAsync(user.Id, ct);
                if (!existingScopes.Any(s => s.SiteId == siteId))
                {
                    var scope = new Scope(ScopeId.New(), user.Id, siteId, null);
                    await _repository.AddScopeAsync(scope, ct);
                }
            }

            if (user.Roles.Contains(Role.Manager))
            {
                var caps = await _repository.GetActiveCapabilitiesForUserAsync(user.Id, ct);
                var allCaps = await _repository.GetAllCapabilitiesAsync(ct);
                var auditRead = allCaps.FirstOrDefault(c => c.Code == "AUDIT_READ");
                if (auditRead != null && !caps.Any(uc => uc.CapabilityId == auditRead.Id))
                {
                    var uc = new UserCapability(
                        UserCapabilityId.New(),
                        user.Id,
                        auditRead.Id,
                        user.Id,
                        DateTime.UtcNow,
                        1);
                    await _repository.AddUserCapabilityAsync(uc, ct);
                }
            }
        }

        return true;
    }

    public async Task<bool> ApplyPostAreaFixtureAsync(Guid siteId, Guid areaId, CancellationToken ct = default)
    {
        if (!IsFixtureEnabled || siteId == Guid.Empty || areaId == Guid.Empty)
            return false;

        foreach (var user in DeterministicUsers)
        {
            var existingScopes = await _repository.GetScopesForUserAsync(user.Id, ct);
            var siteScope = existingScopes.FirstOrDefault(s => s.SiteId == siteId && s.AreaId == null);
            if (siteScope != null)
            {
                var areaScope = new Scope(ScopeId.New(), user.Id, siteId, areaId);
                if (!existingScopes.Any(s => s.SiteId == siteId && s.AreaId == areaId))
                {
                    await _repository.AddScopeAsync(areaScope, ct);
                }
            }
        }

        return true;
    }

    private static IReadOnlyList<User> CreateUsers()
    {
        var adminId = UserId.Parse("00000000-0000-0000-0000-000000000001");
        var engineerId = UserId.Parse("00000000-0000-0000-0000-000000000002");
        var operatorId = UserId.Parse("00000000-0000-0000-0000-000000000003");
        var managerId = UserId.Parse("00000000-0000-0000-0000-000000000004");
        var viewerId = UserId.Parse("00000000-0000-0000-0000-000000000005");

        return new[]
        {
            new User(adminId, "admin", "AQAAAAIAAYagAAAAEJ7U8Kx8mHs5jKZy6JqV0c7f3e2d1b9a4c5d6e7f8g9h0i1j2k3l4m5n6o7p8q9r0s1t2u3v4w5x6y7z8", UserStatus.Active, new[] { Role.Administrator }),
            new User(engineerId, "engineer", "AQAAAAIAAYagAAAAEJ7U8Kx8mHs5jKZy6JqV0c7f3e2d1b9a4c5d6e7f8g9h0i1j2k3l4m5n6o7p8q9r0s1t2u3v4w5x6y7z8", UserStatus.Active, new[] { Role.Engineer }),
            new User(operatorId, "operator", "AQAAAAIAAYagAAAAEJ7U8Kx8mHs5jKZy6JqV0c7f3e2d1b9a4c5d6e7f8g9h0i1j2k3l4m5n6o7p8q9r0s1t2u3v4w5x6y7z8", UserStatus.Active, new[] { Role.Operator }),
            new User(managerId, "manager", "AQAAAAIAAYagAAAAEJ7U8Kx8mHs5jKZy6JqV0c7f3e2d1b9a4c5d6e7f8g9h0i1j2k3l4m5n6o7p8q9r0s1t2u3v4w5x6y7z8", UserStatus.Active, new[] { Role.Manager }),
            new User(viewerId, "viewer", "AQAAAAIAAYagAAAAEJ7U8Kx8mHs5jKZy6JqV0c7f3e2d1b9a4c5d6e7f8g9h0i1j2k3l4m5n6o7p8q9r0s1t2u3v4w5x6y7z8", UserStatus.Active, new[] { Role.Viewer }),
        };
    }
}
