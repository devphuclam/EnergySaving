using System.Reflection;
using IUMP.Modules.Acquisition.Contracts;
using IUMP.Modules.Acquisition.Application;
using IUMP.Modules.Telemetry.Contracts;
using IUMP.Modules.Telemetry.Domain;
using IUMP.Modules.Telemetry.Application;

namespace IUMP.Tests.Unit.Telemetry;

public static class ArchitectureVerification
{
    public static int CheckCount { get; private set; }

    public static List<string> Run()
    {
        CheckCount = 0;
        var failures = new List<string>();

        Check(typeof(TelemetryProviderSnapshot).GetProperty("SourceType") is not null,
            "SourceType present", failures);
        Check(typeof(TelemetryProviderSnapshot).GetProperty("OrganizationVersion") is null,
            "no aggregated OrganizationVersion", failures);
        Check(typeof(TelemetryProviderSnapshot).GetProperty("SiteVersion") is not null,
            "exact SiteVersion", failures);
        Check(typeof(TelemetryProviderSnapshot).GetProperty("AreaVersion") is not null,
            "exact AreaVersion", failures);
        Check(typeof(TelemetryProviderSnapshot).GetProperty("AssetVersion") is not null,
            "exact AssetVersion", failures);
        Check(typeof(TelemetryProviderSnapshot).GetProperty("PointVersion") is not null,
            "exact PointVersion", failures);
        Check(typeof(TelemetryProviderSnapshot).GetProperty("CompatibilityIdentity") is not null,
            "CompatibilityIdentity present", failures);
        Check(typeof(TelemetryProviderSnapshot).GetProperty("CompatibilityVersion") is not null,
            "CompatibilityVersion present", failures);
        Check(typeof(TelemetryProviderSnapshot).GetProperty("CompatibilityStatus") is not null,
            "CompatibilityStatus present", failures);

        var validatorType = typeof(CanonicalTelemetryOriginalResultValidator);
        var ensureValidMethod = validatorType.GetMethod("EnsureValid",
            BindingFlags.Public | BindingFlags.Static);
        Check(ensureValidMethod is not null, "CanonicalTelemetryOriginalResultValidator.EnsureValid exists", failures);

        Check(!typeof(CanonicalTelemetryOriginalResult).GetProperties()
            .Any(p => p.Name == "LatestAdvanced" && p.PropertyType != typeof(bool?)),
            "LatestAdvanced is nullable in CanonicalTelemetryOriginalResult", failures);

        var clientType = typeof(ITelemetryIngestionClient);
        Check(clientType.GetMethod("DispatchCanonicalAsync") is not null,
            "DispatchCanonicalAsync exists", failures);

        var attemptType = typeof(SimulatorProductionAttempt);
        Check(attemptType.GetProperty("MeasurementPersisted") is not null, "attempt has MeasurementPersisted", failures);
        Check(attemptType.GetProperty("PersistedMeasurementId") is not null, "attempt has PersistedMeasurementId", failures);
        Check(attemptType.GetProperty("QualityCode") is not null, "attempt has QualityCode", failures);
        Check(attemptType.GetProperty("ReasonCode") is not null, "attempt has ReasonCode", failures);
        Check(attemptType.GetProperty("OriginalCorrelationId") is not null, "attempt has OriginalCorrelationId", failures);
        Check(attemptType.GetProperty("OriginalLineageId") is not null, "attempt has OriginalLineageId", failures);

        var dispatchResultType = typeof(TelemetryDispatchResult);
        Check(dispatchResultType.GetProperty("MeasurementPersisted") is not null,
            "TelemetryDispatchResult has MeasurementPersisted", failures);

        var migrationDirs = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "database", "migrations"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "database", "migrations"),
        };
        var migrationPath = migrationDirs
            .Select(dir => Path.GetFullPath(Path.Combine(dir, "0007_acquisition_run.sql")))
            .FirstOrDefault(File.Exists);
        var migrationText = migrationPath is not null ? File.ReadAllText(migrationPath) : "";
        Check(migrationText.Contains("measurement_persisted boolean"), "migration 0007 has measurement_persisted", failures);
        Check(migrationText.Contains("persisted_measurement_id uuid"), "migration 0007 has persisted_measurement_id", failures);
        Check(migrationText.Contains("quality_code text"), "migration 0007 has quality_code", failures);
        Check(migrationText.Contains("reason_code text"), "migration 0007 has reason_code", failures);
        Check(migrationText.Contains("original_correlation_id text"), "migration 0007 has original_correlation_id", failures);
        Check(migrationText.Contains("original_lineage_id text"), "migration 0007 has original_lineage_id", failures);

        var srcDirs = migrationDirs
            .Select(dir => Path.GetFullPath(Path.Combine(dir, "..", "..", "src")))
            .Where(Directory.Exists)
            .Select(dir => Path.Combine(dir, "Modules", "Telemetry", "Application"))
            .Where(Directory.Exists)
            .ToList();
        var telemetryAppDir = srcDirs.FirstOrDefault();
        if (telemetryAppDir is not null)
        {
            foreach (var phase8File in new[]
            {
                Path.Combine(telemetryAppDir, "LatestOrderingService.cs"),
                Path.Combine(telemetryAppDir, "SourceHealthService.cs"),
                Path.Combine(telemetryAppDir, "SourceHealthEvaluation.cs"),
            })
                Check(!File.Exists(phase8File), $"Phase 8 file not present: {Path.GetFileName(phase8File)}", failures);
        }

        return failures;
    }

    private static void Check(bool condition, string message, List<string> failures)
    {
        CheckCount++;
        if (!condition) failures.Add(message);
    }
}
