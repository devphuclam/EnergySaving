using IUMP.Modules.IAM.Domain;
using IUMP.Modules.IAM.Contracts;

namespace IUMP.Modules.IAM.Application;

public enum EligibilityResult
{
    Eligible,
    UserNotFound,
    UserDisabled,
    NoScope,
    ScopeMismatch
}

public interface IActiveUserEligibility
{
    EligibilityResult Check(UserId userId, Guid? siteId = null, Guid? areaId = null);
    bool IsActiveUser(UserId userId);
    bool IsDataOwnerEligible(UserId userId, Guid? siteId = null);
    User? FindByUsername(string normalizedUsername);
    User? FindByUserId(UserId userId);
    IReadOnlyList<Scope> GetScopesForUser(UserId userId);
}

public sealed class ActiveUserEligibility : IActiveUserEligibility
{
    private readonly IIamCommandRepository? _repository;
    private readonly Dictionary<string, User> _usersByUsername;
    private readonly Dictionary<Guid, User> _usersById;
    private readonly Dictionary<Guid, HashSet<Guid>> _userSiteScopes;

    public ActiveUserEligibility()
    {
        _usersByUsername = new Dictionary<string, User>();
        _usersById = new Dictionary<Guid, User>();
        _userSiteScopes = new Dictionary<Guid, HashSet<Guid>>();
    }

    public ActiveUserEligibility(IIamCommandRepository repository)
    {
        _repository = repository;
        _usersByUsername = new Dictionary<string, User>();
        _usersById = new Dictionary<Guid, User>();
        _userSiteScopes = new Dictionary<Guid, HashSet<Guid>>();
    }

    public ActiveUserEligibility(IEnumerable<User> users)
    {
        _usersByUsername = new Dictionary<string, User>();
        _usersById = new Dictionary<Guid, User>();
        _userSiteScopes = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var user in users)
        {
            _usersByUsername[user.Username.ToLowerInvariant()] = user;
            _usersById[user.Id.Value] = user;
        }
    }

    public void AddOrUpdateUser(User user)
    {
        _usersByUsername[user.Username.ToLowerInvariant()] = user;
        _usersById[user.Id.Value] = user;
    }

    public void AddScope(UserId userId, Guid siteId)
    {
        if (!_userSiteScopes.ContainsKey(userId.Value))
            _userSiteScopes[userId.Value] = new HashSet<Guid>();
        _userSiteScopes[userId.Value].Add(siteId);
    }

    public User? FindByUsername(string normalizedUsername)
    {
        _usersByUsername.TryGetValue(normalizedUsername, out var user);
        return user;
    }

    public User? FindByUserId(UserId userId)
    {
        _usersById.TryGetValue(userId.Value, out var user);
        return user;
    }

    public IReadOnlyList<Scope> GetScopesForUser(UserId userId)
    {
        return Array.Empty<Scope>();
    }

    public EligibilityResult Check(UserId userId, Guid? siteId = null, Guid? areaId = null)
    {
        if (_repository != null)
        {
            var user = _repository.GetUserAsync(userId).GetAwaiter().GetResult();
            if (user == null)
                return EligibilityResult.UserNotFound;
            if (user.Status == UserStatus.Disabled)
                return EligibilityResult.UserDisabled;
            if (siteId.HasValue)
            {
                var scopes = _repository.GetScopesForUserAsync(userId).GetAwaiter().GetResult();
                if (!scopes.Any(s => s.SiteId == siteId))
                    return EligibilityResult.ScopeMismatch;
            }
            return EligibilityResult.Eligible;
        }

        if (!_usersById.TryGetValue(userId.Value, out var found))
            return EligibilityResult.UserNotFound;

        if (found.Status == UserStatus.Disabled)
            return EligibilityResult.UserDisabled;

        if (siteId.HasValue && _userSiteScopes.TryGetValue(userId.Value, out var sites))
        {
            if (!sites.Contains(siteId.Value))
                return EligibilityResult.ScopeMismatch;
        }

        return EligibilityResult.Eligible;
    }

    public bool IsActiveUser(UserId userId)
    {
        if (_usersById.TryGetValue(userId.Value, out var user))
            return user.IsActive();
        return false;
    }

    public bool IsDataOwnerEligible(UserId userId, Guid? siteId = null)
    {
        if (!_usersById.TryGetValue(userId.Value, out var user))
            return false;
        return user.IsActive();
    }
}
