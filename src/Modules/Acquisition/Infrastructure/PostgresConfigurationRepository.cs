using IUMP.Modules.Acquisition.Contracts;
using IUMP.Infrastructure.Postgres;
using Npgsql;

namespace IUMP.Modules.Acquisition.Infrastructure;

public sealed class PostgresConfigurationRepository : IAcquisitionConfigurationRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresTransactionContext _hostTransactions;
    private readonly AsyncLocal<TransactionHolder?> _state = new();

    public PostgresConfigurationRepository(
        NpgsqlDataSource dataSource,
        PostgresTransactionContext hostTransactions)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _hostTransactions = hostTransactions ?? throw new ArgumentNullException(nameof(hostTransactions));
    }

    public Task<SimulatorConfigurationHead?> GetBySourceIdAsync(
        Guid sourceId,
        CancellationToken ct = default) =>
        QueryHeadAsync("source_id=@value", sourceId, ct);

    public Task<SimulatorConfigurationHead?> GetHeadAsync(
        Guid configurationId,
        CancellationToken ct = default) =>
        QueryHeadAsync("configuration_id=@value", configurationId, ct);

    public async Task<SimulatorConfigurationVersion?> GetVersionAsync(
        Guid configurationId,
        long configurationVersion,
        CancellationToken ct = default)
    {
        var values = await QueryAsync("""
            SELECT configuration_id,configuration_version,interval_seconds,minimum_value,
                   maximum_value,deterministic_seed,scenario_type,algorithm_id,algorithm_version,
                   created_by_user_id,created_by_username,created_at_utc,correlation_id,causation_id
            FROM acquisition.simulator_configuration_version
            WHERE configuration_id=@id AND configuration_version=@version
            """, command =>
        {
            command.Parameters.AddWithValue("id", configurationId);
            command.Parameters.AddWithValue("version", configurationVersion);
        }, MapVersion, ct);
        return values.SingleOrDefault();
    }

    public async Task<IReadOnlyList<SimulatorConfigurationVersion>> ListVersionsAsync(
        Guid configurationId,
        CancellationToken ct = default) =>
        await QueryAsync("""
            SELECT configuration_id,configuration_version,interval_seconds,minimum_value,
                   maximum_value,deterministic_seed,scenario_type,algorithm_id,algorithm_version,
                   created_by_user_id,created_by_username,created_at_utc,correlation_id,causation_id
            FROM acquisition.simulator_configuration_version
            WHERE configuration_id=@id
            ORDER BY configuration_version
            """, command => command.Parameters.AddWithValue("id", configurationId), MapVersion, ct);

    public async Task<IReadOnlyList<SimulatorConfigurationHead>> ListHeadsAsync(
        CancellationToken ct = default) =>
        await QueryAsync("""
            SELECT configuration_id,source_id,current_configuration_version,version
            FROM acquisition.simulator_configuration
            ORDER BY configuration_id
            """, _ => { }, reader => new SimulatorConfigurationHead(
                reader.GetGuid(0), reader.GetGuid(1),
                reader.GetInt64(2), reader.GetInt64(3)), ct);

    public async Task CreateAsync(
        SimulatorConfigurationHead head,
        SimulatorConfigurationVersion firstVersion,
        CancellationToken ct = default)
    {
        var ownsTransaction = _state.Value?.Current is null;
        IConfigurationTransaction? transaction = null;
        if (ownsTransaction) transaction = await BeginTransactionAsync(ct);
        try
        {
            await ExecuteAsync("""
                INSERT INTO acquisition.simulator_configuration
                    (configuration_id,source_id,current_configuration_version,version,
                     created_by_user_id,created_by_username,created_at_utc,updated_at_utc)
                VALUES
                    (@id,@source_id,@current_version,@version,@actor_id,@actor_name,
                     @created_at,@created_at)
                """, command =>
            {
                command.Parameters.AddWithValue("id", head.ConfigurationId);
                command.Parameters.AddWithValue("source_id", head.SourceId);
                command.Parameters.AddWithValue("current_version", head.CurrentConfigurationVersion);
                command.Parameters.AddWithValue("version", head.Version);
                command.Parameters.AddWithValue("actor_id", firstVersion.CreatedByUserId);
                command.Parameters.AddWithValue("actor_name", firstVersion.CreatedByUsername);
                command.Parameters.AddWithValue("created_at", firstVersion.CreatedAtUtc);
            }, ct);
            await InsertVersionAsync(firstVersion, ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            throw new InvalidOperationException("CONFIGURATION_CONFLICT", exception);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task AppendVersionAsync(
        Guid configurationId,
        long expectedAggregateVersion,
        SimulatorConfigurationVersion nextVersion,
        CancellationToken ct = default)
    {
        var ownsTransaction = _state.Value?.Current is null;
        IConfigurationTransaction? transaction = null;
        if (ownsTransaction) transaction = await BeginTransactionAsync(ct);
        try
        {
            var affected = await ExecuteAsync("""
                UPDATE acquisition.simulator_configuration
                SET current_configuration_version=@configuration_version,
                    version=version+1,
                    updated_at_utc=now()
                WHERE configuration_id=@id AND version=@expected_version
                """, command =>
            {
                command.Parameters.AddWithValue("id", configurationId);
                command.Parameters.AddWithValue("configuration_version", nextVersion.ConfigurationVersion);
                command.Parameters.AddWithValue("expected_version", expectedAggregateVersion);
            }, ct);
            if (affected != 1) throw new InvalidOperationException("CONFIGURATION_VERSION_CONFLICT");
            await InsertVersionAsync(nextVersion, ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public Task<IConfigurationTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        var holder = _state.Value ??= new TransactionHolder();
        if (holder.Current is not null)
            throw new InvalidOperationException("CONFIGURATION_TRANSACTION_ALREADY_ACTIVE");
        return BeginTransactionCoreAsync(holder, ct);
    }

    private async Task<IConfigurationTransaction> BeginTransactionCoreAsync(
        TransactionHolder holder,
        CancellationToken ct)
    {
        var connection = await _dataSource.OpenConnectionAsync(ct);
        var transaction = await connection.BeginTransactionAsync(
            System.Data.IsolationLevel.RepeatableRead,
            ct);
        var state = new TransactionState(connection, transaction);
        holder.Current = state;
        return new ConfigurationTransaction(state, () => holder.Current = null);
    }

    private async Task<SimulatorConfigurationHead?> QueryHeadAsync(
        string predicate,
        Guid value,
        CancellationToken ct)
    {
        var values = await QueryAsync($"""
            SELECT configuration_id,source_id,current_configuration_version,version
            FROM acquisition.simulator_configuration
            WHERE {predicate}
            """, command => command.Parameters.AddWithValue("value", value),
            reader => new SimulatorConfigurationHead(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetInt64(2), reader.GetInt64(3)), ct);
        return values.SingleOrDefault();
    }

    private Task<int> InsertVersionAsync(
        SimulatorConfigurationVersion version,
        CancellationToken ct) =>
        ExecuteAsync("""
            INSERT INTO acquisition.simulator_configuration_version
                (configuration_id,configuration_version,interval_seconds,minimum_value,maximum_value,
                 deterministic_seed,scenario_type,algorithm_id,algorithm_version,created_by_user_id,
                 created_by_username,created_at_utc,correlation_id,causation_id)
            VALUES
                (@id,@version,@interval,@minimum,@maximum,@seed,@scenario,@algorithm_id,
                 @algorithm_version,@actor_id,@actor_name,@created_at,@correlation_id,@causation_id)
            """, command =>
        {
            command.Parameters.AddWithValue("id", version.ConfigurationId);
            command.Parameters.AddWithValue("version", version.ConfigurationVersion);
            command.Parameters.AddWithValue("interval", version.IntervalSeconds);
            command.Parameters.AddWithValue("minimum", version.MinimumValue);
            command.Parameters.AddWithValue("maximum", version.MaximumValue);
            command.Parameters.AddWithValue("seed", (decimal)version.DeterministicSeed);
            command.Parameters.AddWithValue("scenario", version.ScenarioType.ToString());
            command.Parameters.AddWithValue("algorithm_id", version.AlgorithmId);
            command.Parameters.AddWithValue("algorithm_version", version.AlgorithmVersion);
            command.Parameters.AddWithValue("actor_id", version.CreatedByUserId);
            command.Parameters.AddWithValue("actor_name", version.CreatedByUsername);
            command.Parameters.AddWithValue("created_at", version.CreatedAtUtc);
            command.Parameters.AddWithValue("correlation_id", (object?)version.CorrelationId ?? DBNull.Value);
            command.Parameters.AddWithValue("causation_id", (object?)version.CausationId ?? DBNull.Value);
        }, ct);

    private static SimulatorConfigurationVersion MapVersion(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetInt64(1),
        reader.GetInt32(2),
        reader.GetDouble(3),
        reader.GetDouble(4),
        checked((ulong)reader.GetDecimal(5)),
        Enum.Parse<SimulatorScenario>(reader.GetString(6), false),
        reader.GetString(7),
        reader.GetInt32(8),
        reader.GetString(9),
        reader.GetString(10),
        reader.GetDateTime(11).ToUniversalTime(),
        reader.IsDBNull(12) ? null : reader.GetString(12),
        reader.IsDBNull(13) ? null : reader.GetString(13));

    private async Task<int> ExecuteAsync(
        string sql,
        Action<NpgsqlCommand> bind,
        CancellationToken ct)
    {
        var (connection, owns) = await AcquireAsync(ct);
        try
        {
            await using var command = new NpgsqlCommand(sql, connection, CurrentTransaction);
            bind(command);
            return await command.ExecuteNonQueryAsync(ct);
        }
        finally { if (owns) await connection.DisposeAsync(); }
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        Action<NpgsqlCommand> bind,
        Func<NpgsqlDataReader, T> map,
        CancellationToken ct)
    {
        var (connection, owns) = await AcquireAsync(ct);
        try
        {
            await using var command = new NpgsqlCommand(sql, connection, CurrentTransaction);
            bind(command);
            await using var reader = await command.ExecuteReaderAsync(ct);
            var values = new List<T>();
            while (await reader.ReadAsync(ct)) values.Add(map(reader));
            return values;
        }
        finally { if (owns) await connection.DisposeAsync(); }
    }

    private NpgsqlTransaction? CurrentTransaction =>
        _state.Value?.Current?.Transaction ?? _hostTransactions.Current?.Transaction;

    private async Task<(NpgsqlConnection Connection, bool Owns)> AcquireAsync(CancellationToken ct) =>
        _state.Value?.Current is { } state
            ? (state.Connection, false)
            : _hostTransactions.Current is { IsCompleted: false } host
                ? (host.Connection, false)
                : (await _dataSource.OpenConnectionAsync(ct), true);

    private sealed record TransactionState(NpgsqlConnection Connection, NpgsqlTransaction Transaction);

    private sealed class TransactionHolder
    {
        public TransactionState? Current { get; set; }
    }

    private sealed class ConfigurationTransaction(
        TransactionState state,
        Action completed) : IConfigurationTransaction
    {
        private bool _completed;
        public async Task CommitAsync(CancellationToken ct = default)
        {
            if (_completed) return;
            try { await state.Transaction.CommitAsync(ct); }
            finally { await FinishAsync(); }
        }
        public async Task RollbackAsync(CancellationToken ct = default)
        {
            if (_completed) return;
            try { await state.Transaction.RollbackAsync(ct); }
            finally { await FinishAsync(); }
        }
        public void Dispose()
        {
            if (!_completed)
                RollbackAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        private async Task FinishAsync()
        {
            if (_completed) return;
            _completed = true;
            completed();
            await state.Transaction.DisposeAsync();
            await state.Connection.DisposeAsync();
        }
    }
}
