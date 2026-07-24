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
}

public sealed class ActiveUserEligibility : IActiveUserEligibility
{
    private readonly IIamCommandRepository? _repository;
    private readonly Dictionary<Guid, UserStatus> _userStatuses;
    private readonly Dictionary<Guid, HashSet<Guid>> _userSiteScopes;

    public ActiveUserEligibility()
    {
        _userStatuses = new Dictionary<Guid, UserStatus>();
        _userSiteScopes = new Dictionary<Guid, HashSet<Guid>>();
    }

    public ActiveUserEligibility(IIamCommandRepository repository)
    {
        _repository = repository;
        _userStatuses = new Dictionary<Guid, UserStatus>();
        _userSiteScopes = new Dictionary<Guid, HashSet<Guid>>();
    }

    public ActiveUserEligibility(IEnumerable<User> users)
    {
        _userStatuses = new Dictionary<Guid, UserStatus>();
        _userSiteScopes = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var user in users)
        {
            _userStatuses[user.Id.Value] = user.Status;
        }
    }

    public void AddOrUpdateUser(User user)
    {
        _userStatuses[user.Id.Value] = user.Status;
    }

    public void AddScope(UserId userId, Guid siteId)
    {
        if (!_userSiteScopes.ContainsKey(userId.Value))
            _userSiteScopes[userId.Value] = new HashSet<Guid>();
        _userSiteScopes[userId.Value].Add(siteId);
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

        if (!_userStatuses.TryGetValue(userId.Value, out var status))
            return EligibilityResult.UserNotFound;

        if (status == UserStatus.Disabled)
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
        if (_userStatuses.TryGetValue(userId.Value, out var status))
            return status == UserStatus.Active;
        return false;
    }

    public bool IsDataOwnerEligible(UserId userId, Guid? siteId = null)
    {
        if (!_userStatuses.TryGetValue(userId.Value, out var status))
            return false;
        return status == UserStatus.Active;
    }
}
