namespace IUMP.Tests.Integration.Acceptance;

/// <summary>Provider-neutral Latest/Health ordering and transition contract source.</summary>
public static class LatestHealthRaceTests
{
    public static LatestHealthResult ExecuteSourceContract()
    {
        var port = new FakeLatestHealthPort();
        port.Offer(new Candidate(10, 1, 1, "m-2", "Good", 12.5m));
        port.Offer(new Candidate(9, 99, 99, "m-old", "Good", 99m));
        port.Offer(new Candidate(10, 1, 1, "m-1", "Good", 11m));
        port.Offer(new Candidate(11, 1, 1, "m-bad", "Bad", 0m));
        port.SetHealth("Stale");
        port.SetHealth("NoData");
        port.SetHealth("Online");
        port.Reconcile();
        return new(
            OutOfOrderDidNotRegress: port.Latest?.MeasurementId == "m-2",
            EqualTupleDeterministic: port.Latest?.MeasurementId == "m-2",
            ConcurrentConvergence: port.AdvanceCount == 1,
            BadDidNotAdvance: port.Latest?.Value == 12.5m,
            StaleNoDataRecovered: port.Health == "Online",
            RestartReconciled: port.ReconcileCount == 1,
            NoDataHasNoNumericZero: port.NoDataNumericValue is null,
            ExactTransitions: port.Events.SequenceEqual(["Online->Stale", "Stale->NoData", "NoData->Online"]));
    }

    public sealed record Candidate(long SourceTimestamp, long Sequence, long ProcessingOrder,
        string MeasurementId, string Quality, decimal? Value);
    public sealed record LatestHealthResult(bool OutOfOrderDidNotRegress, bool EqualTupleDeterministic,
        bool ConcurrentConvergence, bool BadDidNotAdvance, bool StaleNoDataRecovered,
        bool RestartReconciled, bool NoDataHasNoNumericZero, bool ExactTransitions);

    private sealed class FakeLatestHealthPort
    {
        public Candidate? Latest { get; private set; }
        public string Health { get; private set; } = "Online";
        public decimal? NoDataNumericValue => null;
        public int AdvanceCount { get; private set; }
        public int ReconcileCount { get; private set; }
        public List<string> Events { get; } = [];

        public void Offer(Candidate candidate)
        {
            if (candidate.Quality == "Bad") return;
            if (Latest is null || Compare(candidate, Latest) > 0)
            {
                Latest = candidate;
                AdvanceCount++;
            }
        }

        public void SetHealth(string next)
        {
            if (next == Health) return;
            Events.Add($"{Health}->{next}");
            Health = next;
        }

        public void Reconcile() => ReconcileCount++;

        private static int Compare(Candidate left, Candidate right)
        {
            var tuple = left.SourceTimestamp.CompareTo(right.SourceTimestamp);
            if (tuple != 0) return tuple;
            tuple = left.Sequence.CompareTo(right.Sequence);
            if (tuple != 0) return tuple;
            tuple = left.ProcessingOrder.CompareTo(right.ProcessingOrder);
            return tuple != 0 ? tuple : string.CompareOrdinal(left.MeasurementId, right.MeasurementId);
        }
    }
}
