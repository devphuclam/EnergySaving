using IUMP.Modules.IAM.Domain;

namespace IUMP.Modules.IAM.Contracts;

public sealed record IdentityRuntimeSnapshot(
    Guid UserId,
    string Username,
    bool IsActive,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> SiteScopes,
    IReadOnlyCollection<string> AreaScopes);

/// Contract-namespaced facade that keeps IAM-owned identifiers and entities
/// inside the IAM module while exposing only immutable runtime facts.
public sealed class IdentityRuntimeGateway(IIamCommandRepository repository)
{
    public async Task<IdentityRuntimeSnapshot?> ResolveAsync(
        string userId,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(userId, out var id)) return null;
        var user = await repository.GetUserAsync(new UserId(id), ct);
        if (user is null) return null;
        var scopes = await repository.GetScopesForUserAsync(user.Id, ct);
        return new IdentityRuntimeSnapshot(
            user.Id.Value, user.Username, user.IsActive(),
            user.Roles.Select(role => role.ToString()).ToArray(),
            scopes.Where(scope => scope.SiteId.HasValue && !scope.AreaId.HasValue)
                .Select(scope => scope.SiteId!.Value.ToString("D"))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            scopes.Where(scope => scope.AreaId.HasValue)
                .Select(scope => scope.AreaId!.Value.ToString("D"))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }
}
