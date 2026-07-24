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
        if (caller.Role == Role.Administrator)
            return AuthorizationResult.Allowed;

        var inScope = caller.Scopes.Any(s =>
            s.SiteId == siteId &&
            (areaId == null || s.AreaId == areaId || s.AreaId == null));

        if (!inScope && siteId.HasValue)
            return AuthorizationResult.Forbidden;

        switch (caller.Role)
        {
            case Role.Engineer:
                return IsEngineerCapability(capability) ? AuthorizationResult.Allowed : AuthorizationResult.Forbidden;

            case Role.Operator:
                return capability.StartsWith("VIEW_") || capability == "READ"
                    ? AuthorizationResult.Allowed : AuthorizationResult.Forbidden;

            case Role.Manager:
                return capability.StartsWith("VIEW_") || capability == "READ"
                    ? AuthorizationResult.Allowed : AuthorizationResult.Forbidden;

            case Role.Viewer:
                return capability.StartsWith("VIEW_") || capability == "READ"
                    ? AuthorizationResult.Allowed : AuthorizationResult.Forbidden;

            default:
                return AuthorizationResult.Forbidden;
        }
    }

    public AuthorizationResult CheckTarget(ICallerContext caller, Guid targetSiteId, Guid? targetAreaId = null)
    {
        if (caller.Role == Role.Administrator)
            return AuthorizationResult.Allowed;

        var inScope = caller.Scopes.Any(s => s.SiteId == targetSiteId);

        if (!inScope)
            return AuthorizationResult.Forbidden;

        return AuthorizationResult.Allowed;
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
