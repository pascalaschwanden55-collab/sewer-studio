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
    private readonly RetrievalQualityPolicy _policy = RetrievalQualityPolicy.Default;

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

        // Qualitaetsbewusstes Ranking (Green bevorzugt, Yellow niedriger, Red nur als Fallback).
        var results = RankAndFilter(queryVec, candidates, topK, _policy, out var mismatchCount);

        if (mismatchCount > 0 && Interlocked.CompareExchange(ref _dimensionMismatchWarned, 1, 0) == 0)
        {
            Debug.WriteLine(
                $"[RetrievalService] WARNUNG: {mismatchCount} Embeddings mit falscher Dimension " +
                $"(erwartet {queryVec.Length}, DB enthält andere). KB-Rebuild empfohlen!");
        }

        return results;
    }

    /// <summary>
    /// Reines, qualitaetsbewusstes Ranking (kein DB-/Ollama-Zugriff -> unit-testbar).
    /// Green wird bevorzugt, Yellow zugelassen aber niedriger gewichtet, Red standardmaessig
    /// ausgeschlossen und NUR als kontrollierter Fallback (immer zuletzt, stark abgewertet)
    /// eingesetzt, falls sonst weniger als topK Treffer entstehen. Unbekannte/leere Stufe gilt
    /// konservativ als Yellow. Reihenfolge stabil (Tie-Break ueber SampleId). Der gemeldete Score
    /// ist qualitaetsgewichtet, damit Konsumenten, die nach Score neu sortieren, die Bevorzugung
    /// beibehalten.
    /// </summary>
    public static IReadOnlyList<RetrievalResult> RankAndFilter(
        float[] queryVec,
        IReadOnlyList<(string SampleId, float[] Vector, SampleRecord? Sample)> candidates,
        int topK,
        RetrievalQualityPolicy policy,
        out int dimensionMismatchCount)
    {
        dimensionMismatchCount = 0;
        if (queryVec is null || candidates is null || topK <= 0)
            return [];

        var primary = new List<(double Adj, string Id, SampleRecord Sample)>();
        var red = new List<(double Cos, string Id, SampleRecord Sample)>();

        foreach (var (id, vector, sample) in candidates)
        {
            if (sample is null)
                continue;
            if (vector is null || vector.Length != queryVec.Length)
            {
                dimensionMismatchCount++;
                continue;
            }

            var cos = CosineSimilarity(queryVec, vector);
            switch (ParseQuality(sample.QualityGateLevel, policy.UnknownAs))
            {
                case RetrievalQuality.Red:
                    red.Add((cos, id, sample));
                    break;
                case RetrievalQuality.Green:
                    primary.Add((cos * policy.GreenWeight, id, sample));
                    break;
                default: // Yellow (inkl. unbekannt, falls UnknownAs = Yellow)
                    primary.Add((cos * policy.YellowWeight, id, sample));
                    break;
            }
        }

        primary.Sort((a, b) =>
        {
            var c = b.Adj.CompareTo(a.Adj);
            return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id);
        });

        var results = new List<RetrievalResult>(Math.Min(topK, primary.Count + red.Count));
        foreach (var p in primary)
        {
            if (results.Count >= topK) break;
            results.Add(new RetrievalResult(p.Sample, p.Adj));
        }

        // Kontrollierter Fallback: nur falls sonst zu wenige Treffer; Red immer zuletzt + abgewertet,
        // damit es nie als starkes Few-Shot-Beispiel dient.
        if (results.Count < topK && policy.AllowRedFallback && red.Count > 0)
        {
            red.Sort((a, b) =>
            {
                var c = b.Cos.CompareTo(a.Cos);
                return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id);
            });
            foreach (var r in red)
            {
                if (results.Count >= topK) break;
                results.Add(new RetrievalResult(r.Sample, r.Cos * policy.RedFallbackWeight));
            }
        }

        return results;
    }

    private static RetrievalQuality ParseQuality(string? level, RetrievalQuality unknownAs)
        => level?.Trim().ToLowerInvariant() switch
        {
            "green" => RetrievalQuality.Green,
            "yellow" => RetrievalQuality.Yellow,
            "red" => RetrievalQuality.Red,
            _ => unknownAs,
        };

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
                   s.CaseId, s.VsaCode, s.Beschreibung, s.MeterStart, s.MeterEnd,
                   s.QualityGateLevel
            FROM Embeddings e
            LEFT JOIN Samples s ON e.SampleId = s.SampleId
            """;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            var blob = (byte[])reader.GetValue(1);
            // QualityGateLevel (Index 7) angehaengt am Ende -> bestehende Ordinale 2..6 unveraendert.
            // Bei Alt-DBs ohne Wert (NULL) konservativ als leer behandeln.
            SampleRecord? sample = reader.IsDBNull(2) ? null : new SampleRecord(
                id, reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.GetDouble(5), reader.GetDouble(6),
                reader.IsDBNull(7) ? "" : reader.GetString(7));
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

/// <summary>Qualitaetsstufe eines KB-Samples fuer das Retrieval-Ranking.</summary>
public enum RetrievalQuality { Green, Yellow, Red }

/// <summary>
/// Policy fuer qualitaetsbewusstes Retrieval.
///
/// Default = reiner FILTER, keine Re-Rangierung: Red ausgeschlossen (nur kontrollierter Fallback,
/// stark abgewertet), Green und Yellow GLEICH gewichtet -> innerhalb der akzeptablen Qualitaet
/// entscheidet die Cosine-Aehnlichkeit. Begruendung (deterministisch an der echten KB gemessen,
/// 2026-06-04): eine Green-Bevorzugung senkt die Praezision@8, weil die QualityGate-Farbe die
/// Evidenz-Staerke misst, nicht die Label-Korrektheit (Protokoll-Codes sind auch bei "Red" korrekt),
/// und der Green-Bestand klein und code-schief ist. Wer dennoch Green bevorzugen will, setzt
/// YellowWeight &lt; GreenWeight (kostet aber Praezision). Unbekannte/leere Stufe gilt konservativ
/// als Yellow (nicht als Green vertrauen, nicht wie Red ausschliessen).
/// </summary>
public sealed record RetrievalQualityPolicy(
    double GreenWeight = 1.0,
    double YellowWeight = 1.0,
    double RedFallbackWeight = 0.15,
    bool AllowRedFallback = true,
    RetrievalQuality UnknownAs = RetrievalQuality.Yellow)
{
    public static RetrievalQualityPolicy Default { get; } = new();
}

