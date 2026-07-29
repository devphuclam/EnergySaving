using IUMP.Api.Infrastructure;
using IUMP.Modules.Integration.Contracts;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Integration;

public static class IdempotentCommandExecutorTests
{
    public static async Task<List<string>> Run()
    {
        var failures = new List<string>();
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
            Task.FromResult(CommandExecutionResult.Ok(201, "{\"id\":2}", null)));
        if (calls != 1 || !replay.IsReplay || replay.Body != first.Body)
            failures.Add("same command must execute once and replay the stored response");
        var conflict = await executor.ExecuteAsync(identity, new byte[32].Select((_, i) => (byte)(i + 1)).ToArray(),
            _ => Task.FromResult(CommandExecutionResult.Ok(200, "{}", null)));
        if (conflict.Code != "IDEMPOTENCY_CONFLICT") failures.Add("different fingerprints must conflict");
        var expiredIdentity = new CommandIdentity(Guid.NewGuid(), "Catalog.Create.v1", "expired");
        await store.RegisterOrReadAsync(expiredIdentity, new byte[32], null, TimeSpan.FromSeconds(-1));
        var reclaimed = await executor.ExecuteAsync(expiredIdentity, new byte[32], _ =>
            Task.FromResult(CommandExecutionResult.Ok(201, "{\"id\":3}", null)));
        if (reclaimed.StatusCode != 201 || reclaimed.IsReplay) failures.Add("expired Pending must be reclaimed exactly once");
        return failures;
    }
}
