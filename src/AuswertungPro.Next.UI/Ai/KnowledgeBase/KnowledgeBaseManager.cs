// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.Training;

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
        var vector = await embedder.EmbedAsync(sample.Beschreibung, ct).ConfigureAwait(false);
        if (vector is null)
            return false;

        var versionId = GetOrCreateCurrentVersionId();
        UpsertSample(sample, versionId);
        UpsertEmbedding(sample.SampleId, vector);
        return true;
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
    }

    /// <summary>
    /// Löscht die gesamte Wissensdatenbank und indiziert alle übergebenen Samples neu.
    /// Gibt die Anzahl erfolgreich indizierter Samples zurück.
    /// Phase 1: Embeddings parallel erzeugen (GPU-bound).
    /// Phase 2: SQLite-Writes sequentiell (SQLite ist nicht thread-safe für Writes).
    /// </summary>
    public async Task<int> RebuildAsync(
        IReadOnlyList<TrainingSample> samples,
        IProgress<int>? progress = null,
        CancellationToken ct = default,
        int concurrency = 1)
    {
        // Phase 1: Embeddings parallel erzeugen (VOR dem Löschen, damit bei Fehler Daten erhalten bleiben)
        var embeddings = new ConcurrentDictionary<int, float[]>();
        var done = 0;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, samples.Count),
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, concurrency), CancellationToken = ct },
            async (i, token) =>
            {
                var vec = await embedder.EmbedAsync(samples[i].Beschreibung, token).ConfigureAwait(false);
                if (vec is not null)
                    embeddings[i] = vec;
                var n = Interlocked.Increment(ref done);
                progress?.Report(n);
            });

        // Phase 2: Löschen + Neuaufbau in einer Transaktion
        _currentVersionId = null;
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

    /// <summary>True wenn das Sample bereits indiziert ist.</summary>
    public bool IsIndexed(string sampleId)
    {
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM Samples WHERE SampleId = $id LIMIT 1";
        cmd.Parameters.AddWithValue("$id", sampleId);
        return cmd.ExecuteScalar() is not null;
    }

    // ── Intern ───────────────────────────────────────────────────────────

    private string? _currentVersionId;

    private string GetOrCreateCurrentVersionId()
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

    private void FinalizeCurrentVersion(int count)
    {
        if (_currentVersionId is null) return;
        ExecuteNonQuery(
            "UPDATE Versions SET SampleCount = $c WHERE VersionId = $v",
            ("$c", count),
            ("$v", _currentVersionId));
        _currentVersionId = null;
    }

    private void UpsertSample(TrainingSample s, string versionId)
    {
        ExecuteNonQuery("""
            INSERT OR REPLACE INTO Samples
                (SampleId, CaseId, VsaCode, Beschreibung, MeterStart, MeterEnd,
                 IsStreck, FramePath, ExportedUtc, VersionId)
            VALUES ($id, $caseId, $code, $desc, $ms, $me, $streck, $frame, $exp, $ver)
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
            ("$ver",    versionId));
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
}
