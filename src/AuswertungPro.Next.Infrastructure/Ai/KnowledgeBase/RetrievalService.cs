// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

/// <summary>
/// Sucht die ähnlichsten Samples aus der Wissensdatenbank via Cosine-Similarity.
/// Wird als few-shot Kontext für den Classification-Prompt genutzt.
/// </summary>
public sealed class RetrievalService(
    KnowledgeBaseContext db,
    EmbeddingService embedder,
    IReadOnlySet<string>? evalHaltungKeys = null) : IRetrievalService
{
    private static int _dimensionMismatchWarned;
    private readonly RetrievalQualityPolicy _policy = RetrievalQualityPolicy.Default;

    // ── In-Memory-Cache der Embeddings+Samples (#6: Retrieval entkalten) ──────────────
    // Wird einmal geladen und nur neu geladen, wenn sich die Embeddings-Tabelle aendert.
    // Erkennung ueber eine billige Kennzahl (Zeilenzahl + groesste rowid) statt bei JEDER
    // Query ~21.860 Embeddings (~67 MB) neu zu lesen und zu parsen -> die Retrieval-Latenz
    // wird unabhaengig vom KB-Wachstum (genau das Self-Training-Ziel).
    private readonly object _cacheLock = new();
    private List<(string SampleId, float[] Vector, SampleRecord? Sample)>? _cache;
    private long _cacheRowCount = -1;
    private long _cacheMaxRowId = -1;
    private long _cacheHumanConfirmedCount = -1;
    private string _cacheMaxConfirmedAtUtc = string.Empty;

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

        // Embeddings aus dem In-Memory-Cache (laedt nur beim ersten Mal bzw. nach KB-Aenderung
        // alle Blobs - statt bei JEDER Query ~21.860 Embeddings neu zu lesen/parsen).
        var candidates = GetCandidatesCached();

        // Qualitaetsbewusstes Ranking (Green bevorzugt, Yellow niedriger, Red nur als Fallback).
        var results = RankAndFilter(queryVec, candidates, topK, _policy, out var mismatchCount);

        if (mismatchCount > 0 && Interlocked.CompareExchange(ref _dimensionMismatchWarned, 1, 0) == 0)
        {
            BestEffort.ReportWarning(
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
            // Defense-in-Depth: Auch historische oder ueber einen alten Schreibpfad
            // vorhandene Samples zaehlen nur nach menschlicher Bestaetigung als KB-Beleg.
            if (sample.HumanConfirmed != true)
                continue;
            // Bestehende Alt-Eintraege mit Platzhalter- oder unbrauchbar kurzem Text
            // bleiben fuer Sicherung/Audit erhalten, duerfen aber nicht als Qwen-
            // Few-Shot-Beispiel ausgeliefert werden.
            if (!GoldDescriptionPolicy.IsKnowledgeTextReady(sample.Beschreibung))
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
                BestEffort.ReportWarning(
                    $"[RetrievalService] MODELL-MISMATCH: KB enthält '{StoredEmbedModel}', " +
                    $"aktuell konfiguriert: '{embedder.ModelName}'. KB-Rebuild empfohlen!");
            }

            return !HasModelMismatch;
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning($"[RetrievalService] Modell-Check fehlgeschlagen: {ex.Message}");
            return true;
        }
    }

    // ── Intern ────────────────────────────────────────────────────────────

    /// <summary>
    /// Liefert die Embeddings+Samples aus dem Cache. Laedt sie nur beim ersten Aufruf neu bzw.
    /// wenn sich die Embeddings-Tabelle geaendert hat (neue/geloeschte Zeilen oder KB-Rebuild).
    /// Hinweis: eine reine In-Place-Aenderung der QualityGate-Stufe eines bestehenden Samples
    /// (ohne neue Embedding-Zeile) wird erst beim naechsten Reload sichtbar - fuers Few-Shot-
    /// Retrieval unkritisch (Default-Policy gewichtet Green=Yellow, nur Red-Ausschluss zaehlt).
    /// </summary>
    private List<(string SampleId, float[] Vector, SampleRecord? Sample)> GetCandidatesCached()
    {
        lock (_cacheLock)
        {
            var (rowCount, maxRowId, humanConfirmedCount, maxConfirmedAtUtc) = ReadEmbeddingsStamp();
            if (_cache is not null
                && rowCount >= 0
                && rowCount == _cacheRowCount
                && maxRowId == _cacheMaxRowId
                && humanConfirmedCount == _cacheHumanConfirmedCount
                && string.Equals(maxConfirmedAtUtc, _cacheMaxConfirmedAtUtc, StringComparison.Ordinal))
                return _cache;

            _cache = LoadAllEmbeddingsWithSamples();
            _cacheRowCount = rowCount;
            _cacheMaxRowId = maxRowId;
            _cacheHumanConfirmedCount = humanConfirmedCount;
            _cacheMaxConfirmedAtUtc = maxConfirmedAtUtc;
            return _cache;
        }
    }

    /// <summary>
    /// Billige Kennzahl der Embeddings-Tabelle zur Cache-Invalidierung (Zeilenzahl + groesste rowid).
    /// Bei Fehler (-1,-1) -> Cache gilt als ungueltig und wird neu geladen (nie veraltete Daten).
    /// </summary>
    private (long RowCount, long MaxRowId, long HumanConfirmedCount, string MaxConfirmedAtUtc) ReadEmbeddingsStamp()
    {
        try
        {
            using var cmd = db.Connection.CreateCommand();
            cmd.CommandText = """
                SELECT COUNT(*),
                       COALESCE(MAX(e.rowid), 0),
                       COALESCE(SUM(CASE WHEN s.HumanConfirmed = 1 THEN 1 ELSE 0 END), 0),
                       COALESCE(MAX(s.ConfirmedAtUtc), '')
                FROM Embeddings e
                LEFT JOIN Samples s ON e.SampleId = s.SampleId
                """;
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
                return (reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetString(3));
        }
        catch
        {
            // Cache verwerfen -> erzwingt Neuladen.
        }
        return (-1, -1, -1, string.Empty);
    }

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
                   s.QualityGateLevel,
                   s.HumanConfirmed, s.Corrected, s.ConfirmedByUser, s.ConfirmedAtUtc
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
            // Gold-Metadaten (Audit Fix #3): Indizes 8-11, alle nullable.
            SampleRecord? sample = reader.IsDBNull(2) ? null : new SampleRecord(
                id, reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.GetDouble(5), reader.GetDouble(6),
                reader.IsDBNull(7) ? "" : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetInt64(8) != 0,
                reader.IsDBNull(9) ? null : reader.GetInt64(9) != 0,
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : DateTime.Parse(reader.GetString(11), null, System.Globalization.DateTimeStyles.RoundtripKind));

            // Audit Fix #6a: Defense-in-Depth — Eval-kontaminierte Haltungen NIE als Few-Shot
            // ausliefern, auch wenn sie (historisch / aus einer Alt-DB / ueber einen ungeguardeten
            // Schreibpfad) in der Tabelle stehen. Zweite Verteidigungslinie zusaetzlich zum
            // Schreib-Guard (KnowledgeBaseManager.IsEvalContaminated). Greift nur, wenn ein
            // Eval-Haltungs-Satz konfiguriert ist (sonst kein Verhaltenswechsel).
            if (sample is not null
                && evalHaltungKeys is { Count: > 0 }
                && EvalContaminationGuard.IsEvalHaltung(evalHaltungKeys, sample.CaseId))
            {
                continue;
            }

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

