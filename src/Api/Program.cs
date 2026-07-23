using IUMP.BuildingBlocks.Correlation;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

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

app.MapGet("/health/live", () => Results.Ok(new
{
    service = "iump-api",
    status = "live",
    release = "R0"
}));

app.MapGet("/health/ready", (IConfiguration configuration) =>
{
    var configured = !string.IsNullOrWhiteSpace(configuration["IUMP_CONNECTION_STRING"]);
    return Results.Json(
        new
        {
            status = "blocked",
            blocker = "BLOCKED_BY_DATABASE_ACCESS",
            configuration = configured ? "present_not_verified" : "missing"
        },
        statusCode: StatusCodes.Status503ServiceUnavailable);
});

app.MapGet("/", () => Results.Ok(new
{
    service = "IUMP API",
    release = "R0",
    scope = "engineering-foundation"
}));

app.Run();
