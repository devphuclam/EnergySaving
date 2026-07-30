using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IUMP.Api.Infrastructure;
using IUMP.BuildingBlocks.Persistence;
using IUMP.Composition.Postgres;
using IUMP.Infrastructure.Postgres;
using IUMP.Modules.Acquisition.Application;
using IUMP.Modules.Acquisition.Contracts;
using IUMP.Modules.Acquisition.Domain;
using IUMP.Modules.Audit.Contracts;
using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.Catalog.Domain;
using IUMP.Modules.IAM.Application;
using IUMP.Modules.IAM.Contracts;
using IUMP.Modules.IAM.Domain;
using IUMP.Modules.Integration.Contracts;
using IUMP.Modules.Operations.Contracts;
using IUMP.Modules.Organization.Application;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;
using IUMP.Modules.Telemetry.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

LocalEnvironmentFile.LoadFromAncestors(Directory.GetCurrentDirectory());
var configuration = PostgresRuntimeConfiguration.CreateRuntime();
var services = new ServiceCollection();
services.AddSingleton<ICredentialVerifier, LocalCredentialVerifier>();
services.AddIumpPostgresModules(configuration);
services.AddScoped<PostgresConfigurationCommandPort>();
services.AddScoped<PostgresSimulatorCommandPort>();
await using var provider = services.BuildServiceProvider();

if (args.Contains("--bootstrap", StringComparer.Ordinal))
{
    await BootstrapAsync(provider);
    return;
}

if (args.Contains("--functional", StringComparer.Ordinal))
{
    await VerifyFunctionalJourneyAsync(provider);
    return;
}

if (args.Contains("--recovery", StringComparer.Ordinal))
{
    await VerifyRecoveryAndRaceAsync(provider);
    return;
}

await VerifyAsync(provider);

static async Task VerifyFunctionalJourneyAsync(ServiceProvider provider)
{
    using var scope = provider.CreateScope();
    var sp = scope.ServiceProvider;
    var iam = sp.GetRequiredService<IIamCommandRepository>();
    var organization = sp.GetRequiredService<IOrganizationCommandRepository>();
    var commands = sp.GetRequiredService<PostgresConfigurationCommandPort>();
    var simulator = sp.GetRequiredService<PostgresSimulatorCommandPort>();
    var production = sp.GetRequiredService<ISimulatorProductionCoordinator>();
    var transactions = sp.GetRequiredService<IHostTransactionFactory>();
    var latest = sp.GetRequiredService<IPointLatestProjectionRepository>();
    var health = sp.GetRequiredService<ISourceHealthRepository>();
    var audit = sp.GetRequiredService<IAuditEventConsumer>();
    var runRepository = sp.GetRequiredService<IAcquisitionRunRepository>();

    var admin = await iam.FindUserByUsernameAsync("admin") ??
        throw new InvalidOperationException("FUNCTIONAL_ADMIN_MISSING");
    var engineer = await iam.FindUserByUsernameAsync("engineer") ??
        throw new InvalidOperationException("FUNCTIONAL_ENGINEER_MISSING");
    _ = await organization.FindSiteByCodeAsync("IUMP_ROOT") ??
        throw new InvalidOperationException("FUNCTIONAL_ROOT_SITE_MISSING");
    var adminPrincipal = new ServerPrincipal(
        admin.Id.Value, admin.Username, new HashSet<string>(), new HashSet<string>(), true);
    var principal = adminPrincipal;
    var suffix = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

    async Task<CommandExecutionResult> ExecuteConfigurationAsync(
        string operation,
        Guid? target,
        long? expectedVersion,
        params CommandFingerprintField[] fields)
    {
        await using var tx = await transactions.BeginAsync();
        var name = fields.FirstOrDefault(field =>
            field.Name.Equals("name", StringComparison.OrdinalIgnoreCase))?.Value?.ToString() ?? string.Empty;
        var result = await commands.ExecuteAsync(
            operation,
            new ConfigurationCommandRequest(target, name, expectedVersion, fields),
            principal, tx);
        if (result.StatusCode >= 400)
        {
            await ((IHostTransactionController)tx).RollbackAsync();
            throw new InvalidOperationException(
                $"FUNCTIONAL_COMMAND_FAILED:{operation}:{result.StatusCode}:{result.Body}");
        }
        await ((IHostTransactionController)tx).CommitAsync();
        return result;
    }

    static Guid ReadId(CommandExecutionResult result)
    {
        using var json = JsonDocument.Parse(result.Body);
        return json.RootElement.GetProperty("id").GetGuid();
    }

    CommandExecutionResult siteResult;
    await using (var tx = await transactions.BeginAsync())
    {
        siteResult = await commands.CreateSiteAsync(
            new ConfigurationCommandRequest(
                null, $"Functional Site {suffix}", null, Array.Empty<CommandFingerprintField>()),
            adminPrincipal, tx);
        if (siteResult.StatusCode >= 400)
        {
            await ((IHostTransactionController)tx).RollbackAsync();
            throw new InvalidOperationException(
                $"FUNCTIONAL_SITE_CREATE_FAILED:{siteResult.StatusCode}:{siteResult.Body}");
        }
        await ((IHostTransactionController)tx).CommitAsync();
    }
    var siteId = ReadId(siteResult);
    await ExecuteConfigurationAsync(
        "Organization.ActivateSite.v1", siteId, 1,
        CommandFingerprintField.String("name", string.Empty));
    await iam.AddScopeAsync(new Scope(ScopeId.New(), engineer.Id, siteId, null));
    principal = new ServerPrincipal(
        engineer.Id.Value, engineer.Username,
        new HashSet<string> { siteId.ToString("D") }, new HashSet<string>(), false);

    var areaResult = await ExecuteConfigurationAsync(
        "Organization.CreateArea.v1", siteId, null,
        CommandFingerprintField.String("name", $"Functional Area {suffix}"));
    var areaId = ReadId(areaResult);
    await iam.AddScopeAsync(new Scope(
        ScopeId.New(), engineer.Id, siteId, areaId));
    principal = principal with
    {
        AreaIds = new HashSet<string> { areaId.ToString("D") }
    };
    await ExecuteConfigurationAsync(
        "Organization.ActivateArea.v1", areaId, 1,
        CommandFingerprintField.String("name", string.Empty));

    var assetResult = await ExecuteConfigurationAsync(
        "Organization.CreateAsset.v1", areaId, null,
        CommandFingerprintField.String("name", $"Functional Asset {suffix}"));
    var assetId = ReadId(assetResult);
    await ExecuteConfigurationAsync(
        "Organization.ActivateAsset.v1", assetId, 1,
        CommandFingerprintField.String("name", string.Empty));

    var metricId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    var unitId = Guid.Parse("00000000-0000-0000-0000-000000000011");
    var pointResult = await ExecuteConfigurationAsync(
        "Organization.CreatePoint.v1", assetId, null,
        CommandFingerprintField.String("name", $"Functional Point {suffix}"),
        CommandFingerprintField.Uuid("metricId", metricId),
        CommandFingerprintField.Uuid("unitId", unitId),
        CommandFingerprintField.Uuid("dataOwnerUserId", engineer.Id.Value),
        CommandFingerprintField.Int64("expectedIntervalSeconds", 1),
        CommandFingerprintField.Int64("noDataAfterSeconds", 3));
    var pointId = ReadId(pointResult);

    var sourceResult = await ExecuteConfigurationAsync(
        "Acquisition.CreateSource.v1", null, null,
        CommandFingerprintField.String("name", $"Functional Source {suffix}"));
    var sourceId = ReadId(sourceResult);
    await ExecuteConfigurationAsync(
        "Acquisition.ActivateSource.v1", sourceId, 1,
        CommandFingerprintField.String("name", string.Empty));

    var configurationResult = await ExecuteConfigurationAsync(
        "Acquisition.CreateSimulatorConfiguration.v1", null, null,
        CommandFingerprintField.Uuid("sourceId", sourceId),
        CommandFingerprintField.String("scenarioType", "Constant"),
        CommandFingerprintField.Int64("intervalSeconds", 1),
        CommandFingerprintField.Decimal("minimumValue", 42m),
        CommandFingerprintField.Decimal("maximumValue", 42m),
        CommandFingerprintField.Int64("deterministicSeed", 42));
    var configurationId = ReadId(configurationResult);

    var mappingResult = await ExecuteConfigurationAsync(
        "Acquisition.CreateMapping.v1", null, null,
        CommandFingerprintField.Uuid("sourceId", sourceId),
        CommandFingerprintField.Uuid("pointId", pointId),
        CommandFingerprintField.Timestamp(
            "effectiveFromUtc", DateTime.UtcNow.AddMinutes(-1)));
    var mappingId = ReadId(mappingResult);
    await ExecuteConfigurationAsync(
        "Acquisition.ActivateMapping.v1", mappingId, 1,
        CommandFingerprintField.String("name", string.Empty));
    async Task<CommandExecutionResult> ActivatePointRaceAsync()
    {
        using var activationScope = provider.CreateScope();
        var activationCommands = activationScope.ServiceProvider
            .GetRequiredService<PostgresConfigurationCommandPort>();
        var activationTransactions = activationScope.ServiceProvider
            .GetRequiredService<IHostTransactionFactory>();
        await using var activationTx = await activationTransactions.BeginAsync();
        var result = await activationCommands.ExecuteAsync(
            "Organization.ActivatePoint.v1",
            new ConfigurationCommandRequest(
                pointId, string.Empty, 1,
                [CommandFingerprintField.String("name", string.Empty)]),
            principal, activationTx);
        if (result.StatusCode < 400)
            await ((IHostTransactionController)activationTx).CommitAsync();
        else
            await ((IHostTransactionController)activationTx).RollbackAsync();
        return result;
    }

    async Task<bool> ChangePointConfigurationRaceAsync()
    {
        using var contenderScope = provider.CreateScope();
        var contender = contenderScope.ServiceProvider
            .GetRequiredService<IOrganizationCommandRepository>();
        var current = await contender.GetPointAsync(new PointId(pointId));
        if (current is null ||
            !current.TryUpdateConfiguration(
                "Concurrent configuration change", current.MetricId,
                current.UnitId, current.DataOwnerUserId, 2, 6))
            return false;
        try
        {
            await contender.UpdatePointAsync(current);
            return true;
        }
        catch (InvalidOperationException exception)
            when (exception.Message.Contains(
                "VERSION_CONFLICT", StringComparison.Ordinal))
        {
            return false;
        }
    }

    var activationTask = ActivatePointRaceAsync();
    var configurationChangeTask = ChangePointConfigurationRaceAsync();
    await Task.WhenAll(activationTask, configurationChangeTask);
    var activationResult = await activationTask;
    var configurationChanged = await configurationChangeTask;
    var activationRacePassed =
        (activationResult.StatusCode < 400 && !configurationChanged) ||
        (activationResult.StatusCode == 409 && configurationChanged);
    if (!activationRacePassed)
        throw new InvalidOperationException(
            $"FUNCTIONAL_POINT_ACTIVATION_CONFIGURATION_RACE_FAILED:{activationResult.StatusCode}:{configurationChanged}");

    var racedPoint = await organization.GetPointAsync(new PointId(pointId)) ??
        throw new InvalidOperationException("FUNCTIONAL_RACED_POINT_MISSING");
    if (!racedPoint.IsActive)
        await ExecuteConfigurationAsync(
            "Organization.ActivatePoint.v1", pointId, racedPoint.Version,
            CommandFingerprintField.String("name", string.Empty));

    await using (var tx = await transactions.BeginAsync())
    {
        var start = await simulator.ExecuteAsync(
            "Simulator.Start.v1", sourceId, null, principal, tx);
        if (start.StatusCode >= 400)
        {
            await ((IHostTransactionController)tx).RollbackAsync();
            throw new InvalidOperationException(
                $"FUNCTIONAL_SIMULATOR_START_FAILED:{start.StatusCode}:{start.Body}");
        }
        await ((IHostTransactionController)tx).CommitAsync();
    }

    var cycle = await production.RunOnceAsync($"functional-{suffix}");
    var current = await latest.GetCurrentAsync(pointId);
    var sourceHealth = await health.GetSourceHealthAsync(pointId);
    var auditEnvelope = AuditEventEnvelope.Create(
        Guid.NewGuid(), "FunctionalJourneyVerified.v1", "FunctionalJourney",
        pointId.ToString("D"), "Verified",
        "PostgreSQL functional journey verified.", DateTime.UtcNow,
        $"functional-{suffix}");
    _ = await audit.ConsumeAsync(auditEnvelope);

    var completedJourneyRun = await runRepository.GetCurrentBySourceAsync(sourceId) ??
        throw new InvalidOperationException("FUNCTIONAL_CURRENT_RUN_MISSING");
    await using (var tx = await transactions.BeginAsync())
    {
        var stopped = await simulator.ExecuteAsync(
            "Simulator.Stop.v1", completedJourneyRun.RunId,
            completedJourneyRun.Version, principal, tx);
        if (stopped.StatusCode >= 400)
        {
            await ((IHostTransactionController)tx).RollbackAsync();
            throw new InvalidOperationException(
                $"FUNCTIONAL_RACE_SETUP_STOP_FAILED:{stopped.StatusCode}:{stopped.Body}");
        }
        await ((IHostTransactionController)tx).CommitAsync();
    }

    async Task<CommandExecutionResult> StartVsMappingChangeAsync()
    {
        using var contenderScope = provider.CreateScope();
        var contenderSimulator = contenderScope.ServiceProvider
            .GetRequiredService<PostgresSimulatorCommandPort>();
        var contenderTransactions = contenderScope.ServiceProvider
            .GetRequiredService<IHostTransactionFactory>();
        await using var tx = await contenderTransactions.BeginAsync();
        var result = await contenderSimulator.ExecuteAsync(
            "Simulator.Start.v1", sourceId, null, principal, tx);
        if (result.StatusCode < 400)
            await ((IHostTransactionController)tx).CommitAsync();
        else
            await ((IHostTransactionController)tx).RollbackAsync();
        return result;
    }

    async Task<bool> InactivateMappingRaceAsync()
    {
        using var contenderScope = provider.CreateScope();
        var contender = contenderScope.ServiceProvider
            .GetRequiredService<ICatalogCommandRepository>();
        var currentMapping = await contender.GetMappingAsync(new MappingId(mappingId));
        if (currentMapping is null || !currentMapping.TryInactivate()) return false;
        try
        {
            await contender.UpdateMappingAsync(currentMapping);
            return true;
        }
        catch (InvalidOperationException exception)
            when (exception.Message.Contains(
                "VERSION_CONFLICT", StringComparison.Ordinal) ||
                exception.Message.Contains("CONFLICT", StringComparison.Ordinal))
        {
            return false;
        }
    }

    var startRaceTask = StartVsMappingChangeAsync();
    var mappingChangeTask = InactivateMappingRaceAsync();
    await Task.WhenAll(startRaceTask, mappingChangeTask);
    var startRaceResult = await startRaceTask;
    var mappingChanged = await mappingChangeTask;
    var racedRun = await runRepository.GetCurrentBySourceAsync(sourceId);
    var racedPoints = racedRun is null
        ? Array.Empty<SimulatorRunPointState>()
        : (await runRepository.ListPointStatesAsync(racedRun.RunId)).ToArray();
    var startVsMappingRacePassed = startRaceResult.StatusCode < 400
        ? racedRun is not null &&
          racedPoints.Count(value =>
              value.PointId == pointId &&
              value.MappingId == mappingId &&
              value.MappingVersion == 2) == 1
        : mappingChanged && racedRun is null;
    if (!startVsMappingRacePassed)
        throw new InvalidOperationException(
            $"FUNCTIONAL_START_MAPPING_RACE_FAILED:{startRaceResult.StatusCode}:{mappingChanged}:{racedRun is not null}");

    var passed = activationRacePassed && startVsMappingRacePassed &&
        cycle.FinalizedAttempts >= 1 &&
        current is { NumericValue: 42 } &&
        sourceHealth?.Status == "Online";
    Console.WriteLine(
        $"functional-runtime target=127.0.0.1:5433/iump_dev " +
        $"site=PASS engineer_scope=PASS area=PASS asset=PASS point=PASS source=PASS " +
        $"configuration={configurationId != Guid.Empty} " +
        $"mapping=PASS point_activation_vs_configuration=PASS start_vs_mapping_change=PASS " +
        $"simulator=PASS attempts={cycle.FinalizedAttempts} " +
        $"latest={(current is { NumericValue: 42 } ? "PASS" : "FAIL")} " +
        $"health={(sourceHealth?.Status == "Online" ? "PASS" : "FAIL")} audit=PASS");
    Environment.ExitCode = passed ? 0 : 1;
}

static async Task VerifyRecoveryAndRaceAsync(ServiceProvider provider)
{
    using var scope = provider.CreateScope();
    var sp = scope.ServiceProvider;
    var runs = sp.GetRequiredService<IAcquisitionRunRepository>();
    var attempts = sp.GetRequiredService<IProductionAttemptService>();
    var attemptRepository =
        sp.GetRequiredService<ISimulatorProductionAttemptRepository>();
    var telemetry = sp.GetRequiredService<ITelemetryIngestionClient>();
    var telemetryQueries = sp.GetRequiredService<ITelemetryQueryRepository>();
    var latest = sp.GetRequiredService<IPointLatestProjectionRepository>();
    var telemetryTransactions = sp.GetRequiredService<ITelemetryFlowUnitOfWork>();
    var commandService = sp.GetRequiredService<SimulatorRunCommandService>();
    var iam = sp.GetRequiredService<IIamCommandRepository>();
    var audit = sp.GetRequiredService<IAuditEventConsumer>();
    var finalizer = new FinalizeTelemetryAttempt(attempts, telemetry);

    SimulatorRun? running = null;
    SimulatorRunPointState? point = null;
    var runUnitOfWork = sp.GetRequiredService<ISimulatorRunUnitOfWork>();
    foreach (var candidate in (await runs.ListRunningAsync())
        .OrderByDescending(value => value.CreatedAtUtc))
    {
        var candidatePoints = await runs.ListPointStatesAsync(candidate.RunId);
        var valid = candidatePoints.Count > 0 &&
            candidatePoints.All(value =>
            {
                try
                {
                    _ = DeterministicGenerator.Deserialize(value.PrngState);
                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            });
        if (valid)
        {
            running = candidate;
            point = candidatePoints.OrderBy(value => value.PointId).First();
            break;
        }
        await using var repair = await runUnitOfWork.BeginAsync();
        _ = await runs.ChangeStatusAsync(
            candidate.RunId, candidate.Version, SimulatorRunStatus.Stopped,
            DateTime.UtcNow, "INVALID_FIXTURE_STATE",
            "Invalid local verification fixture was stopped.", repair);
        await repair.CommitAsync();
    }
    if (running is null)
        throw new InvalidOperationException("RECOVERY_RUNNING_RUN_MISSING");
    if (point is null)
        throw new InvalidOperationException("RECOVERY_RUN_POINT_MISSING");
    var beforeRun = await runs.GetAsync(running.RunId) ??
        throw new InvalidOperationException("RECOVERY_RUN_MISSING");
    var beforePoint = await runs.GetPointStateAsync(running.RunId, point.PointId) ??
        throw new InvalidOperationException("RECOVERY_POINT_MISSING");

    var first = await attempts.ReserveAsync(
        running.RunId, point.PointId,
        $"crash-before-{Guid.NewGuid():N}",
        $"crash-before-{Guid.NewGuid():N}");
    var reservedPoint = await runs.GetPointStateAsync(running.RunId, point.PointId) ??
        throw new InvalidOperationException("RECOVERY_RESERVED_POINT_MISSING");
    var pendingBeforeTelemetry =
        first.Attempt.Status == SimulatorProductionAttemptStatus.Pending &&
        reservedPoint.NextSourceSequence == beforePoint.NextSourceSequence + 1;
    var firstRecovery = await finalizer.ExecuteAsync(first.Attempt);
    var firstCompleted = await attemptRepository.GetAsync(
        first.Attempt.RunId, first.Attempt.PointId, first.Attempt.SourceSequence);
    var afterFirstRun = await runs.GetAsync(running.RunId) ??
        throw new InvalidOperationException("RECOVERY_AFTER_FIRST_RUN_MISSING");
    var crashBeforeRecovered =
        firstRecovery.FirstTransition &&
        firstRecovery.TelemetryResult.Outcome ==
            TelemetryAttemptOutcome.Accepted &&
        firstCompleted?.Status == SimulatorProductionAttemptStatus.Completed &&
        afterFirstRun.GeneratedCount == beforeRun.GeneratedCount + 1 &&
        afterFirstRun.AcceptedCount == beforeRun.AcceptedCount + 1;

    var second = await attempts.ReserveAsync(
        running.RunId, point.PointId,
        $"crash-after-{Guid.NewGuid():N}",
        $"crash-after-{Guid.NewGuid():N}");
    var terminalBeforeAcquisition =
        await telemetry.DispatchCanonicalAsync(second.Attempt.Payload);
    var duplicateBeforeAcquisition =
        await telemetry.DispatchCanonicalAsync(second.Attempt.Payload);
    var secondStillPending = (await attemptRepository.GetAsync(
        second.Attempt.RunId, second.Attempt.PointId,
        second.Attempt.SourceSequence))?.Status ==
        SimulatorProductionAttemptStatus.Pending;
    var secondRecovery = await finalizer.ExecuteAsync(second.Attempt);
    var secondCompleted = await attemptRepository.GetAsync(
        second.Attempt.RunId, second.Attempt.PointId,
        second.Attempt.SourceSequence);
    var afterSecondRun = await runs.GetAsync(running.RunId) ??
        throw new InvalidOperationException("RECOVERY_AFTER_SECOND_RUN_MISSING");
    var exactReplay = await telemetry.DispatchCanonicalAsync(
        second.Attempt.Payload);
    var crashAfterRecovered =
        terminalBeforeAcquisition.Disposition ==
            CanonicalTelemetryDisposition.Accepted &&
        duplicateBeforeAcquisition.Disposition ==
            CanonicalTelemetryDisposition.Duplicate &&
        duplicateBeforeAcquisition.OriginalResult.FinalClassification ==
            ProductionFinalClassification.Accepted &&
        secondStillPending &&
        secondRecovery.FirstTransition &&
        secondRecovery.TelemetryResult.Outcome ==
            TelemetryAttemptOutcome.Duplicate &&
        secondCompleted?.Status == SimulatorProductionAttemptStatus.Completed &&
        afterSecondRun.GeneratedCount == afterFirstRun.GeneratedCount + 1 &&
        afterSecondRun.AcceptedCount == afterFirstRun.AcceptedCount + 1 &&
        exactReplay.Disposition == CanonicalTelemetryDisposition.Duplicate &&
        exactReplay.OriginalResult.PersistedMeasurementId ==
            terminalBeforeAcquisition.OriginalResult.PersistedMeasurementId;

    var firstRaw = await telemetryQueries.GetMeasurementAsync(
        first.Attempt.Payload.MeasurementId);
    var latestNoRegression = false;
    if (firstRaw is not null)
    {
        await using var tx = await telemetryTransactions.BeginRepeatableReadAsync();
        var older = new LatestProjectionCandidate(
            firstRaw.MeasurementId, firstRaw.PointId,
            firstRaw.SourceTimestampUtc, firstRaw.SourceSequence,
            firstRaw.ProcessingAtUtc, firstRaw.QualityCode,
            firstRaw.NumericValue, firstRaw.UnitCode, firstRaw.ReceivedAtUtc);
        var noRegression = await latest.CompareAndSetAsync(older, tx);
        latestNoRegression = !noRegression.Advanced;
        await tx.RollbackAsync();
    }

    var admin = await iam.FindUserByUsernameAsync("admin") ??
        throw new InvalidOperationException("RECOVERY_ADMIN_MISSING");
    var duplicateStart = await commandService.StartAsync(
        new StartSimulatorCommand(
            running.SourceId, admin.Id.ToString(),
            $"duplicate-start-{Guid.NewGuid():N}", null));
    var startRaceSafe = duplicateStart.IsSuccess &&
        duplicateStart.RunId == running.RunId &&
        (await runs.GetCurrentBySourceAsync(running.SourceId))?.RunId ==
            running.RunId;

    var auditEnvelope = AuditEventEnvelope.Create(
        Guid.NewGuid(), "RecoveryVerification.v1", "RecoveryVerification",
        running.RunId.ToString("D"), "Verified",
        "Crash recovery and race verification.", DateTime.UtcNow,
        $"recovery-{Guid.NewGuid():N}");
    var auditFirst = await audit.ConsumeAsync(auditEnvelope);
    var auditReplay = await audit.ConsumeAsync(auditEnvelope);
    var auditDeduplicated =
        auditFirst.AuditEventId == auditReplay.AuditEventId;

    var failures = new[]
    {
        ("pending-before-telemetry", pendingBeforeTelemetry),
        ("crash-before-telemetry", crashBeforeRecovered),
        ("terminal-before-acquisition", crashAfterRecovered),
        ("latest-no-regression", latestNoRegression),
        ("simulator-start-race", startRaceSafe),
        ("audit-deduplication", auditDeduplicated)
    }.Where(result => !result.Item2).Select(result => result.Item1).ToArray();
    Console.WriteLine(
        $"postgres-recovery target=127.0.0.1:5433/iump_dev scenarios=6 " +
        $"failures={failures.Length} pending_before_telemetry={(pendingBeforeTelemetry ? "PASS" : "FAIL")} " +
        $"crash_before={(crashBeforeRecovered ? "PASS" : "FAIL")} " +
        $"crash_after={(crashAfterRecovered ? "PASS" : "FAIL")} " +
        $"crash_after_detail={terminalBeforeAcquisition.Disposition}/" +
        $"{duplicateBeforeAcquisition.Disposition}/" +
        $"{duplicateBeforeAcquisition.OriginalResult.FinalClassification}/" +
        $"{secondStillPending}/{secondRecovery.TelemetryResult.Outcome}/" +
        $"{secondCompleted?.Status}/{afterSecondRun.GeneratedCount - afterFirstRun.GeneratedCount}/" +
        $"{afterSecondRun.AcceptedCount - afterFirstRun.AcceptedCount}/" +
        $"{afterSecondRun.RejectedCount - afterFirstRun.RejectedCount}/" +
        $"{secondCompleted?.FinalClassification}/{exactReplay.Disposition} " +
        $"latest_no_regression={(latestNoRegression ? "PASS" : "FAIL")} " +
        $"start_race={(startRaceSafe ? "PASS" : "FAIL")} " +
        $"audit_dedup={(auditDeduplicated ? "PASS" : "FAIL")}");
    foreach (var failure in failures)
        Console.WriteLine($"FAIL: {failure}");
    Environment.ExitCode = failures.Length == 0 ? 0 : 1;
}

static async Task BootstrapAsync(ServiceProvider provider)
{
    const string passwordKey = "IUMP_BOOTSTRAP_ADMIN_PASSWORD";
    var password = Environment.GetEnvironmentVariable(passwordKey) ??
        Environment.GetEnvironmentVariable(passwordKey, EnvironmentVariableTarget.User);
    var generated = string.IsNullOrWhiteSpace(password);
    if (generated)
    {
        password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        Environment.SetEnvironmentVariable(
            passwordKey, password, EnvironmentVariableTarget.User);
    }

    using var scope = provider.CreateScope();
    var iam = scope.ServiceProvider.GetRequiredService<IIamCommandRepository>();
    var organization =
        scope.ServiceProvider.GetRequiredService<IOrganizationCommandRepository>();
    var hasher = new PasswordHasher<string>();
    var passwordHash = hasher.HashPassword("admin", password!);
    var adminId = UserId.Parse("00000000-0000-0000-0000-000000000001");
    var admin = await iam.FindUserByUsernameAsync("admin");
    if (admin is null)
        await iam.AddUserAsync(new User(
            adminId, "admin", passwordHash, UserStatus.Active, Role.Administrator));

    var site = await organization.FindSiteByCodeAsync("IUMP_ROOT");
    if (site is null)
    {
        site = new Site(
            new SiteId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            "IUMP_ROOT", "IUMP Local Development", null, "Asia/Bangkok",
            SiteStatus.Draft, 1);
        await organization.AddSiteAsync(site);
        _ = site.TryActivate();
        await organization.UpdateSiteAsync(site);
    }

    var fixture = new PocIdentityFixture(
        iam, new FixedHashProvider(passwordHash), enabled: true);
    var postSite = await fixture.ApplyPostSiteFixtureAsync(site.Id.Value);

    var area = await organization.FindAreaByCodeAsync(site.Id, "ENGINEERING");
    if (area is null)
    {
        area = new Area(
            new AreaId(Guid.Parse("20000000-0000-0000-0000-000000000001")),
            site.Id, "ENGINEERING", "Engineering", null, AreaStatus.Draft, 1);
        await organization.AddAreaAsync(area);
        _ = area.TryActivate();
        await organization.UpdateAreaAsync(area);
    }
    var postArea = await fixture.ApplyPostAreaFixtureAsync(site.Id.Value, area.Id.Value);
    Console.WriteLine(
        $"bootstrap target=PASS admin=PASS root_site=PASS post_site_fixture={(postSite ? "PASS" : "FAIL")} " +
        $"engineer_scope={(postArea ? "PASS" : "FAIL")} generated_secret_stored={generated}");
    if (!postSite || !postArea)
        Environment.ExitCode = 1;
}

static async Task VerifyAsync(ServiceProvider provider)
{
    var assertions = 0;
    var scenarios = 0;
    var failures = new List<string>();
    void Check(bool condition, string code)
    {
        assertions++;
        if (!condition) failures.Add(code);
    }

    using var scope = provider.CreateScope();
    var sp = scope.ServiceProvider;

    scenarios++;
    var iam = sp.GetRequiredService<IIamCommandRepository>();
    var admin = await iam.FindUserByUsernameAsync("admin");
    var engineer = await iam.FindUserByUsernameAsync("engineer");
    Check(admin is not null && admin.HasRole(Role.Administrator), "IAM_ADMIN");
    Check(engineer is not null && engineer.HasRole(Role.Engineer), "IAM_ENGINEER");
    Check(engineer is not null &&
        (await iam.GetScopesForUserAsync(engineer.Id)).Count >= 2, "IAM_ENGINEER_SCOPE");

    scenarios++;
    var catalog = sp.GetRequiredService<ICatalogCommandRepository>();
    Check((await catalog.GetAllMetricsAsync()).Count >= 2, "CATALOG_METRICS");
    Check((await catalog.GetAllUnitsAsync()).Count >= 2, "CATALOG_UNITS");

    scenarios++;
    var organization =
        sp.GetRequiredService<IOrganizationCommandRepository>();
    var root = await organization.FindSiteByCodeAsync("IUMP_ROOT");
    var rootAreas = root is null
        ? Array.Empty<Area>()
        : (await organization.GetAreasForSiteAsync(root.Id)).ToArray();
    Check(root is { IsActive: true }, "ORGANIZATION_ROOT");
    Check(rootAreas.Any(a => a.Code == "ENGINEERING"),
        "ORGANIZATION_ENGINEERING_AREA");

    scenarios++;
    var transactionFactory = sp.GetRequiredService<IHostTransactionFactory>();
    var rollbackCode = $"ROLLBACK_{Guid.NewGuid():N}";
    await using (var tx = await transactionFactory.BeginAsync())
    {
        await organization.AddSiteAsync(new Site(
            SiteId.New(), rollbackCode, "Rollback Probe", null, "UTC",
            SiteStatus.Draft, 1));
        await ((IHostTransactionController)tx).RollbackAsync();
    }
    Check(await organization.FindSiteByCodeAsync(rollbackCode) is null,
        "HOST_TRANSACTION_ROLLBACK");

    scenarios++;
    var nestedBackend = sp.GetRequiredService<IHostTransactionBackend>();
    var nestedRollbackCode = $"NESTED_ROLLBACK_{Guid.NewGuid():N}";
    var outerCommitCode = $"OUTER_COMMIT_{Guid.NewGuid():N}";
    await using (var outer = await transactionFactory.BeginAsync())
    {
        var nested = await nestedBackend.BeginAsync();
        await organization.AddSiteAsync(new Site(
            SiteId.New(), nestedRollbackCode, "Nested Rollback Probe", null, "UTC",
            SiteStatus.Draft, 1));
        await nestedBackend.RollbackAsync(nested);
        await nested.DisposeAsync();
        await organization.AddSiteAsync(new Site(
            SiteId.New(), outerCommitCode, "Outer Commit Probe", null, "UTC",
            SiteStatus.Draft, 1));
        await ((IHostTransactionController)outer).CommitAsync();
    }
    Check(await organization.FindSiteByCodeAsync(nestedRollbackCode) is null,
        "HOST_TRANSACTION_SAVEPOINT_ROLLBACK");
    Check(await organization.FindSiteByCodeAsync(outerCommitCode) is not null,
        "HOST_TRANSACTION_OUTER_REMAINS_USABLE");

    scenarios++;
    var authorization = sp.GetRequiredService<IOrganizationAuthorization>();
    var engineerDenied = await authorization.AuthorizeAsync(
        engineer!.Id.ToString(), OrganizationResource.SiteChild,
        Guid.NewGuid().ToString("D"));
    var adminAllowed = await authorization.AuthorizeAsync(
        admin!.Id.ToString(), OrganizationResource.RootSite, null);
    Check(!engineerDenied.IsAllowed &&
        engineerDenied.Code.Equals("NotFound", StringComparison.OrdinalIgnoreCase),
        "AUTHORIZATION_OUT_OF_SCOPE_FAILS_CLOSED");
    Check(adminAllowed.IsAllowed, "AUTHORIZATION_ADMIN_ROOT_ALLOWED");

    scenarios++;
    var sourceId = Guid.NewGuid();
    var configurationId = Guid.NewGuid();
    var now = DateTime.UtcNow;
    var configurationRepository =
        sp.GetRequiredService<IAcquisitionConfigurationRepository>();
    var head = new SimulatorConfigurationHead(configurationId, sourceId, 1, 1);
    var version = new SimulatorConfigurationVersion(
        configurationId, 1, 1, 42, 42, 7, SimulatorScenario.Constant,
        SimulatorConfigurationConstants.AlgorithmId,
        SimulatorConfigurationConstants.AlgorithmVersion,
        admin!.Id.ToString(), "admin", now, "runtime-configuration", null);
    await configurationRepository.CreateAsync(head, version);
    Check((await configurationRepository.GetHeadAsync(configurationId)) is not null,
        "ACQUISITION_CONFIGURATION");

    scenarios++;
    var runId = Guid.NewGuid();
    var pointId = Guid.NewGuid();
    var mappingId = Guid.NewGuid();
    var run = new SimulatorRun(
        runId, sourceId, 1, configurationId, 1,
        SimulatorConfigurationConstants.AlgorithmId,
        SimulatorConfigurationConstants.AlgorithmVersion,
        SimulatorRunStatus.Running, 1, 0, 0, 0, null, null,
        now, now, null, null, null, admin.Id.ToString(), "admin",
        "runtime-run", null);
    var pointState = new SimulatorRunPointState(
        runId, pointId, 1, mappingId, 1, Guid.NewGuid(), Guid.NewGuid(), "kW",
        1, 0, new DeterministicGenerator().Initialize(
            7, pointId, configurationId, 1, 1),
        now, root!.Id.ToString(), null,
        null, null, 0, null, 1);
    var runUnitOfWork = sp.GetRequiredService<ISimulatorRunUnitOfWork>();
    var runs = sp.GetRequiredService<IAcquisitionRunRepository>();
    await using (var tx = await runUnitOfWork.BeginAsync())
    {
        var lockKeys = new[]
        {
            root.Id.Value,
            rootAreas.First().Id.Value,
            Guid.NewGuid(),
            pointId,
            mappingId,
            runId,
            Guid.NewGuid()
        };
        foreach (var target in Enum.GetValues<SimulatorStartLockTarget>())
            await tx.LockAsync(target, lockKeys[(int)target - 1].ToString("D"));
        await runs.CreateAsync(run, [pointState], tx);
        await tx.CommitAsync();
    }
    Check((await runs.GetAsync(runId))?.Status == SimulatorRunStatus.Running,
        "ACQUISITION_RUN");
    Check((await runs.ListPointStatesAsync(runId)).Count == 1,
        "ACQUISITION_RUN_POINT");

    scenarios++;
    var telemetryUnitOfWork =
        sp.GetRequiredService<ITelemetryFlowUnitOfWork>();
    var ingestion = sp.GetRequiredService<ITelemetryIngestionRepository>();
    var latest = sp.GetRequiredService<IPointLatestProjectionRepository>();
    var health = sp.GetRequiredService<ISourceHealthProjectionRepository>();
    var measurementId = Guid.NewGuid();
    var fingerprint = SHA256.HashData(
        Encoding.UTF8.GetBytes(measurementId.ToString("D")));
    var terminal = new TelemetryTerminalResult(
        measurementId, sourceId, runId, pointId, mappingId, 1, 0,
        SimulatorConfigurationConstants.AlgorithmId, 1, configurationId, 1,
        TelemetryFinalClassification.Accepted, true, measurementId,
        MeasurementQuality.Good, null, null, true, now,
        "runtime-telemetry", "runtime-lineage", fingerprint);
    var raw = new RawMeasurement(
        measurementId, sourceId, runId, pointId, mappingId, 1, 0,
        now, now, now, 42, "kW", MeasurementQuality.Good,
        null, "runtime-telemetry", "runtime-lineage");
    var candidate = new LatestProjectionCandidate(
        measurementId, pointId, now, 0, now, MeasurementQuality.Good,
        42, "kW", now);
    await using (var tx = await telemetryUnitOfWork.BeginRepeatableReadAsync())
    {
        await ingestion.StageTerminalAsync(terminal, tx);
        await ingestion.StageRawAsync(raw, tx);
        var advanced = await latest.CompareAndSetAsync(candidate, tx);
        var healthResult = await health.CompareAndSetAsync(
            new SourceHealthEvaluationInput(
                pointId, sourceId, root.Id.ToString(), null, "Active", "Active",
                "Running", 1, 1, 0, now, 1, 3, 1, 1, 1),
            SourceHealthStatus.Online, now, tx);
        Check(advanced.Advanced, "LATEST_ADVANCE");
        Check(healthResult.Current.Status == SourceHealthStatus.Online,
            "SOURCE_HEALTH_STAGE");
        await tx.CommitAsync();
    }
    Check((await ingestion.GetTerminalAsync(measurementId))?.MeasurementPersisted == true,
        "TELEMETRY_TERMINAL");
    Check((await latest.GetCurrentAsync(pointId))?.NumericValue == 42,
        "LATEST_VALUE");
    Check((await sp.GetRequiredService<ISourceHealthRepository>()
        .GetSourceHealthAsync(pointId))?.Status == "Online", "SOURCE_HEALTH_QUERY");

    scenarios++;
    await using (var tx = await telemetryUnitOfWork.BeginRepeatableReadAsync())
    {
        var older = candidate with
        {
            MeasurementId = Guid.NewGuid(),
            SourceTimestampUtc = now.AddMinutes(-1)
        };
        var result = await latest.CompareAndSetAsync(older, tx);
        Check(!result.Advanced, "LATEST_NO_REGRESSION");
        await tx.RollbackAsync();
    }

    scenarios++;
    var idempotency = sp.GetRequiredService<ICommandIdempotencyStore>();
    var identity = new CommandIdentity(
        admin.Id.Value, CommandOperationCodes.CreateSite, $"runtime-{Guid.NewGuid():N}");
    var commandFingerprint = SHA256.HashData(
        Encoding.UTF8.GetBytes(identity.IdempotencyKey));
    var registered = await idempotency.RegisterOrReadAsync(
        identity, commandFingerprint, "runtime-suite", TimeSpan.FromSeconds(30));
    var completed = await idempotency.CompleteAsync(
        registered.Record.Id, registered.Record.Version,
        new StoredHttpResult(201, "{\"result\":\"ok\"}", "runtime-resource",
            "/runtime-resource", "\"1\"", "runtime-idempotency"),
        DateTime.UtcNow.AddHours(24));
    var replay = await idempotency.RegisterOrReadAsync(
        identity, commandFingerprint, "runtime-suite", TimeSpan.FromSeconds(30));
    var conflict = await idempotency.RegisterOrReadAsync(
        identity, SHA256.HashData(Encoding.UTF8.GetBytes("different")),
        "runtime-suite", TimeSpan.FromSeconds(30));
    Check(registered.Created && completed?.Status == CommandIdempotencyStatus.Completed,
        "IDEMPOTENCY_COMPLETE");
    Check(replay.Equivalent && replay.Record.OriginalResult?.Body == "{\"result\":\"ok\"}",
        "IDEMPOTENCY_REPLAY");
    Check(conflict.Conflict, "IDEMPOTENCY_CONFLICT");

    scenarios++;
    var scheduler = sp.GetRequiredService<IDurableJobScheduler>();
    var jobClaims = sp.GetRequiredService<IJobClaimRepository>();
    var jobType = new JobType("RuntimeVerification.v1");
    var jobKey = new IdempotencyKey($"runtime-{Guid.NewGuid():N}");
    var scheduled = await scheduler.EnqueueAsync(
        jobType, jobKey, SafeJobPayload.Create("{\"probe\":true}"), DateTime.UtcNow);
    var duplicate = await scheduler.EnqueueAsync(
        jobType, jobKey, SafeJobPayload.Create("{\"probe\":true}"), DateTime.UtcNow);
    var claims = await jobClaims.ClaimDueAsync(
        DateTime.UtcNow.AddSeconds(1), "runtime-suite", 100);
    var claim = claims.SingleOrDefault(value => value.Job.Id == scheduled.Job.Id);
    var jobComplete = claim is null
        ? new JobOperationResult(false, false, "NOT_CLAIMED")
        : await jobClaims.CompleteAsync(claim, DateTime.UtcNow);
    Check(scheduled.Created && duplicate.Equivalent, "OPERATIONS_IDEMPOTENCY");
    Check(jobComplete.Succeeded, "OPERATIONS_CLAIM_COMPLETE");

    scenarios++;
    var audit = sp.GetRequiredService<IAuditEventConsumer>();
    var auditQuery = sp.GetRequiredService<IAuditQueryRepository>();
    var auditEnvelope = AuditEventEnvelope.Create(
        Guid.NewGuid(), "RuntimeVerification.v1", "RuntimeProbe",
        measurementId.ToString("D"), "Verified", "Runtime adapter verification.",
        DateTime.UtcNow, "runtime-audit");
    var auditRecord = await audit.ConsumeAsync(auditEnvelope);
    var auditReplay = await audit.ConsumeAsync(auditEnvelope);
    var auditRows = await auditQuery.QueryAsync(
        new AuditQueryRequest("RuntimeProbe", null, null, "runtime-audit", null, 1, 20));
    Check(auditRecord.AuditEventId == auditReplay.AuditEventId, "AUDIT_REPLAY");
    Check(auditRows.Any(value => value.SourceEventId == auditEnvelope.SourceEventId),
        "AUDIT_QUERY");

    Console.WriteLine(
        $"postgres-runtime target=127.0.0.1:5433/iump_dev scenarios={scenarios} " +
        $"assertions={assertions} failures={failures.Count}");
    foreach (var failure in failures)
        Console.WriteLine($"FAIL: {failure}");
    Environment.ExitCode = failures.Count == 0 ? 0 : 1;
}

sealed class FixedHashProvider(string hash) : IPocCredentialHashProvider
{
    public string GetPasswordHash(string username) => hash;
}

sealed class LocalCredentialVerifier : ICredentialVerifier
{
    private readonly PasswordHasher<string> _hasher = new();

    public bool Verify(string password, string storedHash)
    {
        var result = _hasher.VerifyHashedPassword(string.Empty, storedHash, password);
        return result is PasswordVerificationResult.Success or
            PasswordVerificationResult.SuccessRehashNeeded;
    }
}
