using IUMP.Modules.Organization.Contracts;

namespace IUMP.Tests.Unit.Fakes;

public sealed class FakeActivationIdentityQuery : IActivationIdentityQuery
{
    public ActivationDataOwnerSnapshot Snapshot { get; set; } = new("owner-user", true, true, true, true, false, 1, 1);
    public bool ChangeOnSecondRead { get; set; }
    private int _reads;

    public Task<ActivationDataOwnerSnapshot> GetDataOwnerAsync(string dataOwnerUserId, string siteId, string areaId, CancellationToken ct = default)
    {
        _reads++;
        if (ChangeOnSecondRead && _reads > 1) return Task.FromResult(Snapshot with { UserVersion = Snapshot.UserVersion + 1 });
        return Task.FromResult(Snapshot with { DataOwnerUserId = dataOwnerUserId });
    }
}

public sealed class FakeActivationCatalogQuery : IActivationCatalogQuery
{
    public ActivationCatalogSnapshot? Snapshot { get; set; }
    public bool ChangeOnSecondRead { get; set; }
    private int _reads;

    public Task<ActivationCatalogSnapshot?> GetActivationSnapshotAsync(string pointId, string metricId, string unitId, DateTime atUtc, CancellationToken ct = default)
    {
        _reads++;
        if (Snapshot is null) return Task.FromResult<ActivationCatalogSnapshot?>(null);
        if (ChangeOnSecondRead && _reads > 1) return Task.FromResult<ActivationCatalogSnapshot?>(Snapshot with { MappingVersion = Snapshot.MappingVersion + 1 });
        return Task.FromResult<ActivationCatalogSnapshot?>(Snapshot with { MetricId = metricId, UnitId = unitId });
    }
}
