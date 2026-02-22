// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Collections.Generic;
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
    /// </summary>
    public async Task<int> RebuildAsync(
        IReadOnlyList<TrainingSample> samples,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        // Alles löschen
        ExecuteNonQuery("DELETE FROM Embeddings");
        ExecuteNonQuery("DELETE FROM Samples");
        ExecuteNonQuery("DELETE FROM Versions");

        _currentVersionId = null;

        var indexed = 0;
        for (var i = 0; i < samples.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var ok = await IndexSampleAsync(samples[i], ct).ConfigureAwait(false);
            if (ok) indexed++;
            progress?.Report(i + 1);
        }

        // Versions-Eintrag abschließen
        FinalizeCurrentVersion(indexed);
        return indexed;
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
