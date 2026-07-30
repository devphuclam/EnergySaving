using IUMP.Modules.IAM.Contracts;

namespace IUMP.Api.Infrastructure;

public sealed class HttpServerPrincipalAccessor(
    IHttpContextAccessor contexts,
    IAuthService auth) : IServerPrincipalAccessor
{
    public ServerPrincipal? Current
    {
        get
        {
            var raw = contexts.HttpContext?.Request.Cookies[AuthEndpointHandlers.AuthCookie];
            if (string.IsNullOrWhiteSpace(raw)) return null;
            try
            {
                var bytes = Convert.FromHexString(raw);
                var hash = Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
                var me = auth.ResolveMe(hash);
                if (me is null || !Guid.TryParse(me.UserId, out var userId)) return null;
                var siteIds = me.Scopes
                    .Where(value => Guid.TryParse(value, out _))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var areaIds = (me.AreaScopes ?? Array.Empty<string>())
                    .Where(value => Guid.TryParse(value, out _))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                return new ServerPrincipal(
                    userId, me.Username, siteIds, areaIds,
                    me.Roles.Contains("Administrator", StringComparer.OrdinalIgnoreCase),
                    me.Roles.ToHashSet(StringComparer.OrdinalIgnoreCase),
                    me.Capabilities.ToHashSet(StringComparer.OrdinalIgnoreCase));
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
