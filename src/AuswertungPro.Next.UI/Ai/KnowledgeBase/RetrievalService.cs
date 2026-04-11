// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;

namespace AuswertungPro.Next.UI.Ai.KnowledgeBase;

/// <summary>
/// Sucht die ähnlichsten Samples aus der Wissensdatenbank via Cosine-Similarity.
/// Wird als few-shot Kontext für den Classification-Prompt genutzt.
/// </summary>
public sealed class RetrievalService(
    KnowledgeBaseContext db,
    EmbeddingService embedder,
    AppSettings? settings = null) : IRetrievalService
{
    // Konfigurierbare Schwellen (Defaults: 0.35 einfach, 0.45 hybrid)
    private const double DefaultMinSimilarity = 0.35;
    private const double DefaultHybridSimilarity = 0.45;
    private static int _dimensionMismatchWarned;

    /// <summary>Aktuelles Embedding-Modell in der DB (null = leer / unbekannt).</summary>
    public string? StoredEmbedModel { get; private set; }

    /// <summary>True wenn die DB-Embeddings von einem anderen Modell stammen als dem aktuellen.</summary>
    public bool HasModelMismatch { get; private set; }

    /// <summary>
    /// Gibt die Top-K ähnlichsten Samples für einen Query-Text zurück.
    /// Optionales Hybrid-Scoring: Embedding-Aehnlichkeit + Code-Familie + Material-Match.
    /// </summary>
    public async Task<IReadOnlyList<RetrievalResult>> RetrieveAsync(
        string queryText,
        int topK = 5,
        string? queryVsaCode = null,
        string? queryMaterial = null,
        CancellationToken ct = default)
    {
        var queryVec = await embedder.EmbedAsync(queryText, ct).ConfigureAwait(false);
        if (queryVec is null)
            return [];

        var candidates = LoadAllEmbeddingsWithSamples();
        var scored = new List<(string SampleId, double Score, SampleRecord? Sample)>(candidates.Count);
        var mismatchCount = 0;
        var useHybrid = !string.IsNullOrEmpty(queryVsaCode);

        foreach (var (sampleId, vector, sample) in candidates)
        {
            if (vector.Length != queryVec.Length)
            {
                mismatchCount++;
                continue;
            }

            var embeddingScore = CosineSimilarity(queryVec, vector);

            double finalScore;
            if (useHybrid && sample is not null)
            {
                // Hybrid-Scoring: Embedding (60%) + Code-Familie (30%) + Material (10%)
                var codeBoost = CodeFamilyScore(queryVsaCode!, sample.VsaCode);
                var materialBoost = MaterialScore(queryMaterial, sample);
                finalScore = 0.6 * embeddingScore + 0.3 * codeBoost + 0.1 * materialBoost;
            }
            else
            {
                finalScore = embeddingScore;
            }

            scored.Add((sampleId, finalScore, sample));
        }

        if (mismatchCount > 0 && Interlocked.CompareExchange(ref _dimensionMismatchWarned, 1, 0) == 0)
        {
            Debug.WriteLine(
                $"[RetrievalService] WARNUNG: {mismatchCount} Embeddings mit falscher Dimension " +
                $"(erwartet {queryVec.Length}, DB enthält andere). KB-Rebuild empfohlen!");
        }

        scored.Sort((a, b) => b.Score.CompareTo(a.Score));

        // Konfigurierbare Schwellen aus AppSettings (Fallback auf Defaults)
        var minSimilarity = useHybrid
            ? settings?.KbRetrievalHybridSimilarity ?? DefaultHybridSimilarity
            : settings?.KbRetrievalMinSimilarity ?? DefaultMinSimilarity;

        var results = new List<RetrievalResult>(Math.Min(topK, scored.Count));
        for (var i = 0; i < Math.Min(topK, scored.Count); i++)
        {
            var (_, score, sample) = scored[i];
            if (sample is not null && score >= minSimilarity)
                results.Add(new RetrievalResult(sample, score));
        }

        return results;
    }

    /// <summary>
    /// Bewertet Code-Familien-Aehnlichkeit:
    /// Gleicher Code = 1.0, gleiche Gruppe (z.B. BA*) = 0.7, sonst 0.0.
    /// </summary>
    private static double CodeFamilyScore(string queryCode, string sampleCode)
    {
        if (string.IsNullOrEmpty(queryCode) || string.IsNullOrEmpty(sampleCode))
            return 0.0;

        if (string.Equals(queryCode, sampleCode, StringComparison.OrdinalIgnoreCase))
            return 1.0;

        // Gleiche Hauptgruppe (erste 2 Zeichen: BA, BB, BC, BD)
        if (queryCode.Length >= 2 && sampleCode.Length >= 2
            && string.Equals(queryCode[..2], sampleCode[..2], StringComparison.OrdinalIgnoreCase))
            return 0.7;

        return 0.0;
    }

    /// <summary>Rohrmaterial-Match: gleiches Material = 1.0, sonst 0.0.</summary>
    private static double MaterialScore(string? queryMaterial, SampleRecord sample)
    {
        // SampleRecord hat kein Rohrmaterial-Feld im JOIN — nur Beschreibung pruefen
        if (string.IsNullOrEmpty(queryMaterial) || string.IsNullOrEmpty(sample.Beschreibung))
            return 0.0;

        return sample.Beschreibung.Contains(queryMaterial, StringComparison.OrdinalIgnoreCase)
            ? 1.0 : 0.0;
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

