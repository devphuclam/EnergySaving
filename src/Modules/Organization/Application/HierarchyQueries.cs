using IUMP.Modules.Organization.Contracts;

namespace IUMP.Modules.Organization.Application;

public interface IOrganizationScopeFilter
{
    IReadOnlyCollection<string> ResolveSiteScopes(string userId, IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> siteScopes, IReadOnlyCollection<string> areaScopes);
}

public sealed class OrganizationScopeFilterService : IOrganizationScopeFilter
{
    public IReadOnlyCollection<string> ResolveSiteScopes(string userId, IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> siteScopes, IReadOnlyCollection<string> areaScopes)
    {
        if (roles.Any(r => string.Equals(r, "Administrator", StringComparison.OrdinalIgnoreCase)))
            return Array.Empty<string>();

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in siteScopes) result.Add(s);
        return result.ToArray();
    }
}
