using System.Data.Common;
using Npgsql;

using IUMP.BuildingBlocks.Persistence;

namespace IUMP.Infrastructure.Postgres;

public sealed record PostgresRuntimeConfiguration(
    string ConnectionString,
    string Host,
    int Port,
    string Database,
    string Username)
{
    public const int ApprovedLocalPort = 5433;
    public const string ApprovedLocalHost = "127.0.0.1";
    public const string ApprovedLocalDatabase = "iump_dev";
    public const string RuntimeRole = "iump_app";
    public const int RequiredMigrationLevel = 15;

    public string SafeTarget => $"{Host}:{Port}/{Database} as {Username}";

    public override string ToString() => SafeTarget;

    public static PostgresRuntimeConfiguration CreateRuntime(string? configuredConnectionString = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredConnectionString))
            return ParseAndValidate(configuredConnectionString, requireRuntimeRole: true);

        var host = Required("IUMP_DB_HOST");
        var portText = Required("IUMP_DB_PORT");
        var database = Required("IUMP_DB_NAME");
        var password = Optional("IUMP_APP_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("RUNTIME_DEPENDENCY_UNAVAILABLE");
        if (!int.TryParse(portText, out var port))
            throw new InvalidOperationException("RUNTIME_DEPENDENCY_UNAVAILABLE");

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = database,
            Username = RuntimeRole,
            Password = password,
            Pooling = true,
            Enlist = true,
            IncludeErrorDetail = false,
            LogParameters = false,
            ApplicationName = "IUMP"
        };
        return ParseAndValidate(builder.ConnectionString, requireRuntimeRole: true);
    }

    public static PostgresRuntimeConfiguration CreateBootstrap()
    {
        var host = Required("IUMP_DB_HOST");
        var portText = Required("IUMP_DB_PORT");
        var database = Required("IUMP_DB_NAME");
        var username = Required("IUMP_DB_USER");
        var password = Required("IUMP_DB_PASSWORD");
        if (!int.TryParse(portText, out var port))
            throw new InvalidOperationException("DATABASE_CONNECTION_RUNTIME_FAILURE");

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = database,
            Username = username,
            Password = password,
            Pooling = true,
            Enlist = true,
            IncludeErrorDetail = false,
            LogParameters = false,
            ApplicationName = "IUMP-Migration"
        };
        return ParseAndValidate(builder.ConnectionString, requireRuntimeRole: false);
    }

    private static PostgresRuntimeConfiguration ParseAndValidate(
        string connectionString,
        bool requireRuntimeRole)
    {
        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException("RUNTIME_DEPENDENCY_UNAVAILABLE", exception);
        }

        if (!string.Equals(builder.Host, ApprovedLocalHost, StringComparison.Ordinal) ||
            builder.Port != ApprovedLocalPort ||
            !string.Equals(builder.Database, ApprovedLocalDatabase, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(builder.Password) ||
            (requireRuntimeRole &&
             !string.Equals(builder.Username, RuntimeRole, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("RUNTIME_DEPENDENCY_UNAVAILABLE");
        }

        builder.Pooling = true;
        builder.IncludeErrorDetail = false;
        builder.LogParameters = false;
        return new PostgresRuntimeConfiguration(
            builder.ConnectionString,
            builder.Host!,
            builder.Port,
            builder.Database!,
            builder.Username!);
    }

    private static string Required(string name) =>
        Optional(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException("RUNTIME_DEPENDENCY_UNAVAILABLE");

    private static string? Optional(string name) =>
        Environment.GetEnvironmentVariable(name) ??
        Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
}

public static class LocalEnvironmentFile
{
    private static readonly HashSet<string> AllowedKeys = new(StringComparer.Ordinal)
    {
        "IUMP_DB_HOST",
        "IUMP_DB_PORT",
        "IUMP_DB_NAME",
        "IUMP_DB_USER",
        "IUMP_DB_PASSWORD",
        "IUMP_MIGRATION_PASSWORD",
        "IUMP_APP_PASSWORD",
        "IUMP_READONLY_PASSWORD",
        "ConnectionStrings__IumpDatabase"
    };

    public static void LoadIfPresent(string contentRoot)
    {
        foreach (var filename in new[] { ".env.local", ".env" })
        {
            var path = Path.Combine(contentRoot, filename);
            if (!File.Exists(path)) continue;

            foreach (var line in File.ReadLines(path))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
                var separator = trimmed.IndexOf('=');
                if (separator <= 0) continue;
                var key = trimmed[..separator].Trim();
                if (!AllowedKeys.Contains(key) ||
                    !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
                    continue;
                var value = trimmed[(separator + 1)..].Trim().Trim('"');
                Environment.SetEnvironmentVariable(key, value, EnvironmentVariableTarget.Process);
            }
        }
    }

    public static void LoadFromAncestors(string startDirectory)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startDirectory));
        for (var depth = 0; current is not null && depth < 6; depth++, current = current.Parent)
        {
            LoadIfPresent(current.FullName);
            if (File.Exists(Path.Combine(current.FullName, "IUMP.slnx"))) return;
        }
    }
}

public sealed class PostgresHostTransaction : IHostTransaction, IHostTransactionController
{
    private readonly NpgsqlConnection _connection;
    private readonly NpgsqlTransaction _transaction;
    private readonly Action<PostgresHostTransaction> _completed;
    private bool _disposed;

    internal PostgresHostTransaction(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Action<PostgresHostTransaction> completed)
    {
        _connection = connection;
        _transaction = transaction;
        _completed = completed;
    }

    public Guid TransactionId { get; } = Guid.NewGuid();
    public string IsolationIntent => "REPEATABLE READ";
    public bool IsCompleted { get; private set; }
    public NpgsqlConnection Connection => _connection;
    public NpgsqlTransaction Transaction => _transaction;

    public async ValueTask CommitAsync(CancellationToken ct = default)
    {
        if (IsCompleted) return;
        await _transaction.CommitAsync(ct);
        IsCompleted = true;
    }

    public async ValueTask RollbackAsync(CancellationToken ct = default)
    {
        if (IsCompleted) return;
        await _transaction.RollbackAsync(ct);
        IsCompleted = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        if (!IsCompleted)
            await RollbackAsync();
        await _transaction.DisposeAsync();
        await _connection.DisposeAsync();
        _completed(this);
        _disposed = true;
    }
}

public sealed class PostgresTransactionContext
{
    public PostgresHostTransaction? Current { get; internal set; }
}

public sealed class PostgresHostTransactionFactory(
    NpgsqlDataSource dataSource,
    PostgresTransactionContext context) : IHostTransactionFactory
{
    public async ValueTask<IHostTransaction> BeginAsync(CancellationToken ct = default)
    {
        var connection = await dataSource.OpenConnectionAsync(ct);
        try
        {
            var transaction = await connection.BeginTransactionAsync(
                System.Data.IsolationLevel.RepeatableRead,
                ct);
            if (context.Current is not null)
                throw new InvalidOperationException("POSTGRES_HOST_TRANSACTION_ALREADY_ACTIVE");
            var host = new PostgresHostTransaction(connection, transaction, completed =>
            {
                if (ReferenceEquals(context.Current, completed))
                    context.Current = null;
            });
            context.Current = host;
            return host;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}

public static class PostgresTransactionResolver
{
    public static PostgresHostTransaction Require(IHostTransaction transaction)
    {
        var current = transaction;
        for (var depth = 0; depth < 4; depth++)
        {
            if (current is PostgresHostTransaction postgres)
                return postgres;
            if (current is IHostTransactionAccessor accessor)
            {
                current = accessor.InnerTransaction;
                continue;
            }
            break;
        }
        throw new InvalidOperationException("POSTGRES_HOST_TRANSACTION_REQUIRED");
    }
}

public sealed class PostgresHostTransactionBackend(
    IHostTransactionFactory factory,
    PostgresTransactionContext context) : IHostTransactionBackend
{
    public async ValueTask<IHostTransaction> BeginAsync(CancellationToken ct = default)
    {
        if (context.Current is not { IsCompleted: false } ambient)
            return await factory.BeginAsync(ct);
        var savepoint = $"iump_nested_{Guid.NewGuid():N}";
        await ambient.Transaction.SaveAsync(savepoint, ct);
        return new BorrowedPostgresHostTransaction(ambient, savepoint);
    }

    public ValueTask CommitAsync(IHostTransaction transaction, CancellationToken ct = default) =>
        ((IHostTransactionController)transaction).CommitAsync(ct);

    public ValueTask RollbackAsync(IHostTransaction transaction, CancellationToken ct = default) =>
        ((IHostTransactionController)transaction).RollbackAsync(ct);

    private sealed class BorrowedPostgresHostTransaction(
        PostgresHostTransaction inner,
        string savepoint) :
        IHostTransaction,
        IHostTransactionController,
        IHostTransactionAccessor
    {
        private bool _completed;
        public Guid TransactionId => inner.TransactionId;
        public string IsolationIntent => inner.IsolationIntent;
        public bool IsCompleted => _completed || inner.IsCompleted;
        public IHostTransaction InnerTransaction => inner;
        public async ValueTask CommitAsync(CancellationToken ct = default)
        {
            if (IsCompleted) return;
            await inner.Transaction.ReleaseAsync(savepoint, ct);
            _completed = true;
        }
        public async ValueTask RollbackAsync(CancellationToken ct = default)
        {
            if (IsCompleted) return;
            await inner.Transaction.RollbackAsync(savepoint, ct);
            _completed = true;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
