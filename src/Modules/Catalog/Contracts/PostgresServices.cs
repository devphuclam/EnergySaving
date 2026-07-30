using IUMP.Infrastructure.Postgres;
using IUMP.Modules.Catalog.Infrastructure;
using IUMP.Modules.Organization.Contracts;

namespace IUMP.Modules.Catalog.Contracts;

public static class CatalogPostgresServices
{
    public static IReadOnlyList<PostgresServiceBinding> Bindings { get; } =
    [
        new(typeof(PostgresCatalogRepositories),
            typeof(ICatalogCommandRepository),
            typeof(ICatalogEligibilityQueryRepository),
            typeof(ISourceMappingSnapshotQuery),
            typeof(ICatalogPointReadinessQuery),
            typeof(ICatalogSourceScopeQuery),
            typeof(IActivationCatalogParticipant)),
        new(typeof(CatalogRuntimeGateway))
    ];
}
