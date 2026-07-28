using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Integration.Contracts;
using IUMP.Modules.Organization.Application;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;

namespace IUMP.Tests.Integration.Organization;

public interface IPointActivationProviderFactory
{
    PointId PointId { get; }
    long ExpectedVersion { get; }
    OrganizationCommandContext Context { get; }
    IOrganizationCommandRepository TargetLookup { get; }
    IActivationIdentityParticipant Iam { get; }
    IActivationOrganizationParticipant Organization { get; }
    IActivationCatalogParticipant Catalog { get; }
    IOrganizationAuthorization Authorization { get; }
    ITransactionalOutboxWriter Outbox { get; }
    HostTransactionCoordinator HostTransaction { get; }
    IReadOnlyList<OwnerEventEnvelope> CommittedOutbox { get; }
    int StagedMutationCount { get; }
    IReadOnlyList<Guid> ParticipantTransactionIds { get; }
}

public interface IPointActivationProviderFactorySet
{
    IReadOnlyList<IPointActivationProviderFactory> Cases { get; }
}

// Provider-neutral contract runner: the factory supplies adapters, while this source invokes the real orchestrator.
public static class PointActivationTransactionTests
{
    public static List<string> Run(IPointActivationProviderFactorySet factorySet)
    {
        var failures = new List<string>();
        foreach (var factory in factorySet.Cases)
        {
            var result = ActivateMeasurementPoint.ExecuteAsync(factory.PointId, factory.ExpectedVersion, factory.Context,
                factory.TargetLookup, factory.Iam, factory.Organization, factory.Catalog, factory.Authorization,
                factory.Outbox, factory.HostTransaction).GetAwaiter().GetResult();
            if (factory.Context.CausationId == "retry-exhaustion")
            {
                if (result.ErrorCode != "TRANSIENT_DATABASE_CONFLICT" || factory.StagedMutationCount != 0 || factory.CommittedOutbox.Count != 0) failures.Add("T103 retry exhaustion must classify as TRANSIENT_DATABASE_CONFLICT with no staged work.");
                continue;
            }
            if (factory.Context.CausationId == "outbox-failure" || factory.Context.CausationId == "provider-drift")
            {
                if (result.IsSuccess || factory.StagedMutationCount != 0 || factory.CommittedOutbox.Count != 0) failures.Add($"{factory.Context.CausationId} must rollback all staged work.");
                continue;
            }
            if (!result.IsSuccess || result.Outcome != ActivationOutcome.Allowed) failures.Add("T103 success case must activate through ActivateMeasurementPoint.");
            if (factory.HostTransaction.LockTrace.Count != 9 || factory.HostTransaction.LockTrace[^1].Target != LockTarget.IntegrationOutbox) failures.Add("T103 must prove exact nine-target lock trace with Integration last.");
            if (factory.CommittedOutbox.Count != 1 || factory.StagedMutationCount != 1) failures.Add("T103 must prove exactly one mutation and one outbox row.");
            if (factory.ParticipantTransactionIds.Count == 0 || factory.ParticipantTransactionIds.Any(id => id != factory.HostTransaction.TransactionId)) failures.Add("T103 participants must share one host TransactionId.");
        }
        return failures;
    }
}
