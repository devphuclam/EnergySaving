using IUMP.Modules.IAM.Domain;

namespace IUMP.Modules.IAM.Contracts;

public interface IIamCommandRepository
{
    Task<User?> GetUserAsync(UserId userId, CancellationToken ct = default);
    Task<User?> FindUserByUsernameAsync(string username, CancellationToken ct = default);
    Task AddUserAsync(User user, CancellationToken ct = default);
    Task UpdateUserAsync(User user, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetAllUsersAsync(CancellationToken ct = default);

    Task AddScopeAsync(Scope scope, CancellationToken ct = default);
    Task<IReadOnlyList<Scope>> GetScopesForUserAsync(UserId userId, CancellationToken ct = default);

    Task AddUserCapabilityAsync(UserCapability capability, CancellationToken ct = default);
    Task RevokeUserCapabilityAsync(UserCapabilityId capabilityId, DateTime revokedAt, CancellationToken ct = default);
    Task<IReadOnlyList<Capability>> GetAllCapabilitiesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<UserCapability>> GetActiveCapabilitiesForUserAsync(UserId userId, CancellationToken ct = default);
}
