using IUMP.Infrastructure.Postgres;
using IUMP.Modules.Organization.Infrastructure;

namespace IUMP.Modules.Organization.Contracts;

public static class OrganizationPostgresServices
{
    public static IReadOnlyList<PostgresServiceBinding> Bindings { get; } =
    [
        new(typeof(PostgresOrganizationRepositories),
            typeof(IOrganizationCommandRepository),
            typeof(IOrganizationActivationTargetQuery),
            typeof(IOrganizationQueryRepository),
            typeof(IActivationOrganizationParticipant)),
        new(typeof(OrganizationRuntimeGateway))
    ];
}
