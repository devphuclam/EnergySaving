using IUMP.Composition.Postgres;
using IUMP.Infrastructure.Postgres;
using IUMP.Modules.Audit.Contracts;
using IUMP.Modules.IAM.Contracts;
using IUMP.Worker;
using IUMP.Worker.Integration;

LocalEnvironmentFile.LoadFromAncestors(Directory.GetCurrentDirectory());

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

var configuredConnection = builder.Configuration.GetConnectionString("IumpDatabase") ??
    Environment.GetEnvironmentVariable("ConnectionStrings__IumpDatabase") ??
    Environment.GetEnvironmentVariable(
        "ConnectionStrings__IumpDatabase",
        EnvironmentVariableTarget.User);
var postgres = PostgresRuntimeConfiguration.CreateRuntime(configuredConnection);

builder.Services.AddIumpPostgresModules(postgres);
builder.Services.AddSingleton<ICredentialVerifier, NonInteractiveCredentialVerifier>();
builder.Services.AddSingleton<RequiredConsumerRegistry>(provider =>
{
    var scopes = provider.GetRequiredService<IServiceScopeFactory>();
    var registry = new RequiredConsumerRegistry();
    registry.RegisterTransactional("*", "Audit.v1", async (outbox, transaction, ct) =>
    {
        using var scope = scopes.CreateScope();
        var audit = scope.ServiceProvider
            .GetRequiredService<ITransactionalAuditEventConsumer>();
        var envelope = AuditEventEnvelope.Create(
            outbox.EventId,
            outbox.EventType.EndsWith(".v1", StringComparison.Ordinal)
                ? outbox.EventType
                : $"{outbox.EventType}.v1",
            "IntegrationEvent",
            outbox.EventId.ToString("D"),
            "Delivered",
            "Integration event delivered to Audit.",
            DateTime.UtcNow,
            outbox.CorrelationId ?? outbox.EventId.ToString("D"));
        await audit.ConsumeAsync(envelope, transaction, ct);
        return true;
    });
    return registry;
});
builder.Services.AddScoped<OutboxDispatcherWorker>();
builder.Services.AddHostedService<PostgresRuntimeWorker>();

await builder.Build().RunAsync();

sealed class NonInteractiveCredentialVerifier : ICredentialVerifier
{
    public bool Verify(string password, string storedHash) => false;
}
