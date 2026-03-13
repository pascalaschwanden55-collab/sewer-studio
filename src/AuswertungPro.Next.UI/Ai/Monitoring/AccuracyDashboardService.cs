using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.UI.Ai.Monitoring;

/// <summary>
/// Per-VSA-Code Precision/Recall/F1 computation from ValidationLog.
/// Provides dashboard-ready accuracy metrics.
/// </summary>
public sealed class AccuracyDashboardService
{
    private readonly SqliteConnection _conn;

    public AccuracyDashboardService(SqliteConnection connection)
    {
        _conn = connection;
    }

    /// <summary>Compute per-code accuracy metrics.</summary>
    public IReadOnlyList<CodeAccuracyMetric> ComputeMetrics()
    {
        var entries = LoadValidationEntries();
        if (entries.Count == 0) return Array.Empty<CodeAccuracyMetric>();

        var allCodes = entries
            .SelectMany(e => new[] { e.SuggestedCode, e.FinalCode })
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var metrics = new List<CodeAccuracyMetric>(allCodes.Count);

        foreach (var code in allCodes)
        {
            int tp = 0, fp = 0, fn = 0;

            foreach (var e in entries)
            {
                var suggested = e.SuggestedCode?.Equals(code, StringComparison.OrdinalIgnoreCase) ?? false;
                var actual = e.FinalCode?.Equals(code, StringComparison.OrdinalIgnoreCase) ?? false;

                if (suggested && actual) tp++;
                else if (suggested && !actual) fp++;
                else if (!suggested && actual) fn++;
            }

            var precision = tp + fp > 0 ? (double)tp / (tp + fp) : 0;
            var recall = tp + fn > 0 ? (double)tp / (tp + fn) : 0;
            var f1 = precision + recall > 0 ? 2 * precision * recall / (precision + recall) : 0;

            metrics.Add(new CodeAccuracyMetric(
                VsaCode: code,
                TruePositives: tp,
                FalsePositives: fp,
                FalseNegatives: fn,
                Precision: precision,
                Recall: recall,
                F1Score: f1,
                TotalSamples: tp + fp + fn));
        }

        return metrics.OrderByDescending(m => m.TotalSamples).ToList();
    }

    /// <summary>Compute overall accuracy.</summary>
    public OverallAccuracy ComputeOverall()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                COUNT(*) as Total,
                COALESCE(SUM(CASE WHEN WasCorrect = 1 THEN 1 ELSE 0 END), 0) as Correct
            FROM ValidationLog
            """;
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var total = reader.GetInt32(0);
            var correct = reader.GetInt32(1);
            return new OverallAccuracy(total, correct, total > 0 ? (double)correct / total : 0);
        }
        return new OverallAccuracy(0, 0, 0);
    }

    private IReadOnlyList<ValidationEntry> LoadValidationEntries()
    {
        var entries = new List<ValidationEntry>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT SuggestedCode, FinalCode, WasCorrect FROM ValidationLog WHERE SuggestedCode != '' AND FinalCode != ''";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            entries.Add(new ValidationEntry(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2) == 1));
        }
        return entries;
    }

    private sealed record ValidationEntry(string SuggestedCode, string FinalCode, bool WasCorrect);
}

public sealed record CodeAccuracyMetric(
    string VsaCode,
    int TruePositives,
    int FalsePositives,
    int FalseNegatives,
    double Precision,
    double Recall,
    double F1Score,
    int TotalSamples
);

public sealed record OverallAccuracy(int Total, int Correct, double Accuracy);
