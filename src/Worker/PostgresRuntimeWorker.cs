using IUMP.Modules.Acquisition.Contracts;
using IUMP.Worker.Integration;
using System.Diagnostics;

namespace IUMP.Worker;

public sealed class PostgresRuntimeWorker(
    IServiceScopeFactory scopes,
    ILogger<PostgresRuntimeWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "IUMP PostgreSQL runtime worker started with DatabaseState {DatabaseState}",
            "available");
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        do
        {
            var cycleCorrelationId = Guid.NewGuid().ToString("N");
            var cycleStarted = Stopwatch.GetTimestamp();
            try
            {
                using var scope = scopes.CreateScope();
                var production =
                    scope.ServiceProvider.GetRequiredService<ISimulatorProductionCoordinator>();
                _ = await production.RunOnceAsync(
                    Environment.MachineName, stoppingToken);

                var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcherWorker>();
                _ = await dispatcher.DispatchOnceAsync(DateTime.UtcNow, stoppingToken);
                logger.LogDebug(
                    "Runtime cycle {EventName} completed with CorrelationId {CorrelationId}, Worker {Worker}, and DurationMs {DurationMs}",
                    "PostgresRuntimeCycle",
                    cycleCorrelationId,
                    Environment.MachineName,
                    Stopwatch.GetElapsedTime(cycleStarted).TotalMilliseconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    "Runtime cycle {EventName} failed with CorrelationId {CorrelationId}, Component {Component}, Worker {Worker}, DurationMs {DurationMs}, and ErrorType {ErrorType}; details are redacted",
                    "PostgresRuntimeCycle",
                    cycleCorrelationId,
                    nameof(PostgresRuntimeWorker),
                    Environment.MachineName,
                    Stopwatch.GetElapsedTime(cycleStarted).TotalMilliseconds,
                    exception.GetType().Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
