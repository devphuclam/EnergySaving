using IUMP.Api.Infrastructure;
using IUMP.BuildingBlocks.Persistence;
using IUMP.Composition.Postgres;
using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.IAM.Contracts;
using IUMP.Modules.IAM.Domain;
using IUMP.Modules.Organization.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace IUMP.Tests.Integration.OperationalWorkspace;

/// T038: PostgreSQL scoped search/filter/paging and dependency-safe lifecycle red tests
/// for configuration management. Written against the Phase 2 public seams before green
/// implementation; they do not compile until the management ports exist.
public static class ConfigurationManagementTests
{
    private static int _assertionCount;
    private static int _testCount;

    public static int TestCount => _testCount;
    public static int AssertionCount => _assertionCount;

    public static async Task<IReadOnlyList<string>> RunAsync(
        IServiceProvider root)
    {
        var failures = new List<string>();
        _assertionCount = 0;
        _testCount = 0;
        var suffix = Guid.NewGuid().ToString("N")[..8];

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
                "T038 requires an existing active Administrator bootstrap account.", failures);
            if (admin is null) return failures;

            var adminPrincipal = new ServerPrincipal(
                admin.Id.Value, admin.Username,
                new HashSet<string>(), new HashSet<string>(), true,
                new HashSet<string> { "Administrator" });

            var organization = services.GetRequiredService<OrganizationRuntimeGateway>();
            var siteA = await organization.CreateSiteAsync(
                $"T038 Site A {suffix}", admin.Id.ToString());
            var siteB = await organization.CreateSiteAsync(
                $"T038 Site B {suffix}", admin.Id.ToString());
            Check(siteA.IsSuccess && siteB.IsSuccess,
                "T038 could not create two Sites.", failures);
            if (!siteA.IsSuccess || !siteB.IsSuccess) return failures;

            var query = services.GetRequiredService<IConfigurationManagementQueryPort>();
            await ScopedSitePagingAsync(query, adminPrincipal, siteA.Id!.Value, siteB.Id!.Value, failures);
            await SearchAndStatusFilterBeforePagingAsync(query, adminPrincipal, suffix, failures);
            await ScopedChildPagingAsync(query, adminPrincipal, siteA.Id.Value, siteB.Id.Value, suffix, failures);
            await ScopedSourceAndMappingPagingAsync(query, services, adminPrincipal,
                siteA.Id.Value, siteB.Id.Value, suffix, failures);
            await ScopedConfigurationPagingAsync(query, adminPrincipal, siteA.Id.Value, siteB.Id.Value, suffix, failures);
            await DetailAndDuplicateJourneyAsync(services, query, adminPrincipal, siteA.Id.Value, suffix, failures);
            await DependencySafeLifecycleAsync(services, adminPrincipal, siteA.Id.Value, suffix, failures);
        }
        catch (Exception ex)
        {
            failures.Add($"T038 unexpected exception: {ex.Message}");
        }

        Console.WriteLine(
            $"T038: cases={_testCount}; assertions={_assertionCount}; failures={failures.Count}");
        return failures;
    }

    private static async Task ScopedSitePagingAsync(IConfigurationManagementQueryPort query,
        ServerPrincipal admin, Guid siteA, Guid siteB, List<string> failures)
    {
        _testCount++;
        var page = await query.QueryAsync("sites",
            new ManagementQueryFilter(Page: 1, PageSize: 1), admin);
        Check(page.TotalCount >= 2 && page.Items.Count == 1,
            "T038 site paging returns one row with a scope-filtered total.", failures);
    }

    private static async Task SearchAndStatusFilterBeforePagingAsync(
        IConfigurationManagementQueryPort query, ServerPrincipal admin,
        string suffix, List<string> failures)
    {
        _testCount++;
        var search = await query.QueryAsync("sites",
            new ManagementQueryFilter(Search: $"T038 Site A {suffix}", Page: 1, PageSize: 10), admin);
        Check(search.TotalCount == 1 && search.Items.Count == 1,
            "T038 search narrows the result set before paging.", failures);

        var draftOnly = await query.QueryAsync("sites",
            new ManagementQueryFilter(Status: "Draft", Search: suffix, Page: 1, PageSize: 10), admin);
        Check(draftOnly.TotalCount >= 2 && draftOnly.Items.All(item =>
                ((SiteManagementItem)item).Status == "Draft"),
            "T038 status filter applies before paging and total counts.", failures);
    }

    private static async Task ScopedChildPagingAsync(IConfigurationManagementQueryPort query,
        ServerPrincipal admin, Guid siteA, Guid siteB, string suffix, List<string> failures)
    {
        _testCount++;
        var areas = await query.QueryAsync("areas",
            new ManagementQueryFilter(SiteId: siteA.ToString("D"), Page: 1, PageSize: 10), admin);
        var sites = await query.QueryAsync("sites",
            new ManagementQueryFilter(Search: suffix, Page: 1, PageSize: 10), admin);
        Check(areas.TotalCount == 0 && sites.TotalCount == 2,
            "T038 child queries apply the parent scope before paging.", failures);

        var assets = await query.QueryAsync("assets",
            new ManagementQueryFilter(Search: suffix, Page: 1, PageSize: 10), admin);
        var points = await query.QueryAsync("points",
            new ManagementQueryFilter(Search: suffix, Page: 1, PageSize: 10), admin);
        Check(assets.TotalCount == 0 && points.TotalCount == 0,
            "T038 empty scoped child queries return zero counts, not global counts.", failures);
    }

    private static async Task ScopedSourceAndMappingPagingAsync(
        IConfigurationManagementQueryPort query, IServiceProvider services,
        ServerPrincipal admin, Guid siteA, Guid siteB, string suffix, List<string> failures)
    {
        _testCount++;
        var catalog = services.GetRequiredService<CatalogRuntimeGateway>();
        var sourceA = await catalog.CreateSourceAsync(
            $"T038-SRC-A-{suffix}", $"T038 Source A {suffix}", siteId: siteA);
        var sourceB = await catalog.CreateSourceAsync(
            $"T038-SRC-B-{suffix}", $"T038 Source B {suffix}", siteId: siteB);
        Check(sourceA.Id != Guid.Empty && sourceB.Id != Guid.Empty,
            "T038 could not create scoped Sources.", failures);

        var sources = await query.QueryAsync("data-sources",
            new ManagementQueryFilter(SiteId: siteA.ToString("D"), Page: 1, PageSize: 10), admin);
        Check(sources.TotalCount >= 1 && sources.Items.All(item =>
                ((SourceManagementItem)item).SiteId == siteA.ToString("D")),
            "T038 Source paging is scope-filtered before counts.", failures);

        var mappings = await query.QueryAsync("source-point-mappings",
            new ManagementQueryFilter(SiteId: siteA.ToString("D"), Page: 1, PageSize: 10), admin);
        Check(mappings.TotalCount == 0,
            "T038 Mapping paging returns zero when no Point exists in scope.", failures);
    }

    private static async Task ScopedConfigurationPagingAsync(
        IConfigurationManagementQueryPort query, ServerPrincipal admin,
        Guid siteA, Guid siteB, string suffix, List<string> failures)
    {
        _testCount++;
        var configurations = await query.QueryAsync("simulator-configurations",
            new ManagementQueryFilter(SiteId: siteA.ToString("D"), Page: 1, PageSize: 10), admin);
        Check(configurations.TotalCount == 0,
            "T038 Simulator Configuration paging returns zero without a scoped Source.", failures);
    }

    private static async Task DetailAndDuplicateJourneyAsync(
        IServiceProvider services, IConfigurationManagementQueryPort query,
        ServerPrincipal admin, Guid siteA, string suffix, List<string> failures)
    {
        _testCount++;
        var detail = await query.GetDetailAsync("sites", siteA, admin);
        Check(detail is SiteManagementItem item && item.Id == siteA,
            "T038 site detail returns the authorized entity.", failures);

        using var duplicateScope = services.CreateScope();
        var commands = duplicateScope.ServiceProvider
            .GetRequiredService<IConfigurationManagementCommandPort>();
        var transactionFactory = duplicateScope.ServiceProvider
            .GetRequiredService<IHostTransactionFactory>();
        await using var transaction = await transactionFactory.BeginAsync();
        var duplicated = await commands.DuplicateAsync("sites", siteA, admin,
            transaction);
        await ((IHostTransactionController)transaction).CommitAsync();
        Check(duplicated.StatusCode == 201 && duplicated.ResourceReference is not null,
            "T038 duplicate returns a created resource reference.", failures);
        if (duplicated.ResourceReference is null) return;
        var duplicateId = Guid.Parse(duplicated.ResourceReference);
        var duplicateDetail = await query.GetDetailAsync("sites", duplicateId, admin);
        Check(duplicateDetail is SiteManagementItem dup &&
            dup.Id == duplicateId && dup.Status == "Draft" && dup.Version == 1,
            "T038 duplicate is persisted as a new Draft at version 1.", failures);
    }

    private static async Task DependencySafeLifecycleAsync(
        IServiceProvider services, ServerPrincipal admin, Guid siteA,
        string suffix, List<string> failures)
    {
        _testCount++;
        using var scope = services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogRuntimeGateway>();
        var source = await catalog.CreateSourceAsync(
            $"T038-SRC-DEP-{suffix}", $"T038 Dependency Source {suffix}", siteId: siteA);
        Check(source.Id != Guid.Empty, "T038 could not create dependency Source.", failures);
        if (source.Id == Guid.Empty) return;

        var transition = await catalog.TransitionSourceAsync(
            source.Id, source.Version, "activate");
        Check(transition is not null,
            "T038 could not activate dependency Source.", failures);

        var delete = await catalog.DeleteSourceAsync(source.Id, 2);
        Check(!delete.IsAllowed && delete.Code is "DEPENDENT_HISTORY" or "InvalidState",
            "T038 Active Source deletion is rejected by dependency protection.", failures);
    }

    private static void Check(bool condition, string message, List<string> failures)
    {
        _assertionCount++;
        if (!condition) failures.Add(message);
    }
}
