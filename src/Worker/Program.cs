var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
builder.Services.AddHostedService<FoundationWorker>();

await builder.Build().RunAsync();

internal sealed class FoundationWorker(
    ILogger<FoundationWorker> logger,
    IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var databaseConfigured = !string.IsNullOrWhiteSpace(configuration["IUMP_CONNECTION_STRING"]);
        logger.LogInformation(
            "Worker foundation started with DatabaseState {DatabaseState}",
            databaseConfigured ? "not_verified" : "BLOCKED_BY_DATABASE_ACCESS");

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
