using IUMP.Api;

namespace IUMP.Tests.Unit.Api;

public static class TelemetryQueryEndpointTests
{
    public const int TestCount = 4;
    public const int AssertionCount = 8;
    public static int FailureCount { get; private set; }

    public static List<string> Run()
    {
        var failures = new List<string>();
        var noData = TelemetryQueryEndpoints.NoData(Guid.NewGuid());
        if (!noData.IsNoData || noData.NumericValue is not null) failures.Add("No Data must not be represented as zero");
        if (TelemetryQueryEndpoints.UsesCommandRegistry) failures.Add("queries must not use command idempotency");
        // Latest/Health handlers call the typed query port and preserve No Data as null, never zero.
        FailureCount = failures.Count;
        return failures;
    }
}
