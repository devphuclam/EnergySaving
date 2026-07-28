using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Integration.Contracts;
using IUMP.Modules.Organization.Application;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Integration.Organization;

public interface IPointActivationProviderFactory
{
    PointId PointId { get; }
    long ExpectedVersion { get; }
    ActivationCaseOutcome Outcome { get; }
    OrganizationCommandContext Context { get; }
    IOrganizationCommandRepository TargetLookup { get; }
    IActivationIdentityParticipant Iam { get; }
    IActivationOrganizationParticipant Organization { get; }
    IActivationCatalogParticipant Catalog { get; }
    IOrganizationAuthorization Authorization { get; }
    ITransactionalOutboxWriter Outbox { get; }
    HostTransactionCoordinator HostTransaction { get; }
    FakeAtomicBackend Backend { get; }
    IReadOnlyList<OwnerEventEnvelope> CommittedOutbox { get; }
    int StagedMutationCount { get; }
    IReadOnlyList<Guid> ParticipantTransactionIds { get; }
}

public interface IPointActivationProviderFactorySet
{
    IReadOnlyList<IPointActivationProviderFactory> Cases { get; }
}

public sealed class PointActivationTransactionTests
{
    public int TestCount;
    public int CompositeCheckCount;

    public List<string> Run(IPointActivationProviderFactorySet factorySet)
    {
        TestCount = 0;
        CompositeCheckCount = 0;
        var failures = new List<string>();
        foreach (var factory in factorySet.Cases)
        {
            TestCount++;
            var beforePoint = factory.TargetLookup.GetPointAsync(factory.PointId).GetAwaiter().GetResult();
            var beforeLifecycle = beforePoint is not null
                ? factory.TargetLookup.GetLifecycleForPointAsync(factory.PointId.ToString()).GetAwaiter().GetResult()
                : new List<PointLifecycleEntry>();
            var beforeOutboxCount = factory.CommittedOutbox.Count;

            Exception? executionException = null;
            ActivationResult result;
            try
            {
                result = ActivateMeasurementPoint.ExecuteAsync(factory.PointId, factory.ExpectedVersion, factory.Context,
                    factory.TargetLookup, factory.Iam, factory.Organization, factory.Catalog, factory.Authorization,
                    factory.Outbox, factory.HostTransaction).GetAwaiter().GetResult();
            }
            catch (Exception ex) when (factory.Outcome == ActivationCaseOutcome.BeginFailure)
            {
                executionException = ex;
                result = ActivationResult.Failure(ActivationOutcome.Validation, "EXECUTION_EXCEPTION", ex.GetType().Name);
            }

            var afterPoint = factory.TargetLookup.GetPointAsync(factory.PointId).GetAwaiter().GetResult();
            var afterLifecycle = afterPoint is not null
                ? factory.TargetLookup.GetLifecycleForPointAsync(factory.PointId.ToString()).GetAwaiter().GetResult()
                : new List<PointLifecycleEntry>();
            var afterOutboxCount = factory.CommittedOutbox.Count;

            switch (factory.Outcome)
            {
                case ActivationCaseOutcome.Success:
                    CompositeCheckCount += 7;
                    if (!result.IsSuccess || result.Outcome != ActivationOutcome.Allowed)
                        failures.Add($"{factory.Outcome}: must succeed, got {result.ErrorCode}");
                    if (factory.HostTransaction.LockTrace.Count != 9 || factory.HostTransaction.LockTrace[^1].Target != LockTarget.IntegrationOutbox)
                        failures.Add($"{factory.Outcome}: must prove exact 9-target lock trace with Integration last");
                    if (afterPoint is null || afterPoint.Status != PointStatus.Active)
                        failures.Add($"{factory.Outcome}: Point must be Active after commit");
                    if (afterPoint is not null && afterPoint.Version != (beforePoint?.Version ?? 0) + 1)
                        failures.Add($"{factory.Outcome}: Point version must increment by 1");
                    if (afterLifecycle.Count != beforeLifecycle.Count + 1)
                        failures.Add($"{factory.Outcome}: exactly one lifecycle entry expected");
                    if (afterOutboxCount != beforeOutboxCount + 1)
                        failures.Add($"{factory.Outcome}: exactly one outbox envelope expected");
                    if (factory.ParticipantTransactionIds.Count == 0 || factory.ParticipantTransactionIds.Any(id => id != factory.HostTransaction.TransactionId))
                        failures.Add($"{factory.Outcome}: participants must share one host TransactionId");
                    break;

                case ActivationCaseOutcome.OutboxFailure:
                case ActivationCaseOutcome.ProviderDrift:
                    CompositeCheckCount += 5;
                    if (result.IsSuccess) failures.Add($"{factory.Outcome}: must fail, not succeed");
                    if (afterPoint is not null && afterPoint.Version != (beforePoint?.Version ?? 0))
                        failures.Add($"{factory.Outcome}: committed Point must not change after failure");
                    if (afterLifecycle.Count != beforeLifecycle.Count)
                        failures.Add($"{factory.Outcome}: committed lifecycle must not change after failure");
                    if (afterOutboxCount != beforeOutboxCount)
                        failures.Add($"{factory.Outcome}: committed outbox must not change after failure");
                    var hasStaged = factory.Backend.GetWorkspace(factory.HostTransaction)?.HasStagedOrgState ?? false;
                    if (hasStaged) failures.Add($"{factory.Outcome}: backend workspace must be cleared after rollback");
                    break;

                case ActivationCaseOutcome.RetryExhaustion:
                    CompositeCheckCount += 3;
                    if (result.ErrorCode != "TRANSIENT_DATABASE_CONFLICT")
                        failures.Add($"{factory.Outcome}: must classify as TRANSIENT_DATABASE_CONFLICT, got {result.ErrorCode}");
                    if (afterPoint is not null && afterPoint.Version != (beforePoint?.Version ?? 0))
                        failures.Add($"{factory.Outcome}: committed Point must not change");
                    if (factory.StagedMutationCount != 0)
                        failures.Add($"{factory.Outcome}: no staged mutation after exhaustion");
                    break;

                case ActivationCaseOutcome.StaleVersion:
                    CompositeCheckCount += 2;
                    if (result.ErrorCode != "VERSION_CONFLICT")
                        failures.Add($"{factory.Outcome}: must be VERSION_CONFLICT, got {result.ErrorCode}");
                    if (afterPoint is not null && afterPoint.Version != (beforePoint?.Version ?? 0))
                        failures.Add($"{factory.Outcome}: committed Point must not change after stale version");
                    break;

                case ActivationCaseOutcome.AtomicCommitFailure:
                    CompositeCheckCount += 8;
                    if (result.IsSuccess) failures.Add($"{factory.Outcome}: must fail on commit");
                    if (afterPoint is not null && afterPoint.Status == PointStatus.Active)
                        failures.Add($"{factory.Outcome}: Point must not be Active after failed commit");
                    if (afterPoint is not null && afterPoint.Version != (beforePoint?.Version ?? 0))
                        failures.Add($"{factory.Outcome}: Point version must not change after failed commit");
                    if (afterLifecycle.Count != beforeLifecycle.Count)
                        failures.Add($"{factory.Outcome}: lifecycle must not change after failed commit");
                    if (afterOutboxCount != beforeOutboxCount)
                        failures.Add($"{factory.Outcome}: outbox must not change after failed commit");
                    if (factory.Backend.GetWorkspace(factory.HostTransaction) is not null)
                        failures.Add($"{factory.Outcome}: workspace must be removed after commit failure");
                    if (factory.Backend.RollbackCount != 1)
                        failures.Add($"{factory.Outcome}: backend rollback must be called exactly once after commit failure, got {factory.Backend.RollbackCount}");
                    if (factory.Backend.CommitCount != 0)
                        failures.Add($"{factory.Outcome}: backend commit count must be 0 after failure, got {factory.Backend.CommitCount}");
                    break;

                case ActivationCaseOutcome.BeginFailure:
                    CompositeCheckCount += 10;
                    if (executionException is not null)
                        failures.Add($"{factory.Outcome}: must return a stable result, got {executionException.GetType().Name}");
                    if (result.ErrorCode != "TRANSACTION_ROLLED_BACK")
                        failures.Add($"{factory.Outcome}: must return TRANSACTION_ROLLED_BACK, got {result.ErrorCode}");
                    if (afterPoint is null || beforePoint is null || afterPoint.Status != beforePoint.Status || afterPoint.Version != beforePoint.Version)
                        failures.Add($"{factory.Outcome}: Point status/version must remain unchanged");
                    if (afterLifecycle.Count != beforeLifecycle.Count)
                        failures.Add($"{factory.Outcome}: lifecycle must remain unchanged");
                    if (afterOutboxCount != beforeOutboxCount)
                        failures.Add($"{factory.Outcome}: outbox must remain unchanged");
                    if (factory.Backend.CommitCount != 0)
                        failures.Add($"{factory.Outcome}: backend commit count must be 0");
                    if (factory.Backend.RollbackCount != 0)
                        failures.Add($"{factory.Outcome}: backend rollback count must be 0 because begin created no transaction");
                    if (factory.HostTransaction.IsBegun || factory.HostTransaction.IsCompleted || factory.HostTransaction.TransactionId != Guid.Empty)
                        failures.Add($"{factory.Outcome}: host must remain unbegun, incomplete, and empty");
                    if (factory.Backend.GetWorkspace(factory.HostTransaction) is not null)
                        failures.Add($"{factory.Outcome}: no workspace may exist");
                    try { factory.HostTransaction.DisposeAsync().GetAwaiter().GetResult(); }
                    catch (Exception ex) { failures.Add($"{factory.Outcome}: DisposeAsync must be safe, got {ex.GetType().Name}"); }
                    break;
            }
        }
        return failures;
    }
}
