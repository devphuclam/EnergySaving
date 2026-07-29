using IUMP.Api;

namespace IUMP.Tests.Unit.Api;

public static class TelemetryQueryEndpointTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();
        var noData = TelemetryQueryEndpoints.NoData(Guid.NewGuid());
        if (!noData.IsNoData || noData.NumericValue is not null) failures.Add("No Data must not be represented as zero");
        if (TelemetryQueryEndpoints.UsesCommandRegistry) failures.Add("queries must not use command idempotency");
        return failures;
    }
}
