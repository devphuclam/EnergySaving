using IUMP.Modules.Integration.Contracts;
using IUMP.BuildingBlocks.Persistence;
using Microsoft.AspNetCore.Http;

namespace IUMP.Api.Infrastructure;

public sealed record CommandExecutionResult(int StatusCode, string Body, string? ResourceReference,
    string? Location = null, string? ETag = null, string? CorrelationId = null)
{
    public static CommandExecutionResult Ok(int statusCode, string body, string? resourceReference,
        string? location = null, string? etag = null, string? correlationId = null) =>
        new(statusCode, body, resourceReference, location, etag, correlationId);
}

public sealed record IdempotentCommandResponse(int StatusCode, string Body, string Code, bool IsReplay,
    string? ResourceReference = null, string? Location = null, string? ETag = null, string? CorrelationId = null);

public sealed class IdempotentHttpResult(IdempotentCommandResponse response) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = response.StatusCode;
        httpContext.Response.ContentType = "application/json";
        if (!string.IsNullOrWhiteSpace(response.Location)) httpContext.Response.Headers["Location"] = response.Location;
        if (!string.IsNullOrWhiteSpace(response.ETag)) httpContext.Response.Headers["ETag"] = response.ETag;
        if (!string.IsNullOrWhiteSpace(response.CorrelationId)) httpContext.Response.Headers["X-Correlation-Id"] = response.CorrelationId;
        await httpContext.Response.WriteAsync(response.Body);
    }
}

public sealed class IdempotentCommandExecutor
{
    private static readonly TimeSpan Lease = TimeSpan.FromSeconds(30);
    private readonly ICommandIdempotencyStore _store;
    private readonly IUtcClock _clock;
    private readonly IServerPrincipalAccessor? _principalAccessor;

    public IdempotentCommandExecutor(ICommandIdempotencyStore store, IUtcClock? clock = null,
        IServerPrincipalAccessor? principalAccessor = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? new SystemUtcClock();
        _principalAccessor = principalAccessor;
    }

    public async Task<IdempotentCommandResponse> ExecuteAsync(
        CommandIdentity identity,
        byte[] fingerprint,
        Func<CancellationToken, Task<CommandExecutionResult>> mutation,
        CancellationToken ct = default)
    {
        if (_principalAccessor is not null && (_principalAccessor.Current is null ||
            _principalAccessor.Current.UserId != identity.CallerUserId))
            return new(401, "{\"errorCode\":\"UNAUTHENTICATED\"}", "UNAUTHENTICATED", false);

        var nowUtc = _clock.UtcNow.ToUniversalTime();
        var registration = await _store.RegisterOrReadAsync(identity, fingerprint, null, Lease, ct);
        if (registration.Conflict)
            return new(409, "{\"errorCode\":\"IDEMPOTENCY_CONFLICT\"}", "IDEMPOTENCY_CONFLICT", false);
        if (registration.InProgress)
            return new(409, "{\"errorCode\":\"IDEMPOTENCY_IN_PROGRESS\"}", "IDEMPOTENCY_IN_PROGRESS", false);
        if (registration.Equivalent && registration.Record.OriginalResult is { } replay)
            return new(replay.StatusCode, replay.Body, "DUPLICATE", true, replay.ResourceReference,
                replay.Location, replay.ETag, replay.OriginalCorrelationId);

        var reservation = registration.Record;
        if (reservation.Status == CommandIdempotencyStatus.Pending && !reservation.IsLeaseLive(nowUtc))
        {
            reservation = await _store.TryReclaimExpiredAsync(
                reservation.Id, reservation.Version, "server-principal", nowUtc.Add(Lease), ct) ?? reservation;
            if (reservation.IsLeaseLive(nowUtc) && reservation.PendingOwner != "server-principal")
                return new(409, "{\"errorCode\":\"IDEMPOTENCY_IN_PROGRESS\"}", "IDEMPOTENCY_IN_PROGRESS", false);
        }

        CommandExecutionResult result;
        try { result = await mutation(ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (TransientDatabaseConflictException)
        {
            return new(503, "{\"errorCode\":\"TRANSIENT_DATABASE_CONFLICT\",\"retryable\":true}", "TRANSIENT_DATABASE_CONFLICT", false);
        }

        var completed = await _store.CompleteAsync(reservation.Id, reservation.Version,
            new StoredHttpResult(result.StatusCode, result.Body, result.ResourceReference, result.Location, result.ETag, result.CorrelationId),
            nowUtc.AddHours(24), ct);
        if (completed is null)
            return new(503, "{\"errorCode\":\"TRANSIENT_DATABASE_CONFLICT\",\"retryable\":true}", "TRANSIENT_DATABASE_CONFLICT", false);
        return new(result.StatusCode, result.Body, "EXECUTED", false, result.ResourceReference,
            result.Location, result.ETag, result.CorrelationId);
    }

    /// <summary>Runs owner mutation and Integration outbox append inside one host transaction.</summary>
    public async Task<IdempotentCommandResponse> ExecuteTransactionalAsync(
        CommandIdentity identity, byte[] fingerprint, IHostTransactionFactory transactionFactory,
        Func<IHostTransaction, CancellationToken, Task<CommandExecutionResult>> mutation,
        CancellationToken ct = default)
    {
        await using var transaction = await transactionFactory.BeginAsync(ct);
        try
        {
            var response = await ExecuteAsync(identity, fingerprint,
                _ => mutation(transaction, ct), ct);
            if (response.Code is "EXECUTED" or "DUPLICATE") await transaction.CommitAsyncIfSupported(ct);
            return response;
        }
        catch
        {
            await transaction.RollbackAsyncIfSupported(CancellationToken.None);
            throw;
        }
    }
}

internal static class HostTransactionExtensions
{
    public static ValueTask CommitAsyncIfSupported(this IHostTransaction transaction, CancellationToken ct) =>
        transaction is IHostTransactionController controller ? controller.CommitAsync(ct) : ValueTask.CompletedTask;

    public static ValueTask RollbackAsyncIfSupported(this IHostTransaction transaction, CancellationToken ct) =>
        transaction is IHostTransactionController controller ? controller.RollbackAsync(ct) : ValueTask.CompletedTask;
}
