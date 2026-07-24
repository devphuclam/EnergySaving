using IUMP.Modules.IAM.Domain;

namespace IUMP.Modules.IAM.Application;

public interface ICallerContext
{
    UserId UserId { get; }
    string Username { get; }
    Role Role { get; }
    IReadOnlyList<string> Capabilities { get; }
    IReadOnlyList<Scope> Scopes { get; }
    string CorrelationId { get; }
}

public sealed class CallerContext : ICallerContext
{
    public UserId UserId { get; }
    public string Username { get; }
    public Role Role { get; }
    public IReadOnlyList<string> Capabilities { get; }
    public IReadOnlyList<Scope> Scopes { get; }
    public string CorrelationId { get; }

    public CallerContext(UserId userId, string username, Role role,
        IReadOnlyList<string> capabilities, IReadOnlyList<Scope> scopes, string correlationId)
    {
        UserId = userId;
        Username = username;
        Role = role;
        Capabilities = capabilities;
        Scopes = scopes;
        CorrelationId = correlationId;
    }
}
