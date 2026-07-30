using IUMP.Infrastructure.Postgres;
using IUMP.Modules.Operations.Infrastructure;

namespace IUMP.Modules.Operations.Contracts;

public static class OperationsPostgresServices
{
    public static IReadOnlyList<PostgresServiceBinding> Bindings { get; } =
    [
        new(typeof(PostgresJobRepositories),
            typeof(IOperationsStore),
            typeof(IDurableJobScheduler),
            typeof(IJobClaimRepository),
            typeof(IAuditDeliveryOperationsRepository))
    ];
}
