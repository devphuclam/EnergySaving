using IUMP.Modules.IAM.Domain;

namespace IUMP.Modules.IAM.Application;

public interface ICallerContext
{
    UserId UserId { get; }
    string Username { get; }
    IReadOnlyList<Role> Roles { get; }
    Role PrimaryRole { get; }
    IReadOnlyList<string> Capabilities { get; }
    IReadOnlyList<Scope> Scopes { get; }
    string CorrelationId { get; }
}

public sealed class CallerContext : ICallerContext
{
    public UserId UserId { get; }
    public string Username { get; }
    public IReadOnlyList<Role> Roles { get; }
    public Role PrimaryRole { get; }
    public IReadOnlyList<string> Capabilities { get; }
    public IReadOnlyList<Scope> Scopes { get; }
    public string CorrelationId { get; }

    public CallerContext(UserId userId, string username, IReadOnlyList<Role> roles,
        IReadOnlyList<string> capabilities, IReadOnlyList<Scope> scopes, string correlationId)
    {
        UserId = userId;
        Username = username;
        Roles = roles;
        PrimaryRole = roles.Count > 0 ? roles[0] : Role.Viewer;
        Capabilities = capabilities;
        Scopes = scopes;
        CorrelationId = correlationId;
    }

    public CallerContext(UserId userId, string username, Role role,
        IReadOnlyList<string> capabilities, IReadOnlyList<Scope> scopes, string correlationId)
        : this(userId, username, new[] { role }, capabilities, scopes, correlationId)
    {
    }
}
