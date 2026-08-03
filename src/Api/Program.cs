using IUMP.Api;
using IUMP.Api.Infrastructure;
using IUMP.BuildingBlocks.Correlation;
using IUMP.Composition.Postgres;
using IUMP.Infrastructure.Postgres;
using IUMP.Modules.IAM.Contracts;
using Npgsql;

LocalEnvironmentFile.LoadFromAncestors(Directory.GetCurrentDirectory());

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

var configuredConnection = builder.Configuration.GetConnectionString("IumpDatabase") ??
    Environment.GetEnvironmentVariable("ConnectionStrings__IumpDatabase") ??
    Environment.GetEnvironmentVariable(
        "ConnectionStrings__IumpDatabase",
        EnvironmentVariableTarget.User);
var postgres = PostgresRuntimeConfiguration.CreateRuntime(configuredConnection);

builder.Services.AddIumpPostgresModules(postgres);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICredentialVerifier, CredentialVerifier>();
builder.Services.AddScoped<IServerPrincipalAccessor, HttpServerPrincipalAccessor>();
builder.Services.AddScoped<IConfigurationCommandPort, PostgresConfigurationCommandPort>();
builder.Services.AddScoped<IConfigurationQueryPort, PostgresConfigurationQueryPort>();
builder.Services.AddScoped<ISimulatorCommandPort, PostgresSimulatorCommandPort>();
builder.Services.AddScoped<ISimulatorSelectedStartCommandPort>(provider =>
    provider.GetRequiredService<PostgresSimulatorCommandPort>());
builder.Services.AddScoped<ISimulatorQueryPort, PostgresSimulatorQueryPort>();
builder.Services.AddScoped<ISimulatorWorkspaceCommandPort, PostgresSimulatorWorkspaceCommandPort>();
builder.Services.AddScoped<ITelemetryQueryPort, PostgresTelemetryQueryPort>();
builder.Services.AddScoped<ITelemetryWorkspaceQueryPort, PostgresTelemetryWorkspacePorts>();
builder.Services.AddScoped<IAuditQueryPort, PostgresAuditQueryPort>();
builder.Services.AddScoped<IdempotentCommandExecutor>();
builder.Services.AddSingleton<IUtcClock, SystemUtcClock>();
builder.Services.AddAuthAntiforgery(builder.Environment.IsDevelopment());
builder.Services.AddIumpSessionAuthentication();
builder.Services.AddAuthorization();

var app = builder.Build();

app.Use(async (context, next) =>
{
    var supplied = context.Request.Headers[CorrelationId.HeaderName].FirstOrDefault();
    var correlationId = CorrelationId.Create(supplied);
    context.Response.Headers[CorrelationId.HeaderName] = correlationId.Value;

    using (app.Logger.BeginScope(new Dictionary<string, object>
    {
        ["CorrelationId"] = correlationId.Value
    }))
    {
        await next(context);
    }
});
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/health/live", () => Results.Ok(new
{
    service = "iump-api",
    status = "live",
    release = "R1"
}));

app.MapGet("/health/ready", async (NpgsqlDataSource dataSource, CancellationToken ct) =>
{
    try
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT current_database(),
                   inet_server_port(),
                   to_regclass('integration.command_idempotency') IS NOT NULL,
                   to_regclass('acquisition.simulator_configuration_receipt') IS NOT NULL,
                   (SELECT count(*) FROM information_schema.tables
                    WHERE table_schema IN
                      ('iam','catalog','organization','acquisition','telemetry',
                       'operations','integration','audit'))
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return Results.Json(new { status = "not-ready", reason = "DATABASE_UNREACHABLE" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        var databaseMatches = reader.GetString(0) == PostgresRuntimeConfiguration.ApprovedLocalDatabase;
        var portMatches = reader.GetInt32(1) == PostgresRuntimeConfiguration.ApprovedLocalPort;
        var migrationMarker = reader.GetBoolean(2) && reader.GetBoolean(3);
        var ownedTableCount = reader.GetInt64(4);
        if (!databaseMatches || !portMatches)
            return Results.Json(new { status = "not-ready", reason = "RUNTIME_DEPENDENCY_UNAVAILABLE" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        if (!migrationMarker || ownedTableCount < 31)
            return Results.Json(new { status = "not-ready", reason = "MIGRATION_PENDING" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        return Results.Ok(new
        {
            status = "ready",
            database = PostgresRuntimeConfiguration.ApprovedLocalDatabase,
            port = PostgresRuntimeConfiguration.ApprovedLocalPort,
            migrationLevel = PostgresRuntimeConfiguration.RequiredMigrationLevel
        });
    }
    catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.InvalidPassword)
    {
        return Results.Json(new { status = "not-ready", reason = "DATABASE_AUTHENTICATION_FAILED" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception exception) when (
        exception is NpgsqlException or TimeoutException or InvalidOperationException)
    {
        return Results.Json(new { status = "not-ready", reason = "DATABASE_UNREACHABLE" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapAuthEndpoints();
app.MapConfigurationEndpoints();
app.MapSimulatorEndpoints();
app.MapTelemetryQueryEndpoints();
app.MapAuditEndpoints();
app.MapOperationalDashboardEndpoints();
app.MapOperationalWorkspaceEndpoints();
app.MapConfigurationManagementEndpoints();

app.MapGet("/", () => Results.Ok(new
{
    service = "IUMP API",
    release = "R1",
    scope = "asset-simulator-latest"
}));

app.Run();
