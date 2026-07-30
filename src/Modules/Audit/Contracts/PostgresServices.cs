using IUMP.Infrastructure.Postgres;
using IUMP.Modules.Audit.Application;
using IUMP.Modules.Audit.Infrastructure;

namespace IUMP.Modules.Audit.Contracts;

public static class AuditPostgresServices
{
    public static IReadOnlyList<PostgresServiceBinding> Bindings { get; } =
    [
        new(typeof(PostgresAuditRepositories),
            typeof(IAuditAppendRepository),
            typeof(ITransactionalAuditAppendRepository),
            typeof(IAuditConflictRepository),
            typeof(IAuditQueryRepository)),
        new(typeof(AuditAuthorization)),
        new(typeof(AuditEventConsumer),
            typeof(IAuditEventConsumer),
            typeof(ITransactionalAuditEventConsumer)),
        new(typeof(AuditQueryService))
    ];
}
