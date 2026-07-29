using IUMP.Modules.Integration.Contracts;

namespace IUMP.Tests.Unit.Integration;

public static class CommandIdempotencyDomainTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();
        var id = new CommandIdentity(Guid.NewGuid(), "Simulator.Start.v1", "key-1");
        var pending = CommandIdempotencyRecord.Pending(id, new byte[32], DateTime.UtcNow.AddSeconds(30));
        if (pending.Status != CommandIdempotencyStatus.Pending) failures.Add("new record must be Pending");
        var completed = pending.Complete(200, "{}", null, DateTime.UtcNow.AddHours(24));
        if (completed.Status != CommandIdempotencyStatus.Completed || completed.OriginalHttpStatus != 200)
            failures.Add("completion must preserve the original response");
        if (!completed.IsExpired(DateTime.UtcNow.AddHours(25))) failures.Add("retention must expire");
        if (!pending.IsLeaseLive(DateTime.UtcNow)) failures.Add("live Pending lease must be detected");
        return failures;
    }
}
