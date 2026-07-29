using IUMP.Modules.Integration.Contracts;

namespace IUMP.Api.Infrastructure;

public sealed record CommandExecutionResult(int StatusCode, string Body, string? ResourceReference,
    string? Location = null, string? ETag = null, string? CorrelationId = null)
{
    public static CommandExecutionResult Ok(int statusCode, string body, string? resourceReference,
        string? location = null, string? etag = null, string? correlationId = null) =>
        new(statusCode, body, resourceReference, location, etag, correlationId);
}

public sealed record IdempotentCommandResponse(int StatusCode, string Body, string Code, bool IsReplay,
    string? ResourceReference = null, string? Location = null, string? ETag = null);

public sealed class IdempotentCommandExecutor(ICommandIdempotencyStore store)
{
    private static readonly TimeSpan Lease = TimeSpan.FromSeconds(30);

    public async Task<IdempotentCommandResponse> ExecuteAsync(
        CommandIdentity identity,
        byte[] fingerprint,
        Func<CancellationToken, Task<CommandExecutionResult>> mutation,
        CancellationToken ct = default)
    {
        var registration = await store.RegisterOrReadAsync(identity, fingerprint, null, Lease, ct);
        if (registration.Conflict)
            return new(409, "{\"errorCode\":\"IDEMPOTENCY_CONFLICT\"}", "IDEMPOTENCY_CONFLICT", false);
        if (registration.InProgress)
            return new(409, "{\"errorCode\":\"IDEMPOTENCY_IN_PROGRESS\"}", "IDEMPOTENCY_IN_PROGRESS", false);
        if (registration.Equivalent && registration.Record.OriginalResult is { } replay)
            return new(replay.StatusCode, replay.Body, "DUPLICATE", true, replay.ResourceReference, replay.Location, replay.ETag);

        var reservation = registration.Record;
        if (reservation.Status == CommandIdempotencyStatus.Pending && !reservation.IsLeaseLive(DateTime.UtcNow))
        {
            reservation = await store.TryReclaimExpiredAsync(
                reservation.Id, reservation.Version, "api", DateTime.UtcNow.Add(Lease), ct) ?? reservation;
            if (reservation.IsLeaseLive(DateTime.UtcNow) && reservation.PendingOwner != "api")
                return new(409, "{\"errorCode\":\"IDEMPOTENCY_IN_PROGRESS\"}", "IDEMPOTENCY_IN_PROGRESS", false);
        }

        CommandExecutionResult result;
        try { result = await mutation(ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            return new(503, "{\"errorCode\":\"TRANSIENT_DATABASE_CONFLICT\",\"retryable\":true}", "TRANSIENT_DATABASE_CONFLICT", false);
        }

        var completed = await store.CompleteAsync(reservation.Id, reservation.Version,
            new StoredHttpResult(result.StatusCode, result.Body, result.ResourceReference, result.Location, result.ETag, result.CorrelationId),
            DateTime.UtcNow.AddHours(24), ct);
        if (completed is null)
            return new(503, "{\"errorCode\":\"TRANSIENT_DATABASE_CONFLICT\",\"retryable\":true}", "TRANSIENT_DATABASE_CONFLICT", false);
        return new(result.StatusCode, result.Body, "EXECUTED", false, result.ResourceReference, result.Location, result.ETag);
    }
}
