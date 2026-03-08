using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.UI.Ai.Monitoring;

/// <summary>
/// Tracks confidence score distributions over time windows.
/// Computes KL-divergence between consecutive weeks to detect model drift.
/// KL-Divergence > 0.1 triggers a drift warning.
/// </summary>
public sealed class ConfidenceDistributionTracker
{
    public const double DriftThreshold = 0.1;
    private const int BinCount = 20;

    private readonly List<TimestampedConfidence> _history = new();
    private readonly object _lock = new();

    /// <summary>Record a new confidence observation.</summary>
    public void Record(double confidence, DateTime? timestamp = null)
    {
        lock (_lock)
        {
            _history.Add(new TimestampedConfidence(
                Math.Clamp(confidence, 0, 1),
                timestamp ?? DateTime.UtcNow));
        }
    }

    /// <summary>Record multiple observations.</summary>
    public void RecordBatch(IEnumerable<double> confidences, DateTime? timestamp = null)
    {
        var ts = timestamp ?? DateTime.UtcNow;
        lock (_lock)
        {
            foreach (var c in confidences)
                _history.Add(new TimestampedConfidence(Math.Clamp(c, 0, 1), ts));
        }
    }

    /// <summary>
    /// Check for drift between the most recent two weeks.
    /// Returns null if insufficient data.
    /// </summary>
    public DriftCheckResult? CheckDrift()
    {
        lock (_lock)
        {
            if (_history.Count < 20) return null;

            var now = DateTime.UtcNow;
            var thisWeek = _history.Where(h => h.Timestamp >= now.AddDays(-7)).Select(h => h.Confidence).ToList();
            var lastWeek = _history.Where(h => h.Timestamp >= now.AddDays(-14) && h.Timestamp < now.AddDays(-7))
                .Select(h => h.Confidence).ToList();

            if (thisWeek.Count < 10 || lastWeek.Count < 10) return null;

            var histThis = BuildHistogram(thisWeek);
            var histLast = BuildHistogram(lastWeek);
            var kl = KlDivergence(histThis, histLast);

            return new DriftCheckResult(
                KlDivergence: kl,
                IsDrifting: kl > DriftThreshold,
                ThisWeekCount: thisWeek.Count,
                LastWeekCount: lastWeek.Count,
                ThisWeekMean: thisWeek.Average(),
                LastWeekMean: lastWeek.Average());
        }
    }

    /// <summary>Get the current histogram for visualization.</summary>
    public IReadOnlyList<HistogramBin> GetCurrentHistogram(int days = 7)
    {
        lock (_lock)
        {
            var cutoff = DateTime.UtcNow.AddDays(-days);
            var values = _history.Where(h => h.Timestamp >= cutoff).Select(h => h.Confidence).ToList();
            if (values.Count == 0) return Array.Empty<HistogramBin>();

            var hist = BuildHistogram(values);
            var result = new List<HistogramBin>(BinCount);
            for (int i = 0; i < BinCount; i++)
            {
                result.Add(new HistogramBin(
                    BinLower: (double)i / BinCount,
                    BinUpper: (double)(i + 1) / BinCount,
                    Count: (int)(hist[i] * values.Count),
                    Fraction: hist[i]));
            }
            return result;
        }
    }

    private static double[] BuildHistogram(IReadOnlyList<double> values)
    {
        var hist = new double[BinCount];
        foreach (var v in values)
        {
            var idx = Math.Min((int)(v * BinCount), BinCount - 1);
            hist[idx]++;
        }
        var total = values.Count;
        for (int i = 0; i < BinCount; i++)
            hist[i] /= total;
        return hist;
    }

    private static double KlDivergence(double[] p, double[] q)
    {
        const double eps = 1e-10;
        double kl = 0;
        for (int i = 0; i < p.Length; i++)
        {
            var pi = Math.Max(p[i], eps);
            var qi = Math.Max(q[i], eps);
            kl += pi * Math.Log(pi / qi);
        }
        return Math.Max(0, kl);
    }

    private sealed record TimestampedConfidence(double Confidence, DateTime Timestamp);
}

public sealed record DriftCheckResult(
    double KlDivergence,
    bool IsDrifting,
    int ThisWeekCount,
    int LastWeekCount,
    double ThisWeekMean,
    double LastWeekMean
);

public sealed record HistogramBin(
    double BinLower,
    double BinUpper,
    int Count,
    double Fraction
);
