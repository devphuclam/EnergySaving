using IUMP.Modules.IAM.Contracts;
using IUMP.Modules.IAM.Domain;
using IUMP.BuildingBlocks.Persistence;
using IUMP.Infrastructure.Postgres;
using IUMP.Modules.Organization.Contracts;
using Npgsql;
using System.Security.Cryptography;

namespace IUMP.Modules.IAM.Infrastructure;

public sealed class PostgresIamRepositories :
    IIamCommandRepository,
    IIamPrincipalSessionRepository,
    IActivationIdentityParticipant
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresTransactionContext _hostTransactions;
    private readonly AsyncLocal<TransactionHolder?> _state = new();

    public PostgresIamRepositories(
        NpgsqlDataSource dataSource,
        PostgresTransactionContext hostTransactions)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _hostTransactions = hostTransactions ?? throw new ArgumentNullException(nameof(hostTransactions));
    }

    public async Task<User?> GetUserAsync(UserId userId, CancellationToken ct = default) =>
        await QueryUserAsync("WHERE u.user_id = @value", userId.Value, ct);

    public async Task<User?> FindUserByUsernameAsync(string username, CancellationToken ct = default) =>
        await QueryUserAsync("WHERE u.username = @value", username, ct);

    public async Task AddUserAsync(User user, CancellationToken ct = default)
    {
        try
        {
            await ExecuteAsync("""
                INSERT INTO iam.user_account
                    (user_id, username, password_hash, status, version)
                VALUES (@id, @username, @password_hash, @status, 1)
                """, command =>
            {
                command.Parameters.AddWithValue("id", user.Id.Value);
                command.Parameters.AddWithValue("username", user.Username);
                command.Parameters.AddWithValue("password_hash", user.PasswordHash);
                command.Parameters.AddWithValue("status", user.Status.ToString());
            }, ct);

            foreach (var role in user.Roles.Distinct())
                await AssignRoleAsync(user.Id, role, user.Id, ct);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new InvalidOperationException("IAM_UNIQUE_CONFLICT", exception);
        }
    }

    public Task UpdateUserAsync(User user, CancellationToken ct = default) =>
        ExecuteExpectedAsync("""
            UPDATE iam.user_account
            SET username = @username,
                password_hash = @password_hash,
                status = @status,
                updated_at = now(),
                version = version + 1
            WHERE user_id = @id
            """, command =>
        {
            command.Parameters.AddWithValue("id", user.Id.Value);
            command.Parameters.AddWithValue("username", user.Username);
            command.Parameters.AddWithValue("password_hash", user.PasswordHash);
            command.Parameters.AddWithValue("status", user.Status.ToString());
        }, "IAM_USER_NOT_FOUND", ct);

    public async Task<IReadOnlyList<User>> GetAllUsersAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT u.user_id, u.username, u.password_hash, u.status,
                   COALESCE(array_agg(r.code ORDER BY r.code)
                       FILTER (WHERE r.code IS NOT NULL), ARRAY[]::text[])
            FROM iam.user_account u
            LEFT JOIN iam.user_role ur ON ur.user_id = u.user_id
            LEFT JOIN iam.role r ON r.role_id = ur.role_id
            GROUP BY u.user_id, u.username, u.password_hash, u.status
            ORDER BY u.username
            """;
        return await QueryAsync(sql, null, reader => MapUser(reader), ct);
    }

    public async Task<IReadOnlyList<Role>> GetRoleCodesAsync(CancellationToken ct = default)
    {
        var values = await QueryAsync(
            "SELECT code FROM iam.role ORDER BY code",
            null,
            reader => Enum.Parse<Role>(reader.GetString(0), ignoreCase: false),
            ct);
        return values;
    }

    public Task AssignRoleAsync(UserId userId, Role role, UserId assignedBy, CancellationToken ct = default) =>
        ExecuteAsync("""
            INSERT INTO iam.user_role (user_role_id, user_id, role_id, assigned_by)
            SELECT @id, @user_id, role_id, @assigned_by
            FROM iam.role
            WHERE code = @role
            ON CONFLICT (user_id, role_id) DO NOTHING
            """, command =>
        {
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("user_id", userId.Value);
            command.Parameters.AddWithValue("assigned_by", assignedBy.Value);
            command.Parameters.AddWithValue("role", role.ToString());
        }, ct);

    public async Task<IReadOnlyList<Role>> GetRolesForUserAsync(UserId userId, CancellationToken ct = default) =>
        await QueryAsync("""
            SELECT r.code
            FROM iam.user_role ur
            JOIN iam.role r ON r.role_id = ur.role_id
            WHERE ur.user_id = @value
            ORDER BY r.code
            """, command => command.Parameters.AddWithValue("value", userId.Value),
            reader => Enum.Parse<Role>(reader.GetString(0), false), ct);

    public Task RevokeRoleAsync(UserId userId, Role role, CancellationToken ct = default) =>
        ExecuteAsync("""
            DELETE FROM iam.user_role ur
            USING iam.role r
            WHERE ur.role_id = r.role_id
              AND ur.user_id = @user_id
              AND r.code = @role
            """, command =>
        {
            command.Parameters.AddWithValue("user_id", userId.Value);
            command.Parameters.AddWithValue("role", role.ToString());
        }, ct);

    public Task AddScopeAsync(Scope scope, CancellationToken ct = default)
    {
        if (!scope.SiteId.HasValue)
            throw new InvalidOperationException("IAM_SCOPE_SITE_REQUIRED");
        return ExecuteAsync("""
            INSERT INTO iam.user_scope (scope_id, user_id, site_id, area_id)
            VALUES (@id, @user_id, @site_id, @area_id)
            ON CONFLICT DO NOTHING
            """, command =>
        {
            command.Parameters.AddWithValue("id", scope.Id.Value);
            command.Parameters.AddWithValue("user_id", scope.UserId.Value);
            command.Parameters.AddWithValue("site_id", scope.SiteId.Value);
            command.Parameters.AddWithValue("area_id", (object?)scope.AreaId ?? DBNull.Value);
        }, ct);
    }

    public async Task<IReadOnlyList<Scope>> GetScopesForUserAsync(UserId userId, CancellationToken ct = default) =>
        await QueryAsync("""
            SELECT scope_id, user_id, site_id, area_id
            FROM iam.user_scope
            WHERE user_id = @value
            ORDER BY created_at, scope_id
            """, command => command.Parameters.AddWithValue("value", userId.Value),
            reader => new Scope(
                new ScopeId(reader.GetGuid(0)),
                new UserId(reader.GetGuid(1)),
                reader.GetGuid(2),
                reader.IsDBNull(3) ? null : reader.GetGuid(3)), ct);

    public Task AddUserCapabilityAsync(UserCapability capability, CancellationToken ct = default) =>
        ExecuteAsync("""
            INSERT INTO iam.user_capability
                (user_capability_id, user_id, capability_id, assigned_by, assigned_at, version)
            VALUES (@id, @user_id, @capability_id, @assigned_by, @assigned_at, @version)
            ON CONFLICT (user_id, capability_id) DO UPDATE
            SET revoked_at = NULL,
                assigned_by = EXCLUDED.assigned_by,
                assigned_at = EXCLUDED.assigned_at,
                version = iam.user_capability.version + 1
            """, command =>
        {
            command.Parameters.AddWithValue("id", capability.Id.Value);
            command.Parameters.AddWithValue("user_id", capability.UserId.Value);
            command.Parameters.AddWithValue("capability_id", capability.CapabilityId.Value);
            command.Parameters.AddWithValue("assigned_by", capability.AssignedBy.Value);
            command.Parameters.AddWithValue("assigned_at", capability.AssignedAt.ToUniversalTime());
            command.Parameters.AddWithValue("version", capability.Version);
        }, ct);

    public Task RevokeUserCapabilityAsync(
        UserCapabilityId capabilityId,
        DateTime revokedAt,
        CancellationToken ct = default) =>
        ExecuteExpectedAsync("""
            UPDATE iam.user_capability
            SET revoked_at = @revoked_at,
                version = version + 1
            WHERE user_capability_id = @id
              AND revoked_at IS NULL
            """, command =>
        {
            command.Parameters.AddWithValue("id", capabilityId.Value);
            command.Parameters.AddWithValue("revoked_at", revokedAt.ToUniversalTime());
        }, "IAM_CAPABILITY_NOT_ACTIVE", ct);

    public async Task<IReadOnlyList<Capability>> GetAllCapabilitiesAsync(CancellationToken ct = default) =>
        await QueryAsync("""
            SELECT capability_id, code, name
            FROM iam.capability
            ORDER BY code
            """, null,
            reader => new Capability(
                new CapabilityId(reader.GetGuid(0)),
                reader.GetString(1),
                reader.GetString(2)), ct);

    public async Task<IReadOnlyList<UserCapability>> GetActiveCapabilitiesForUserAsync(
        UserId userId,
        CancellationToken ct = default) =>
        await QueryAsync("""
            SELECT user_capability_id, user_id, capability_id, assigned_by, assigned_at, version
            FROM iam.user_capability
            WHERE user_id = @value AND revoked_at IS NULL
            ORDER BY assigned_at, user_capability_id
            """, command => command.Parameters.AddWithValue("value", userId.Value),
            reader => new UserCapability(
                new UserCapabilityId(reader.GetGuid(0)),
                new UserId(reader.GetGuid(1)),
                new CapabilityId(reader.GetGuid(2)),
                new UserId(reader.GetGuid(3)),
                reader.GetDateTime(4).ToUniversalTime(),
                reader.GetInt64(5)), ct);

    public Task<IIamTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        var holder = _state.Value ??= new TransactionHolder();
        if (holder.Current is not null)
            throw new InvalidOperationException("IAM_TRANSACTION_ALREADY_ACTIVE");
        return BeginTransactionCoreAsync(holder, ct);
    }

    private async Task<IIamTransaction> BeginTransactionCoreAsync(
        TransactionHolder holder,
        CancellationToken ct)
    {
        var connection = await _dataSource.OpenConnectionAsync(ct);
        var transaction = await connection.BeginTransactionAsync(
            System.Data.IsolationLevel.RepeatableRead,
            ct);
        var state = new TransactionState(connection, transaction);
        holder.Current = state;
        return new IamTransaction(state, () => holder.Current = null);
    }

    public Task AddSessionAsync(Session session, CancellationToken ct = default) =>
        ExecuteAsync("""
            INSERT INTO iam.user_session
                (session_id, user_id, token_hash, issued_at, idle_expires_at, absolute_expires_at, revoked_at)
            VALUES (@id, @user_id, @token_hash, @issued_at, @idle_expires_at, @absolute_expires_at, @revoked_at)
            """, command =>
        {
            command.Parameters.AddWithValue("id", session.Id.Value);
            command.Parameters.AddWithValue("user_id", session.UserId.Value);
            command.Parameters.AddWithValue("token_hash", session.TokenHash);
            command.Parameters.AddWithValue("issued_at", session.IssuedAt.ToUniversalTime());
            command.Parameters.AddWithValue("idle_expires_at", session.IdleExpiresAt.ToUniversalTime());
            command.Parameters.AddWithValue("absolute_expires_at", session.AbsoluteExpiresAt.ToUniversalTime());
            command.Parameters.AddWithValue("revoked_at", (object?)session.RevokedAt?.ToUniversalTime() ?? DBNull.Value);
        }, ct);

    public async Task<Session?> FindSessionByTokenHashAsync(string tokenHash, CancellationToken ct = default)
    {
        var values = await QueryAsync("""
            SELECT session_id, user_id, token_hash, issued_at, idle_expires_at, absolute_expires_at, revoked_at
            FROM iam.user_session
            WHERE token_hash = @value
            ORDER BY created_at DESC
            LIMIT 1
            """, command => command.Parameters.AddWithValue("value", tokenHash), MapSession, ct);
        return values.SingleOrDefault();
    }

    public async Task<IReadOnlyList<Session>> GetSessionsForUserAsync(
        UserId userId,
        CancellationToken ct = default) =>
        await QueryAsync("""
            SELECT session_id, user_id, token_hash, issued_at, idle_expires_at, absolute_expires_at, revoked_at
            FROM iam.user_session
            WHERE user_id = @value
            ORDER BY issued_at, session_id
            """, command => command.Parameters.AddWithValue("value", userId.Value), MapSession, ct);

    public Task RevokeSessionAsync(SessionId sessionId, DateTime revokedAt, CancellationToken ct = default) =>
        ExecuteAsync("""
            UPDATE iam.user_session
            SET revoked_at = COALESCE(revoked_at, @revoked_at)
            WHERE session_id = @id
            """, command =>
        {
            command.Parameters.AddWithValue("id", sessionId.Value);
            command.Parameters.AddWithValue("revoked_at", revokedAt.ToUniversalTime());
        }, ct);

    public Task RevokeAllSessionsForUserAsync(UserId userId, DateTime revokedAt, CancellationToken ct = default) =>
        ExecuteAsync("""
            UPDATE iam.user_session
            SET revoked_at = COALESCE(revoked_at, @revoked_at)
            WHERE user_id = @user_id
            """, command =>
        {
            command.Parameters.AddWithValue("user_id", userId.Value);
            command.Parameters.AddWithValue("revoked_at", revokedAt.ToUniversalTime());
        }, ct);

    private async Task<User?> QueryUserAsync(string predicate, object value, CancellationToken ct)
    {
        var sql = $"""
            SELECT u.user_id, u.username, u.password_hash, u.status,
                   COALESCE(array_agg(r.code ORDER BY r.code)
                       FILTER (WHERE r.code IS NOT NULL), ARRAY[]::text[])
            FROM iam.user_account u
            LEFT JOIN iam.user_role ur ON ur.user_id = u.user_id
            LEFT JOIN iam.role r ON r.role_id = ur.role_id
            {predicate}
            GROUP BY u.user_id, u.username, u.password_hash, u.status
            """;
        var users = await QueryAsync(
            sql,
            command => command.Parameters.AddWithValue("value", value),
            MapUser,
            ct);
        return users.SingleOrDefault();
    }

    private static User MapUser(NpgsqlDataReader reader)
    {
        var roles = reader.GetFieldValue<string[]>(4)
            .Select(value => Enum.Parse<Role>(value, false))
            .ToArray();
        return new User(
            new UserId(reader.GetGuid(0)),
            reader.GetString(1),
            reader.GetString(2),
            Enum.Parse<UserStatus>(reader.GetString(3), false),
            roles);
    }

    private static Session MapSession(NpgsqlDataReader reader)
    {
        var session = new Session(
            new SessionId(reader.GetGuid(0)),
            new UserId(reader.GetGuid(1)),
            reader.GetString(2),
            reader.GetDateTime(3).ToUniversalTime(),
            reader.GetDateTime(4).ToUniversalTime(),
            reader.GetDateTime(5).ToUniversalTime());
        if (!reader.IsDBNull(6))
            session.Revoke(reader.GetDateTime(6).ToUniversalTime());
        return session;
    }

    private async Task ExecuteExpectedAsync(
        string sql,
        Action<NpgsqlCommand> bind,
        string code,
        CancellationToken ct)
    {
        var affected = await ExecuteCoreAsync(sql, bind, ct);
        if (affected != 1)
            throw new InvalidOperationException(code);
    }

    private async Task ExecuteAsync(string sql, Action<NpgsqlCommand> bind, CancellationToken ct) =>
        _ = await ExecuteCoreAsync(sql, bind, ct);

    private async Task<int> ExecuteCoreAsync(string sql, Action<NpgsqlCommand> bind, CancellationToken ct)
    {
        var (connection, ownsConnection) = await AcquireConnectionAsync(ct);
        try
        {
            await using var command = new NpgsqlCommand(
                sql, connection, _state.Value?.Current?.Transaction ?? _hostTransactions.Current?.Transaction);
            bind(command);
            return await command.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            if (ownsConnection) await connection.DisposeAsync();
        }
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        Action<NpgsqlCommand>? bind,
        Func<NpgsqlDataReader, T> map,
        CancellationToken ct)
    {
        var (connection, ownsConnection) = await AcquireConnectionAsync(ct);
        try
        {
            await using var command = new NpgsqlCommand(
                sql, connection, _state.Value?.Current?.Transaction ?? _hostTransactions.Current?.Transaction);
            bind?.Invoke(command);
            await using var reader = await command.ExecuteReaderAsync(ct);
            var results = new List<T>();
            while (await reader.ReadAsync(ct))
                results.Add(map(reader));
            return results;
        }
        finally
        {
            if (ownsConnection) await connection.DisposeAsync();
        }
    }

    public async ValueTask AcquireLockAsync(
        IHostTransaction transaction,
        LockRequest request,
        CancellationToken ct = default)
    {
        if (request.Target != LockTarget.IamUser ||
            !Guid.TryParse(request.Id, out var userId))
            throw new InvalidOperationException("IAM_LOCK_TARGET_INVALID");
        var postgres = PostgresTransactionResolver.Require(transaction);
        await using var command = new NpgsqlCommand(
            "SELECT user_id FROM iam.user_account WHERE user_id=@id FOR UPDATE",
            postgres.Connection,
            postgres.Transaction);
        command.Parameters.AddWithValue("id", userId);
        _ = await command.ExecuteScalarAsync(ct);
    }

    public Task<ActivationDataOwnerSnapshot> ReadDataOwnerAsync(
        IHostTransaction transaction,
        string dataOwnerUserId,
        string siteId,
        string areaId,
        CancellationToken ct = default) =>
        ReadActivationOwnerAsync(transaction, dataOwnerUserId, siteId, areaId, ct);

    public Task<ActivationDataOwnerSnapshot> RecheckDataOwnerAsync(
        IHostTransaction transaction,
        string dataOwnerUserId,
        string siteId,
        string areaId,
        CancellationToken ct = default) =>
        ReadActivationOwnerAsync(transaction, dataOwnerUserId, siteId, areaId, ct);

    private static async Task<ActivationDataOwnerSnapshot> ReadActivationOwnerAsync(
        IHostTransaction transaction,
        string dataOwnerUserId,
        string siteId,
        string areaId,
        CancellationToken ct)
    {
        if (!Guid.TryParse(dataOwnerUserId, out var userId) ||
            !Guid.TryParse(siteId, out var siteGuid) ||
            !Guid.TryParse(areaId, out var areaGuid))
            return new ActivationDataOwnerSnapshot(
                dataOwnerUserId, false, false, false, false, false, 0, 0);
        var postgres = PostgresTransactionResolver.Require(transaction);
        await using var command = new NpgsqlCommand("""
            SELECT u.status,u.version,
                   EXISTS (
                     SELECT 1 FROM iam.user_scope s
                     WHERE s.user_id=u.user_id AND s.site_id=@site_id AND s.area_id IS NULL),
                   EXISTS (
                     SELECT 1 FROM iam.user_scope s
                     WHERE s.user_id=u.user_id AND s.site_id=@site_id AND s.area_id=@area_id),
                   GREATEST(1,(
                     SELECT count(*)::bigint FROM iam.user_scope s
                     WHERE s.user_id=u.user_id))
            FROM iam.user_account u
            WHERE u.user_id=@user_id
            """, postgres.Connection, postgres.Transaction);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("site_id", siteGuid);
        command.Parameters.AddWithValue("area_id", areaGuid);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return new ActivationDataOwnerSnapshot(
                dataOwnerUserId, false, false, false, false, false, 0, 0);
        var siteScope = reader.GetBoolean(2);
        var areaScope = reader.GetBoolean(3);
        return new ActivationDataOwnerSnapshot(
            dataOwnerUserId,
            true,
            reader.GetString(0) == "Active",
            siteScope,
            areaScope,
            false,
            reader.GetInt64(1),
            reader.GetInt64(4),
            siteScope || areaScope ? siteId : null,
            areaScope ? areaId : null);
    }

    private async Task<(NpgsqlConnection Connection, bool OwnsConnection)> AcquireConnectionAsync(
        CancellationToken ct)
    {
        if (_state.Value?.Current is { } state)
            return (state.Connection, false);
        if (_hostTransactions.Current is { IsCompleted: false } host)
            return (host.Connection, false);
        return (await _dataSource.OpenConnectionAsync(ct), true);
    }

    private sealed record TransactionState(
        NpgsqlConnection Connection,
        NpgsqlTransaction Transaction);

    private sealed class TransactionHolder
    {
        public TransactionState? Current { get; set; }
    }

    private sealed class IamTransaction(
        TransactionState state,
        Action completed) : IIamTransaction
    {
        private bool _isCompleted;

        public async Task CommitAsync(CancellationToken ct = default)
        {
            if (_isCompleted) return;
            try { await state.Transaction.CommitAsync(ct); }
            finally { await FinishAsync(); }
        }

        public async Task RollbackAsync(CancellationToken ct = default)
        {
            if (_isCompleted) return;
            try { await state.Transaction.RollbackAsync(ct); }
            finally { await FinishAsync(); }
        }

        private async Task FinishAsync()
        {
            if (_isCompleted) return;
            _isCompleted = true;
            completed();
            await state.Transaction.DisposeAsync();
            await state.Connection.DisposeAsync();
        }
    }
}

public sealed class PostgresAuthService(
    IIamCommandRepository users,
    IIamPrincipalSessionRepository sessions,
    ICredentialVerifier credentials) : IAuthService
{
    public LoginResult Login(LoginRequest request, DateTime now)
    {
        var user = users.FindUserByUsernameAsync(
                request.Username.Trim().ToLowerInvariant())
            .GetAwaiter().GetResult();
        if (user is null || !user.IsActive() ||
            !credentials.Verify(request.Password, user.PasswordHash))
            return new LoginResult(false, "Authentication failed.", null, null);

        var token = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToHexString(token).ToLowerInvariant();
        var tokenHash = Convert.ToHexString(SHA256.HashData(token)).ToLowerInvariant();
        var issued = now.ToUniversalTime();
        var session = new Session(
            SessionId.New(), user.Id, tokenHash, issued,
            issued.AddMinutes(20), issued.AddHours(8));
        sessions.AddSessionAsync(session).GetAwaiter().GetResult();
        return new LoginResult(true, null, rawToken, session.AbsoluteExpiresAt);
    }

    public MeSnapshot? ResolveMe(string tokenHash)
    {
        var session = sessions.FindSessionByTokenHashAsync(tokenHash)
            .GetAwaiter().GetResult();
        var now = DateTime.UtcNow;
        if (session is null || !session.IsValid(now)) return null;
        var user = users.GetUserAsync(session.UserId).GetAwaiter().GetResult();
        if (user is null || !user.IsActive()) return null;
        var roles = users.GetRolesForUserAsync(user.Id).GetAwaiter().GetResult()
            .Select(value => value.ToString()).ToArray();
        var assignedScopes = users.GetScopesForUserAsync(user.Id).GetAwaiter().GetResult();
        var scopes = assignedScopes
            .Where(value => value.SiteId.HasValue && !value.AreaId.HasValue)
            .Select(value => value.SiteId!.Value.ToString("D"))
            .Where(value => value.Length > 0).ToArray();
        var areaScopes = assignedScopes
            .Where(value => value.AreaId.HasValue)
            .Select(value => value.AreaId!.Value.ToString("D"))
            .ToArray();
        var capabilities = users.GetActiveCapabilitiesForUserAsync(user.Id).GetAwaiter().GetResult();
        var all = users.GetAllCapabilitiesAsync().GetAwaiter().GetResult()
            .ToDictionary(value => value.Id, value => value.Code);
        var codes = capabilities
            .Where(value => all.ContainsKey(value.CapabilityId))
            .Select(value => all[value.CapabilityId])
            .ToList();
        if (roles.Contains(Role.Administrator.ToString()) && !codes.Contains("AUDIT_READ"))
            codes.Add("AUDIT_READ");
        return new MeSnapshot(
            user.Id.ToString(), user.Username, roles, scopes, codes, areaScopes);
    }

    public bool RevokeSession(string tokenHash, DateTime now)
    {
        var session = sessions.FindSessionByTokenHashAsync(tokenHash)
            .GetAwaiter().GetResult();
        if (session is null) return false;
        sessions.RevokeSessionAsync(session.Id, now.ToUniversalTime())
            .GetAwaiter().GetResult();
        return true;
    }
}
