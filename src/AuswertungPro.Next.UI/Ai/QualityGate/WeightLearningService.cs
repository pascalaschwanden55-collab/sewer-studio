using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.UI.Ai.QualityGate;

/// <summary>
/// Coordinate-descent optimizer for per-category evidence weights.
/// For each category with >= MinSamples validation entries, searches
/// 8 dimensions × 7 candidate values, minimizing binary cross-entropy.
/// Learned weights are persisted to SQLite CategoryWeights table.
/// </summary>
public sealed class WeightLearningService
{
    public int MinSamples { get; set; } = 20;

    private readonly SqliteConnection _conn;

    private static readonly double[] CandidateValues = { 0.0, 0.05, 0.10, 0.15, 0.20, 0.30, 0.40 };
    private const int WeightDimensions = 8;

    public WeightLearningService(SqliteConnection connection)
    {
        _conn = connection;
    }

    /// <summary>
    /// Re-learn weights for all categories that have enough validation data.
    /// </summary>
    public async Task ReLearnAsync(CancellationToken ct = default)
    {
        var categories = GetCategoriesWithSufficientData();

        foreach (var category in categories)
        {
            ct.ThrowIfCancellationRequested();
            var samples = LoadValidationSamples(category);
            if (samples.Count < MinSamples) continue;

            var bestWeights = await Task.Run(() => OptimizeWeights(samples, ct), ct).ConfigureAwait(false);
            SaveWeights(category, bestWeights, samples.Count);
        }
    }

    /// <summary>Load persisted weights for a specific category.</summary>
    public CategoryWeights? LoadWeights(string category)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT WeightsJson FROM CategoryWeights WHERE Category = @cat";
        cmd.Parameters.AddWithValue("@cat", category);
        var json = cmd.ExecuteScalar() as string;
        return json is not null ? CategoryWeights.FromJson(json) : null;
    }

    /// <summary>Load all persisted category weights.</summary>
    public IReadOnlyList<CategoryWeights> LoadAllWeights()
    {
        var result = new List<CategoryWeights>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT WeightsJson FROM CategoryWeights";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var json = reader.GetString(0);
            result.Add(CategoryWeights.FromJson(json));
        }
        return result;
    }

    private IReadOnlyList<string> GetCategoriesWithSufficientData()
    {
        var result = new List<string>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT VsaCode, COUNT(*) as cnt
            FROM ValidationLog
            GROUP BY VsaCode
            HAVING cnt >= @min
            """;
        cmd.Parameters.AddWithValue("@min", MinSamples);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(reader.GetString(0));
        return result;
    }

    private IReadOnlyList<ValidationSample> LoadValidationSamples(string category)
    {
        var samples = new List<ValidationSample>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT WasCorrect, EvidenceJson FROM ValidationLog WHERE VsaCode = @cat";
        cmd.Parameters.AddWithValue("@cat", category);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var correct = reader.GetInt32(0) == 1;
            var evidenceJson = reader.GetString(1);
            EvidenceVector? evidence = null;
            try { evidence = JsonSerializer.Deserialize<EvidenceVector>(evidenceJson); }
            catch { /* skip malformed */ }
            if (evidence is not null)
                samples.Add(new ValidationSample(correct, evidence));
        }
        return samples;
    }

    /// <summary>
    /// Coordinate descent: for each dimension, try all candidate values keeping others fixed.
    /// Pick the value that minimizes BCE loss. Repeat until convergence.
    /// </summary>
    private double[] OptimizeWeights(IReadOnlyList<ValidationSample> samples, CancellationToken ct)
    {
        var weights = CategoryWeights.Default().ToArray();
        var bestLoss = ComputeBceLoss(weights, samples);

        const int maxIterations = 5;
        for (int iter = 0; iter < maxIterations; iter++)
        {
            ct.ThrowIfCancellationRequested();
            bool improved = false;

            for (int dim = 0; dim < WeightDimensions; dim++)
            {
                var original = weights[dim];
                var bestVal = original;
                var dimBestLoss = bestLoss;

                foreach (var candidate in CandidateValues)
                {
                    weights[dim] = candidate;
                    var loss = ComputeBceLoss(weights, samples);
                    if (loss < dimBestLoss)
                    {
                        dimBestLoss = loss;
                        bestVal = candidate;
                    }
                }

                weights[dim] = bestVal;
                if (dimBestLoss < bestLoss)
                {
                    bestLoss = dimBestLoss;
                    improved = true;
                }
            }

            if (!improved) break;
        }

        // Normalize
        var sum = weights.Sum();
        if (sum > 0)
            for (int i = 0; i < weights.Length; i++)
                weights[i] /= sum;

        return weights;
    }

    private static double ComputeBceLoss(double[] weights, IReadOnlyList<ValidationSample> samples)
    {
        double totalLoss = 0;
        int count = 0;
        const double eps = 1e-7;

        foreach (var sample in samples)
        {
            var score = ComputeWeightedScore(weights, sample.Evidence);
            if (double.IsNaN(score)) continue;

            score = Math.Clamp(score, eps, 1.0 - eps);
            var target = sample.WasCorrect ? 1.0 : 0.0;
            totalLoss += -(target * Math.Log(score) + (1 - target) * Math.Log(1 - score));
            count++;
        }

        return count > 0 ? totalLoss / count : double.MaxValue;
    }

    private static double ComputeWeightedScore(double[] weights, EvidenceVector ev)
    {
        double[] signals =
        {
            ev.YoloConf ?? double.NaN,
            ev.DinoConf ?? double.NaN,
            ev.SamMaskStability ?? double.NaN,
            ev.QwenVisionConf ?? double.NaN,
            ev.LlmCodeConf ?? double.NaN,
            ev.KbSimilarity ?? double.NaN,
            ev.KbCodeAgreement.HasValue ? (ev.KbCodeAgreement.Value ? 1.0 : 0.0) : double.NaN,
            ev.PlausibilityScore ?? double.NaN
        };

        double sumW = 0, sumWV = 0;
        for (int i = 0; i < 8; i++)
        {
            if (double.IsNaN(signals[i])) continue;
            sumW += weights[i];
            sumWV += weights[i] * Math.Clamp(signals[i], 0, 1);
        }

        return sumW > 0 ? sumWV / sumW : double.NaN;
    }

    private void SaveWeights(string category, double[] weights, int validationCount)
    {
        var cw = new CategoryWeights { Category = category, ValidationCount = validationCount, UpdatedUtc = DateTime.UtcNow };
        cw.FromArray(weights);

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO CategoryWeights (Category, WeightsJson, ValidationCount, UpdatedUtc)
            VALUES (@cat, @json, @count, @utc)
            """;
        cmd.Parameters.AddWithValue("@cat", category);
        cmd.Parameters.AddWithValue("@json", cw.ToJson());
        cmd.Parameters.AddWithValue("@count", validationCount);
        cmd.Parameters.AddWithValue("@utc", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    private sealed record ValidationSample(bool WasCorrect, EvidenceVector Evidence);
}
