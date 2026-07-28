using IUMP.Modules.Acquisition.Contracts;

namespace IUMP.Worker;

public sealed class SimulatorProductionWorker
{
    private readonly ISimulatorProductionCoordinator _coordinator;
    private readonly ILogger<SimulatorProductionWorker> _logger;

    public SimulatorProductionWorker(
        ISimulatorProductionCoordinator coordinator,
        ILogger<SimulatorProductionWorker> logger)
    {
        _coordinator = coordinator;
        _logger = logger;
    }

    public async Task<SimulatorProductionCycleResult> RunOnceAsync(
        string workerId,
        CancellationToken ct = default)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["WorkerId"] = workerId
        });
        _logger.LogInformation("Simulator production cycle started");
        try
        {
            var result = await _coordinator.RunOnceAsync(workerId, ct);
            foreach (var failure in result.Failures)
            {
                _logger.LogWarning(
                    "Simulator Point production failed with Code {Code}, RunId {RunId}, PointId {PointId}, CorrelationId {CorrelationId}",
                    failure.Code, failure.RunId, failure.PointId, failure.CorrelationId);
            }
            _logger.LogInformation(
                "Simulator production cycle completed with RunningRuns {RunningRuns}, ClaimedPoints {ClaimedPoints}, DispatchedAttempts {DispatchedAttempts}, FinalizedAttempts {FinalizedAttempts}, FailedPoints {FailedPoints}",
                result.RunningRuns, result.ClaimedPoints, result.DispatchedAttempts,
                result.FinalizedAttempts, result.FailedPoints);
            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogInformation("Simulator production cycle cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Simulator production cycle failed");
            throw;
        }
    }
}
