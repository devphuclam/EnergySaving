using IUMP.Modules.IAM.Domain;

namespace IUMP.Modules.IAM.Application;

public interface IPocIdentityFixture
{
    IReadOnlyList<User> GetDeterministicUsers();
    bool IsFixtureEnabled { get; }
    bool ApplyPostSiteFixture(Guid siteId);
    bool IsIdempotent { get; }
}

public sealed class PocIdentityFixture : IPocIdentityFixture
{
    private static readonly IReadOnlyList<User> DeterministicUsers = CreateUsers();

    public IReadOnlyList<User> GetDeterministicUsers() => DeterministicUsers;
    public bool IsFixtureEnabled { get; } = true;

    public bool ApplyPostSiteFixture(Guid siteId)
    {
        return siteId != Guid.Empty;
    }

    public bool IsIdempotent => true;

    private static IReadOnlyList<User> CreateUsers()
    {
        var adminId = UserId.Parse("00000000-0000-0000-0000-000000000001");
        var engineerId = UserId.Parse("00000000-0000-0000-0000-000000000002");
        var operatorId = UserId.Parse("00000000-0000-0000-0000-000000000003");
        var managerId = UserId.Parse("00000000-0000-0000-0000-000000000004");
        var viewerId = UserId.Parse("00000000-0000-0000-0000-000000000005");

        return new[]
        {
            new User(adminId, "admin", "PLACEHOLDER_HASH", UserStatus.Active, Role.Administrator),
            new User(engineerId, "engineer", "PLACEHOLDER_HASH", UserStatus.Active, Role.Engineer),
            new User(operatorId, "operator", "PLACEHOLDER_HASH", UserStatus.Active, Role.Operator),
            new User(managerId, "manager", "PLACEHOLDER_HASH", UserStatus.Active, Role.Manager),
            new User(viewerId, "viewer", "PLACEHOLDER_HASH", UserStatus.Active, Role.Viewer),
        };
    }
}
