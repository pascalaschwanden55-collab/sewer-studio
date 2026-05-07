// AuswertungPro – KI Videoanalyse Modul
using System;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.CodeCatalog;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

/// <summary>
/// Orchestriert das Indizieren, Deindizieren und Neuaufbauen der KI-Wissensdatenbank.
/// Approved Samples werden mit ihrem Embedding in SQLite gespeichert.
/// </summary>
public sealed class KnowledgeBaseManager(
    KnowledgeBaseContext db,
    EmbeddingService embedder,
    KnowledgeBaseWriter? writer = null)
{
    // Schuetzt vor gleichzeitigem Rebuild + Index (Datenverlust vermeiden)
    private static readonly SemaphoreSlim RebuildGuard = new(1, 1);

    // Phase 2.2: Zentraler Writer serialisiert alle SQLite-Schreibvorgaenge.
    // Wenn nicht injiziert, lokal erzeugt (default-Verhalten — abwaerts-kompatibel).
    private readonly KnowledgeBaseWriter _writer = writer ?? new KnowledgeBaseWriter(db);

    // V4.2 Fix: Disk-Full-Guard — warnt wenn weniger als 1 GB frei auf KB-Laufwerk.
    private const long MinFreeBytes = 1024L * 1024L * 1024L; // 1 GB
    private static DateTime _lastDiskWarning = DateTime.MinValue;

    private static bool CheckDiskSpace(string dbPath)
    {
        try
        {
            var root = System.IO.Path.GetPathRoot(System.IO.Path.GetFullPath(dbPath));
            if (string.IsNullOrEmpty(root)) return true;
            var drive = new System.IO.DriveInfo(root);
            if (!drive.IsReady) return true;
            if (drive.AvailableFreeSpace < MinFreeBytes)
            {
                // Max 1 Log-Zeile pro Minute (nicht fluten).
                if ((DateTime.UtcNow - _lastDiskWarning).TotalMinutes > 1)
                {
                    _lastDiskWarning = DateTime.UtcNow;
                    System.Diagnostics.Debug.WriteLine(
                        $"[KB] WARNUNG: Nur {drive.AvailableFreeSpace / 1024 / 1024} MB frei auf {root} — KB-Writes werden ausgesetzt");
                }
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            // Nicht stumm schlucken: bei DriveInfo-Fehlern (Berechtigungen,
            // ungueltiger Pfad, abgemeldetes Laufwerk) loggen — sonst denkt
            // der Aufrufer faelschlich, der Speicherplatz sei in Ordnung.
            // 'true' bleibt das Default damit ein Log-Failure nicht alle
            // Writes blockiert.
            if ((DateTime.UtcNow - _lastDiskWarning).TotalMinutes > 1)
            {
                _lastDiskWarning = DateTime.UtcNow;
                System.Diagnostics.Debug.WriteLine(
                    $"[KB] CheckDiskSpace fehlgeschlagen ({dbPath}): {ex.GetType().Name}: {ex.Message}");
            }
            return true;
        }
    }

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
        // V4.2 Fix: Disk-Full-Guard — verhindert KB-Corruption bei voller Platte.
        // KB-Root wird aus KnowledgeRoot gezogen (C:\KI_BRAIN), Fallback: aktuelles Laufwerk.
        var checkPath = Environment.GetEnvironmentVariable("SEWERSTUDIO_KB_ROOT")
                     ?? @"C:\KI_BRAIN";
        if (!CheckDiskSpace(checkPath))
            return false;

        // Warten falls gerade ein Rebuild laeuft (verhindert Datenverlust)
        await RebuildGuard.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await IndexSampleCoreAsync(sample, ct).ConfigureAwait(false);
        }
        finally
        {
            RebuildGuard.Release();
        }
    }

    private async Task<bool> IndexSampleCoreAsync(
        TrainingSample sample,
        CancellationToken ct)
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
        // Phase 2.2: Writer serialisiert + handhabt Commit/Rollback.
        _writer.ExecuteInTransaction((_, _) =>
        {
            UpsertSample(sample, versionId);
            UpsertEmbedding(sample.SampleId, vector);
        });
        AuswertungPro.Next.Application.Ai.KnowledgeMirrorNotifier.NotifyChanged();
        return true;
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

        // Phase 2: Eine Transaktion fuer alle UPSERTs (Phase 2.2: via Writer-Lock)
        var versionId = GetOrCreateCurrentVersionId();
        try
        {
            _writer.ExecuteInTransaction((_, _) =>
            {
                foreach (var (sample, vec) in ready)
                {
                    UpsertSample(sample, versionId);
                    UpsertEmbedding(sample.SampleId, vec);
                }
            });
            AuswertungPro.Next.Application.Ai.KnowledgeMirrorNotifier.NotifyChanged();
            return ready.Select(r => r.Sample.SampleId).ToList();
        }
        catch
        {
            // Phase 2.2: Rollback bereits durch ExecuteInTransaction.
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

        AuswertungPro.Next.Application.Ai.KnowledgeMirrorNotifier.NotifyChanged();
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

        // Exklusiver Zugriff: blockiert IndexSampleAsync waehrend Rebuild
        await RebuildGuard.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await RebuildCoreAsync(samples, progress, ct, concurrency).ConfigureAwait(false);
        }
        finally
        {
            RebuildGuard.Release();
        }
    }

    private async Task<int> RebuildCoreAsync(
        IReadOnlyList<TrainingSample> samples,
        IProgress<int>? progress,
        CancellationToken ct,
        int concurrency)
    {
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

        // Phase 2: Loeschen + Neuaufbau in einer Transaktion (Phase 2.2: via Writer-Lock)
        lock (_versionLock) { _currentVersionId = null; }
        var indexed = 0;
        try
        {
            _writer.ExecuteInTransaction((_, _) =>
            {
                ExecuteNonQuery("DELETE FROM Embeddings");
                ExecuteNonQuery("DELETE FROM Samples");
                ExecuteNonQuery("DELETE FROM Versions");

                var versionId = GetOrCreateCurrentVersionId();
                for (var i = 0; i < samples.Count; i++)
                {
                    if (!embeddings.TryGetValue(i, out var vec)) continue;
                    UpsertSample(samples[i], versionId);
                    UpsertEmbedding(samples[i].SampleId, vec);
                    indexed++;
                }

                FinalizeCurrentVersion(indexed);
            });

            Debug.WriteLine($"[KnowledgeBaseManager] KB-Rebuild erfolgreich: {indexed}/{samples.Count} Samples indiziert");
            AuswertungPro.Next.Application.Ai.KnowledgeMirrorNotifier.NotifyChanged();
            return indexed;
        }
        catch
        {
            // Phase 2.2: Rollback bereits durch ExecuteInTransaction.
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

    /// <summary>
    /// Audit 2026-05-06 Top-10: Versions-Pruning. Behaelt die letzten
    /// <paramref name="keepLastN"/> Versionen + alle Versionen juenger als
    /// <paramref name="keepDaysMin"/> Tage. Gibt Anzahl geloeschter Versionen
    /// zurueck.
    ///
    /// Aktuelle Version (in <c>_currentVersionId</c>) wird nie geloescht.
    /// </summary>
    public int PruneOldVersions(int keepLastN = 20, int keepDaysMin = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(-keepDaysMin).ToString("O");

        // Versionen kandidatentauglich: aelter als cutoff und NICHT in den
        // top-N juengsten. Aktuelle Version (currentVersionId) auch ausnehmen.
        var current = _currentVersionId;
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            DELETE FROM Versions
            WHERE CreatedAt < $cutoff
              AND VersionId NOT IN (
                  SELECT VersionId FROM Versions ORDER BY CreatedAt DESC LIMIT $keepN
              )
              AND ($current IS NULL OR VersionId != $current)
            """;
        cmd.Parameters.AddWithValue("$cutoff", cutoff);
        cmd.Parameters.AddWithValue("$keepN", keepLastN);
        cmd.Parameters.AddWithValue("$current", (object?)current ?? DBNull.Value);
        return cmd.ExecuteNonQuery();
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
        // Phase 4.4: aktuelle RunId mitschreiben falls Run aktiv (sonst NULL).
        var runId = GetActiveRunId();
        ExecuteNonQuery("""
            INSERT OR REPLACE INTO Samples
                (SampleId, CaseId, VsaCode, Beschreibung, MeterStart, MeterEnd,
                 IsStreck, FramePath, ExportedUtc, VersionId, SourceType,
                 Rohrmaterial, NennweiteMm, IsKorrigiert, QualityGateLevel, RunId)
            VALUES ($id, $caseId, $code, $desc, $ms, $me, $streck, $frame, $exp, $ver, $source,
                    $rm, $dn, $korr, $qg, $run)
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
            ("$source", s.SourceType ?? ""),
            ("$rm",     s.Rohrmaterial),
            ("$dn",     s.NennweiteMm),
            ("$korr",   s.IsKorrigiert ? 1 : 0),
            ("$qg",     s.QualityGateLevel),
            ("$run",    (object?)runId ?? DBNull.Value));
    }

    // ── Phase 4.4: TrainingRuns / Provenance ────────────────────────────

    private string? _activeRunId;
    private readonly object _runLock = new();

    /// <summary>
    /// Phase 4.4: Aktuelle Run-Id (falls ein Run aktiv ist) — wird automatisch
    /// von UpsertSample mitgeschrieben. Wenn null, bekommen Samples RunId=NULL.
    /// </summary>
    public string? GetActiveRunId()
    {
        lock (_runLock) { return _activeRunId; }
    }

    /// <summary>
    /// Phase 4.4: Startet einen neuen Trainings-/Indexierungs-Run mit
    /// Provenance-Metadaten. Gibt die generierte RunId zurueck. Aktiver Run
    /// wird automatisch von UpsertSample fuer alle nachfolgenden Inserts
    /// uebernommen, bis EndRun aufgerufen wird.
    /// </summary>
    /// <param name="modelName">Embed-/Vision-Modell (z.B. "nomic-embed-text").</param>
    /// <param name="modelVersion">Versions-Tag (z.B. "v1.5", "F16"). Leer erlaubt.</param>
    /// <param name="promptVersion">Prompt-Version aus PipelineVersions. Leer erlaubt.</param>
    /// <param name="pipelineVersion">Pipeline-Version. Leer erlaubt.</param>
    /// <param name="notes">Freitext zur Beschreibung des Runs.</param>
    public string BeginRun(
        string modelName,
        string modelVersion = "",
        string promptVersion = "",
        string pipelineVersion = "",
        string notes = "")
    {
        var runId = Guid.NewGuid().ToString("N");
        var startedUtc = DateTime.UtcNow.ToString("O");

        _writer.Execute(_ =>
        {
            ExecuteNonQuery("""
                INSERT INTO TrainingRuns
                    (RunId, StartedUtc, EndedUtc, ModelName, ModelVersion,
                     PromptVersion, PipelineVersion, Status, SampleCount, Notes)
                VALUES ($id, $start, NULL, $mn, $mv, $pv, $piv, 'in_progress', 0, $notes)
                """,
                ("$id",    (object?)runId),
                ("$start", (object?)startedUtc),
                ("$mn",    (object?)modelName),
                ("$mv",    (object?)modelVersion),
                ("$pv",    (object?)promptVersion),
                ("$piv",   (object?)pipelineVersion),
                ("$notes", (object?)notes));
        });

        lock (_runLock) { _activeRunId = runId; }
        return runId;
    }

    /// <summary>
    /// Phase 4.4: Beendet einen Run. Setzt EndedUtc, Status und Notes.
    /// SampleCount wird nicht gesetzt — kann via SQL-COUNT-Query nachvollzogen
    /// werden (Samples WHERE RunId=...). EndRun ist idempotent gegen RunId-Wiederholung.
    /// </summary>
    /// <param name="runId">RunId aus BeginRun.</param>
    /// <param name="status">"completed", "failed", "cancelled". Default: completed.</param>
    /// <param name="finalNotes">Wenn nicht null: ueberschreibt Notes-Feld.</param>
    public void EndRun(string runId, string status = "completed", string? finalNotes = null)
    {
        if (string.IsNullOrWhiteSpace(runId)) return;
        var endedUtc = DateTime.UtcNow.ToString("O");

        _writer.Execute(_ =>
        {
            if (finalNotes is null)
            {
                ExecuteNonQuery("""
                    UPDATE TrainingRuns
                       SET EndedUtc = $end, Status = $st
                     WHERE RunId = $id
                    """,
                    ("$id",  (object?)runId),
                    ("$end", (object?)endedUtc),
                    ("$st",  (object?)status));
            }
            else
            {
                ExecuteNonQuery("""
                    UPDATE TrainingRuns
                       SET EndedUtc = $end, Status = $st, Notes = $notes
                     WHERE RunId = $id
                    """,
                    ("$id",    (object?)runId),
                    ("$end",   (object?)endedUtc),
                    ("$st",    (object?)status),
                    ("$notes", (object?)finalNotes));
            }
        });

        lock (_runLock)
        {
            if (string.Equals(_activeRunId, runId, StringComparison.OrdinalIgnoreCase))
                _activeRunId = null;
        }
    }

    private void UpsertEmbedding(string sampleId, float[] vector)
    {
        // Phase 2.1: ModelVersion-Spalte mitschreiben.
        // Bis EmbeddingService einen echten Modell-Tag liefert (z.B. via
        // Ollama /api/show), bleibt ModelVersion leer. Default '' im Schema
        // erlaubt das, ohne FK/NOT NULL zu brechen.
        ExecuteNonQuery("""
            INSERT OR REPLACE INTO Embeddings(SampleId, Model, ModelVersion, Vector, CreatedAt)
            VALUES ($id, $model, $modelVersion, $vec, $at)
            """,
            ("$id",           (object?)sampleId),
            ("$model",        (object?)embedder.ModelName),
            ("$modelVersion", (object?)embedder.ModelVersion),
            ("$vec",          (object?)EmbeddingService.ToBlob(vector)),
            ("$at",           (object?)DateTime.UtcNow.ToString("O")));
    }

    private void ExecuteNonQuery(string sql, params (string Name, object? Value)[] parameters)
    {
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    // ── Quality Gate ────────────────────────────────────────────────────

    /// <summary>
    /// Bewertet alle bestehenden Samples ohne QualityGateLevel nachtraeglich.
    /// Liest jedes Sample, evaluiert es mit SampleQualityGateService und
    /// schreibt das Ergebnis (Green/Yellow/Red) per UPDATE zurueck.
    /// Kann ohne EmbeddingService aufgerufen werden (statisch, eigener DB-Zugriff).
    /// </summary>
    public static int BackfillQualityGateLevels()
    {
        var gate = new AuswertungPro.Next.Application.Ai.Training.SampleQualityGateService();
        var samples = new List<(string Id, string Level)>();

        using var ctx = new KnowledgeBaseContext();

        // Alle Samples ohne QualityGateLevel laden
        using (var cmd = ctx.Connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT SampleId, CaseId, VsaCode, Beschreibung, MeterStart, MeterEnd,
                       IsStreck, FramePath, SourceType, Rohrmaterial, NennweiteMm,
                       IsKorrigiert
                FROM Samples
                WHERE QualityGateLevel IS NULL
                """;

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var sample = new AuswertungPro.Next.Domain.Ai.Training.TrainingSample
                {
                    SampleId = reader.GetString(0),
                    CaseId = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Code = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Beschreibung = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    MeterStart = reader.IsDBNull(4) ? 0 : reader.GetDouble(4),
                    MeterEnd = reader.IsDBNull(5) ? 0 : reader.GetDouble(5),
                    IsStreckenschaden = !reader.IsDBNull(6) && reader.GetInt64(6) == 1,
                    FramePath = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    SourceType = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    Rohrmaterial = reader.IsDBNull(9) ? null : reader.GetString(9),
                    NennweiteMm = reader.IsDBNull(10) ? null : (int?)reader.GetInt64(10),
                    IsKorrigiert = !reader.IsDBNull(11) && reader.GetInt64(11) == 1
                };

                var result = gate.Evaluate(sample);
                samples.Add((sample.SampleId, result.Grade.ToString()));
            }
        }

        if (samples.Count == 0) return 0;

        // Batch-Update in einer Transaktion
        using var tx = ctx.Connection.BeginTransaction();
        using var updateCmd = ctx.Connection.CreateCommand();
        updateCmd.CommandText = "UPDATE Samples SET QualityGateLevel = $qg WHERE SampleId = $id";
        var pQg = updateCmd.Parameters.Add("$qg", Microsoft.Data.Sqlite.SqliteType.Text);
        var pId = updateCmd.Parameters.Add("$id", Microsoft.Data.Sqlite.SqliteType.Text);

        foreach (var (id, level) in samples)
        {
            pId.Value = id;
            pQg.Value = level;
            updateCmd.ExecuteNonQuery();
        }

        tx.Commit();
        Debug.WriteLine($"[KnowledgeBaseManager] QualityGate-Backfill: {samples.Count} Samples bewertet");
        return samples.Count;
    }

    // ── Embedding-Text ─────────────────────────────────────────────────

    /// <summary>
    /// Baut einen sinnvollen Text fuer das Embedding aus Code + VSA-Label + Beschreibung.
    /// Wenn die Beschreibung nur der Code selbst ist (z.B. "BDB"), wird das VSA-Label
    /// als Kontext angehaengt, damit das Embedding semantisch brauchbar ist.
    /// Beispiel: "BDB" → "BDB — Kameraposition, Beginn der Bestandsaufnahme"
    /// </summary>
    public static string BuildEmbeddingText(TrainingSample sample)
    {
        var baseText = BuildBaseEmbeddingText(sample);
        return AppendEmbeddingContext(baseText, sample.Rohrmaterial, sample.NennweiteMm);
    }

    private static string BuildBaseEmbeddingText(TrainingSample sample)
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
    /// Baut Embedding-Text mit optionalem Rohrmaterial/DN-Kontext.
    /// Fuer Video-Selbsttraining: KB-Anreicherung mit Kontextfeldern.
    /// </summary>
    public static string BuildEmbeddingText(
        string code, string beschreibung, string? rohrmaterial, int? nennweiteMm)
    {
        var sample = new AuswertungPro.Next.Domain.Ai.Training.TrainingSample { Code = code, Beschreibung = beschreibung };
        return AppendEmbeddingContext(BuildBaseEmbeddingText(sample), rohrmaterial, nennweiteMm);
    }

    private static string AppendEmbeddingContext(string baseText, string? rohrmaterial, int? nennweiteMm)
    {
        var hasRm = !string.IsNullOrWhiteSpace(rohrmaterial);
        var hasDn = nennweiteMm.HasValue && nennweiteMm.Value > 0;

        if (!hasRm && !hasDn) return baseText;

        var context = (hasRm, hasDn) switch
        {
            (true, true) => $" [Material: {rohrmaterial}, DN{nennweiteMm}]",
            (true, false) => $" [Material: {rohrmaterial}]",
            (false, true) => $" [DN{nennweiteMm}]",
            _ => ""
        };

        return baseText + context;
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

        return AuswertungPro.Next.Application.CodeCatalog.VsaLeitungscodeValidator.IsValid(sample.Code);
    }

    /// <summary>Bridge fuer alte Aufrufer; delegiert an VsaLeitungscodeValidator.IsValid.</summary>
    public static bool IsValidVsaLeitungscode(string code)
        => AuswertungPro.Next.Application.CodeCatalog.VsaLeitungscodeValidator.IsValid(code);
}
