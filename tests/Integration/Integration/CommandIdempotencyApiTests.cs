using System.Security.Cryptography;
using System.Text;
using IUMP.Api.Infrastructure;
using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Integration.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IUMP.Tests.Integration.Integration;

public static class CommandIdempotencyApiTests
{
    public static async Task<IReadOnlyList<string>> RunAsync(IServiceProvider services)
    {
        var failures = new List<string>();
        var store = services.GetRequiredService<ICommandIdempotencyStore>();
        var transactional = services.GetRequiredService<ITransactionalCommandIdempotencyStore>();
        var transactions = services.GetRequiredService<IHostTransactionFactory>();
        var outbox = services.GetRequiredService<ITransactionalOutboxWriter>();
        var delivery = services.GetRequiredService<IIntegrationDeliveryRepository>();
        var caller = Guid.NewGuid();
        var key = $"integration-{Guid.NewGuid():N}";
        var identity = new CommandIdentity(caller, CommandOperationCodes.CreateSite, key);
        var fingerprint = SHA256.HashData(Encoding.UTF8.GetBytes(key));

        var registrations = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            store.RegisterOrReadAsync(
                identity, fingerprint, "integration-suite", TimeSpan.FromSeconds(30))));
        Check(registrations.Count(value => value.Created) == 1,
            "T219 concurrent duplicate must create exactly one Pending row", failures);
        Check(registrations.All(value => value.Created || value.InProgress),
            "T219 concurrent duplicates must observe Created or live Pending", failures);

        var first = registrations.Single(value => value.Created).Record;
        var eventId = Guid.NewGuid();
        var result = new StoredHttpResult(
            201, "{\"result\":\"created\"}", "resource-1",
            "/api/v1/sites/resource-1", "\"1\"", "correlation-original");
        await using (var transaction = await transactions.BeginAsync())
        {
            await outbox.EnqueueAsync(Envelope(eventId, caller), transaction);
            var completed = await transactional.CompleteInTransactionAsync(
                first.Id, first.Version, result, DateTime.UtcNow.AddHours(24),
                transaction);
            Check(completed is not null,
                "T219 transactional completion must stage", failures);
            await ((IHostTransactionController)transaction).CommitAsync();
        }

        var replay = await store.RegisterOrReadAsync(
            identity, fingerprint, "integration-suite", TimeSpan.FromSeconds(30));
        Check(replay.Equivalent &&
            replay.Record.OriginalResult?.StatusCode == 201 &&
            replay.Record.OriginalResult.Body == result.Body &&
            replay.Record.OriginalResult.Location == result.Location &&
            replay.Record.OriginalResult.ETag == result.ETag &&
            replay.Record.OriginalResult.OriginalCorrelationId ==
                result.OriginalCorrelationId,
            "T219 replay must preserve exact HTTP evidence", failures);
        Check((await delivery.GetAsync(eventId)) is not null,
            "T219 owner completion and outbox must commit atomically", failures);

        var conflict = await store.RegisterOrReadAsync(
            identity, SHA256.HashData(Encoding.UTF8.GetBytes("different")),
            "integration-suite", TimeSpan.FromSeconds(30));
        Check(conflict.Conflict,
            "T219 same identity with another fingerprint must conflict", failures);

        var rollbackIdentity = new CommandIdentity(
            caller, CommandOperationCodes.CreateSite, $"rollback-{Guid.NewGuid():N}");
        var rollbackFingerprint = SHA256.HashData(
            Encoding.UTF8.GetBytes(rollbackIdentity.IdempotencyKey));
        var rollbackRegistration = await store.RegisterOrReadAsync(
            rollbackIdentity, rollbackFingerprint, "integration-suite",
            TimeSpan.FromSeconds(30));
        var rollbackEventId = Guid.NewGuid();
        await using (var transaction = await transactions.BeginAsync())
        {
            await outbox.EnqueueAsync(
                Envelope(rollbackEventId, caller), transaction);
            _ = await transactional.CompleteInTransactionAsync(
                rollbackRegistration.Record.Id,
                rollbackRegistration.Record.Version,
                result, DateTime.UtcNow.AddHours(24), transaction);
            await ((IHostTransactionController)transaction).RollbackAsync();
        }
        var afterRollback = await store.GetAsync(rollbackRegistration.Record.Id);
        Check(afterRollback?.Status == CommandIdempotencyStatus.Pending &&
            await delivery.GetAsync(rollbackEventId) is null,
            "T219 crash/rollback must leave completion and outbox uncommitted", failures);

        var expiredIdentity = new CommandIdentity(
            caller, CommandOperationCodes.UpdateSite,
            $"expired-{Guid.NewGuid():N}");
        var expiredFingerprint = SHA256.HashData(
            Encoding.UTF8.GetBytes(expiredIdentity.IdempotencyKey));
        var expired = await store.RegisterOrReadAsync(
            expiredIdentity, expiredFingerprint, "first-owner",
            TimeSpan.FromMilliseconds(-1));
        var reclaimed = await store.TryReclaimExpiredAsync(
            expired.Record.Id, expired.Record.Version, "second-owner",
            DateTime.UtcNow.AddSeconds(30));
        Check(reclaimed is { Version: 2, AttemptCount: 1 },
            "T219 expired Pending lease must be reclaimable", failures);
        Check(first.ExpiresAtUtc >= first.CreatedAtUtc.AddHours(23.9),
            "T219 command evidence must retain the 24-hour cleanup horizon", failures);

        await ExecuteHttpExecutorCoverageAsync(
            store, transactions, outbox, delivery, failures);
        return failures;
    }

    private static async Task ExecuteHttpExecutorCoverageAsync(
        ICommandIdempotencyStore store,
        IHostTransactionFactory transactions,
        ITransactionalOutboxWriter outbox,
        IIntegrationDeliveryRepository delivery,
        List<string> failures)
    {
        var executor = new IdempotentCommandExecutor(store);
        var caller = Guid.NewGuid();
        var identity = new CommandIdentity(
            caller, CommandOperationCodes.CreateSite,
            $"http-executor-{Guid.NewGuid():N}");
        var fingerprint = SHA256.HashData(
            Encoding.UTF8.GetBytes(identity.IdempotencyKey));
        var eventId = Guid.NewGuid();
        var mutationCount = 0;
        var original = await executor.ExecuteTransactionalAsync(
            identity, fingerprint, transactions,
            async (transaction, ct) =>
            {
                Interlocked.Increment(ref mutationCount);
                await outbox.EnqueueAsync(
                    Envelope(eventId, caller), transaction, ct);
                return CommandExecutionResult.Ok(
                    201, "{\"result\":\"http-created\"}", "http-resource",
                    "/api/v1/sites/http-resource", "\"7\"",
                    "http-original-correlation");
            });
        var replay = await executor.ExecuteTransactionalAsync(
            identity, fingerprint, transactions,
            (_, _) =>
            {
                Interlocked.Increment(ref mutationCount);
                return Task.FromResult(CommandExecutionResult.Ok(
                    500, "must-not-run", null));
            });
        Check(original.StatusCode == 201 && replay.IsReplay &&
            replay.StatusCode == original.StatusCode &&
            replay.Body == original.Body &&
            replay.Location == original.Location &&
            replay.ETag == original.ETag &&
            replay.CorrelationId == original.CorrelationId &&
            mutationCount == 1 &&
            await delivery.GetAsync(eventId) is not null,
            "T219 actual HTTP executor must replay exact bytes/headers and mutate once",
            failures);

        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;
        await new IdempotentHttpResult(replay).ExecuteAsync(context);
        body.Position = 0;
        using var reader = new StreamReader(body, Encoding.UTF8);
        var writtenBody = await reader.ReadToEndAsync();
        Check(context.Response.StatusCode == replay.StatusCode &&
            writtenBody == replay.Body &&
            context.Response.Headers.Location == replay.Location &&
            context.Response.Headers.ETag == replay.ETag &&
            context.Response.Headers["X-Correlation-Id"] == replay.CorrelationId,
            "T219 IResult must emit the stored exact HTTP response", failures);

        var beforeCompletionIdentity = new CommandIdentity(
            caller, CommandOperationCodes.UpdateSite,
            $"crash-before-completion-{Guid.NewGuid():N}");
        var beforeFingerprint = SHA256.HashData(
            Encoding.UTF8.GetBytes(beforeCompletionIdentity.IdempotencyKey));
        var beforeEvent = Guid.NewGuid();
        var beforeCrash = false;
        try
        {
            _ = await executor.ExecuteTransactionalAsync(
                beforeCompletionIdentity, beforeFingerprint, transactions,
                async (transaction, ct) =>
                {
                    await outbox.EnqueueAsync(
                        Envelope(beforeEvent, caller), transaction, ct);
                    throw new InvalidOperationException(
                        "SIMULATED_CRASH_BEFORE_COMPLETION");
                });
        }
        catch (InvalidOperationException exception)
        {
            beforeCrash = exception.Message ==
                "SIMULATED_CRASH_BEFORE_COMPLETION";
        }
        var beforeRecord = (await store.RegisterOrReadAsync(
            beforeCompletionIdentity, beforeFingerprint, null,
            TimeSpan.FromMilliseconds(-1))).Record;
        Check(beforeCrash &&
            beforeRecord.Status == CommandIdempotencyStatus.Pending &&
            await delivery.GetAsync(beforeEvent) is null,
            "T219 crash before completion must leave Pending and no outbox",
            failures);

        var afterCompletionIdentity = new CommandIdentity(
            caller, CommandOperationCodes.UpdateSite,
            $"crash-after-completion-{Guid.NewGuid():N}");
        var afterFingerprint = SHA256.HashData(
            Encoding.UTF8.GetBytes(afterCompletionIdentity.IdempotencyKey));
        var afterEvent = Guid.NewGuid();
        var afterCrash = false;
        try
        {
            _ = await executor.ExecuteTransactionalAsync(
                afterCompletionIdentity, afterFingerprint,
                new RollbackOnCommitFactory(transactions),
                async (transaction, ct) =>
                {
                    await outbox.EnqueueAsync(
                        Envelope(afterEvent, caller), transaction, ct);
                    return CommandExecutionResult.Ok(
                        200, "{\"result\":\"staged\"}", "after-resource");
                });
        }
        catch (InvalidOperationException exception)
        {
            afterCrash = exception.Message ==
                "SIMULATED_CRASH_AFTER_COMPLETION";
        }
        var afterRecord = (await store.RegisterOrReadAsync(
            afterCompletionIdentity, afterFingerprint, null,
            TimeSpan.FromMilliseconds(-1))).Record;
        Check(afterCrash &&
            afterRecord.Status == CommandIdempotencyStatus.Pending &&
            await delivery.GetAsync(afterEvent) is null,
            "T219 crash after staged completion but before commit must roll back completion and outbox",
            failures);
    }

    private static OwnerEventEnvelope Envelope(Guid eventId, Guid actorId) =>
        new(eventId, "IntegrationCommandVerified.v1", 1, "IUMP.Tests",
            "Site", Guid.NewGuid().ToString("D"), 1,
            actorId.ToString("D"), "integration-test",
            new Dictionary<string, object?>(),
            new Dictionary<string, object?> { ["status"] = "created" },
            "Create", "Integration command verified.", DateTime.UtcNow,
            $"integration-{eventId:N}", null, null, null);

    private static void Check(bool condition, string message, List<string> failures)
    {
        if (!condition) failures.Add(message);
    }

    private sealed class RollbackOnCommitFactory(
        IHostTransactionFactory inner) : IHostTransactionFactory
    {
        public async ValueTask<IHostTransaction> BeginAsync(
            CancellationToken ct = default) =>
            new RollbackOnCommitTransaction(await inner.BeginAsync(ct));
    }

    private sealed class RollbackOnCommitTransaction(
        IHostTransaction inner) :
        IHostTransaction,
        IHostTransactionController,
        IHostTransactionAccessor
    {
        public Guid TransactionId => inner.TransactionId;
        public string IsolationIntent => inner.IsolationIntent;
        public bool IsCompleted => inner.IsCompleted;
        public IHostTransaction InnerTransaction => inner;

        public async ValueTask CommitAsync(
            CancellationToken ct = default)
        {
            if (inner is IHostTransactionController controller)
                await controller.RollbackAsync(CancellationToken.None);
            throw new InvalidOperationException(
                "SIMULATED_CRASH_AFTER_COMPLETION");
        }

        public ValueTask RollbackAsync(
            CancellationToken ct = default) =>
            inner is IHostTransactionController controller
                ? controller.RollbackAsync(ct)
                : ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
