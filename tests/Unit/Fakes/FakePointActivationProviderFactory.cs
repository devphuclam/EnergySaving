using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Integration.Contracts;
using IUMP.Modules.Organization.Application;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;
using IUMP.Tests.Integration.Organization;
using IUMP.Tests.Unit.Organization;

namespace IUMP.Tests.Unit.Fakes;

public enum ActivationCaseOutcome
{
    Success,
    OutboxFailure,
    ProviderDrift,
    RetryExhaustion,
    StaleVersion,
    AtomicCommitFailure
}

public sealed class FakePointActivationProviderFactorySet : IPointActivationProviderFactorySet
{
    public IReadOnlyList<IPointActivationProviderFactory> Cases { get; } = new IPointActivationProviderFactory[]
    {
        FakePointActivationProviderFactory.Create(ActivationCaseOutcome.Success),
        FakePointActivationProviderFactory.Create(ActivationCaseOutcome.OutboxFailure),
        FakePointActivationProviderFactory.Create(ActivationCaseOutcome.ProviderDrift),
        FakePointActivationProviderFactory.Create(ActivationCaseOutcome.RetryExhaustion),
        FakePointActivationProviderFactory.Create(ActivationCaseOutcome.StaleVersion),
        FakePointActivationProviderFactory.Create(ActivationCaseOutcome.AtomicCommitFailure)
    };
}

public sealed class FakePointActivationProviderFactory : IPointActivationProviderFactory
{
    public required PointId PointId { get; init; }
    public long ExpectedVersion => 1;
    public required ActivationCaseOutcome Outcome { get; init; }
    public required OrganizationCommandContext Context { get; init; }
    public required FakeOrganizationCommandRepository Repo { get; init; }
    public required FakeAtomicBackend Backend { get; init; }
    public required IActivationIdentityParticipant Iam { get; init; }
    public required IActivationOrganizationParticipant Organization { get; init; }
    public required IActivationCatalogParticipant Catalog { get; init; }
    public required IOrganizationAuthorization Authorization { get; init; }
    public required ITransactionalOutboxWriter Outbox { get; init; }
    public required HostTransactionCoordinator HostTransaction { get; init; }
    public IOrganizationCommandRepository TargetLookup => Repo;
    public IReadOnlyList<OwnerEventEnvelope> CommittedOutbox => Backend.CommittedEnvelopes;
    public int StagedMutationCount => ((FakeActivationOrganizationParticipant)Organization).StageCount;
    public IReadOnlyList<Guid> ParticipantTransactionIds =>
        ((FakeActivationIdentityQuery)Iam).TransactionIds
            .Concat(((FakeActivationOrganizationParticipant)Organization).TransactionIds)
            .Concat(((FakeActivationCatalogQuery)Catalog).TransactionIds)
            .Concat(((FakeTransactionalOutboxWriter)Outbox).TransactionIds).ToArray();

    public static FakePointActivationProviderFactory Create(ActivationCaseOutcome outcome = ActivationCaseOutcome.Success)
    {
        var repo = new FakeOrganizationCommandRepository();
        var backend = new FakeAtomicBackend(repo);
        var site = new Site(SiteId.New(), "T103-SITE", "T103", null, "UTC", SiteStatus.Active, 1);
        var area = new Area(AreaId.New(), site.Id, "T103-AREA", "Area", null, AreaStatus.Active, 1);
        var asset = new Asset(AssetId.New(), site.Id, area.Id, "T103-ASSET", "Asset", null, AssetStatus.Active, 1);
        var point = new MeasurementPoint(PointId.New(), site.Id, area.Id, asset.Id, "T103-POINT", "test", "metric-1", "unit-1", "owner-user", 60, 300, PointStatus.Draft, 1);
        repo.AddSiteAsync(site).GetAwaiter().GetResult(); repo.AddAreaAsync(area).GetAwaiter().GetResult(); repo.AddAssetAsync(asset).GetAwaiter().GetResult(); repo.AddPointAsync(point).GetAwaiter().GetResult();

        var providerDrift = outcome == ActivationCaseOutcome.ProviderDrift;
        var retryExhaustion = outcome == ActivationCaseOutcome.RetryExhaustion;
        var outboxFailure = outcome == ActivationCaseOutcome.OutboxFailure;
        var staleVersion = outcome == ActivationCaseOutcome.StaleVersion;
        var commitFailure = outcome == ActivationCaseOutcome.AtomicCommitFailure;

        // For stale version: update point version to exceed expectedVersion (1)
        if (staleVersion)
        {
            var stalePoint = new MeasurementPoint(point.Id, point.SiteId, point.AreaId, point.AssetId,
                point.Code, point.Description, point.MetricId, point.UnitId, point.DataOwnerUserId,
                point.ExpectedIntervalSeconds, point.NoDataAfterSeconds, PointStatus.Active, 5);
            repo.AddLifecycleEntryDirect(new PointLifecycleEntry("stale-entry", point.Id.ToString(), 5,
                PointStatus.Draft, PointStatus.Active, "admin", "admin", "Activated", DateTime.UtcNow, "stale", null));
            repo.ReplacePointDirect(stalePoint);
        }

        var iam = new FakeActivationIdentityQuery
        {
            ChangeOnSecondRead = providerDrift,
            TransientFailures = retryExhaustion ? 4 : 0,
            Snapshot = new ActivationDataOwnerSnapshot("owner-user", true, true, true, true, false, 1, 1, site.Id.ToString(), area.Id.ToString())
        };
        var org = new FakeActivationOrganizationParticipant(repo, backend);
        var catalog = new FakeActivationCatalogQuery
        {
            ChangeOnSecondRead = providerDrift,
            Snapshot = new ActivationCatalogSnapshot("metric-1", 1, "Active", "unit-1", 1, "Active", true, 1, "mapping-1", 1, "Active", "source-1", 1, "Active", "Simulator", DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddHours(1), 1, point.Id.ToString(), point.Id.ToString(), "metric-1|unit-1", "Active")
        };
        var outbox = new FakeTransactionalOutboxWriter(backend) { FailOnEnqueue = outboxFailure };
        backend.FailOnCommit = commitFailure;

        return new FakePointActivationProviderFactory
        {
            PointId = point.Id,
            Outcome = outcome,
            Context = new OrganizationCommandContext("admin", "t103", outcome.ToString()),
            Repo = repo,
            Backend = backend,
            Iam = iam,
            Organization = org,
            Catalog = catalog,
            Authorization = new FakeOrganizationAuthorization(new("admin", "admin@test", true, new[] { "Administrator" }, Array.Empty<string>(), Array.Empty<string>())),
            Outbox = outbox,
            HostTransaction = new HostTransactionCoordinator(backend)
        };
    }
}
