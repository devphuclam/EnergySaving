using IUMP.Infrastructure.Postgres;
using IUMP.Modules.Telemetry.Infrastructure;

namespace IUMP.Modules.Telemetry.Contracts;

public static class TelemetryPostgresServices
{
    public static IReadOnlyList<PostgresServiceBinding> Bindings { get; } =
    [
        new(typeof(PostgresTelemetryRepositories),
            typeof(ITelemetryIngestionRepository),
            typeof(IPointLatestProjectionRepository),
            typeof(ILatestProjectionRepository),
            typeof(ISourceHealthProjectionRepository),
            typeof(ISourceHealthRepository),
            typeof(ITelemetryQueryRepository)),
        new(typeof(PostgresTelemetryFlowUnitOfWork),
            typeof(ITelemetryFlowUnitOfWork)),
        new(typeof(PostgresMeasurementAcceptedEventWriter),
            typeof(IMeasurementAcceptedEventWriter))
    ];
}
