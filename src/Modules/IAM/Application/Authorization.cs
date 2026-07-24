using IUMP.Modules.IAM.Domain;

namespace IUMP.Modules.IAM.Application;

public enum AuthorizationResult
{
    Allowed,
    Forbidden,
    NotFound
}

public interface IAuthorizationDecision
{
    AuthorizationResult Check(ICallerContext caller, string capability, Guid? siteId = null, Guid? areaId = null);
    AuthorizationResult CheckTarget(ICallerContext caller, Guid targetSiteId, Guid? targetAreaId = null);
}

public sealed class AuthorizationDecision : IAuthorizationDecision
{
    public AuthorizationResult Check(ICallerContext caller, string capability, Guid? siteId = null, Guid? areaId = null)
    {
        if (caller.Roles.Contains(Role.Administrator))
            return AuthorizationResult.Allowed;

        var inScope = caller.Scopes.Any(s =>
            s.SiteId == siteId &&
            (areaId == null || s.AreaId == areaId || s.AreaId == null));

        if (!inScope && siteId.HasValue)
            return AuthorizationResult.NotFound;

        var anyAllowed = caller.Roles.Any(r => IsRoleAllowed(r, capability));
        if (!anyAllowed)
            return AuthorizationResult.Forbidden;

        return AuthorizationResult.Allowed;
    }

    public AuthorizationResult CheckTarget(ICallerContext caller, Guid targetSiteId, Guid? targetAreaId = null)
    {
        if (caller.Roles.Contains(Role.Administrator))
            return AuthorizationResult.Allowed;

        var inScope = caller.Scopes.Any(s => s.SiteId == targetSiteId);

        if (!inScope)
            return AuthorizationResult.NotFound;

        return AuthorizationResult.Allowed;
    }

    private static bool IsRoleAllowed(Role role, string capability)
    {
        switch (role)
        {
            case Role.Engineer:
                return IsEngineerCapability(capability);

            case Role.Operator:
            case Role.Manager:
            case Role.Viewer:
                return capability.StartsWith("VIEW_") || capability == "READ";

            default:
                return false;
        }
    }

    private static bool IsEngineerCapability(string capability)
    {
        return capability switch
        {
            "MANAGE_SITE" => false,
            "MANAGE_AREA" => true,
            "MANAGE_ASSET" => true,
            "MANAGE_POINT" => true,
            "MANAGE_SOURCE" => true,
            "MANAGE_MAPPING" => true,
            "MANAGE_SIMULATOR" => true,
            "CREATE_ROOT_SITE" => false,
            "READ" => true,
            "VIEW_HIERARCHY" => true,
            "VIEW_LATEST" => true,
            "VIEW_HEALTH" => true,
            _ => false
        };
    }
}
