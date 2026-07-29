using IUMP.Api.Infrastructure;
using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Integration.Contracts;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Integration;

public static class IdempotentCommandExecutorTests
{
    public static int TestCount { get; private set; }
    public static int AssertionCount { get; private set; }
    public static int FailureCount { get; private set; }

    public static async Task<List<string>> Run()
    {
        var failures = new List<string>();
        var assertions = 0;
        void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) failures.Add(message);
        }

        var store = new FakeCommandIdempotencyStore();
        var executor = new IdempotentCommandExecutor(store);
        var identity = new CommandIdentity(Guid.NewGuid(), "Catalog.Create.v1", "same");
        var calls = 0;
        var first = await executor.ExecuteAsync(identity, new byte[32], _ =>
        {
            calls++;
            return Task.FromResult(CommandExecutionResult.Ok(201, "{\"id\":1}", null));
        });
        var replay = await executor.ExecuteAsync(identity, new byte[32], _ =>
            Task.FromResult(CommandExecutionResult.Ok(500, "should-not-run", null)));
        Check(calls == 1 && replay.IsReplay && replay.Body == first.Body,
            "same command must execute once and replay the stored response");

        var conflict = await executor.ExecuteAsync(identity, Enumerable.Range(0, 32).Select(i => (byte)i).ToArray(),
            _ => Task.FromResult(CommandExecutionResult.Ok(200, "{}", null)));
        Check(conflict.Code == "IDEMPOTENCY_CONFLICT", "different fingerprints must conflict");

        var expiredIdentity = new CommandIdentity(Guid.NewGuid(), "Catalog.Create.v1", "expired");
        await store.RegisterOrReadAsync(expiredIdentity, new byte[32], null, TimeSpan.FromSeconds(-1));
        var reclaimed = await executor.ExecuteAsync(expiredIdentity, new byte[32], _ =>
            Task.FromResult(CommandExecutionResult.Ok(201, "{\"id\":3}", null)));
        Check(reclaimed.StatusCode == 201 && !reclaimed.IsReplay, "expired Pending must be reclaimed exactly once");

        var richIdentity = new CommandIdentity(Guid.NewGuid(), "Organization.CreateSite.v1", "rich");
        var rich = await executor.ExecuteAsync(richIdentity, new byte[32], _ =>
            Task.FromResult(CommandExecutionResult.Ok(201, "{\"id\":4}", "site-4", "/api/v1/sites/site-4", "\"4\"", "corr-4")));
        var richReplay = await executor.ExecuteAsync(richIdentity, new byte[32], _ =>
            Task.FromResult(CommandExecutionResult.Ok(500, "should-not-run", null)));
        Check(richReplay.Location == rich.Location && richReplay.ETag == rich.ETag && richReplay.CorrelationId == "corr-4",
            "replay must preserve exact Location, ETag and original correlation");

        var liveIdentity = new CommandIdentity(Guid.NewGuid(), "Asset.Update.v1", "live");
        await store.RegisterOrReadAsync(liveIdentity, new byte[32], null, TimeSpan.FromSeconds(30));
        var live = await executor.ExecuteAsync(liveIdentity, new byte[32], _ =>
            Task.FromResult(CommandExecutionResult.Ok(200, "should-not-run", null)));
        Check(live.Code == "IDEMPOTENCY_IN_PROGRESS" && live.StatusCode == 409,
            "a live Pending reservation must fail closed without mutation");

        var concurrentStore = new FakeCommandIdempotencyStore();
        var concurrentExecutor = new IdempotentCommandExecutor(concurrentStore);
        var concurrentIdentity = new CommandIdentity(Guid.NewGuid(), "Asset.Update.v1", "concurrent");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var concurrentCalls = 0;
        async Task<CommandExecutionResult> BlockingMutation(CancellationToken ct)
        {
            Interlocked.Increment(ref concurrentCalls);
            entered.SetResult();
            await release.Task.WaitAsync(ct);
            return CommandExecutionResult.Ok(200, "{\"ok\":true}", "asset-1");
        }
        var firstTask = concurrentExecutor.ExecuteAsync(concurrentIdentity, new byte[32], BlockingMutation);
        await entered.Task;
        var second = await concurrentExecutor.ExecuteAsync(concurrentIdentity, new byte[32], _ =>
            Task.FromResult(CommandExecutionResult.Ok(500, "should-not-run", null)));
        release.SetResult();
        var firstConcurrent = await firstTask;
        Check(concurrentCalls == 1 && second.Code == "IDEMPOTENCY_IN_PROGRESS" && firstConcurrent.StatusCode == 200,
            "concurrent same-key requests must have one owner and one in-progress response");

        var crashStore = new FakeCommandIdempotencyStore();
        var crashExecutor = new IdempotentCommandExecutor(crashStore);
        var crashIdentity = new CommandIdentity(Guid.NewGuid(), "Asset.Update.v1", "crash");
        var crash = await crashExecutor.ExecuteAsync(crashIdentity, new byte[32], _ =>
            throw new TransientDatabaseConflictException("OWNER_MUTATION_FAILED"));
        var crashRow = (await crashStore.GetAsync((await crashStore.RegisterOrReadAsync(
            crashIdentity, new byte[32], null, TimeSpan.FromSeconds(1))).Record.Id))!;
        Check(crash.Code == "TRANSIENT_DATABASE_CONFLICT" && crashRow.Status == CommandIdempotencyStatus.Pending,
            "a crash after registration must leave Pending for later reclamation");

        var txStore = new FakeCommandIdempotencyStore { FailTransactionalCompletion = true };
        var txFactory = new FakePhase9TransactionFactory();
        var txExecutor = new IdempotentCommandExecutor(txStore);
        var txIdentity = new CommandIdentity(Guid.NewGuid(), "Asset.Update.v1", "tx-failure");
        var ownerMutationCount = 0;
        var outboxAppendCount = 0;
        var txResponse = await txExecutor.ExecuteTransactionalAsync(txIdentity, new byte[32], txFactory,
            (tx, _) =>
            {
                ownerMutationCount++;
                outboxAppendCount++;
                return Task.FromResult(CommandExecutionResult.Ok(200, "{\"ok\":true}", "asset-2"));
            });
        Check(txResponse.Code == "TRANSIENT_DATABASE_CONFLICT" && ownerMutationCount == 1 && outboxAppendCount == 1 &&
              txFactory.BeginCount == 1 && txFactory.Last.RollbackCount == 1 && txFactory.Last.CommitCount == 0,
            "completion failure must roll back the owner mutation/outbox transaction");

        TestCount = 8;
        AssertionCount = assertions;
        FailureCount = failures.Count;
        return failures;
    }
}
