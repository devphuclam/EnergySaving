namespace IUMP.Modules.IAM.Domain;

public enum UserStatus
{
    Active,
    Disabled
}

public enum Role
{
    Administrator,
    Engineer,
    Operator,
    Manager,
    Viewer
}

public readonly record struct UserId(Guid Value)
{
    public static UserId New() => new(Guid.NewGuid());
    public static UserId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString("D");
}

public sealed class User
{
    public UserId Id { get; }
    public string Username { get; }
    public string PasswordHash { get; }
    public UserStatus Status { get; private set; }
    public IReadOnlyList<Role> Roles { get; }

    public User(UserId id, string username, string passwordHash, UserStatus status, IReadOnlyList<Role> roles)
    {
        Id = id;
        Username = username;
        PasswordHash = passwordHash;
        Status = status;
        Roles = roles;
    }

    public User(UserId id, string username, string passwordHash, UserStatus status, Role role)
        : this(id, username, passwordHash, status, new[] { role })
    {
    }

    public void Disable() => Status = UserStatus.Disabled;
    public bool IsActive() => Status == UserStatus.Active;
    public bool HasRole(Role role) => Roles.Contains(role);
}

public readonly record struct ScopeId(Guid Value)
{
    public static ScopeId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public sealed class Scope
{
    public ScopeId Id { get; }
    public UserId UserId { get; }
    public Guid? SiteId { get; }
    public Guid? AreaId { get; }

    public Scope(ScopeId id, UserId userId, Guid? siteId, Guid? areaId)
    {
        Id = id;
        UserId = userId;
        SiteId = siteId;
        AreaId = areaId;
    }

    public bool IsSiteScope => SiteId.HasValue && !AreaId.HasValue;
    public bool IsAreaScope => AreaId.HasValue;
}

public readonly record struct CapabilityId(Guid Value)
{
    public static CapabilityId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public sealed class Capability
{
    public CapabilityId Id { get; }
    public string Code { get; }
    public string Name { get; }

    public Capability(CapabilityId id, string code, string name)
    {
        Id = id;
        Code = code;
        Name = name;
    }
}

public readonly record struct SessionId(Guid Value)
{
    public static SessionId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public sealed class Session
{
    public SessionId Id { get; }
    public UserId UserId { get; }
    public string TokenHash { get; }
    public DateTime IssuedAt { get; }
    public DateTime IdleExpiresAt { get; private set; }
    public DateTime AbsoluteExpiresAt { get; }
    public DateTime? RevokedAt { get; private set; }

    public Session(SessionId id, UserId userId, string tokenHash, DateTime issuedAt,
        DateTime idleExpiresAt, DateTime absoluteExpiresAt)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        IssuedAt = issuedAt;
        IdleExpiresAt = idleExpiresAt;
        AbsoluteExpiresAt = absoluteExpiresAt;
    }

    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsExpired(DateTime now) => now > AbsoluteExpiresAt || now > IdleExpiresAt;

    public void Revoke(DateTime at)
    {
        RevokedAt = at;
    }

    public void Touch(DateTime now, TimeSpan idleTimeout)
    {
        IdleExpiresAt = now + idleTimeout;
    }

    public bool IsValid(DateTime now) =>
        !IsRevoked && !IsExpired(now);
}

public readonly record struct UserCapabilityId(Guid Value)
{
    public static UserCapabilityId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public sealed class UserCapability
{
    public UserCapabilityId Id { get; }
    public UserId UserId { get; }
    public CapabilityId CapabilityId { get; }
    public UserId AssignedBy { get; }
    public DateTime AssignedAt { get; }
    public DateTime? RevokedAt { get; private set; }
    public long Version { get; private set; }

    public UserCapability(UserCapabilityId id, UserId userId, CapabilityId capabilityId,
        UserId assignedBy, DateTime assignedAt, long version)
    {
        Id = id;
        UserId = userId;
        CapabilityId = capabilityId;
        AssignedBy = assignedBy;
        AssignedAt = assignedAt;
        Version = version;
    }

    public bool IsActive => !RevokedAt.HasValue;

    public void Revoke(DateTime at)
    {
        RevokedAt = at;
        Version++;
    }
}
