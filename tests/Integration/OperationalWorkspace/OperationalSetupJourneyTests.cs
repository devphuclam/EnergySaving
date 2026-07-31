using IUMP.Api.Infrastructure;
using IUMP.BuildingBlocks.Persistence;
using IUMP.Composition.Postgres;
using IUMP.Modules.Acquisition.Contracts;
using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.IAM.Application;
using IUMP.Modules.IAM.Contracts;
using IUMP.Modules.IAM.Domain;
using IUMP.Modules.Integration.Contracts;
using IUMP.Modules.Organization.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace IUMP.Tests.Integration.OperationalWorkspace;

public static class OperationalSetupJourneyTests
{
    public static async Task<IReadOnlyList<string>> RunAsync(
        IServiceProvider root)
    {
        var failures = new List<string>();
        var suffix = Guid.NewGuid().ToString("N")[..10];

        try
        {
            using var scope = root.CreateScope();
            var services = scope.ServiceProvider;
            var iam = services.GetRequiredService<IIamCommandRepository>();
            var admin = (await iam.GetAllUsersAsync())
                .FirstOrDefault(user =>
                    user.Status == UserStatus.Active &&
                    user.HasRole(Role.Administrator));
            Check(admin is not null,
                "T014 requires an existing active Administrator bootstrap account.",
                failures);
            if (admin is null) return failures;

            var adminPrincipal = new ServerPrincipal(
                admin.Id.Value,
                admin.Username,
                new HashSet<string>(),
                new HashSet<string>(),
                true,
                new HashSet<string> { "Administrator" });
            var workspace = services.GetRequiredService<IOperationalWorkspaceQueryPort>();
            var defaultStatus = await workspace.GetStatusAsync(adminPrincipal);
            Check(
                defaultStatus.Landing == WorkspaceLanding.Dashboard,
                "T014 default Administrator status no longer lands on Dashboard.",
                failures);
            var newSetupStatus = await workspace.GetStatusAsync(
                adminPrincipal, WorkspaceStatusRequest.NewSetup());
            Check(
                newSetupStatus.Landing == WorkspaceLanding.SetupWizard &&
                newSetupStatus.NextStep == WorkspaceStep.SiteAndEngineer &&
                newSetupStatus.SelectedSiteId is null &&
                newSetupStatus.AuthorizedSites.Count == 0 &&
                newSetupStatus.OperationalChainCount == 0 &&
                newSetupStatus.IncompleteChainCount == 0,
                "T014 Administrator new-setup mode did not return an empty SiteAndEngineer wizard.",
                failures);

            var engineerUsername = $"phase1-engineer-{suffix}";
            var engineer = new User(
                UserId.New(), engineerUsername,
                new PasswordHasher<string>().HashPassword(
                    engineerUsername, "phase1-test-password"),
                UserStatus.Active, Role.Engineer);
            await iam.AddUserAsync(engineer);

            var organization =
                services.GetRequiredService<OrganizationRuntimeGateway>();
            var incompleteSite = await organization.CreateSiteAsync(
                $"Phase 1 Incomplete Site {suffix}", admin.Id.ToString());
            Check(
                incompleteSite.IsSuccess &&
                incompleteSite.Id is not null &&
                incompleteSite.Version is not null,
                "T014 could not create the earlier incomplete Site.",
                failures);
            if (!incompleteSite.IsSuccess ||
                incompleteSite.Id is null ||
                incompleteSite.Version is null)
                return failures;
            var activatedIncompleteSite = await organization.TransitionSiteAsync(
                incompleteSite.Id.Value,
                incompleteSite.Version.Value,
                "activate",
                admin.Id.ToString());
            var incompleteAssignment =
                await services.GetRequiredService<EngineerScopeAssignmentService>()
                    .AssignSiteAsync(
                        incompleteSite.Id.Value,
                        engineer.Id.Value,
                        admin.Id.Value);
            Check(
                activatedIncompleteSite.IsSuccess &&
                incompleteAssignment.IsSuccess,
                "T014 could not prepare the earlier incomplete authorized Site.",
                failures);

            var site = await organization.CreateSiteAsync(
                $"Phase 1 Site {suffix}", admin.Id.ToString());
            Check(site.IsSuccess, "T014 Administrator could not create Site.", failures);
            if (!site.IsSuccess || site.Id is null || site.Version is null)
                return failures;

            var selectedDraft = await workspace.GetStatusAsync(
                adminPrincipal, WorkspaceStatusRequest.ForSite(site.Id.Value));
            Check(
                selectedDraft.SelectedSiteId == site.Id.Value &&
                selectedDraft.AuthorizedSites.Count == 1 &&
                selectedDraft.AuthorizedSites[0].SiteId == site.Id.Value &&
                selectedDraft.Chain?.SiteId == site.Id.Value &&
                selectedDraft.OperationalChainCount == 0,
                "T014 selected-site status did not reconstruct only the newly created Site.",
                failures);

            var activatedSite = await organization.TransitionSiteAsync(
                site.Id.Value, site.Version.Value, "activate", admin.Id.ToString());
            Check(activatedSite.IsSuccess,
                "T014 Administrator could not activate Site before handoff.", failures);

            var assignment =
                await services.GetRequiredService<EngineerScopeAssignmentService>()
                    .AssignSiteAsync(
                        site.Id.Value, engineer.Id.Value, admin.Id.Value);
            var assignmentReplay =
                await services.GetRequiredService<EngineerScopeAssignmentService>()
                    .AssignSiteAsync(
                        site.Id.Value, engineer.Id.Value, admin.Id.Value);
            Check(
                assignment.IsSuccess && assignment.Code == "ASSIGNED" &&
                assignmentReplay.IsSuccess &&
                assignmentReplay.Code == "ALREADY_ASSIGNED",
                "T014 Administrator handoff was not duplicate-safe.", failures);

            var engineerPrincipal = new ServerPrincipal(
                engineer.Id.Value, engineer.Username,
                new HashSet<string>
                {
                    incompleteSite.Id.Value.ToString("D"),
                    site.Id.Value.ToString("D")
                },
                new HashSet<string>(), false,
                new HashSet<string> { "Engineer" });

            var area = await ExecuteAsync(
                services, engineerPrincipal, "Organization.CreateArea.v1",
                site.Id.Value, $"Phase 1 Area {suffix}", null, []);
            var areaId = Guid.Empty;
            var areaCreated = area.StatusCode == 201 &&
                Guid.TryParse(area.ResourceReference, out areaId);
            var asset = !areaCreated
                ? OrganizationRuntimeMutation.Failure(409, "AREA_CREATE_FAILED")
                : await organization.CreateAssetAsync(
                    areaId, $"Phase 1 Asset {suffix}", engineer.Id.ToString());
            Check(areaCreated && asset.IsSuccess,
                "T014 Engineer could not continue the assigned hierarchy.", failures);
            if (!areaCreated || asset.Id is null) return failures;

            var catalog = services.GetRequiredService<CatalogRuntimeGateway>();
            var metric = await catalog.CreateMetricAsync(
                $"P1M{suffix}", $"Phase 1 Metric {suffix}");
            var unit = await catalog.CreateUnitAsync(
                $"P1U{suffix}", $"u{suffix}");
            _ = await catalog.CreateCompatibilityAsync(
                metric.Id, unit.Id, true);
            var point = await organization.CreatePointAsync(
                asset.Id.Value, $"Phase 1 Point {suffix}",
                metric.Id, unit.Id, engineer.Id.Value, 10, 30,
                engineer.Id.ToString());
            Check(point.IsSuccess,
                "T014 Engineer could not create Measurement Point.", failures);
            if (point.Id is null) return failures;

            var source = await ExecuteAsync(
                services, engineerPrincipal, "Acquisition.CreateSource.v1", null,
                $"Phase 1 Source {suffix}", null,
                [CommandFingerprintField.Uuid("siteId", site.Id.Value)]);
            Check(source.StatusCode == 201 && Guid.TryParse(
                    source.ResourceReference, out var sourceId),
                "T014 Engineer could not create scoped Data Source.", failures);
            if (!Guid.TryParse(source.ResourceReference, out sourceId))
                return failures;

            var beforeMapping = await ReloadStatusAsync(root, engineerPrincipal);
            Check(
                beforeMapping.NextStep == WorkspaceStep.Mapping &&
                beforeMapping.Chain?.SourceId == sourceId,
                "T014 persisted resume did not retain the pre-Mapping Data Source.",
                failures);
            var areaOnlyPrincipal = new ServerPrincipal(
                engineer.Id.Value,
                engineer.Username,
                new HashSet<string>(),
                new HashSet<string> { areaId.ToString("D") },
                false,
                new HashSet<string> { "Engineer" });
            var areaBeforeMapping = await ReloadStatusAsync(root, areaOnlyPrincipal);
            Check(
                areaBeforeMapping.NextStep == WorkspaceStep.DataSource &&
                areaBeforeMapping.Chain?.SourceId is null,
                "T014 Area-only scope exposed a Site-wide Source before a Mapping relationship authorized it.",
                failures);

            var mapping = await ExecuteAsync(
                services, engineerPrincipal, "Acquisition.CreateMapping.v1", null,
                $"Phase 1 Mapping {suffix}", null,
                [
                    CommandFingerprintField.Uuid("sourceId", sourceId),
                    CommandFingerprintField.Uuid("pointId", point.Id.Value),
                    CommandFingerprintField.Timestamp(
                        "effectiveFromUtc", DateTime.UtcNow.AddMinutes(-1))
                ]);
            Check(mapping.StatusCode == 201 &&
                Guid.TryParse(mapping.ResourceReference, out var mappingId),
                "T014 Engineer could not create Source Mapping.", failures);
            if (!Guid.TryParse(mapping.ResourceReference, out mappingId))
                return failures;

            var configuration = await ExecuteAsync(
                services, engineerPrincipal,
                "Acquisition.CreateSimulatorConfiguration.v1", null,
                $"Phase 1 Configuration {suffix}", null,
                [
                    CommandFingerprintField.Uuid("sourceId", sourceId),
                    CommandFingerprintField.String("scenarioType", "Constant"),
                    CommandFingerprintField.Int64("intervalSeconds", 10),
                    CommandFingerprintField.Decimal("minimumValue", 42),
                    CommandFingerprintField.Decimal("maximumValue", 42),
                    CommandFingerprintField.Int64("deterministicSeed", 42)
                ]);
            Check(configuration.StatusCode == 201,
                "T014 Engineer could not create Simulator Configuration.", failures);
            if (!Guid.TryParse(
                configuration.ResourceReference, out var configurationId))
                return failures;

            var areaValidation = await services
                .GetRequiredService<IOperationalWorkspaceQueryPort>()
                .ValidateChainAsync(
                    new WorkspaceChainSelection(
                        site.Id.Value, null, areaId, null,
                        asset.Id.Value, null, point.Id.Value, null,
                        sourceId, null, mappingId, null,
                        configurationId, null),
                    areaOnlyPrincipal);
            Check(
                areaValidation.Valid,
                "T014 Area-only scope could not validate its persisted mapped chain.",
                failures);

            var readyToActivate = await ReloadStatusAsync(root, engineerPrincipal);
            Check(
                readyToActivate.CompletedSteps.Count == 7 &&
                readyToActivate.NextStep == WorkspaceStep.ValidateAndActivate &&
                !readyToActivate.SimulatorAutoStart,
                "T014 complete draft chain was not resumable at validation.",
                failures);

            var runs = services.GetRequiredService<IAcquisitionRunRepository>();
            var runningBefore = (await runs.ListRunningAsync())
                .Count(value => value.SourceId == sourceId);

            var activationSteps = new[]
            {
                ("Organization.ActivateArea.v1", areaId, 1L),
                ("Organization.ActivateAsset.v1", asset.Id.Value, asset.Version!.Value),
                ("Acquisition.ActivateSource.v1", sourceId, 1L),
                ("Acquisition.ActivateMapping.v1", mappingId, 1L),
                ("Organization.ActivatePoint.v1", point.Id.Value, point.Version!.Value)
            };
            for (var index = 0; index < activationSteps.Length; index++)
            {
                var (operation, target, version) = activationSteps[index];
                var result = await ExecuteAsync(
                    services, engineerPrincipal, operation, target,
                    string.Empty, version, []);
                Check(result.StatusCode == 200,
                    $"T014 ordered activation failed at {operation}.", failures);
                if (result.StatusCode != 200) break;
                if (index == 0)
                {
                    using var retryScope = root.CreateScope();
                    var retry = await retryScope.ServiceProvider
                        .GetRequiredService<IOperationalWorkspaceQueryPort>()
                        .ValidateChainAsync(
                            new WorkspaceChainSelection(
                                site.Id.Value, null, areaId, null,
                                asset.Id.Value, null, point.Id.Value, null,
                                sourceId, null, mappingId, null,
                                configurationId, null),
                            engineerPrincipal);
                    Check(
                        retry.Valid &&
                        !retry.ActivationSteps.Contains("area") &&
                        retry.ActivationSteps.Contains("asset"),
                        "T014 partial retry did not skip the committed Area transition.",
                        failures);
                }
            }

            var completed = await ReloadStatusAsync(root, engineerPrincipal);
            var selectedCompleted = await workspace.GetStatusAsync(
                adminPrincipal, WorkspaceStatusRequest.ForSite(site.Id.Value));
            var runningAfter = (await runs.ListRunningAsync())
                .Count(value => value.SourceId == sourceId);
            Check(
                completed.Landing == WorkspaceLanding.Dashboard &&
                completed.CompletedSteps.Count == 8 &&
                completed.Chain?.SiteId == site.Id.Value &&
                completed.OperationalChainCount == 1 &&
                completed.IncompleteChainCount == 1,
                "T014 multi-Site status did not select the later operational chain and count both authorized chains.",
                failures);
            Check(
                selectedCompleted.Landing == WorkspaceLanding.Dashboard &&
                selectedCompleted.SelectedSiteId == site.Id.Value &&
                selectedCompleted.AuthorizedSites.Count == 1 &&
                selectedCompleted.OperationalChainCount == 1 &&
                selectedCompleted.IncompleteChainCount == 0 &&
                selectedCompleted.Chain?.SiteId == site.Id.Value,
                "T014 selected-site status did not reconstruct the completed Site independently of chain ordering.",
                failures);
            Check(
                runningBefore == 0 && runningAfter == 0 &&
                !completed.SimulatorAutoStart,
                "T014 setup completion implicitly created or started a Simulator Run.",
                failures);

            var replayStatus = await ReloadStatusAsync(root, engineerPrincipal);
            Check(
                replayStatus.Landing == completed.Landing &&
                replayStatus.CompletedSteps.SequenceEqual(completed.CompletedSteps),
                "T014 status retry/restart changed the persisted result.", failures);

            var overlappingMapping = await ExecuteAsync(
                services, engineerPrincipal, "Acquisition.CreateMapping.v1", null,
                $"Phase 1 Overlapping Mapping {suffix}", null,
                [
                    CommandFingerprintField.Uuid("sourceId", sourceId),
                    CommandFingerprintField.Uuid("pointId", point.Id.Value),
                    CommandFingerprintField.Timestamp(
                        "effectiveFromUtc", DateTime.UtcNow.AddMinutes(-2))
                ]);
            Check(
                overlappingMapping.StatusCode == 201 &&
                Guid.TryParse(
                    overlappingMapping.ResourceReference,
                    out var overlappingMappingId),
                "T014 could not prepare the overlapping Mapping regression.",
                failures);
            if (Guid.TryParse(
                overlappingMapping.ResourceReference,
                out overlappingMappingId))
            {
                var conflictIdentity = new CommandIdentity(
                    engineerPrincipal.UserId,
                    "Acquisition.ActivateMapping.v1",
                    $"phase1-overlap-{Guid.NewGuid():N}");
                var conflictExecutor = new IdempotentCommandExecutor(
                    services.GetRequiredService<ICommandIdempotencyStore>());
                var mutationCount = 0;
                var conflict = await conflictExecutor.ExecuteTransactionalAsync(
                    conflictIdentity, new byte[32],
                    services.GetRequiredService<IHostTransactionFactory>(),
                    async (transaction, ct) =>
                    {
                        mutationCount++;
                        var commands =
                            ActivatorUtilities.CreateInstance<
                                PostgresConfigurationCommandPort>(services);
                        return await commands.ExecuteAsync(
                            "Acquisition.ActivateMapping.v1",
                            new ConfigurationCommandRequest(
                                overlappingMappingId,
                                string.Empty,
                                1,
                                []),
                            engineerPrincipal,
                            transaction,
                            ct);
                    });
                var conflictReplay =
                    await conflictExecutor.ExecuteTransactionalAsync(
                        conflictIdentity, new byte[32],
                        services.GetRequiredService<IHostTransactionFactory>(),
                        (_, _) =>
                        {
                            mutationCount++;
                            return Task.FromResult(
                                CommandExecutionResult.Ok(
                                    500, "must-not-run", null));
                        });
                Check(
                    conflict.StatusCode == 409 &&
                    conflictReplay.IsReplay &&
                    conflictReplay.StatusCode == 409 &&
                    conflictReplay.Body == conflict.Body &&
                    mutationCount == 1,
                    "T014 overlapping Mapping activation must return and replay an exact 409 without aborting idempotency completion.",
                    failures);
            }
        }
        catch (Exception exception)
        {
            failures.Add(
                $"T014 unexpected {exception.GetType().Name}: {exception.Message}");
        }

        return failures;
    }

    private static async Task<CommandExecutionResult> ExecuteAsync(
        IServiceProvider services,
        ServerPrincipal principal,
        string operationCode,
        Guid? targetId,
        string name,
        long? expectedVersion,
        IReadOnlyList<CommandFingerprintField> fields)
    {
        var transactionFactory =
            services.GetRequiredService<IHostTransactionFactory>();
        await using var transaction = await transactionFactory.BeginAsync();
        var commands =
            ActivatorUtilities.CreateInstance<PostgresConfigurationCommandPort>(
                services);
        var result = await commands
            .ExecuteAsync(
                operationCode,
                new ConfigurationCommandRequest(
                    targetId, name, expectedVersion, fields),
                principal, transaction);
        if (result.StatusCode is >= 200 and < 300)
            await ((IHostTransactionController)transaction).CommitAsync();
        else
            await ((IHostTransactionController)transaction).RollbackAsync();
        return result;
    }

    private static async Task<OperationalWorkspaceStatus> ReloadStatusAsync(
        IServiceProvider root,
        ServerPrincipal principal)
    {
        using var freshScope = root.CreateScope();
        return await freshScope.ServiceProvider
            .GetRequiredService<IOperationalWorkspaceQueryPort>()
            .GetStatusAsync(principal);
    }

    private static void Check(
        bool condition,
        string message,
        List<string> failures)
    {
        if (!condition) failures.Add(message);
    }
}
