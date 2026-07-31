using System.Security.Claims;

namespace IUMP.Api.Infrastructure;

public sealed class HttpServerPrincipalAccessor(
    IHttpContextAccessor contexts) : IServerPrincipalAccessor
{
    public ServerPrincipal? Current
    {
        get
        {
            var authenticated = contexts.HttpContext?.User;
            if (authenticated?.Identity?.IsAuthenticated != true ||
                !Guid.TryParse(
                    authenticated.FindFirstValue(ClaimTypes.NameIdentifier),
                    out var userId))
                return null;
            var roles = authenticated.FindAll(ClaimTypes.Role)
                .Select(claim => claim.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var siteIds = authenticated
                .FindAll(IumpSessionAuthentication.SiteScopeClaim)
                .Select(claim => claim.Value)
                .Where(value => Guid.TryParse(value, out _))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var areaIds = authenticated
                .FindAll(IumpSessionAuthentication.AreaScopeClaim)
                .Select(claim => claim.Value)
                .Where(value => Guid.TryParse(value, out _))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var capabilities = authenticated
                .FindAll(IumpSessionAuthentication.CapabilityClaim)
                .Select(claim => claim.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return new ServerPrincipal(
                userId,
                authenticated.Identity.Name ?? userId.ToString("D"),
                siteIds,
                areaIds,
                roles.Contains("Administrator"),
                roles,
                capabilities);
        }
    }
}
