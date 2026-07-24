using IUMP.Modules.IAM.Domain;
using IUMP.Modules.IAM.Contracts;

namespace IUMP.Modules.IAM.Application;

public interface IPocCredentialHashProvider
{
    string GetPasswordHash(string username);
}

public interface IPocIdentityFixture
{
    IReadOnlyList<User> GetDeterministicUsers();
    bool IsFixtureEnabled { get; }
    Task<bool> ApplyPostSiteFixtureAsync(Guid siteId, CancellationToken ct = default);
    Task<bool> ApplyPostAreaFixtureAsync(Guid siteId, Guid areaId, CancellationToken ct = default);
}

public sealed class PocIdentityFixture : IPocIdentityFixture
{
    private readonly IIamCommandRepository _repository;
    private readonly IPocCredentialHashProvider _hashProvider;
    private readonly bool _enabled;

    public PocIdentityFixture(IIamCommandRepository repository)
        : this(repository, new NullPocCredentialHashProvider(), false)
    {
    }

    public PocIdentityFixture(IIamCommandRepository repository,
        IPocCredentialHashProvider hashProvider, bool enabled = false)
    {
        _repository = repository;
        _hashProvider = hashProvider;
        _enabled = enabled;
    }

    public IReadOnlyList<User> GetDeterministicUsers()
    {
        var hash = _hashProvider.GetPasswordHash("poc-placeholder");
        if (string.IsNullOrWhiteSpace(hash))
            return Array.Empty<User>();

        return CreateUsers(hash);
    }

    public bool IsFixtureEnabled => _enabled;

    public async Task<bool> ApplyPostSiteFixtureAsync(Guid siteId, CancellationToken ct = default)
    {
        if (!IsFixtureEnabled || siteId == Guid.Empty)
            return false;

        var hash = _hashProvider.GetPasswordHash("poc-placeholder");
        if (string.IsNullOrWhiteSpace(hash))
            return false;

        var users = CreateUsers(hash);
        var tx = await _repository.BeginTransactionAsync(ct);

        try
        {
            foreach (var user in users)
            {
                var existing = await _repository.FindUserByUsernameAsync(user.Username, ct);
                if (existing == null)
                    await _repository.AddUserAsync(user, ct);

                foreach (var role in user.Roles)
                {
                    var existingRoles = await _repository.GetRolesForUserAsync(user.Id, ct);
                    if (!existingRoles.Contains(role))
                        await _repository.AssignRoleAsync(user.Id, role, user.Id, ct);
                }

                var scopedRoles = new[] { Role.Engineer, Role.Operator, Role.Manager, Role.Viewer };
                if (scopedRoles.Contains(user.Roles[0]))
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
                            UserCapabilityId.New(), user.Id, auditRead.Id, user.Id,
                            DateTime.UtcNow, 1);
                        await _repository.AddUserCapabilityAsync(uc, ct);
                    }
                }
            }

            await tx.CommitAsync(ct);
            return true;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            return false;
        }
    }

    public async Task<bool> ApplyPostAreaFixtureAsync(Guid siteId, Guid areaId, CancellationToken ct = default)
    {
        if (!IsFixtureEnabled || siteId == Guid.Empty || areaId == Guid.Empty)
            return false;

        var users = GetDeterministicUsers();
        if (users.Count == 0)
            return false;

        var tx = await _repository.BeginTransactionAsync(ct);

        try
        {
            foreach (var user in users)
            {
                var existingScopes = await _repository.GetScopesForUserAsync(user.Id, ct);
                var siteScope = existingScopes.FirstOrDefault(s => s.SiteId == siteId && s.AreaId == null);
                if (siteScope != null)
                {
                    if (!existingScopes.Any(s => s.SiteId == siteId && s.AreaId == areaId))
                    {
                        var areaScope = new Scope(ScopeId.New(), user.Id, siteId, areaId);
                        await _repository.AddScopeAsync(areaScope, ct);
                    }
                }
            }

            await tx.CommitAsync(ct);
            return true;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            return false;
        }
    }

    private static IReadOnlyList<User> CreateUsers(string hash)
    {
        var adminId = UserId.Parse("00000000-0000-0000-0000-000000000001");
        var engineerId = UserId.Parse("00000000-0000-0000-0000-000000000002");
        var operatorId = UserId.Parse("00000000-0000-0000-0000-000000000003");
        var managerId = UserId.Parse("00000000-0000-0000-0000-000000000004");
        var viewerId = UserId.Parse("00000000-0000-0000-0000-000000000005");

        return new[]
        {
            new User(adminId, "admin", hash, UserStatus.Active, new[] { Role.Administrator }),
            new User(engineerId, "engineer", hash, UserStatus.Active, new[] { Role.Engineer }),
            new User(operatorId, "operator", hash, UserStatus.Active, new[] { Role.Operator }),
            new User(managerId, "manager", hash, UserStatus.Active, new[] { Role.Manager }),
            new User(viewerId, "viewer", hash, UserStatus.Active, new[] { Role.Viewer }),
        };
    }
}

public sealed class NullPocCredentialHashProvider : IPocCredentialHashProvider
{
    public string GetPasswordHash(string username) => "";
}
