// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Ai.KnowledgeBase;

/// <summary>
/// Sucht die ähnlichsten Samples aus der Wissensdatenbank via Cosine-Similarity.
/// Wird als few-shot Kontext für den Classification-Prompt genutzt.
/// </summary>
public sealed class RetrievalService(
    KnowledgeBaseContext db,
    EmbeddingService embedder)
{
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

        var candidates = LoadAllEmbeddings();
        var scored = new List<(string SampleId, double Score)>(candidates.Count);

        foreach (var (sampleId, vector) in candidates)
        {
            var score = CosineSimilarity(queryVec, vector);
            scored.Add((sampleId, score));
        }

        scored.Sort((a, b) => b.Score.CompareTo(a.Score));

        var results = new List<RetrievalResult>(Math.Min(topK, scored.Count));
        for (var i = 0; i < Math.Min(topK, scored.Count); i++)
        {
            var (sampleId, score) = scored[i];
            var sample = LoadSample(sampleId);
            if (sample is not null)
                results.Add(new RetrievalResult(sample, score));
        }

        return results;
    }

    // ── Intern ────────────────────────────────────────────────────────────

    private List<(string SampleId, float[] Vector)> LoadAllEmbeddings()
    {
        var list = new List<(string, float[])>();
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = "SELECT SampleId, Vector FROM Embeddings";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id   = reader.GetString(0);
            var blob = (byte[])reader.GetValue(1);
            list.Add((id, EmbeddingService.FromBlob(blob)));
        }
        return list;
    }

    private SampleRecord? LoadSample(string sampleId)
    {
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT SampleId, CaseId, VsaCode, Beschreibung, MeterStart, MeterEnd
            FROM Samples WHERE SampleId = $id
            """;
        cmd.Parameters.AddWithValue("$id", sampleId);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new SampleRecord(
            r.GetString(0), r.GetString(1), r.GetString(2),
            r.GetString(3), r.GetDouble(4), r.GetDouble(5));
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

/// <param name="SampleId">Eindeutige ID des Samples.</param>
/// <param name="CaseId">Herkunft (TrainingCase).</param>
/// <param name="VsaCode">Zugehöriger VSA-Code.</param>
/// <param name="Beschreibung">Protokolltext.</param>
/// <param name="MeterStart">Meterposition Beginn.</param>
/// <param name="MeterEnd">Meterposition Ende.</param>
public sealed record SampleRecord(
    string SampleId,
    string CaseId,
    string VsaCode,
    string Beschreibung,
    double MeterStart,
    double MeterEnd);

/// <summary>Ein Retrieval-Ergebnis mit Ähnlichkeitswert.</summary>
public sealed record RetrievalResult(SampleRecord Sample, double Score);
