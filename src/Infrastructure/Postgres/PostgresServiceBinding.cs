namespace IUMP.Infrastructure.Postgres;

/// <summary>
/// Module-owned PostgreSQL registration metadata. Modules expose implementation
/// types without requiring the host composition root to import module internals.
/// </summary>
public sealed record PostgresServiceBinding(
    Type ImplementationType,
    params Type[] ServiceTypes);
