// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

/// <summary>
/// Sucht die ähnlichsten Samples aus der Wissensdatenbank via Cosine-Similarity.
/// Wird als few-shot Kontext für den Classification-Prompt genutzt.
/// </summary>
public sealed class RetrievalService(
    KnowledgeBaseContext db,
    EmbeddingService embedder) : IRetrievalService
{
    private static int _dimensionMismatchWarned;

    /// <summary>Aktuelles Embedding-Modell in der DB (null = leer / unbekannt).</summary>
    public string? StoredEmbedModel { get; private set; }

    /// <summary>True wenn die DB-Embeddings von einem anderen Modell stammen als dem aktuellen.</summary>
    public bool HasModelMismatch { get; private set; }

    /// <summary>
    /// Gibt die Top-K ähnlichsten Samples für einen Query-Text zurück.
    /// </summary>
    public async Task<IReadOnlyList<RetrievalResult>> RetrieveAsync(
        string queryText,
        int topK = 5,
        CancellationToken ct = default)
    {
        var queryVec = await embedder.EmbedAsync(queryText, ct).ConfigureAwait(false);
        if (queryVec is null)
            return [];

        // Eine einzige JOIN-Query statt N+1 (vorher: LoadAllEmbeddings + N× LoadSample)
        var candidates = LoadAllEmbeddingsWithSamples();
        var scored = new List<(string SampleId, double Score, SampleRecord? Sample)>(candidates.Count);
        var mismatchCount = 0;

        foreach (var (sampleId, vector, sample) in candidates)
        {
            if (vector.Length != queryVec.Length)
            {
                mismatchCount++;
                continue;
            }
            var score = CosineSimilarity(queryVec, vector);
            scored.Add((sampleId, score, sample));
        }

        if (mismatchCount > 0 && Interlocked.CompareExchange(ref _dimensionMismatchWarned, 1, 0) == 0)
        {
            Debug.WriteLine(
                $"[RetrievalService] WARNUNG: {mismatchCount} Embeddings mit falscher Dimension " +
                $"(erwartet {queryVec.Length}, DB enthält andere). KB-Rebuild empfohlen!");
        }

        scored.Sort((a, b) => b.Score.CompareTo(a.Score));

        var results = new List<RetrievalResult>(Math.Min(topK, scored.Count));
        for (var i = 0; i < Math.Min(topK, scored.Count); i++)
        {
            var (_, score, sample) = scored[i];
            if (sample is not null)
                results.Add(new RetrievalResult(sample, score));
        }

        return results;
    }

    /// <summary>
    /// Prüft ob die gespeicherten Embeddings zum aktuellen Modell passen.
    /// Setzt StoredEmbedModel und HasModelMismatch.
    /// </summary>
    public bool CheckModelConsistency()
    {
        try
        {
            using var cmd = db.Connection.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT Model FROM Embeddings WHERE Model IS NOT NULL AND TRIM(Model) <> ''";
            using var reader = cmd.ExecuteReader();
            var models = new List<string>();
            while (reader.Read())
            {
                var m = reader.IsDBNull(0) ? null : reader.GetString(0);
                if (!string.IsNullOrWhiteSpace(m))
                    models.Add(m!);
            }

            var distinctModels = models
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            StoredEmbedModel = distinctModels.Count > 0
                ? string.Join(", ", distinctModels)
                : null;
            HasModelMismatch = distinctModels.Any(m =>
                !string.Equals(m, embedder.ModelName, StringComparison.OrdinalIgnoreCase));

            if (HasModelMismatch)
            {
                Debug.WriteLine(
                    $"[RetrievalService] MODELL-MISMATCH: KB enthält '{StoredEmbedModel}', " +
                    $"aktuell konfiguriert: '{embedder.ModelName}'. KB-Rebuild empfohlen!");
            }

            return !HasModelMismatch;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RetrievalService] Modell-Check fehlgeschlagen: {ex.Message}");
            return true;
        }
    }

    // ── Intern ────────────────────────────────────────────────────────────

    /// <summary>
    /// Laedt alle Embeddings MIT Sample-Daten in einer einzelnen JOIN-Query.
    /// Vermeidet N+1-Problem (vorher: 1 Query fuer Embeddings + N Queries fuer Samples).
    /// </summary>
    private List<(string SampleId, float[] Vector, SampleRecord? Sample)> LoadAllEmbeddingsWithSamples()
    {
        var list = new List<(string, float[], SampleRecord?)>();
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT e.SampleId, e.Vector,
                   s.CaseId, s.VsaCode, s.Beschreibung, s.MeterStart, s.MeterEnd
            FROM Embeddings e
            LEFT JOIN Samples s ON e.SampleId = s.SampleId
            """;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            var blob = (byte[])reader.GetValue(1);
            SampleRecord? sample = reader.IsDBNull(2) ? null : new SampleRecord(
                id, reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.GetDouble(5), reader.GetDouble(6));
            list.Add((id, EmbeddingService.FromBlob(blob), sample));
        }
        return list;
    }

    // Legacy-Kompatibilitaet (wird nicht mehr fuer Retrieval genutzt)
    private List<(string SampleId, float[] Vector)> LoadAllEmbeddings()
    {
        return LoadAllEmbeddingsWithSamples()
            .Select(x => (x.SampleId, x.Vector))
            .ToList();
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot   += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        var denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom == 0 ? 0 : dot / denom;
    }
}

