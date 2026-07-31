using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using IUMP.Modules.IAM.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace IUMP.Api;

public static class IumpSessionAuthentication
{
    public const string Scheme = "IumpSession";
    public const string SiteScopeClaim = "iump:site-scope";
    public const string AreaScopeClaim = "iump:area-scope";
    public const string CapabilityClaim = "iump:capability";

    public static IServiceCollection AddIumpSessionAuthentication(
        this IServiceCollection services)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = Scheme;
            options.DefaultChallengeScheme = Scheme;
        }).AddScheme<AuthenticationSchemeOptions, IumpSessionAuthenticationHandler>(
            Scheme, _ => { });
        return services;
    }

    public static ClaimsPrincipal? TryCreatePrincipal(
        string? cookieValue,
        IAuthService auth)
    {
        if (string.IsNullOrWhiteSpace(cookieValue))
            return null;

        try
        {
            var tokenHash = Convert.ToHexString(
                SHA256.HashData(Convert.FromHexString(cookieValue)))
                .ToLowerInvariant();
            var me = auth.ResolveMe(tokenHash);
            if (me is null)
                return null;

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, me.UserId),
                new(ClaimTypes.Name, me.Username)
            };
            claims.AddRange(me.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
            claims.AddRange(me.Scopes.Select(
                scope => new Claim(SiteScopeClaim, scope)));
            claims.AddRange((me.AreaScopes ?? Array.Empty<string>()).Select(
                scope => new Claim(AreaScopeClaim, scope)));
            claims.AddRange(me.Capabilities.Select(
                capability => new Claim(CapabilityClaim, capability)));
            return new ClaimsPrincipal(new ClaimsIdentity(
                claims, Scheme, ClaimTypes.Name, ClaimTypes.Role));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

public sealed class IumpSessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IAuthService auth)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var principal = IumpSessionAuthentication.TryCreatePrincipal(
            Request.Cookies[AuthEndpointHandlers.AuthCookie], auth);
        return Task.FromResult(principal is null
            ? AuthenticateResult.NoResult()
            : AuthenticateResult.Success(
                new AuthenticationTicket(principal, Scheme.Name)));
    }
}
