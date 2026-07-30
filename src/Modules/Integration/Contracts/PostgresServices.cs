using IUMP.Infrastructure.Postgres;
using IUMP.Modules.Integration.Infrastructure;

namespace IUMP.Modules.Integration.Contracts;

public static class IntegrationPostgresServices
{
    public static IReadOnlyList<PostgresServiceBinding> Bindings { get; } =
    [
        new(typeof(PostgresCommandIdempotencyStore),
            typeof(ICommandIdempotencyStore),
            typeof(ITransactionalCommandIdempotencyStore)),
        new(typeof(PostgresIntegrationRepositories),
            typeof(IIntegrationStore),
            typeof(IIntegrationDeliveryRepository),
            typeof(IOutboxClaimRepository),
            typeof(IInboxDeduplicationRepository),
            typeof(IInboxStateRepository),
            typeof(ITransactionalInboxRepository),
            typeof(ITransactionalOutboxWriter))
    ];
}
