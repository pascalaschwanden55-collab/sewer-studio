// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Services.CodeCatalog;

namespace AuswertungPro.Next.UI.Ai.KnowledgeBase;

/// <summary>
/// Orchestriert das Indizieren, Deindizieren und Neuaufbauen der KI-Wissensdatenbank.
/// Approved Samples werden mit ihrem Embedding in SQLite gespeichert.
/// </summary>
public sealed class KnowledgeBaseManager(
    KnowledgeBaseContext db,
    EmbeddingService embedder)
{
    // ── Öffentliche API ───────────────────────────────────────────────────

    /// <summary>
    /// Fügt ein einzelnes approved Sample in die Wissensdatenbank ein.
    /// Existiert bereits ein Eintrag, wird er aktualisiert (UPSERT).
    /// Gibt false zurück wenn das Embedding nicht erzeugt werden konnte.
    /// </summary>
    public async Task<bool> IndexSampleAsync(
        TrainingSample sample,
        CancellationToken ct = default)
    {
        if (!IsIndexWorthy(sample))
        {
            Debug.WriteLine($"[KnowledgeBaseManager] Sample {sample.SampleId} uebersprungen: Qualitaet ungenuegend ({sample.Code}, Beschreibung={sample.Beschreibung?.Length ?? 0} Zeichen)");
            return false;
        }

        var vector = await embedder.EmbedAsync(BuildEmbeddingText(sample), ct).ConfigureAwait(false);
        if (vector is null)
            return false;

        // Atomar: Sample + Embedding in einer Transaction (kein Zustand ohne Embedding)
        var versionId = GetOrCreateCurrentVersionId();
        using var tx = db.Connection.BeginTransaction();
        try
        {
            UpsertSample(sample, versionId);
            UpsertEmbedding(sample.SampleId, vector);
            tx.Commit();
            KnowledgeMirrorService.Current?.NotifyChanged();
            return true;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Indiziert mehrere Samples in einer einzigen SQLite-Transaktion.
    /// Embeddings werden sequentiell erzeugt (Ollama single-request).
    /// Gibt die Anzahl erfolgreich indizierter Samples zurueck.
    /// </summary>
    public async Task<List<string>> IndexSamplesAsync(
        IReadOnlyList<TrainingSample> samples,
        CancellationToken ct = default)
    {
        if (samples.Count == 0) return [];

        // Phase 1: Embeddings erzeugen (vor Transaktion — Netzwerk-I/O)
        var ready = new List<(TrainingSample Sample, float[] Vector)>();
        foreach (var sample in samples)
        {
            ct.ThrowIfCancellationRequested();
            if (!IsIndexWorthy(sample)) continue;
            var vec = await embedder.EmbedAsync(BuildEmbeddingText(sample), ct).ConfigureAwait(false);
            if (vec is not null)
                ready.Add((sample, vec));
        }

        if (ready.Count == 0) return [];

        // Phase 2: Eine Transaktion fuer alle UPSERTs
        var versionId = GetOrCreateCurrentVersionId();
        using var tx = db.Connection.BeginTransaction();
        try
        {
            foreach (var (sample, vec) in ready)
            {
                UpsertSample(sample, versionId);
                UpsertEmbedding(sample.SampleId, vec);
            }
            tx.Commit();
            KnowledgeMirrorService.Current?.NotifyChanged();
            return ready.Select(r => r.Sample.SampleId).ToList();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Entfernt ein Sample und sein Embedding aus der Wissensdatenbank.
    /// </summary>
    public void DeindexSample(string sampleId)
    {
        ExecuteNonQuery(
            "DELETE FROM Samples    WHERE SampleId = $id",
            ("$id", sampleId));

        ExecuteNonQuery(
            "DELETE FROM Embeddings WHERE SampleId = $id",
            ("$id", sampleId));

        KnowledgeMirrorService.Current?.NotifyChanged();
    }

    /// <summary>
    /// Baut die Wissensdatenbank komplett neu auf mit Sicherheitspruefungen.
    /// Phase 1: Embeddings parallel erzeugen (GPU-bound).
    /// Phase 2: Nur loeschen + neu aufbauen wenn genuegend Embeddings vorhanden.
    /// Gibt die Anzahl erfolgreich indizierter Samples zurueck.
    /// </summary>
    public async Task<int> RebuildAsync(
        IReadOnlyList<TrainingSample> samples,
        IProgress<int>? progress = null,
        CancellationToken ct = default,
        int concurrency = 4)
    {
        if (samples.Count == 0)
            return 0;

        // Phase 1: Embeddings parallel erzeugen (VOR dem Loeschen)
        var embeddings = new ConcurrentDictionary<int, float[]>();
        var done = 0;
        var errors = 0;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, samples.Count),
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, concurrency), CancellationToken = ct },
            async (i, token) =>
            {
                if (!IsIndexWorthy(samples[i]))
                {
                    Interlocked.Increment(ref errors);
                    var n2 = Interlocked.Increment(ref done);
                    progress?.Report(n2);
                    return;
                }
                var vec = await embedder.EmbedAsync(BuildEmbeddingText(samples[i]), token).ConfigureAwait(false);
                if (vec is not null)
                    embeddings[i] = vec;
                else
                    Interlocked.Increment(ref errors);
                var n = Interlocked.Increment(ref done);
                progress?.Report(n);
            });

        // Sicherheitspruefung: Wenn weniger als 50% der Embeddings erfolgreich,
        // ist vermutlich Ollama ausgefallen → KB NICHT loeschen!
        var successRate = (double)embeddings.Count / samples.Count;
        if (embeddings.Count == 0)
        {
            Debug.WriteLine("[KnowledgeBaseManager] ABBRUCH: 0 Embeddings erzeugt, KB bleibt unveraendert");
            throw new InvalidOperationException(
                $"KB-Rebuild abgebrochen: 0 von {samples.Count} Embeddings erzeugt. " +
                "Ollama vermutlich nicht erreichbar. Bestehende KB bleibt erhalten.");
        }

        if (successRate < 0.5)
        {
            Debug.WriteLine($"[KnowledgeBaseManager] ABBRUCH: Nur {embeddings.Count}/{samples.Count} Embeddings ({successRate:P0})");
            throw new InvalidOperationException(
                $"KB-Rebuild abgebrochen: Nur {embeddings.Count} von {samples.Count} Embeddings erzeugt ({successRate:P0}). " +
                "Bestehende KB bleibt erhalten. Pruefe Ollama-Verbindung.");
        }

        if (errors > 0)
        {
            Debug.WriteLine($"[KnowledgeBaseManager] WARNUNG: {errors} Embedding-Fehler von {samples.Count} Samples");
        }

        // Phase 2: Loeschen + Neuaufbau in einer Transaktion
        lock (_versionLock) { _currentVersionId = null; }
        using var tx = db.Connection.BeginTransaction();
        try
        {
            ExecuteNonQuery("DELETE FROM Embeddings");
            ExecuteNonQuery("DELETE FROM Samples");
            ExecuteNonQuery("DELETE FROM Versions");

            var versionId = GetOrCreateCurrentVersionId();
            var indexed = 0;
            for (var i = 0; i < samples.Count; i++)
            {
                if (!embeddings.TryGetValue(i, out var vec)) continue;
                UpsertSample(samples[i], versionId);
                UpsertEmbedding(samples[i].SampleId, vec);
                indexed++;
            }

            FinalizeCurrentVersion(indexed);
            tx.Commit();

            Debug.WriteLine($"[KnowledgeBaseManager] KB-Rebuild erfolgreich: {indexed}/{samples.Count} Samples indiziert");
            KnowledgeMirrorService.Current?.NotifyChanged();
            return indexed;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Erstellt einen benannten Versions-Snapshot in der Versions-Tabelle.
    /// Gibt die neue VersionId zurück.
    /// </summary>
    public string CreateVersion(string notes = "")
    {
        var versionId = Guid.NewGuid().ToString("N");
        var count     = GetIndexedCount();

        ExecuteNonQuery(
            "INSERT OR REPLACE INTO Versions(VersionId, CreatedAt, SampleCount, Notes) VALUES($v, $t, $c, $n)",
            ("$v", versionId),
            ("$t", DateTime.UtcNow.ToString("O")),
            ("$c", count),
            ("$n", notes));

        return versionId;
    }

    /// <summary>Anzahl indizierter Samples in der Wissensdatenbank.</summary>
    public int GetIndexedCount()
    {
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Samples";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>True wenn das Sample UND sein Embedding indiziert sind.</summary>
    public bool IsIndexed(string sampleId)
    {
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT 1 FROM Samples s
            INNER JOIN Embeddings e ON s.SampleId = e.SampleId
            WHERE s.SampleId = $id LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$id", sampleId);
        return cmd.ExecuteScalar() is not null;
    }

    // ── Intern ───────────────────────────────────────────────────────────

    private string? _currentVersionId;
    private readonly object _versionLock = new();

    private string GetOrCreateCurrentVersionId()
    {
        lock (_versionLock)
        {
            if (_currentVersionId is not null)
                return _currentVersionId;

            _currentVersionId = Guid.NewGuid().ToString("N");
            ExecuteNonQuery(
                "INSERT OR IGNORE INTO Versions(VersionId, CreatedAt, SampleCount, Notes) VALUES($v, $t, 0, '')",
                ("$v", _currentVersionId),
                ("$t", DateTime.UtcNow.ToString("O")));

            return _currentVersionId;
        }
    }

    private void FinalizeCurrentVersion(int count)
    {
        lock (_versionLock)
        {
            if (_currentVersionId is null) return;
            ExecuteNonQuery(
                "UPDATE Versions SET SampleCount = $c WHERE VersionId = $v",
                ("$c", count),
                ("$v", _currentVersionId));
            _currentVersionId = null;
        }
    }

    private void UpsertSample(TrainingSample s, string versionId)
    {
        ExecuteNonQuery("""
            INSERT OR REPLACE INTO Samples
                (SampleId, CaseId, VsaCode, Beschreibung, MeterStart, MeterEnd,
                 IsStreck, FramePath, ExportedUtc, VersionId, SourceType)
            VALUES ($id, $caseId, $code, $desc, $ms, $me, $streck, $frame, $exp, $ver, $source)
            """,
            ("$id",     s.SampleId),
            ("$caseId", s.CaseId),
            ("$code",   s.Code),
            ("$desc",   s.Beschreibung),
            ("$ms",     s.MeterStart),
            ("$me",     s.MeterEnd),
            ("$streck", s.IsStreckenschaden ? 1 : 0),
            ("$frame",  s.FramePath),
            ("$exp",    s.ExportedUtc?.ToString("O") ?? DateTime.UtcNow.ToString("O")),
            ("$ver",    versionId),
            ("$source", s.SourceType ?? ""));
    }

    private void UpsertEmbedding(string sampleId, float[] vector)
    {
        ExecuteNonQuery("""
            INSERT OR REPLACE INTO Embeddings(SampleId, Model, Vector, CreatedAt)
            VALUES ($id, $model, $vec, $at)
            """,
            ("$id",    sampleId),
            ("$model", embedder.ModelName),
            ("$vec",   EmbeddingService.ToBlob(vector)),
            ("$at",    DateTime.UtcNow.ToString("O")));
    }

    private void ExecuteNonQuery(string sql, params (string Name, object Value)[] parameters)
    {
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);
        cmd.ExecuteNonQuery();
    }

    // ── Quality Gate ────────────────────────────────────────────────────

    // ── Embedding-Text ─────────────────────────────────────────────────

    /// <summary>
    /// Baut einen sinnvollen Text fuer das Embedding aus Code + VSA-Label + Beschreibung.
    /// Wenn die Beschreibung nur der Code selbst ist (z.B. "BDB"), wird das VSA-Label
    /// als Kontext angehaengt, damit das Embedding semantisch brauchbar ist.
    /// Beispiel: "BDB" → "BDB — Kameraposition, Beginn der Bestandsaufnahme"
    /// </summary>
    public static string BuildEmbeddingText(TrainingSample sample)
    {
        var desc = sample.Beschreibung?.Trim() ?? "";
        var code = sample.Code?.Trim() ?? "";

        // Punkt-Codes normalisieren fuer VsaCodeTree
        var normalized = code.Replace(".", "", StringComparison.Ordinal);
        var label = VsaCodeTree.LookupLabel(normalized);

        // Beschreibung ist schon ausfuehrlich genug → direkt verwenden
        if (desc.Length > 15 && !desc.Equals(code, StringComparison.OrdinalIgnoreCase))
            return $"{code}: {desc}";

        // Kurze/fehlende Beschreibung → mit VSA-Label anreichern
        if (label is not null)
        {
            return desc.Equals(code, StringComparison.OrdinalIgnoreCase) || desc.Length < 5
                ? $"{code} — {label}"
                : $"{code} — {label}, {desc}";
        }

        // Kein Label (WinCan-interne Codes) → Code + Beschreibung
        return desc.Length > 0 ? $"{code}: {desc}" : code;
    }

    /// <summary>
    /// Prueft ob ein Sample die Mindestqualitaet fuer KB-Indexierung erfuellt.
    /// STRENG: Nur offizielle VSA/EN 13508-2 Leitungscodes (B-Gruppe + AE).
    /// Keine WinCan-internen Codes (BEGINN, BOGEN, FOTO etc.).
    /// Keine Schachtcodes (D-Gruppe).
    /// Punkt-Codes (BCA.A.A) werden automatisch normalisiert (→ BCAAA).
    /// </summary>
    public static bool IsIndexWorthy(TrainingSample sample)
    {
        if (string.IsNullOrWhiteSpace(sample.Beschreibung) || sample.Beschreibung.Trim().Length < 3)
            return false;

        if (string.IsNullOrWhiteSpace(sample.Code))
            return false;

        return IsValidVsaLeitungscode(sample.Code);
    }

    /// <summary>
    /// Prueft ob ein Code ein gueltiger VSA-Leitungscode ist.
    /// Nur B-Codes (BA, BB, BC, BD) und AE-Codes.
    /// Keine D-Codes (Schacht), keine WinCan-internen Codes.
    /// Punkt-Notation wird automatisch normalisiert.
    /// </summary>
    public static bool IsValidVsaLeitungscode(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length < 3)
            return false;

        // Punkt-Notation normalisieren: "BCA.A.A" → "BCAAA"
        var normalized = code.Replace(".", "", StringComparison.Ordinal).ToUpperInvariant();

        // Nur Leitungscodes: B-Gruppe (BA-BD) und AE-Gruppe
        // Keine D-Codes (Schacht: DA, DB, DC, DD)
        var prefix = normalized[..2];
        if (prefix is not ("BA" or "BB" or "BC" or "BD" or "AE"))
            return false;

        // VsaCodeTree muss den Code kennen
        return VsaCodeTree.LookupLabel(normalized) is not null;
    }
}
