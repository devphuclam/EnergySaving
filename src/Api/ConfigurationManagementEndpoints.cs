namespace IUMP.Api;

using System.Text.Json;
using IUMP.Api.Infrastructure;
using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Integration.Contracts;

public static class ConfigurationManagementEndpointPolicy
{
    public const string RoutePrefix = "/api/v1/configuration-management";
}

public sealed record ActivateSimulatorConfigurationVersionRequest(
    long ExpectedHeadVersion,
    long DraftConfigurationVersion,
    bool RelationshipReviewConfirmed = false,
    bool ValidationConfirmed = false);

public static class ConfigurationManagementEndpoints
{

    public static IEndpointRouteBuilder MapConfigurationManagementEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ConfigurationManagementEndpointPolicy.RoutePrefix);
        group.MapGet("/{resource}", ListAsync)
            .WithName("ConfigurationManagement.List");
        group.MapGet("/{resource}/{id:guid}", DetailAsync)
            .WithName("ConfigurationManagement.Detail");
        group.MapPost("/{resource}/{id:guid}/duplicate", DuplicateAsync)
            .WithMetadata(new RequireAntiforgeryCheckAttribute())
            .WithName("ConfigurationManagement.Duplicate");
        group.MapPost("/{resource}", CreateAsync)
            .WithMetadata(new RequireAntiforgeryCheckAttribute())
            .WithName("ConfigurationManagement.Create");
        group.MapPut("/{resource}/{id:guid}", UpdateAsync)
            .WithMetadata(new RequireAntiforgeryCheckAttribute())
            .WithName("ConfigurationManagement.Update");
        group.MapPost("/{resource}/{id:guid}/validate", ValidateAsync)
            .WithMetadata(new RequireAntiforgeryCheckAttribute())
            .WithName("ConfigurationManagement.Validate");
        group.MapPost("/{resource}/{id:guid}/{action}", LifecycleAsync)
            .WithMetadata(new RequireAntiforgeryCheckAttribute())
            .WithName("ConfigurationManagement.Lifecycle");
        group.MapDelete("/{resource}/{id:guid}", DeleteAsync)
            .WithMetadata(new RequireAntiforgeryCheckAttribute())
            .WithName("ConfigurationManagement.Delete");
        group.MapPost(
                "/simulator-configurations/{configurationId:guid}/activate",
                ActivateSimulatorConfigurationVersionAsync)
            .WithMetadata(new RequireAntiforgeryCheckAttribute())
            .WithName("ConfigurationManagement.ActivateSimulatorConfigurationVersion");
        return endpoints;
    }

    public static async Task<IResult> ListAsync(
        HttpRequest request,
        string resource,
        IConfigurationManagementQueryPort query,
        IServerPrincipalAccessor principalAccessor,
        CancellationToken ct)
    {
        if (!ConfigurationManagementResources.IsKnown(resource))
            return Results.Json(new { errorCode = "UNKNOWN_RESOURCE" },
                statusCode: StatusCodes.Status400BadRequest);
        if (principalAccessor.Current is not { } principal)
            return Results.Unauthorized();
        var page = Positive(request, "page", 1);
        var pageSize = ClampPositive(request, "pageSize", 20, 1, 200);
        if (page < 1 || pageSize < 1)
            return Results.Json(new { errorCode = "INVALID_PAGING" },
                statusCode: StatusCodes.Status400BadRequest);
        var filter = new ManagementQueryFilter(
            Search: Optional(request, "search"),
            Status: Optional(request, "status"),
            SiteId: Optional(request, "siteId"),
            AreaId: Optional(request, "areaId"),
            Page: page,
            PageSize: pageSize);
        try
        {
            var result = await query.QueryAsync(resource, filter, principal, ct);
            return Results.Ok(new
            {
                items = result.Items,
                totalCount = result.TotalCount,
                page = result.Page,
                pageSize = result.PageSize
            });
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or TimeoutException or Npgsql.NpgsqlException)
        {
            return Results.Json(new { errorCode = "DEPENDENCY_UNAVAILABLE" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    public static async Task<IResult> DetailAsync(
        Guid id,
        string resource,
        IConfigurationManagementQueryPort query,
        IServerPrincipalAccessor principalAccessor,
        CancellationToken ct)
    {
        if (!ConfigurationManagementResources.IsKnown(resource))
            return Results.Json(new { errorCode = "UNKNOWN_RESOURCE" },
                statusCode: StatusCodes.Status400BadRequest);
        if (principalAccessor.Current is not { } principal)
            return Results.Unauthorized();
        try
        {
            var detail = await query.GetDetailAsync(resource, id, principal, ct);
            return detail is null
                ? Results.Json(new { errorCode = "NOT_FOUND" },
                    statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(detail);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or TimeoutException or Npgsql.NpgsqlException)
        {
            return Results.Json(new { errorCode = "DEPENDENCY_UNAVAILABLE" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    public static async Task<IResult> DuplicateAsync(
        Guid id,
        string resource,
        HttpRequest request,
        IConfigurationManagementCommandPort commands,
        IdempotentCommandExecutor executor,
        IServerPrincipalAccessor principalAccessor,
        IHostTransactionFactory transactionFactory,
        CancellationToken ct)
    {
        if (!ConfigurationManagementResources.IsKnown(resource))
            return Results.Json(new { errorCode = "UNKNOWN_RESOURCE" },
                statusCode: StatusCodes.Status400BadRequest);
        if (!request.Headers.TryGetValue("Idempotency-Key", out var key) ||
            string.IsNullOrWhiteSpace(key))
            return Results.Problem("Idempotency-Key is required.",
                statusCode: StatusCodes.Status400BadRequest);
        if (principalAccessor.Current is not { } principal)
            return Results.Unauthorized();
        var identity = new CommandIdentity(
            principal.UserId, CommandOperationCodes.DuplicateConfiguration, key!);
        var fingerprint = CommandFingerprintV1.Compute(new CommandFingerprintInput(
            identity.OperationCode, principal.UserId, null, null,
            resource, id, null,
            [CommandFingerprintField.String("resource", resource)]));
        try
        {
            var response = await executor.ExecuteTransactionalAsync(
                identity, fingerprint, transactionFactory,
                (transaction, token) => commands.DuplicateAsync(
                    resource, id, principal, transaction, token), ct);
            return new IdempotentHttpResult(response);
        }
        catch (Exception exception) when (IsRuntimeFailure(exception))
        {
            return DependencyUnavailable();
        }
    }

    public static async Task<IResult> ActivateSimulatorConfigurationVersionAsync(
        Guid configurationId,
        ActivateSimulatorConfigurationVersionRequest? body,
        HttpRequest request,
        IConfigurationManagementCommandPort commands,
        IdempotentCommandExecutor executor,
        IServerPrincipalAccessor principalAccessor,
        IHostTransactionFactory transactionFactory,
        CancellationToken ct)
    {
        if (body is null ||
            body.ExpectedHeadVersion < 1 ||
            body.DraftConfigurationVersion < 1)
            return Results.Json(new { errorCode = "VERSION_FIELDS_REQUIRED" },
                statusCode: StatusCodes.Status400BadRequest);
        if (!body.RelationshipReviewConfirmed)
            return Results.Json(new { errorCode = "RELATIONSHIP_REVIEW_REQUIRED" }, statusCode: 422);
        if (!body.ValidationConfirmed)
            return Results.Json(new { errorCode = "VALIDATION_REQUIRED" }, statusCode: 422);
        if (!request.Headers.TryGetValue("Idempotency-Key", out var key) ||
            string.IsNullOrWhiteSpace(key))
            return Results.Problem("Idempotency-Key is required.",
                statusCode: StatusCodes.Status400BadRequest);
        if (principalAccessor.Current is not { } principal)
            return Results.Unauthorized();
        var identity = new CommandIdentity(principal.UserId,
            CommandOperationCodes.ActivateSimulatorConfigurationVersion, key!);
        var fingerprint = CommandFingerprintV1.Compute(new CommandFingerprintInput(
            identity.OperationCode, principal.UserId,
            "SimulatorConfiguration", configurationId, null, null,
            body.ExpectedHeadVersion,
            [
                CommandFingerprintField.Int64(
                    "draftConfigurationVersion", body.DraftConfigurationVersion),
                CommandFingerprintField.Bool("relationshipReviewConfirmed", body.RelationshipReviewConfirmed),
                CommandFingerprintField.Bool("validationConfirmed", body.ValidationConfirmed)
            ]));
        try
        {
            var response = await executor.ExecuteTransactionalAsync(
                identity, fingerprint, transactionFactory,
                (transaction, token) =>
                    commands.ActivateSimulatorConfigurationVersionAsync(
                        configurationId, body.ExpectedHeadVersion,
                        body.DraftConfigurationVersion, principal, transaction,
                        body.RelationshipReviewConfirmed, body.ValidationConfirmed, token),
                ct);
            return new IdempotentHttpResult(response);
        }
        catch (Exception exception) when (IsRuntimeFailure(exception))
        {
            return DependencyUnavailable();
        }
    }

    public static async Task<IResult> CreateAsync(
        string resource,
        HttpRequest request,
        IConfigurationManagementCommandPort commands,
        IdempotentCommandExecutor executor,
        IServerPrincipalAccessor principalAccessor,
        IHostTransactionFactory transactionFactory,
        CancellationToken ct)
    {
        if (!ConfigurationManagementResources.IsKnown(resource))
            return UnknownResource();
        var fields = await ReadCommandFieldsAsync(request, ct);
        var operation = resource switch
        {
            ConfigurationManagementResources.Sites => CommandOperationCodes.CreateSite,
            ConfigurationManagementResources.Areas => CommandOperationCodes.CreateArea,
            ConfigurationManagementResources.Assets => CommandOperationCodes.CreateAsset,
            ConfigurationManagementResources.Points => CommandOperationCodes.CreatePoint,
            ConfigurationManagementResources.DataSources => CommandOperationCodes.CreateSource,
            ConfigurationManagementResources.SourcePointMappings => CommandOperationCodes.CreateMapping,
            ConfigurationManagementResources.SimulatorConfigurations => CommandOperationCodes.CreateSimulatorConfiguration,
            _ => string.Empty
        };
        var targetId = resource switch
        {
            ConfigurationManagementResources.Areas => GuidField(fields, "siteId"),
            ConfigurationManagementResources.Assets => GuidField(fields, "areaId"),
            ConfigurationManagementResources.Points => GuidField(fields, "assetId"),
            _ => null
        };
        if (resource is (ConfigurationManagementResources.Areas or
            ConfigurationManagementResources.Assets or ConfigurationManagementResources.Points) &&
            targetId is null)
            return Results.Json(new { errorCode = "PARENT_ID_REQUIRED" }, statusCode: 400);
        return await ExecuteManagementCommandAsync(operation, targetId, resource, request,
            fields, commands, executor, principalAccessor, transactionFactory, ct,
            siteCreate: resource == ConfigurationManagementResources.Sites);
    }

    public static async Task<IResult> UpdateAsync(
        string resource,
        Guid id,
        HttpRequest request,
        IConfigurationManagementCommandPort commands,
        IdempotentCommandExecutor executor,
        IServerPrincipalAccessor principalAccessor,
        IHostTransactionFactory transactionFactory,
        CancellationToken ct)
    {
        if (!ConfigurationManagementResources.IsKnown(resource))
            return UnknownResource();
        var fields = await ReadCommandFieldsAsync(request, ct);
        var operation = resource switch
        {
            ConfigurationManagementResources.Sites => CommandOperationCodes.UpdateSite,
            ConfigurationManagementResources.Areas => CommandOperationCodes.UpdateArea,
            ConfigurationManagementResources.Assets => CommandOperationCodes.UpdateAsset,
            ConfigurationManagementResources.Points => CommandOperationCodes.UpdatePoint,
            ConfigurationManagementResources.DataSources => CommandOperationCodes.UpdateSource,
            ConfigurationManagementResources.SourcePointMappings => CommandOperationCodes.UpdateMapping,
            ConfigurationManagementResources.SimulatorConfigurations => CommandOperationCodes.UpdateSimulatorConfiguration,
            _ => string.Empty
        };
        return await ExecuteManagementCommandAsync(operation, id, resource, request,
            fields, commands, executor, principalAccessor, transactionFactory, ct,
            siteUpdate: resource == ConfigurationManagementResources.Sites);
    }

    public static async Task<IResult> DeleteAsync(
        string resource,
        Guid id,
        HttpRequest request,
        IConfigurationManagementCommandPort commands,
        IdempotentCommandExecutor executor,
        IServerPrincipalAccessor principalAccessor,
        IHostTransactionFactory transactionFactory,
        CancellationToken ct)
    {
        if (resource is not (ConfigurationManagementResources.DataSources or
            ConfigurationManagementResources.SourcePointMappings))
            return Results.Json(new
            {
                errorCode = "UNSUPPORTED_ACTION",
                reason = "Chỉ Nguồn dữ liệu và Ánh xạ nguồn ở trạng thái Nháp mới cho phép xóa an toàn."
            }, statusCode: 422);
        var fields = await ReadCommandFieldsAsync(request, ct);
        return await ExecuteManagementCommandAsync(
            resource == ConfigurationManagementResources.DataSources
                ? CommandOperationCodes.UpdateSource : CommandOperationCodes.UpdateMapping,
            id, resource, request, fields, commands, executor, principalAccessor,
            transactionFactory, ct);
    }

    public static async Task<IResult> LifecycleAsync(
        string resource,
        Guid id,
        string action,
        HttpRequest request,
        IConfigurationManagementCommandPort commands,
        IdempotentCommandExecutor executor,
        IServerPrincipalAccessor principalAccessor,
        IHostTransactionFactory transactionFactory,
        CancellationToken ct)
    {
        var operation = LifecycleOperation(resource, action);
        if (operation is null)
            return Results.Json(new { errorCode = "UNSUPPORTED_ACTION", reason =
                "Chuyển trạng thái này không được hỗ trợ bởi miền nghiệp vụ." }, statusCode: 422);
        var fields = await ReadCommandFieldsAsync(request, ct);
        return await ExecuteManagementCommandAsync(operation, id, resource, request,
            fields, commands, executor, principalAccessor, transactionFactory, ct);
    }

    public static async Task<IResult> ValidateAsync(
        string resource,
        Guid id,
        HttpRequest request,
        IConfigurationManagementCommandPort commands,
        IdempotentCommandExecutor executor,
        IServerPrincipalAccessor principalAccessor,
        IHostTransactionFactory transactionFactory,
        CancellationToken ct)
    {
        if (!ConfigurationManagementResources.IsKnown(resource))
            return UnknownResource();
        if (principalAccessor.Current is not { } principal) return Results.Unauthorized();
        if (!request.Headers.TryGetValue("Idempotency-Key", out var key) || string.IsNullOrWhiteSpace(key))
            return Results.Problem("Idempotency-Key is required.", statusCode: 400);
        var operation = CommandOperationCodes.ValidateConfiguration;
        var identity = new CommandIdentity(principal.UserId, operation, key!);
        var fingerprint = CommandFingerprintV1.Compute(new CommandFingerprintInput(
            operation, principal.UserId, resource, id, resource, id, null,
            [CommandFingerprintField.Uuid("targetId", id)]));
        try
        {
            var response = await executor.ExecuteTransactionalAsync(identity, fingerprint,
                transactionFactory,
                (transaction, token) => commands.ValidateAsync(resource, id, principal, transaction, token), ct);
            return new IdempotentHttpResult(response);
        }
        catch (Exception exception) when (IsRuntimeFailure(exception))
        {
            return DependencyUnavailable();
        }
    }

    private static async Task<IResult> ExecuteManagementCommandAsync(
        string operation,
        Guid? targetId,
        string resource,
        HttpRequest request,
        IReadOnlyList<CommandFingerprintField> bodyFields,
        IConfigurationManagementCommandPort commands,
        IdempotentCommandExecutor executor,
        IServerPrincipalAccessor principalAccessor,
        IHostTransactionFactory transactionFactory,
        CancellationToken ct,
        bool siteCreate = false,
        bool siteUpdate = false)
    {
        if (string.IsNullOrWhiteSpace(operation)) return UnknownResource();
        if (!request.Headers.TryGetValue("Idempotency-Key", out var key) || string.IsNullOrWhiteSpace(key))
            return Results.Problem("Idempotency-Key is required.", statusCode: 400);
        if (principalAccessor.Current is not { } principal) return Results.Unauthorized();
        var mutatingUpdate = request.Method is "PUT" or "DELETE" ||
            ConfigurationEndpointPolicy.IsLifecyclePost(request.Method, operation);
        long? expected = null;
        if (mutatingUpdate)
        {
            if (!TryReadExpectedVersion(request, out var version))
                return Results.Problem("A valid If-Match is required.", statusCode: 400);
            expected = version;
        }
        var name = bodyFields.FirstOrDefault(field => field.Name.Equals("name", StringComparison.OrdinalIgnoreCase))?.Value?.ToString() ??
            request.Query["name"].FirstOrDefault() ?? string.Empty;
        var fields = bodyFields.Concat([CommandFingerprintField.String("httpMethod", request.Method)]).ToArray();
        var identity = new CommandIdentity(principal.UserId, operation, key!);
        var fingerprint = CommandFingerprintV1.Compute(new CommandFingerprintInput(
            operation, principal.UserId, resource, targetId, resource, targetId, expected, fields));
        try
        {
            var response = await executor.ExecuteTransactionalAsync(identity, fingerprint, transactionFactory,
                (transaction, token) => siteCreate
                    ? commands.CreateSiteAsync(new ConfigurationCommandRequest(null, name, null, fields), principal, transaction, token)
                    : siteUpdate
                        ? commands.UpdateSiteAsync(new ConfigurationCommandRequest(targetId, name, expected, fields), principal, transaction, token)
                        : commands.ExecuteAsync(operation, new ConfigurationCommandRequest(targetId, name, expected, fields), principal, transaction, token), ct);
            return new IdempotentHttpResult(response);
        }
        catch (Exception exception) when (IsRuntimeFailure(exception))
        {
            return DependencyUnavailable();
        }
    }

    private static string? LifecycleOperation(string resource, string action) =>
        (resource, action.ToLowerInvariant()) switch
        {
            (ConfigurationManagementResources.Sites, "activate") => CommandOperationCodes.ActivateSite,
            (ConfigurationManagementResources.Sites, "deactivate") => CommandOperationCodes.DeactivateSite,
            (ConfigurationManagementResources.Areas, "activate") => CommandOperationCodes.ActivateArea,
            (ConfigurationManagementResources.Areas, "deactivate") => CommandOperationCodes.DeactivateArea,
            (ConfigurationManagementResources.Assets, "activate") => CommandOperationCodes.ActivateAsset,
            (ConfigurationManagementResources.Assets, "deactivate") => CommandOperationCodes.DeactivateAsset,
            (ConfigurationManagementResources.Points, "activate") => CommandOperationCodes.ActivatePoint,
            (ConfigurationManagementResources.Points, "deactivate") => CommandOperationCodes.DeactivatePoint,
            (ConfigurationManagementResources.DataSources, "activate") => CommandOperationCodes.ActivateSource,
            (ConfigurationManagementResources.DataSources, "suspend") => CommandOperationCodes.SuspendSource,
            (ConfigurationManagementResources.DataSources, "decommission") => CommandOperationCodes.DecommissionSource,
            (ConfigurationManagementResources.SourcePointMappings, "activate") => CommandOperationCodes.ActivateMapping,
            (ConfigurationManagementResources.SourcePointMappings, "inactivate") => CommandOperationCodes.InactivateMapping,
            (ConfigurationManagementResources.SourcePointMappings, "supersede") => CommandOperationCodes.SupersedeMapping,
            _ => null
        };

    private static IResult UnknownResource() => Results.Json(new { errorCode = "UNKNOWN_RESOURCE" }, statusCode: 400);

    private static IResult DependencyUnavailable() => Results.Json(
        new { errorCode = "DEPENDENCY_UNAVAILABLE" },
        statusCode: StatusCodes.Status503ServiceUnavailable);

    private static bool IsRuntimeFailure(Exception exception) =>
        exception is not OperationCanceledException &&
        (exception is InvalidOperationException or TimeoutException or Npgsql.NpgsqlException);

    private static Guid? GuidField(IReadOnlyList<CommandFingerprintField> fields, string name) =>
        fields.FirstOrDefault(field => field.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value is Guid guid
            ? guid : Guid.TryParse(fields.FirstOrDefault(field => field.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value?.ToString(), out var parsed) ? parsed : null;

    private static async Task<IReadOnlyList<CommandFingerprintField>> ReadCommandFieldsAsync(HttpRequest request, CancellationToken ct)
    {
        if (request.ContentLength is 0 || request.Body == Stream.Null ||
            request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) != true)
            return Array.Empty<CommandFingerprintField>();
        try
        {
            using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return Array.Empty<CommandFingerprintField>();
            return document.RootElement.EnumerateObject()
                .Where(property => !property.Name.Contains("password", StringComparison.OrdinalIgnoreCase) &&
                    !property.Name.Contains("secret", StringComparison.OrdinalIgnoreCase) &&
                    !property.Name.Contains("token", StringComparison.OrdinalIgnoreCase))
                .Select(property => ToFingerprintField(property.Name, property.Value)).ToArray();
        }
        catch (JsonException) { return Array.Empty<CommandFingerprintField>(); }
    }

    private static CommandFingerprintField ToFingerprintField(string name, JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => CommandFingerprintField.Null(name),
        JsonValueKind.True => CommandFingerprintField.Bool(name, true),
        JsonValueKind.False => CommandFingerprintField.Bool(name, false),
        JsonValueKind.Number when value.TryGetInt64(out var integer) => CommandFingerprintField.Int64(name, integer),
        JsonValueKind.Number when value.TryGetDecimal(out var number) => CommandFingerprintField.Decimal(name, number),
        JsonValueKind.String when value.TryGetGuid(out var guid) => CommandFingerprintField.Uuid(name, guid),
        JsonValueKind.String when value.TryGetDateTime(out var timestamp) => CommandFingerprintField.Timestamp(name, timestamp.ToUniversalTime()),
        JsonValueKind.String => CommandFingerprintField.String(name, value.GetString() ?? string.Empty),
        _ => CommandFingerprintField.String(name, value.GetRawText())
    };

    private static bool TryReadExpectedVersion(HttpRequest request, out long version)
    {
        version = 0;
        if (!request.Headers.TryGetValue("If-Match", out var values) || values.Count != 1) return false;
        var raw = values[0]?.Trim();
        if (raw is null) return false;
        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"') raw = raw[1..^1];
        return long.TryParse(raw, out version) && version > 0;
    }

    private static string? Optional(HttpRequest request, string name) =>
        request.Query[name].FirstOrDefault() is { } value &&
        !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static int Positive(HttpRequest request, string name, int fallback) =>
        int.TryParse(request.Query[name].FirstOrDefault(), out var parsed) ? parsed : fallback;

    private static int ClampPositive(HttpRequest request, string name, int fallback, int min, int max) =>
        Math.Clamp(Positive(request, name, fallback), min, max);
}
