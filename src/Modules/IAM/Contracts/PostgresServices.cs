using IUMP.Infrastructure.Postgres;
using IUMP.Modules.IAM.Application;
using IUMP.Modules.IAM.Infrastructure;
using IUMP.Modules.Organization.Contracts;

namespace IUMP.Modules.IAM.Contracts;

public static class IamPostgresServices
{
    public static IReadOnlyList<PostgresServiceBinding> Bindings { get; } =
    [
        new(typeof(PostgresIamRepositories),
            typeof(IIamCommandRepository),
            typeof(IIamPrincipalSessionRepository),
            typeof(IActivationIdentityParticipant)),
        new(typeof(PostgresAuthService), typeof(IAuthService)),
        new(
            typeof(EngineerScopeAssignmentService),
            typeof(IEngineerScopeAssignmentService)),
        new(typeof(IdentityRuntimeGateway))
    ];
}
