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
    private readonly Dictionary<Guid, List<Scope>> _userScopes;

    public ActiveUserEligibility()
    {
        _usersByUsername = new Dictionary<string, User>();
        _usersById = new Dictionary<Guid, User>();
        _userScopes = new Dictionary<Guid, List<Scope>>();
    }

    public ActiveUserEligibility(IIamCommandRepository repository)
    {
        _repository = repository;
        _usersByUsername = new Dictionary<string, User>();
        _usersById = new Dictionary<Guid, User>();
        _userScopes = new Dictionary<Guid, List<Scope>>();
    }

    public ActiveUserEligibility(IEnumerable<User> users)
    {
        _usersByUsername = new Dictionary<string, User>();
        _usersById = new Dictionary<Guid, User>();
        _userScopes = new Dictionary<Guid, List<Scope>>();
        foreach (var user in users)
        {
            _usersByUsername[user.Username.ToLowerInvariant()] = user;
            _usersById[user.Id.Value] = user;
        }
    }

    public ActiveUserEligibility(IEnumerable<User> users, IEnumerable<Scope> scopes)
        : this(users)
    {
        foreach (var scope in scopes)
        {
            var key = scope.UserId.Value;
            if (!_userScopes.ContainsKey(key))
                _userScopes[key] = new List<Scope>();
            _userScopes[key].Add(scope);
        }
    }

    public void AddOrUpdateUser(User user)
    {
        _usersByUsername[user.Username.ToLowerInvariant()] = user;
        _usersById[user.Id.Value] = user;
    }

    public void AddScope(UserId userId, Scope scope)
    {
        var key = userId.Value;
        if (!_userScopes.ContainsKey(key))
            _userScopes[key] = new List<Scope>();
        _userScopes[key].RemoveAll(s => s.SiteId == scope.SiteId && s.AreaId == scope.AreaId);
        _userScopes[key].Add(scope);
    }

    public void AddScope(UserId userId, Guid siteId)
    {
        AddScope(userId, new Scope(ScopeId.New(), userId, siteId, null));
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
        if (_userScopes.TryGetValue(userId.Value, out var scopes))
            return scopes;
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
                if (scopes.Count == 0)
                    return EligibilityResult.NoScope;
                if (!scopes.Any(s => s.SiteId == siteId && (areaId == null || s.AreaId == areaId || s.AreaId == null)))
                    return EligibilityResult.ScopeMismatch;
            }
            return EligibilityResult.Eligible;
        }

        if (!_usersById.TryGetValue(userId.Value, out var found))
            return EligibilityResult.UserNotFound;

        if (found.Status == UserStatus.Disabled)
            return EligibilityResult.UserDisabled;

        if (siteId.HasValue)
        {
            if (!_userScopes.TryGetValue(userId.Value, out var scopes) || scopes.Count == 0)
                return EligibilityResult.NoScope;

            if (!scopes.Any(s => s.SiteId == siteId && (areaId == null || s.AreaId == areaId || s.AreaId == null)))
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

        if (!user.IsActive())
            return false;

        if (siteId.HasValue)
        {
            if (!_userScopes.TryGetValue(userId.Value, out var scopes) || scopes.Count == 0)
                return false;

            if (!scopes.Any(s => s.SiteId == siteId))
                return false;
        }

        return true;
    }
}
