using IUMP.Modules.Telemetry.Contracts;

namespace IUMP.Modules.Telemetry.Application;

public sealed class SourceHealthService
{
    private readonly ISourceHealthProjectionRepository _repository;

    public SourceHealthService(ISourceHealthProjectionRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public static SourceHealthStatus EvaluateStatus(
        SourceHealthEvaluationInput input,
        DateTime nowUtc)
    {
        ValidateInput(input, nowUtc);

        if (IsAdministrative(input.PointStatus, input.SourceStatus, "Decommissioned"))
            return SourceHealthStatus.Decommissioned;
        if (IsAdministrative(input.PointStatus, input.SourceStatus, "Suspended"))
            return SourceHealthStatus.Suspended;
        if (!string.Equals(input.PointStatus, "Active", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(input.SourceStatus, "Active", StringComparison.OrdinalIgnoreCase))
            return SourceHealthStatus.Suspended;
        if (input.LastAcceptedReceivedAtUtc is null)
            return SourceHealthStatus.NoData;

        var elapsed = nowUtc - input.LastAcceptedReceivedAtUtc.Value;
        if (elapsed <= TimeSpan.FromSeconds(input.ExpectedIntervalSeconds))
            return SourceHealthStatus.Online;
        if (elapsed <= TimeSpan.FromSeconds(input.NoDataAfterSeconds))
            return SourceHealthStatus.Stale;
        return SourceHealthStatus.NoData;
    }

    public async Task<SourceHealthEvaluationResult> EvaluateAsync(
        SourceHealthEvaluationInput input,
        ITelemetryFlowTransaction transaction,
        DateTime nowUtc,
        CancellationToken ct = default)
    {
        ValidateInput(input, nowUtc);
        var current = await _repository.GetCurrentAsync(input.PointId, ct);
        if (current is not null &&
            (current.PointVersion > input.PointVersion ||
             current.SourceVersion > input.SourceVersion ||
             current.ProviderVersion > input.ProviderVersion))
            throw new InvalidOperationException("PROVIDER_VERSION_STALE");

        var status = EvaluateStatus(input, nowUtc);
        var result = await _repository.CompareAndSetAsync(
            input, status, nowUtc, transaction, ct);
        if (!result.Changed) return result;

        var next = result.Current;
        var previous = result.Previous;
        var healthEvent = new PointSourceHealthChangedEvent(
            Guid.NewGuid(),
            input.PointId,
            input.SourceId,
            previous?.Status ?? status,
            next.Status,
            nowUtc,
            next.LastAcceptedReceivedAtUtc,
            input.SiteId,
            input.AreaId);
        await _repository.StageChangedEventAsync(healthEvent, transaction, ct);
        return result;
    }

    public Task<SourceHealthEvaluationResult> EvaluateAndPersistAsync(
        SourceHealthEvaluationInput input,
        ITelemetryFlowTransaction transaction,
        DateTime nowUtc,
        CancellationToken ct = default) =>
        EvaluateAsync(input, transaction, nowUtc, ct);

    private static bool IsAdministrative(
        string pointStatus, string sourceStatus, string expected) =>
        string.Equals(pointStatus, expected, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(sourceStatus, expected, StringComparison.OrdinalIgnoreCase);

    private static void ValidateInput(
        SourceHealthEvaluationInput input,
        DateTime nowUtc)
    {
        if (input.PointId == Guid.Empty || input.SourceId == Guid.Empty ||
            string.IsNullOrWhiteSpace(input.SiteId) || input.PointVersion <= 0 ||
            input.SourceVersion <= 0 || input.ProviderVersion <= 0)
            throw new InvalidOperationException("HEALTH_PROVIDER_INVALID");
        if (input.ExpectedIntervalSeconds <= 0)
            throw new InvalidOperationException("EXPECTED_INTERVAL_INVALID");
        if (input.NoDataAfterSeconds <= input.ExpectedIntervalSeconds)
            throw new InvalidOperationException("NO_DATA_THRESHOLD_INVALID");
        if (nowUtc.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("HEALTH_TIMESTAMP_INVALID");
        if (input.LastAcceptedReceivedAtUtc is { Kind: not DateTimeKind.Utc })
            throw new InvalidOperationException("HEALTH_TIMESTAMP_INVALID");
        if (input.LastAcceptedReceivedAtUtc is { } last && last > nowUtc)
            throw new InvalidOperationException("HEALTH_TIMESTAMP_INVALID");
        if (input.GeneratedCount < 0 || input.AcceptedCount < 0 || input.RejectedCount < 0)
            throw new InvalidOperationException("HEALTH_COUNTER_INVALID");
    }
}
