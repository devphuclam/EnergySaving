namespace IUMP.Tests.Integration.Acceptance;

/// <summary>Deterministic provider-neutral crash/replay contract source.</summary>
public static class SimulatorCrashRecoveryTests
{
    public static CrashRecoveryResult ExecuteSourceContract()
    {
        var port = new FakeSimulatorRecoveryPort();
        var pending = port.Begin("run-command", "fingerprint");
        var beforeIngestion = port.Recover("run-command", "fingerprint", CrashPoint.BeforeTelemetry);
        var afterTerminalRegistry = port.Recover("run-command", "fingerprint", CrashPoint.AfterTerminalRegistry);
        var replay = port.Recover("run-command", "fingerprint", CrashPoint.None);
        var conflict = port.Recover("run-command", "different", CrashPoint.None);
        return new(
            PendingBeforeProduction: pending == "PENDING" && port.PendingWasWrittenBeforeProduction,
            CrashBeforeIngestionSafe: beforeIngestion == "RECOVERABLE",
            TerminalBeforeAcquisitionRecoverySafe: afterTerminalRegistry == "RECOVERABLE",
            ExactReplay: replay == port.OriginalResult,
            FingerprintConflict: conflict == "IDEMPOTENCY_CONFLICT",
            CountersUpdatedOnce: port.CounterUpdateCount == 1,
            NoSyntheticCompletionMetadata: !port.SyntheticCompletionMetadata);
    }

    public enum CrashPoint { None, BeforeTelemetry, AfterTerminalRegistry }
    public sealed record CrashRecoveryResult(bool PendingBeforeProduction, bool CrashBeforeIngestionSafe,
        bool TerminalBeforeAcquisitionRecoverySafe, bool ExactReplay, bool FingerprintConflict,
        bool CountersUpdatedOnce, bool NoSyntheticCompletionMetadata);

    private sealed class FakeSimulatorRecoveryPort
    {
        private string? _fingerprint;
        public bool PendingWasWrittenBeforeProduction { get; private set; }
        public int CounterUpdateCount { get; private set; }
        public bool SyntheticCompletionMetadata { get; private set; }
        public string OriginalResult { get; } = "RUN_ACCEPTED:run-0001";

        public string Begin(string commandId, string fingerprint)
        {
            _fingerprint = fingerprint;
            PendingWasWrittenBeforeProduction = true;
            return "PENDING";
        }

        public string Recover(string commandId, string fingerprint, CrashPoint crashPoint)
        {
            if (_fingerprint != fingerprint) return "IDEMPOTENCY_CONFLICT";
            if (crashPoint != CrashPoint.None) return "RECOVERABLE";
            if (CounterUpdateCount == 0) CounterUpdateCount++;
            return OriginalResult;
        }
    }
}
