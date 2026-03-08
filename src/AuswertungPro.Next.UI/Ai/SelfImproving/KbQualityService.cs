using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.UI.Ai.SelfImproving;

/// <summary>
/// Identifies KB samples that consistently lead to incorrect predictions.
/// A sample is a stale candidate if it appears as a top retrieval hit
/// in validation entries where WasCorrect = false more than 50% of the time.
/// </summary>
public sealed class KbQualityService
{
    private readonly SqliteConnection _conn;

    public double StaleThreshold { get; set; } = 0.50;
    public int MinAppearances { get; set; } = 5;

    public KbQualityService(SqliteConnection connection)
    {
        _conn = connection;
    }

    /// <summary>
    /// Find KB samples that are correlated with incorrect predictions.
    /// Returns sample IDs and their error rates.
    /// </summary>
    public IReadOnlyList<StaleSampleCandidate> FindStaleCandidates()
    {
        // Find codes where the KB-suggested code disagrees with the final correct code
        var codeErrorRates = new Dictionary<string, (int Errors, int Total)>(StringComparer.OrdinalIgnoreCase);

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT SuggestedCode, FinalCode, WasCorrect
            FROM ValidationLog
            WHERE SuggestedCode != '' AND FinalCode != ''
            """;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var suggested = reader.GetString(0);
            var final_ = reader.GetString(1);
            var correct = reader.GetInt32(2) == 1;

            if (!codeErrorRates.TryGetValue(suggested, out var stats))
                stats = (0, 0);

            stats.Total++;
            if (!correct) stats.Errors++;
            codeErrorRates[suggested] = stats;
        }

        // Find KB samples whose code has a high error rate
        var staleCandidates = new List<StaleSampleCandidate>();

        using var sampleCmd = _conn.CreateCommand();
        sampleCmd.CommandText = "SELECT SampleId, VsaCode, Beschreibung FROM Samples";
        using var sampleReader = sampleCmd.ExecuteReader();
        while (sampleReader.Read())
        {
            var sampleId = sampleReader.GetString(0);
            var code = sampleReader.GetString(1);
            var desc = sampleReader.GetString(2);

            if (codeErrorRates.TryGetValue(code, out var stats) &&
                stats.Total >= MinAppearances)
            {
                var errorRate = (double)stats.Errors / stats.Total;
                if (errorRate >= StaleThreshold)
                {
                    staleCandidates.Add(new StaleSampleCandidate(
                        SampleId: sampleId,
                        VsaCode: code,
                        Description: desc,
                        ErrorRate: errorRate,
                        TotalAppearances: stats.Total,
                        ErrorCount: stats.Errors));
                }
            }
        }

        return staleCandidates.OrderByDescending(s => s.ErrorRate).ToList();
    }
}

public sealed record StaleSampleCandidate(
    string SampleId,
    string VsaCode,
    string Description,
    double ErrorRate,
    int TotalAppearances,
    int ErrorCount
);
