using System;
using AuswertungPro.Next.Application.Ai.QualityGate;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Ai.QualityGate;

/// <summary>
/// Coordinate-descent optimizer for per-category evidence weights.
/// For each category with >= MinSamples validation entries, searches
/// 8 dimensions × 7 candidate values, minimizing binary cross-entropy.
/// Learned weights are persisted to SQLite CategoryWeights table.
///
/// Phase 2.2: ReLearnAsync schreibt nicht mehr direkt in die Tabelle, sondern
/// in einen In-Memory-Cache. PersistAsync schreibt anschliessend per UPSERT
/// (mit L1-Diff-Noise-Filter > 0.01) in die explizit getypten Spalten der
/// CategoryWeights-Tabelle. WeightsJson bleibt parallel erhalten, damit der
/// bestehende JSON-Loader (LoadAllWeights) nicht bricht.
/// </summary>
public sealed class WeightLearningService
{
    public int MinSamples { get; set; } = 20;

    /// <summary>
    /// L1-Differenz-Schwelle (Summe der absoluten Differenzen ueber alle 8 Gewichte).
    /// Ist die Differenz zur in der DB liegenden Zeile kleiner gleich diesem Wert,
    /// wird der UPSERT uebersprungen — vermeidet Schreib-Storms bei Mini-Drift.
    /// </summary>
    public double NoiseFilterThreshold { get; set; } = 0.01;

    private readonly SqliteConnection _conn;

    /// <summary>
    /// In-Memory-State der zuletzt gelernten Gewichte pro Kategorie.
    /// Wird durch <see cref="ReLearnAsync"/> befuellt und durch
    /// <see cref="PersistAsync"/> in SQLite UPSERTet.
    /// </summary>
    private readonly ConcurrentDictionary<string, PendingWeights> _pendingWeights = new();

    private static readonly double[] CandidateValues = { 0.0, 0.05, 0.10, 0.15, 0.20, 0.30, 0.40 };
    private const int WeightDimensions = 8;

    public WeightLearningService(SqliteConnection connection)
    {
        _conn = connection;
    }

    /// <summary>
    /// Re-learn weights for all categories that have enough validation data.
    /// Befuellt den internen Pending-State und ruft am Ende <see cref="PersistAsync"/>.
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
            // Pending in den In-Memory-State legen — Persistierung erfolgt gebuendelt.
            _pendingWeights[category] = new PendingWeights(bestWeights, samples.Count);
        }

        // Phase 2.2: einmal pro ReLearn alle gelernten Gewichte gebuendelt persistieren.
        await PersistAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Persistiert alle Per-Code-Weights aus dem internen Pending-State in die
    /// SQLite-Tabelle <c>CategoryWeights</c>. Schreibt nur dann eine Zeile,
    /// wenn die L1-Differenz zur bisher gespeicherten Zeile groesser
    /// <see cref="NoiseFilterThreshold"/> ist (Noise-Filter).
    /// Gibt die Anzahl tatsaechlich geschriebener Zeilen zurueck.
    /// </summary>
    public Task<int> PersistAsync(CancellationToken ct = default)
    {
        int written = 0;
        // Snapshot ueber pending-Eintraege; concurrent-modifications stoeren nicht.
        foreach (var kvp in _pendingWeights.ToArray())
        {
            ct.ThrowIfCancellationRequested();
            var category = kvp.Key;
            var pending = kvp.Value;

            var existing = LoadWeightsArrayForFilter(category);
            if (existing is not null)
            {
                var l1 = L1Distance(existing, pending.Weights);
                if (l1 <= NoiseFilterThreshold)
                {
                    // Noise — Schreib-Storm vermeiden.
                    continue;
                }
            }

            UpsertWeights(category, pending.Weights, pending.ValidationCount);
            written++;
        }

        return Task.FromResult(written);
    }

    /// <summary>Anzahl Eintraege im Pending-State (fuer Tests/Diagnose).</summary>
    public int PendingCount => _pendingWeights.Count;

    /// <summary>
    /// Direkt einen Pending-Eintrag setzen — wird von ReLearnAsync verwendet,
    /// nuetzlich fuer Tests die PersistAsync isoliert pruefen wollen.
    /// </summary>
    internal void SetPending(string category, double[] weights, int validationCount)
    {
        if (weights.Length != WeightDimensions)
            throw new ArgumentException($"Expected {WeightDimensions} weights.", nameof(weights));
        var copy = new double[WeightDimensions];
        Array.Copy(weights, copy, WeightDimensions);
        _pendingWeights[category] = new PendingWeights(copy, validationCount);
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

    /// <summary>
    /// UPSERT einer Kategorie-Zeile. Schreibt sowohl die getypten Per-Signal-
    /// Spalten (Phase 2.2) als auch das JSON (Rueckwaertskompatibilitaet zu
    /// LoadAllWeights/LoadWeights, die WeightsJson lesen).
    /// </summary>
    private void UpsertWeights(string category, double[] weights, int validationCount)
    {
        var cw = new CategoryWeights { Category = category, ValidationCount = validationCount, UpdatedUtc = DateTime.UtcNow };
        cw.FromArray(weights);

        using var cmd = _conn.CreateCommand();
        // INSERT OR REPLACE schreibt alle Spalten konsistent; Pre-2.2-Reader
        // lesen weiterhin WeightsJson, neue Reader koennen die getypten Spalten
        // direkt verwenden.
        cmd.CommandText = """
            INSERT OR REPLACE INTO CategoryWeights (
                Category, WeightsJson, ValidationCount, UpdatedUtc,
                YoloWeight, DinoWeight, SamWeight, QwenWeight,
                LlmWeight, KbWeight, KbAgreementWeight, PlausibilityWeight
            )
            VALUES (
                @cat, @json, @count, @utc,
                @w0, @w1, @w2, @w3,
                @w4, @w5, @w6, @w7
            )
            """;
        cmd.Parameters.AddWithValue("@cat", category);
        cmd.Parameters.AddWithValue("@json", cw.ToJson());
        cmd.Parameters.AddWithValue("@count", validationCount);
        cmd.Parameters.AddWithValue("@utc", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("@w0", weights[0]);
        cmd.Parameters.AddWithValue("@w1", weights[1]);
        cmd.Parameters.AddWithValue("@w2", weights[2]);
        cmd.Parameters.AddWithValue("@w3", weights[3]);
        cmd.Parameters.AddWithValue("@w4", weights[4]);
        cmd.Parameters.AddWithValue("@w5", weights[5]);
        cmd.Parameters.AddWithValue("@w6", weights[6]);
        cmd.Parameters.AddWithValue("@w7", weights[7]);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Liest aktuelle Gewichte einer Kategorie als Array fuer den Noise-Filter.
    /// Bevorzugt die getypten Spalten (Phase 2.2); fallt zurueck auf JSON wenn
    /// die getypten Spalten leer/null sind (z.B. Pre-2.2-Daten).
    /// Liefert null wenn keine Zeile existiert.
    /// </summary>
    private double[]? LoadWeightsArrayForFilter(string category)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT YoloWeight, DinoWeight, SamWeight, QwenWeight,
                   LlmWeight, KbWeight, KbAgreementWeight, PlausibilityWeight,
                   WeightsJson
            FROM CategoryWeights
            WHERE Category = @cat
            """;
        cmd.Parameters.AddWithValue("@cat", category);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        var typed = new double[WeightDimensions];
        for (int i = 0; i < WeightDimensions; i++)
            typed[i] = reader.IsDBNull(i) ? 0.0 : reader.GetDouble(i);

        // Pre-2.2-Daten: getypte Spalten alle 0 -> JSON-Fallback
        if (typed.All(v => v == 0.0))
        {
            var json = reader.IsDBNull(WeightDimensions) ? null : reader.GetString(WeightDimensions);
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    var cw = CategoryWeights.FromJson(json);
                    return cw.ToArray();
                }
                catch { /* fall through, return zeros */ }
            }
        }

        return typed;
    }

    private static double L1Distance(double[] a, double[] b)
    {
        if (a.Length != b.Length) return double.MaxValue;
        double sum = 0;
        for (int i = 0; i < a.Length; i++)
            sum += Math.Abs(a[i] - b[i]);
        return sum;
    }

    private sealed record ValidationSample(bool WasCorrect, EvidenceVector Evidence);

    private sealed record PendingWeights(double[] Weights, int ValidationCount);
}
