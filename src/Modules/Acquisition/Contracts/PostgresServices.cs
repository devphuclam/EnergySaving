using IUMP.Infrastructure.Postgres;
using IUMP.Modules.Acquisition.Infrastructure;

namespace IUMP.Modules.Acquisition.Contracts;

public static class AcquisitionPostgresServices
{
    public static IReadOnlyList<PostgresServiceBinding> Bindings { get; } =
    [
        new(typeof(PostgresConfigurationRepository),
            typeof(IAcquisitionConfigurationRepository)),
        new(typeof(PostgresAcquisitionRunRepository),
            typeof(IAcquisitionRunRepository)),
        new(typeof(PostgresSimulatorProductionAttemptRepository),
            typeof(ISimulatorProductionAttemptRepository)),
        new(typeof(PostgresSimulatorRunUnitOfWork),
            typeof(ISimulatorRunUnitOfWork))
    ];
}
