// BLOCKED: Requires PostgreSQL access (T049). No approved endpoint or psql available.
// This file defines the integration test structure. When database access is approved,
// unskip and run against a TEST catalog database seeded by migration 0003.

// using IUMP.Modules.Catalog.Contracts;
// using IUMP.Modules.Catalog.Domain;
// using Xunit;

// namespace IUMP.Tests.Integration.Catalog;

// public sealed class CatalogRepositoryTests : IClassFixture<CatalogDatabaseFixture>
// {
//     private readonly ICatalogCommandRepository _repo;
//     private readonly ICatalogEligibilityQueryRepository _eligibility;
//
//     public CatalogRepositoryTests(CatalogDatabaseFixture fixture)
//     {
//         _repo = fixture.CreateCommandRepository();
//         _eligibility = fixture.CreateEligibilityRepository();
//     }
//
//     [Fact(Skip = "BLOCKED_BY_DATABASE_ACCESS")]
//     public async Task CreateMetric_And_FetchByCode()
//     {
//         var metric = new Metric(MetricId.New(), "TEST_METRIC", "Test Metric", MetricStatus.Active, 1);
//         await _repo.AddMetricAsync(metric);
//         var fetched = await _repo.FindMetricByCodeAsync("TEST_METRIC");
//         Assert.NotNull(fetched);
//         Assert.Equal(metric.Id, fetched.Id);
//     }
//
//     [Fact(Skip = "BLOCKED_BY_DATABASE_ACCESS")]
//     public async Task CreateUnit_And_FetchByCode()
//     {
//         var unit = new Unit(UnitId.New(), "TEST_UNIT", "TU", UnitStatus.Active, 1);
//         await _repo.AddUnitAsync(unit);
//         var fetched = await _repo.FindUnitByCodeAsync("TEST_UNIT");
//         Assert.NotNull(fetched);
//         Assert.Equal(unit.Id, fetched.Id);
//     }
//
//     [Fact(Skip = "BLOCKED_BY_DATABASE_ACCESS")]
//     public async Task AddCompatibility_And_Fetch()
//     {
//         var metric = new Metric(MetricId.New(), "COMPAT_M", "Compat Metric", MetricStatus.Active, 1);
//         var unit = new Unit(UnitId.New(), "COMPAT_U", "CU", UnitStatus.Active, 1);
//         await _repo.AddMetricAsync(metric);
//         await _repo.AddUnitAsync(unit);
//         var compat = new MetricUnitCompatibility(metric.Id, unit.Id, true, 1);
//         await _repo.AddCompatibilityAsync(compat);
//
//         var fetched = await _repo.GetCompatibilityAsync(metric.Id, unit.Id);
//         Assert.NotNull(fetched);
//         Assert.True(fetched.IsCanonical);
//     }
//
//     [Fact(Skip = "BLOCKED_BY_DATABASE_ACCESS")]
//     public async Task DataSource_Lifecycle()
//     {
//         var ds = new DataSource(DataSourceId.New(), "SIM01", "Simulator 1", SourceType.Simulator, SourceStatus.Draft, 1);
//         await _repo.AddDataSourceAsync(ds);
//
//         var fetched = await _repo.FindDataSourceByCodeAsync("SIM01");
//         Assert.NotNull(fetched);
//         Assert.Equal(SourceStatus.Draft, fetched.Status);
//
//         fetched.TryTransitionTo(SourceStatus.Active);
//         await _repo.UpdateDataSourceAsync(fetched);
//         var after = await _repo.GetDataSourceAsync(fetched.Id);
//         Assert.NotNull(after);
//         Assert.Equal(SourceStatus.Active, after.Status);
//     }
//
//     [Fact(Skip = "BLOCKED_BY_DATABASE_ACCESS")]
//     public async Task SeedData_Present()
//     {
//         var metric = await _repo.FindMetricByCodeAsync("ELECTRIC_POWER");
//         Assert.NotNull(metric);
//         var unit = await _repo.FindUnitByCodeAsync("KW");
//         Assert.NotNull(unit);
//         var canonical = await _repo.GetCanonicalUnitAsync(metric.Id);
//         Assert.NotNull(canonical);
//         Assert.Equal(unit.Id, canonical.UnitId);
//     }
// }

// public sealed class CatalogDatabaseFixture : IDisposable
// {
//     private readonly string _connectionString;
//
//     public CatalogDatabaseFixture()
//     {
//         _connectionString = Environment.GetEnvironmentVariable("IUMP_TEST_CONNECTION_STRING")
//             ?? "Host=localhost;Database=iump_test;Username=test;Password=test";
//     }
//
//     public ICatalogCommandRepository CreateCommandRepository()
//     {
//         // TODO: Create real PostgreSQL-backed repository when database access is approved
//         throw new NotSupportedException("BLOCKED_BY_DATABASE_ACCESS");
//     }
//
//     public ICatalogEligibilityQueryRepository CreateEligibilityRepository()
//     {
//         throw new NotSupportedException("BLOCKED_BY_DATABASE_ACCESS");
//     }
//
//     public void Dispose() { }
// }
