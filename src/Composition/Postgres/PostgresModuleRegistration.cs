using IUMP.BuildingBlocks.Persistence;
using IUMP.Api.Infrastructure;
using IUMP.Infrastructure.Postgres;
using IUMP.Modules.Acquisition.Contracts;
using IUMP.Modules.Audit.Contracts;
using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.IAM.Contracts;
using IUMP.Modules.Integration.Contracts;
using IUMP.Modules.Operations.Contracts;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Telemetry.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace IUMP.Composition.Postgres;

public static class PostgresModuleRegistration
{
    public static IServiceCollection AddIumpPostgresModules(
        this IServiceCollection services,
        PostgresRuntimeConfiguration configuration)
    {
        services.AddSingleton(configuration);
        services.AddSingleton(_ => NpgsqlDataSource.Create(configuration.ConnectionString));
        services.AddScoped<PostgresTransactionContext>();
        services.AddScoped<IHostTransactionFactory, PostgresHostTransactionFactory>();
        services.AddScoped<IHostTransactionBackend, PostgresHostTransactionBackend>();

        var bindings = IamPostgresServices.Bindings
            .Concat(CatalogPostgresServices.Bindings)
            .Concat(OrganizationPostgresServices.Bindings)
            .Concat(AcquisitionPostgresServices.Bindings)
            .Concat(TelemetryPostgresServices.Bindings)
            .Concat(OperationsPostgresServices.Bindings)
            .Concat(IntegrationPostgresServices.Bindings)
            .Concat(AuditPostgresServices.Bindings);
        foreach (var binding in bindings)
        {
            services.AddScoped(binding.ImplementationType);
            foreach (var serviceType in binding.ServiceTypes)
                services.AddScoped(serviceType, provider =>
                    provider.GetRequiredService(binding.ImplementationType));
        }
        services.AddScoped<IWorkspaceSiteExistence, PostgresWorkspaceSiteExistence>();
        services.AddScoped<PostgresOperationalWorkspacePorts>();
        services.AddScoped<IOperationalWorkspaceQueryPort>(provider =>
            provider.GetRequiredService<PostgresOperationalWorkspacePorts>());
        services.AddScoped<IOperationalWorkspaceCommandPort>(provider =>
            provider.GetRequiredService<PostgresOperationalWorkspacePorts>());
        services.AddScoped<PostgresConfigurationManagementPorts>();
        services.AddScoped<IConfigurationManagementQueryPort>(provider =>
            provider.GetRequiredService<PostgresConfigurationManagementPorts>());
        services.AddScoped<IConfigurationManagementCommandPort>(provider =>
            provider.GetRequiredService<PostgresConfigurationManagementPorts>());
        services.AddScoped<IConfigurationCommandPort, PostgresConfigurationCommandPort>();
        services.AddIumpPostgresRuntimeProviders();
        return services;
    }
}
